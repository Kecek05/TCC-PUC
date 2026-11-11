#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>Waits a fixed time, or a random time in a range (a human's pause between decisions).</summary>
    [Serializable]
    public class Wait : ICaptureAction
    {
        public float Seconds = 1f;
        [Tooltip("When above Seconds, the wait is random in [Seconds, MaxSeconds].")]
        public float MaxSeconds;

        public Wait() { }
        public Wait(float seconds, float maxSeconds = 0f)
        {
            Seconds = seconds;
            MaxSeconds = maxSeconds;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            float duration = MaxSeconds > Seconds ? UnityEngine.Random.Range(Seconds, MaxSeconds) : Seconds;
            float t = 0f;
            while (t < duration)
            {
                t += CaptureTime.Delta;
                yield return null;
            }
        }

        public string Describe() => MaxSeconds > Seconds ? $"Wait {Seconds:0.0}-{MaxSeconds:0.0}s" : $"Wait {Seconds:0.00}s";
    }

    /// <summary>
    /// Waits for something on the board (a wave, a crowd, a card) with a timeout. A timeout is logged and the
    /// script carries on: a capture that waits forever records nothing.
    /// </summary>
    [Serializable]
    public class WaitUntil : ICaptureAction
    {
        public WaitCondition Condition = WaitCondition.State;
        public BoardSide Side = BoardSide.Local;
        public GameState State = GameState.InMatch;
        [Tooltip("Wave number, enemy count or max mana, depending on the condition.")]
        public int Amount = 1;
        [Tooltip("EnemiesOnLane: only enemies at least this far down the lane count (0..1).")]
        [Range(0f, 1f)] public float MinProgress;
        public CardType Card = CardType.None;
        public float TimeoutSeconds = 30f;

        public static WaitUntil InState(GameState state, float timeout = 90f) =>
            new() { Condition = WaitCondition.State, State = state, TimeoutSeconds = timeout };

        public static WaitUntil Wave(int wave, BoardSide side = BoardSide.Local, float timeout = 120f) =>
            new() { Condition = WaitCondition.WaveAtLeast, Amount = wave, Side = side, TimeoutSeconds = timeout };

        public static WaitUntil Enemies(int count, BoardSide side, float minProgress = 0f, float timeout = 30f) =>
            new() { Condition = WaitCondition.EnemiesOnLane, Amount = count, Side = side, MinProgress = minProgress, TimeoutSeconds = timeout };

        public static WaitUntil LaneClear(BoardSide side, float timeout = 30f) =>
            new() { Condition = WaitCondition.LaneClear, Side = side, TimeoutSeconds = timeout };

        public static WaitUntil InHand(CardType card, float timeout = 30f) =>
            new() { Condition = WaitCondition.CardInHand, Card = card, TimeoutSeconds = timeout };

        public static WaitUntil MaxMana(int amount, float timeout = 120f) =>
            new() { Condition = WaitCondition.MaxManaAtLeast, Amount = amount, TimeoutSeconds = timeout };

        public static WaitUntil AtMost(int count, BoardSide side, float timeout = 30f) =>
            new() { Condition = WaitCondition.EnemiesAtMost, Amount = count, Side = side, TimeoutSeconds = timeout };

        /// <summary>Wave <paramref name="wave"/> (or later) has had this share of its enemies removed (1 = all gone).</summary>
        public static WaitUntil WaveDone(int wave, BoardSide side = BoardSide.Local, float share = 1f, float timeout = 120f) =>
            new() { Condition = WaitCondition.WaveProgressAtLeast, Amount = wave, Side = side, MinProgress = share, TimeoutSeconds = timeout };

        /// <summary>The wave has one enemy left and it is below this share of its health: the final blow is coming.</summary>
        public static WaitUntil LastEnemy(int wave, BoardSide side, float healthShare, float timeout = 90f) =>
            new() { Condition = WaitCondition.LastEnemyDying, Amount = wave, Side = side, MinProgress = healthShare, TimeoutSeconds = timeout };

        public static WaitUntil Mana(int amount, float timeout = 60f) =>
            new() { Condition = WaitCondition.ManaAtLeast, Amount = amount, TimeoutSeconds = timeout };

        public static WaitUntil Standing(CardType tower, BoardSide side = BoardSide.Local, float timeout = 10f) =>
            new() { Condition = WaitCondition.TowerStanding, Card = tower, Side = side, TimeoutSeconds = timeout };

        public IEnumerator Run(CaptureContext ctx)
        {
            float t = 0f;
            while (!Holds(ctx))
            {
                t += CaptureTime.Delta;
                if (t >= TimeoutSeconds)
                {
                    ctx.Log($"  timeout after {TimeoutSeconds:0}s: {Describe()}");
                    yield break;
                }

                yield return null;
            }
        }

        public bool Holds(CaptureContext ctx)
        {
            switch (Condition)
            {
                case WaitCondition.State:
                    return ctx.State == State;

                case WaitCondition.WaveAtLeast:
                {
                    if (ctx.Waves == null) return false;
                    bool local = ctx.Side(Side) == ctx.LocalTeam;
                    int wave = local ? ctx.Waves.GetLocalCurrentWave().Value : ctx.Waves.GetEnemyCurrentWave().Value;
                    return wave >= Amount;
                }

                case WaitCondition.EnemiesOnLane:
                    return BoardTarget.CountEnemiesOn(ctx.Side(Side), MinProgress) >= Amount;

                case WaitCondition.LaneClear:
                    return BoardTarget.CountEnemiesOn(ctx.Side(Side)) == 0;

                case WaitCondition.CardInHand:
                    return HandCards.Find(ctx, Card) != null;

                case WaitCondition.MaxManaAtLeast:
                    return ctx.ServerMana != null && ctx.ServerMana.GetMaxMana(ctx.LocalTeam) >= Amount;

                case WaitCondition.EnemiesAtMost:
                    return BoardTarget.CountEnemiesOn(ctx.Side(Side)) <= Amount;

                case WaitCondition.TowerStanding:
                    return BoardTarget.TowerOf(Card, Side).Resolve(ctx).HasValue;

                case WaitCondition.ManaAtLeast:
                    return ctx.ServerMana != null && ctx.ServerMana.GetMana(ctx.LocalTeam) >= Amount - 0.01f;

                case WaitCondition.LastEnemyDying:
                {
                    // The wave's own count of what is left (spawned or not): exactly one, on the board, and low.
                    if (ctx.Waves == null) return false;
                    TeamType team = ctx.Side(Side);
                    var remaining = Reflect.Get<System.Collections.Generic.Dictionary<TeamType, int>>(ctx.Waves, "_remainingEnemiesOfWave");
                    if (remaining == null || !remaining.TryGetValue(team, out int left) || left != 1) return false;

                    int wave = team == TeamType.Red ? ctx.Waves.RedCurrentWave.Value : ctx.Waves.BlueCurrentWave.Value;
                    if (wave < Amount) return false;

                    foreach (EnemyManager e in BoardTarget.EnemiesOn(team))
                    {
                        if (e.ServerMovement.Reversed.Value) continue;
                        float max = Mathf.Max(1f, e.ServerHealth.MaxHealth.Value);
                        if (e.ServerHealth.CurrentHealth.Value / max <= MinProgress) return true;
                    }

                    return false;
                }

                case WaitCondition.WaveProgressAtLeast:
                {
                    if (ctx.Waves == null) return false;
                    bool red = ctx.Side(Side) == TeamType.Red;
                    float progress = red ? ctx.Waves.RedCurrentWaveProgressNormalized.Value : ctx.Waves.BlueCurrentWaveProgressNormalized.Value;
                    int wave = red ? ctx.Waves.RedCurrentWave.Value : ctx.Waves.BlueCurrentWave.Value;
                    return wave >= Amount && progress >= MinProgress - 0.0001f;
                }

                default:
                    return true;
            }
        }

        public string Describe() => Condition switch
        {
            WaitCondition.State => $"WaitUntil state {State}",
            WaitCondition.WaveAtLeast => $"WaitUntil {Side} wave >= {Amount}",
            WaitCondition.EnemiesOnLane => $"WaitUntil {Amount}+ enemies on {Side} lane past {MinProgress:0.00}",
            WaitCondition.LaneClear => $"WaitUntil {Side} lane clear",
            WaitCondition.CardInHand => $"WaitUntil {Card} in hand",
            WaitCondition.MaxManaAtLeast => $"WaitUntil max mana >= {Amount}",
            WaitCondition.EnemiesAtMost => $"WaitUntil <= {Amount} enemies on {Side} lane",
            WaitCondition.TowerStanding => $"WaitUntil {Side} {Card} standing",
            WaitCondition.WaveProgressAtLeast => $"WaitUntil {Side} wave {Amount} progress >= {MinProgress:0.00}",
            WaitCondition.ManaAtLeast => $"WaitUntil mana >= {Amount}",
            WaitCondition.LastEnemyDying => $"WaitUntil {Side} wave {Amount}'s last enemy below {MinProgress:P0} health",
            _ => "WaitUntil ?",
        } + $" (<= {TimeoutSeconds:0}s)";
    }

    public enum WaitCondition
    {
        State,
        WaveAtLeast,
        EnemiesOnLane,
        LaneClear,
        CardInHand,
        MaxManaAtLeast,
        EnemiesAtMost,
        TowerStanding,
        WaveProgressAtLeast,
        LastEnemyDying,
        ManaAtLeast,
    }

    /// <summary>Runs its children at the same time and finishes when the last one does.</summary>
    [Serializable]
    public class Parallel : ICaptureAction
    {
        [SerializeReference] public List<ICaptureAction> Actions = new();

        public Parallel() { }
        public Parallel(params ICaptureAction[] actions) => Actions = new List<ICaptureAction>(actions);

        public IEnumerator Run(CaptureContext ctx)
        {
            int running = 0;
            foreach (ICaptureAction action in Actions)
            {
                if (action == null) continue;
                running++;
                ctx.Director.StartCoroutine(RunChild(action, ctx, () => running--));
            }

            while (running > 0) yield return null;
        }

        private static IEnumerator RunChild(ICaptureAction action, CaptureContext ctx, Action done)
        {
            yield return action.Run(ctx);
            done();
        }

        public string Describe() => $"Parallel x{Actions.Count}";
    }

    /// <summary>Runs its children one after another (a composite step).</summary>
    [Serializable]
    public class Sequence : ICaptureAction
    {
        [SerializeReference] public List<ICaptureAction> Actions = new();

        public Sequence() { }
        public Sequence(params ICaptureAction[] actions) => Actions = new List<ICaptureAction>(actions);

        public IEnumerator Run(CaptureContext ctx)
        {
            foreach (ICaptureAction action in Actions)
                if (action != null) yield return action.Run(ctx);
        }

        public string Describe() => $"Sequence x{Actions.Count}";
    }

    /// <summary>Notes the current video time under a label (the gameplay video's narration marks).</summary>
    [Serializable]
    public class Mark : ICaptureAction
    {
        public string Label = "";

        public Mark() { }
        public Mark(string label) => Label = label;

        public IEnumerator Run(CaptureContext ctx)
        {
            ctx.Mark(Label);
            yield break;
        }

        public string Describe() => $"Mark '{Label}'";
    }
}
#endif
