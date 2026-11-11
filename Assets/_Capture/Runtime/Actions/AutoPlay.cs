#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// The player's side of a real match between the scripted beats: a plain, human-paced policy that defends
    /// the lane and keeps the hand cycling. Every play goes through the real hand UI, like a scripted one.
    /// Decides once every <see cref="MinPause"/>..<see cref="MaxPause"/> seconds; runs until
    /// <see cref="Until"/> holds (or forever inside a Parallel that something else ends).
    /// </summary>
    /// <remarks>
    /// Priorities: a Lance on a bunch about to cost health; upgrade a standing tower of a held tower card;
    /// place a held tower card on the next build spot; when the hand holds no tower, send a troop or cast Ice
    /// on the rival's towers so the cycle moves. Cards in <see cref="Reserve"/> are never played (a scripted
    /// beat needs them), and <see cref="KeepMana"/> is never spent below.
    /// </remarks>
    [Serializable]
    public class AutoPlay : ICaptureAction
    {
        public WaitUntil Until = WaitUntil.Wave(10);
        public float MinPause = 0.6f;
        public float MaxPause = 1.2f;
        public List<CardType> Reserve = new();
        public float KeepMana;
        [Tooltip("Path progress of the build spots, in the order towers go down.")]
        public List<float> BuildSpots = new() { 0.32f, 0.52f, 0.7f, 0.2f, 0.85f, 0.42f, 0.62f };
        public bool ShowFinger = true;
        [Tooltip("Off tape: when the hand holds no tower, hop to the rival's field and Ice their towers so the cycle moves.")]
        public bool AllowIceHop;

        public AutoPlay() { }
        public AutoPlay(WaitUntil until, params CardType[] reserve)
        {
            Until = until;
            Reserve = new List<CardType>(reserve);
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            float elapsed = 0f;
            while (!Until.Holds(ctx))
            {
                if (elapsed > Until.TimeoutSeconds)
                {
                    ctx.Log($"  autoplay timed out: {Until.Describe()}");
                    yield break;
                }

                ICaptureAction play = Decide(ctx);
                if (play != null)
                {
                    ctx.Log($"  auto: {play.Describe()}");
                    float before = ctx.Director.Clock;
                    yield return play.Run(ctx);
                    elapsed += ctx.Director.Clock - before;
                }

                float pause = UnityEngine.Random.Range(MinPause, MaxPause);
                float t = 0f;
                while (t < pause)
                {
                    t += CaptureTime.Delta;
                    yield return null;
                }

                elapsed += pause;
            }
        }

        private ICaptureAction Decide(CaptureContext ctx)
        {
            if (ctx.State != GameState.InMatch || ctx.Hand == null) return null;

            float mana = ctx.ServerMana != null ? ctx.ServerMana.GetMana(ctx.LocalTeam) : 0f;
            List<AbstractCard> playable = new();
            foreach (AbstractCard card in ctx.Hand.CardsInHand)
            {
                if (card == null || card.CardData == null || Reserve.Contains(card.CardData.CardType)) continue;
                if (HandCards.Find(ctx, card.CardData.CardType) == null) continue;
                if (!card.CanPlayCard().IsValid) continue;
                if (mana - card.CardData.Cost < KeepMana) continue;
                playable.Add(card);
            }

            if (playable.Count == 0) return null;

            CameraSlide slide = ctx.Slide;
            bool home = slide == null || slide.CurrentSide == CameraSide.Local;

            // 1. A spell on a bunch that is far enough down the lane to matter.
            foreach (AbstractCard card in playable)
            {
                CardType type = card.CardData.CardType;
                if (type is CardType.SpellLance or CardType.SpellFireball && home &&
                    BoardTarget.CountEnemiesOn(ctx.LocalTeam, 0.35f) >= 4)
                    return new PlayCard(type, BoardTarget.Bunch(BoardSide.Local, 1.6f)) { ShowFinger = ShowFinger };
            }

            // 2. Towers: level up what stands, else build on the next spot.
            foreach (AbstractCard card in playable)
            {
                if (card.CardData is not TowerCardDataSO || !home) continue;
                CardType type = card.CardData.CardType;

                BoardTarget upgrade = BoardTarget.UpgradableTowerOf(type);
                if (upgrade.Resolve(ctx).HasValue)
                    return new PlayCard(type, upgrade) { ShowFinger = ShowFinger };

                foreach (float spot in BuildSpots)
                {
                    BoardTarget slot = BoardTarget.Slot(spot);
                    if (slot.Resolve(ctx).HasValue && !TowerNear(ctx, spot))
                        return new PlayCard(type, slot) { ShowFinger = ShowFinger };
                }
            }

            // 3. No tower to play: keep the cycle moving with a troop (sent from anywhere on the board).
            foreach (AbstractCard card in playable)
            {
                if (card.CardData is SpawnEnemyCardDataSO && home)
                    return new PlayCard(card.CardData.CardType, BoardTarget.Field(BoardSide.Local)) { ShowFinger = ShowFinger };
            }

            // 4. Off tape only: Ice their towers (it can only be cast on their field) so it leaves the hand.
            if (AllowIceHop && home && BoardTarget.TowerBunch(BoardSide.Rival).Resolve(ctx).HasValue)
            {
                foreach (AbstractCard card in playable)
                {
                    if (card.CardData.CardType != CardType.SpellIce) continue;
                    return new Sequence(new SnapCamera(BoardSide.Rival), new Wait(0.3f),
                        new PlayCard(CardType.SpellIce, BoardTarget.TowerBunch(BoardSide.Rival, 1.0f)) { ShowFinger = ShowFinger },
                        new Wait(0.3f), new SnapCamera(BoardSide.Local));
                }
            }

            return null;
        }

        /// <summary>Whether a spot already has one of our towers next to it (a slot was used for this spot).</summary>
        private static bool TowerNear(CaptureContext ctx, float spot)
        {
            Vector2? point = BoardTarget.Path(spot, BoardSide.Local).Resolve(ctx);
            if (!point.HasValue) return false;

            foreach (TowerManager t in TowerRegistry.ActiveTowers)
                if (t != null && BoardTarget.TowerTeam(t) == ctx.LocalTeam &&
                    Vector2.Distance(t.transform.position, point.Value) < 1.3f)
                    return true;

            return false;
        }

        public string Describe() => $"AutoPlay until {Until.Describe()}";
    }

    /// <summary>Runs <see cref="Body"/> and stops it the moment <see cref="Until"/> holds (an off-tape stretch).</summary>
    [Serializable]
    public class RunUntil : ICaptureAction
    {
        public WaitUntil Until = WaitUntil.Wave(2);
        [SerializeReference] public ICaptureAction Body;
        [Tooltip("Off tape only: speed the game up meanwhile (the game's own time, just sooner).")]
        public float Speed = 1f;

        public RunUntil() { }
        public RunUntil(WaitUntil until, ICaptureAction body, float speed = 1f)
        {
            Until = until;
            Body = body;
            Speed = speed;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            if (Speed != 1f && ctx.Recorder != null && ctx.Recorder.IsRecording)
                throw new InvalidOperationException("Speed-up is off-tape only");

            Time.timeScale = Speed;

            if (Body is AutoPlay auto)
            {
                // Cooperative: the policy checks the condition between plays, so a drag is never cut short.
                WaitUntil own = auto.Until;
                auto.Until = Until;
                yield return auto.Run(ctx);
                auto.Until = own;
            }
            else
            {
                Coroutine body = Body != null ? ctx.Director.StartCoroutine(Body.Run(ctx)) : null;
                float t = 0f;
                while (!Until.Holds(ctx) && t < Until.TimeoutSeconds)
                {
                    t += CaptureTime.Delta;
                    yield return null;
                }

                if (body != null) ctx.Director.StopCoroutine(body);
            }

            Time.timeScale = 1f;
        }

        public string Describe() => $"RunUntil {Until.Describe()} x{Speed}: {Body?.Describe()}";
    }
}
#endif
