#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    // Staging: what the trailer's director sets up around a shot so the beat it films happens on cue. Every
    // one of these goes through a seam the game already has (health floors, lane holds, GrantMana, the max
    // mana variable the tutorial raises, SpawnEnemy, the bot's own deploy cores). None of them is used by the
    // gameplay video, which plays the match as it comes.

    /// <summary>Neither base can die and, optionally, neither lane can win the race, so a shot never ends the match.</summary>
    [Serializable]
    public class ProtectMatch : ICaptureAction
    {
        public float HealthFloor = 1f;
        [Tooltip("Hold both lanes before the last wave, so the lane race cannot end the match either.")]
        public bool HoldLastWave = true;

        public IEnumerator Run(CaptureContext ctx)
        {
            ctx.Health?.SetHealthFloor(TeamType.Red, HealthFloor);
            ctx.Health?.SetHealthFloor(TeamType.Blue, HealthFloor);

            if (HoldLastWave && ctx.Waves != null)
            {
                int last = ctx.Waves.GetTotalWaves();
                ctx.Waves.HoldLaneBeforeWave(TeamType.Red, last);
                ctx.Waves.HoldLaneBeforeWave(TeamType.Blue, last);
            }

            yield break;
        }

        public string Describe() => $"ProtectMatch floor {HealthFloor}";
    }

    /// <summary>Lifts a team's mana ceiling and keeps it there (the tutorial's RaiseManaCapForDeck seam).</summary>
    [Serializable]
    public class RaiseManaCap : ICaptureAction
    {
        public BoardSide Side = BoardSide.Local;
        public int Max = 10;
        public bool FillPool = true;

        public RaiseManaCap() { }
        public RaiseManaCap(int max, BoardSide side = BoardSide.Local, bool fill = true)
        {
            Max = max;
            Side = side;
            FillPool = fill;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            TeamType team = ctx.Side(Side);
            StageKeeper.Ensure(ctx).HoldManaCap(team, Max);
            yield return null;
            if (FillPool && ctx.ServerMana != null) ctx.ServerMana.GrantMana(team, Max);
        }

        public string Describe() => $"RaiseManaCap {Side} {Max}";
    }

    /// <summary>Refills a team's mana pool (capped by its ceiling).</summary>
    [Serializable]
    public class FillMana : ICaptureAction
    {
        public BoardSide Side = BoardSide.Local;
        [Tooltip("Keep it topped up every frame from now on (staged shots with several plays).")]
        public bool KeepFull;

        public FillMana() { }
        public FillMana(BoardSide side, bool keepFull = false)
        {
            Side = side;
            KeepFull = keepFull;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            TeamType team = ctx.Side(Side);
            if (ctx.ServerMana != null) ctx.ServerMana.GrantMana(team, 99f);
            if (KeepFull) StageKeeper.Ensure(ctx).KeepFull(team);
            yield break;
        }

        public string Describe() => $"FillMana {Side}" + (KeepFull ? " (keep full)" : "");
    }

    /// <summary>
    /// Puts a crowd of wave enemies on a lane, already part way down it (the "wave about to reach the base"
    /// beat), staggered so they read as a column rather than a stack.
    /// </summary>
    [Serializable]
    public class SpawnCrowd : ICaptureAction
    {
        public BoardSide Side = BoardSide.Local;
        public EnemyType Enemy = EnemyType.None;
        public int Count = 8;
        [Range(0f, 1f)] public float FromProgress = 0.45f;
        [Range(0f, 1f)] public float ToProgress = 0.8f;
        [Tooltip("Seconds between spawns; 0 puts the whole column down in one frame.")]
        public float Interval;

        public SpawnCrowd() { }
        public SpawnCrowd(BoardSide side, EnemyType enemy, int count, double from, double to, double interval = 0d)
        {
            Side = side;
            Enemy = enemy;
            Count = count;
            FromProgress = (float)from;
            ToProgress = (float)to;
            Interval = (float)interval;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            EnemyDataSO data = ctx.Settings != null ? ctx.Settings.Enemy(Enemy) : null;
            if (data == null || ctx.Waves == null) throw new InvalidOperationException($"No enemy data for {Enemy}");

            TeamType team = ctx.Side(Side);
            for (int i = 0; i < Count; i++)
            {
                float k = Count > 1 ? i / (float)(Count - 1) : 0f;
                ctx.Waves.SpawnEnemy(data, team, false, null, Mathf.Lerp(FromProgress, ToProgress, k));
                if (Interval > 0f) yield return new Wait(Interval).Run(ctx);
            }
        }

        public string Describe() => $"SpawnCrowd {Count}x {Enemy} on {Side} {FromProgress:0.00}-{ToProgress:0.00}";
    }

    /// <summary>
    /// The rival sends a troop card at the player through the bot's own deploy core (mana, hand and all
    /// skipped only in that the bot is given the card's cost first).
    /// </summary>
    [Serializable]
    public class RivalSendTroops : ICaptureAction
    {
        public CardType Card = CardType.None;

        public RivalSendTroops() { }
        public RivalSendTroops(CardType card) => Card = card;

        public IEnumerator Run(CaptureContext ctx)
        {
            if (ctx.Settings?.Card(Card) is not SpawnEnemyCardDataSO troop || ctx.Waves == null)
                throw new InvalidOperationException($"{Card} is not a troop card");

            // Through SendEnemyFromPlayer, the path a troop card takes once its cost is paid, so the
            // defender gets the game's own INCOMING warning and the reversed march.
            System.Random rng = new();
            foreach (EnemyType type in troop.BuildSpawnOrder(rng))
            {
                ctx.Waves.SendEnemyFromPlayer(type, BotIdentity.AuthId);
                if (troop.DelayBetweenSpawns > 0f) yield return new Wait(troop.DelayBetweenSpawns).Run(ctx);
            }
        }

        public string Describe() => $"RivalSendTroops {Card}";
    }

    /// <summary>
    /// Builds a tower on the rival's field through the tower deployer's core (the bot's own path). The core
    /// only accepts a card the bot holds, so when the named tower is not in its hand any tower that is goes
    /// down instead: the shot needs "a defended lane", not a particular tower.
    /// </summary>
    [Serializable]
    public class RivalPlaceTower : ICaptureAction
    {
        public CardType Card = CardType.None;
        [Range(0f, 1f)] public float NearPathProgress = 0.5f;

        public RivalPlaceTower() { }
        public RivalPlaceTower(CardType card, float nearPathProgress)
        {
            Card = card;
            NearPathProgress = nearPathProgress;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            TeamType team = ctx.RivalTeam;
            Vector2? slot = BoardTarget.Slot(NearPathProgress, BoardSide.Rival).Resolve(ctx);
            if (!slot.HasValue || ctx.TowerDeployer == null)
            {
                ctx.Log("  no free rival slot");
                yield break;
            }

            List<CardType> candidates = new() { Card };
            if (ctx.Settings?.Cards != null)
                foreach (CardDataSO card in ctx.Settings.Cards.CardDataList)
                    if (card is TowerCardDataSO && card.CardType != Card) candidates.Add(card.CardType);

            BaseCardHandManager hands = ServiceLocator.TryGet(out BaseCardHandManager h) ? h : null;
            foreach (CardType candidate in candidates)
            {
                if (hands != null && !hands.TeamHasCardInHand(team, candidate)) continue;

                ctx.ServerMana?.GrantMana(team, 99f);
                TowerValidation result = ctx.TowerDeployer.TryDeployTower(team, candidate, slot.Value,
                    Unity.Netcode.NetworkManager.ServerClientId, out _);
                ctx.Log($"  rival {candidate}: {result.Reason}");
                if (result.IsValid) yield break;
            }
        }

        public string Describe() => $"RivalPlaceTower {Card} near {NearPathProgress:0.00}";
    }

    /// <summary>
    /// Stops the bot deciding (its decision coroutine), so a staged shot is not interrupted by a troop it
    /// happened to send. Its towers keep shooting; only new plays stop.
    /// </summary>
    [Serializable]
    public class PauseBot : ICaptureAction
    {
        public IEnumerator Run(CaptureContext ctx)
        {
            BotController bot = UnityEngine.Object.FindFirstObjectByType<BotController>();
            if (bot != null) bot.StopAllCoroutines();
            yield break;
        }

        public string Describe() => "PauseBot";
    }

    /// <summary>Ends the match with a winner: the loser's floor comes off and its base takes the damage.</summary>
    [Serializable]
    public class EndMatch : ICaptureAction
    {
        public BoardSide Winner = BoardSide.Local;

        public IEnumerator Run(CaptureContext ctx)
        {
            TeamType loser = ctx.Side(Winner == BoardSide.Local ? BoardSide.Rival : BoardSide.Local);
            ctx.Health.SetHealthFloor(loser, 0f);
            ctx.Health.DamageBase(9999f, loser);
            yield break;
        }

        public string Describe() => $"EndMatch winner {Winner}";
    }

    /// <summary>Takes a team's base down to a given health (no lower than its floor).</summary>
    [Serializable]
    public class SetBaseHealth : ICaptureAction
    {
        public BoardSide Side = BoardSide.Rival;
        public float Health = 20f;

        public SetBaseHealth() { }
        public SetBaseHealth(BoardSide side, float health)
        {
            Side = side;
            Health = health;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            TeamType team = ctx.Side(Side);
            float now = team == TeamType.Red ? ctx.Health.RedHealth.Value : ctx.Health.BlueHealth.Value;
            if (now > Health) ctx.Health.DamageBase(now - Health, team);
            yield break;
        }

        public string Describe() => $"SetBaseHealth {Side} {Health}";
    }

    /// <summary>
    /// Freezes the wave count where it is (both lanes wait before their next wave), so a staged shot never
    /// crosses a wave start, whose banner and mana-ceiling reset would land in the middle of it. Off goes back
    /// to holding before the last wave only (<see cref="ProtectMatch"/>'s hold).
    /// </summary>
    /// <remarks>The game keeps ONE hold per lane: run this after ProtectMatch, never before, or it is overwritten.</remarks>
    [Serializable]
    public class HoldWaves : ICaptureAction
    {
        public bool Hold = true;

        public HoldWaves() { }
        public HoldWaves(bool hold) => Hold = hold;

        public IEnumerator Run(CaptureContext ctx)
        {
            if (ctx.Waves == null) yield break;

            int total = ctx.Waves.GetTotalWaves();
            foreach (TeamType team in new[] { TeamType.Red, TeamType.Blue })
            {
                bool local = team == ctx.LocalTeam;
                int current = local ? ctx.Waves.GetLocalCurrentWave().Value : ctx.Waves.GetEnemyCurrentWave().Value;
                ctx.Waves.HoldLaneBeforeWave(team, Hold ? Mathf.Clamp(current + 1, 1, total) : total);
            }
        }

        public string Describe() => Hold ? "HoldWaves" : "ReleaseWaves";
    }

    /// <summary>
    /// Off tape only: runs the match faster (the game's own waves, just sooner) until a condition holds, then
    /// back to normal speed.
    /// </summary>
    [Serializable]
    public class FastForward : ICaptureAction
    {
        public WaitUntil Until = WaitUntil.Wave(4);
        public float Speed = 4f;

        public FastForward() { }
        public FastForward(WaitUntil until, float speed = 4f)
        {
            Until = until;
            Speed = speed;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            if (ctx.Recorder != null && ctx.Recorder.IsRecording)
                throw new InvalidOperationException("FastForward is off-tape only");

            Time.timeScale = Speed;
            float t = 0f;
            while (!Until.Holds(ctx) && t < Until.TimeoutSeconds)
            {
                t += CaptureTime.Delta;
                yield return null;
            }

            Time.timeScale = 1f;
            if (t >= Until.TimeoutSeconds) ctx.Log($"  fast-forward timed out: {Until.Describe()}");
        }

        public string Describe() => $"FastForward x{Speed} until {Until.Describe()}";
    }

    /// <summary>Sets a base's health directly (up or down): a staged shot's starting lives.</summary>
    [Serializable]
    public class SetLives : ICaptureAction
    {
        public BoardSide Side = BoardSide.Local;
        public float Health = 100f;

        public SetLives() { }
        public SetLives(BoardSide side, float health)
        {
            Side = side;
            Health = health;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            if (ctx.Health == null) yield break;
            Unity.Netcode.NetworkVariable<float> hp = ctx.Side(Side) == TeamType.Red ? ctx.Health.RedHealth : ctx.Health.BlueHealth;
            hp.Value = Health;
        }

        public string Describe() => $"SetLives {Side} {Health}";
    }

    public static class BotIdentity
    {
        /// <summary>BotController's synthetic auth id (private const there).</summary>
        public const string AuthId = "BOT_PLAYER";
    }

    /// <summary>
    /// Per-frame holds a staged shot asks for (a raised mana ceiling, a pool kept full). Lives on the director's
    /// object and is cleared when the match is left.
    /// </summary>
    public class StageKeeper : MonoBehaviour
    {
        private static StageKeeper s_instance;
        private CaptureContext _ctx;
        private readonly Dictionary<TeamType, int> _caps = new();
        private readonly HashSet<TeamType> _full = new();

        public static StageKeeper Ensure(CaptureContext ctx)
        {
            if (s_instance == null) s_instance = ctx.Director.gameObject.AddComponent<StageKeeper>();
            s_instance._ctx = ctx;
            return s_instance;
        }

        public static void Clear()
        {
            if (s_instance == null) return;
            s_instance._caps.Clear();
            s_instance._full.Clear();
        }

        public void HoldManaCap(TeamType team, int max) => _caps[team] = max;
        public void KeepFull(TeamType team) => _full.Add(team);

        private void Update()
        {
            BaseServerManaManager mana = _ctx?.ServerMana;
            if (mana == null) return;

            foreach (KeyValuePair<TeamType, int> cap in _caps)
            {
                Unity.Netcode.NetworkVariable<float> max = mana.GetMaxManaNetworkVariable(cap.Key);
                if (max != null && max.Value < cap.Value) max.Value = cap.Value;
            }

            foreach (TeamType team in _full)
                if (mana.GetMana(team) < mana.GetMaxMana(team) - 0.01f) mana.GrantMana(team, 99f);
        }
    }
}
#endif
