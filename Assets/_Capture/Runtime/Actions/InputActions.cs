#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// Plays a card from the hand exactly like a player: the virtual finger presses the card in the hand,
    /// drags it over the board (the ghost, the aim and the slot frames all react) and lets go on the target.
    /// The game's own drop check and server Rpc decide what happens.
    /// </summary>
    [Serializable]
    public class PlayCard : ICaptureAction
    {
        public CardType Card = CardType.None;
        public BoardTarget Target = new();
        [Tooltip("How long the finger takes from the hand to the drop point.")]
        public float DragSeconds = 0.55f;
        [Tooltip("How long to wait for the card to be in hand before giving up (logged, not fatal).")]
        public float WaitForCardSeconds = 20f;
        [Tooltip("Staging: put enough mana in the pool right before the drag (the HUD shows it). Off for a real match.")]
        public bool TopUpMana;
        [Tooltip("Off for a play that happens out of frame: no touch mark.")]
        public bool ShowFinger = true;

        public PlayCard() { }

        public PlayCard(CardType card, BoardTarget target, float dragSeconds = 0.55f)
        {
            Card = card;
            Target = target;
            DragSeconds = dragSeconds;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            AbstractCard card = null;
            float waited = 0f;
            while ((card = HandCards.Find(ctx, Card)) == null)
            {
                waited += CaptureTime.Delta;
                if (waited >= WaitForCardSeconds)
                {
                    ctx.Log($"  {Card} never reached the hand; play skipped");
                    yield break;
                }

                yield return null;
            }

            // A player waits for the mana before dragging; the client's own affordability check says when.
            while (!TopUpMana && card != null && !card.CanPlayCard().IsValid)
            {
                waited += CaptureTime.Delta;
                if (waited >= WaitForCardSeconds)
                {
                    ctx.Log($"  {Card} never became affordable; play skipped");
                    yield break;
                }

                yield return null;
            }

            if (card == null) card = HandCards.Find(ctx, Card);
            if (card == null) yield break;

            if (TopUpMana && ctx.ServerMana != null && ctx.Settings != null)
            {
                int cost = ctx.Settings.Cost(Card);
                float missing = cost - ctx.ServerMana.GetMana(ctx.LocalTeam);
                if (missing > 0f) ctx.ServerMana.GrantMana(ctx.LocalTeam, missing + 0.01f);
                // Let the replicated mana reach the client's prediction before the drop is validated locally.
                yield return new Wait(0.1f).Run(ctx);
            }

            Vector2? world = Target.Resolve(ctx);
            if (!world.HasValue)
            {
                ctx.Log($"  target '{Target}' resolved to nothing; {Card} not played");
                yield break;
            }

            Vector2 lastWorld = world.Value;
            Vector2 from = HandCards.ScreenPoint(card.Rect);

            bool? accepted = null;
            void OnResolved(CardValidation v) => accepted = v.IsValid;
            card.OnDropResolved += OnResolved;

            ctx.Finger.ShowIndicator = ShowFinger;
            yield return ctx.Finger.Drag(card.gameObject, from, () =>
            {
                // A moving target (the lead enemy) is tracked; one that vanished mid-drag keeps its last spot.
                Vector2? now = Target.Resolve(ctx);
                if (now.HasValue) lastWorld = now.Value;
                return HandCards.ScreenPoint(lastWorld);
            }, DragSeconds);
            ctx.Finger.ShowIndicator = true;

            if (card != null) card.OnDropResolved -= OnResolved;
            ctx.Log($"  {Card} -> {Target}: {(accepted == true ? "sent" : "REFUSED")}");
        }

        public string Describe() => $"PlayCard {Card} -> {Target}";
    }

    /// <summary>The table swipe: a finger dragged down the screen goes to the rival's field, up comes home.</summary>
    [Serializable]
    public class Swipe : ICaptureAction
    {
        public BoardSide To = BoardSide.Rival;
        public float DragSeconds = 0.28f;
        [Tooltip("Screen fraction (0..1) the finger travels. Released at speed, so the game reads a flick.")]
        public float Travel = 0.45f;
        public float SettleSeconds = 0.45f;
        public bool ShowFinger = true;

        public Swipe() { }
        public Swipe(BoardSide to, float dragSeconds = 0.28f)
        {
            To = to;
            DragSeconds = dragSeconds;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            CameraSlide slide = ctx.Slide;
            if (slide == null) throw new InvalidOperationException("No CameraSlide in the scene");

            CameraSide want = To == BoardSide.Rival ? CameraSide.Enemy : CameraSide.Local;
            if (slide.CurrentSide == want)
            {
                ctx.Log("  already there");
                yield break;
            }

            float w = Screen.width, h = Screen.height;
            float centre = 0.52f, half = Travel * 0.5f;
            // Down the screen (y falling) reaches the rival's field above ours.
            Vector2 from = To == BoardSide.Rival ? new Vector2(w * 0.58f, h * (centre + half)) : new Vector2(w * 0.58f, h * (centre - half));
            Vector2 to = To == BoardSide.Rival ? new Vector2(w * 0.62f, h * (centre - half)) : new Vector2(w * 0.62f, h * (centre + half));

            ctx.Finger.ShowIndicator = ShowFinger;
            yield return ctx.Finger.Drag(slide.gameObject, from, () => to, DragSeconds, 0.06f, 0f, flick: true);
            ctx.Finger.ShowIndicator = true;

            yield return new Wait(SettleSeconds).Run(ctx);
            if (slide.CurrentSide != want) ctx.Log("  swipe did not commit");
        }

        public string Describe() => $"Swipe to {To}";
    }

    /// <summary>
    /// A tap on a UI object found by name (the deepest active match), e.g. a nav button or a deck slot.
    /// </summary>
    [Serializable]
    public class TapUi : ICaptureAction
    {
        public string ObjectName = "";
        [Tooltip("Optional: the match must sit under an object with this name.")]
        public string UnderName = "";
        public float HoverSeconds = 0.25f;
        public bool ShowFinger = true;

        public TapUi() { }
        public TapUi(string objectName, string underName = "")
        {
            ObjectName = objectName;
            UnderName = underName;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            RectTransform rect = UiFinder.Find(ObjectName, UnderName);
            if (rect == null) throw new InvalidOperationException($"No active UI object '{ObjectName}'" + (UnderName.Length > 0 ? $" under '{UnderName}'" : ""));

            Vector2 at = HandCards.ScreenPoint(rect);
            ctx.Finger.ShowIndicator = ShowFinger;
            yield return ctx.Finger.Tap(rect.gameObject, at);
            ctx.Finger.ShowIndicator = true;
        }

        public string Describe() => $"Tap '{ObjectName}'" + (UnderName.Length > 0 ? $" under '{UnderName}'" : "");
    }

    /// <summary>
    /// Snaps the match camera to a field with no slide (a hard cut inside one take). Uses CameraSlide's own
    /// commit, so everything listening for the side change hears it, then finishes the tween at once.
    /// </summary>
    [Serializable]
    public class SnapCamera : ICaptureAction
    {
        public BoardSide To = BoardSide.Rival;

        public SnapCamera() { }
        public SnapCamera(BoardSide to) => To = to;

        public IEnumerator Run(CaptureContext ctx)
        {
            CameraSlide slide = ctx.Slide;
            if (slide == null) yield break;

            CameraSide want = To == BoardSide.Rival ? CameraSide.Enemy : CameraSide.Local;
            if (slide.CurrentSide != want)
            {
                Reflect.Call(slide, "Commit");
                Transform rig = Reflect.Get<Transform>(slide, "Rig");
                if (rig != null) rig.DOComplete();
            }

            yield break;
        }

        public string Describe() => $"SnapCamera {To}";
    }
}
#endif
