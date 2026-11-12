using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ticks every <see cref="TroopMotion"/> on this peer once a frame, and makes the troops on a lane give each other
/// room: a visual-only push apart, sideways first, a little along the lane after.
/// </summary>
/// <remarks>
/// Three passes, so steering sees the whole lane at once: every troop reads its progress, the lane is sorted by
/// distance and neighbouring pairs push, then every troop is drawn. Sorting is an insertion sort, which is O(n) on a
/// lane that was sorted last frame (troops rarely overtake), and each troop only checks the few neighbours inside a
/// short window after it, so a hundred troops on a field cost a few hundred distance checks. Nothing allocates per
/// frame.
/// <para/>
/// Runs after the movement scripts (they place the root at execution order 0) and before
/// <see cref="EnemyFeedbackPresenter"/>'s LateUpdate, whose hit-stop pin has to win over the route.
/// </remarks>
public static class TroopCrowd
{
    private static readonly List<TroopMotion> Troops = new(128);
    private static readonly List<WaypointPath> Lanes = new(4);
    private static TroopCrowdRunner _runner;
    private static float _dustTokens;

    /// <summary>How many puffs the dust budget may bank for a burst (several troops landing in one frame).</summary>
    private const float DustBurst = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Troops.Clear();
        Lanes.Clear();
        _runner = null;
        _dustTokens = 0f;
    }

    public static void Register(TroopMotion troop)
    {
        if (troop == null || Troops.Contains(troop)) return;
        Troops.Add(troop);
        EnsureRunner();
    }

    public static void Unregister(TroopMotion troop) => Troops.Remove(troop);

    /// <summary>Spends one puff of the board-wide dust budget, if there is one to spend.</summary>
    public static bool TryTakeDust()
    {
        if (_dustTokens < 1f) return false;
        _dustTokens -= 1f;
        return true;
    }

    private static void EnsureRunner()
    {
        if (_runner != null) return;
        // In the active scene, so it dies with the match; the next match's first troop builds a new one.
        GameObject go = new GameObject("TroopCrowd") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
        _runner = go.AddComponent<TroopCrowdRunner>();
    }

    internal static void Tick(float dt)
    {
        for (int i = Troops.Count - 1; i >= 0; i--)
            if (Troops[i] == null) Troops.RemoveAt(i);
        if (Troops.Count == 0) return;

        TroopMotionSettingsSO settings = TroopMotionSettingsSO.Instance;
        _dustTokens = Mathf.Min(DustBurst, _dustTokens + settings.DustPerSecond * dt);
        ServiceLocator.TryGet(out BaseMapTranslator translator);
        float time = Time.time;

        for (int i = 0; i < Troops.Count; i++)
        {
            TroopMotion troop = Troops[i];
            troop.Prepare(dt);
            if (troop.Active) troop.Lane = LaneOf(troop.Path);
        }

        Sort();
        Steer(settings);

        for (int i = 0; i < Troops.Count; i++)
            Troops[i].Apply(dt, translator, settings, time);
    }

    private static int LaneOf(WaypointPath path)
    {
        for (int i = 0; i < Lanes.Count; i++)
            if (Lanes[i] == path) return i;

        // A new scene's paths replace the old ones'; forget the dead before adding.
        for (int i = Lanes.Count - 1; i >= 0; i--)
            if (Lanes[i] == null) Lanes.RemoveAt(i);
        Lanes.Add(path);
        return Lanes.Count - 1;
    }

    /// <summary>Active troops first, grouped by lane, ordered by distance along it.</summary>
    private static bool Before(TroopMotion a, TroopMotion b)
    {
        if (a.Active != b.Active) return a.Active;
        if (a.Lane != b.Lane) return a.Lane < b.Lane;
        return a.S < b.S;
    }

    private static void Sort()
    {
        for (int i = 1; i < Troops.Count; i++)
        {
            TroopMotion key = Troops[i];
            int j = i - 1;
            while (j >= 0 && Before(key, Troops[j]))
            {
                Troops[j + 1] = Troops[j];
                j--;
            }
            Troops[j + 1] = key;
        }
    }

    private static void Steer(TroopMotionSettingsSO settings)
    {
        float maxRadius = 0f;
        float maxSpeed = 0f;
        for (int i = 0; i < Troops.Count && Troops[i].Active; i++)
        {
            maxRadius = Mathf.Max(maxRadius, Troops[i].Radius);
            maxSpeed = Mathf.Max(maxSpeed, Troops[i].Speed);
        }

        float lookAhead = settings.HeadOnLookAhead;
        float reach = 2f * maxRadius * settings.SeparationFactor + 2f * maxSpeed * lookAhead;
        float alongShare = settings.AlongShare;

        for (int i = 0; i < Troops.Count; i++)
        {
            TroopMotion a = Troops[i];
            if (!a.Active) break;

            for (int j = i + 1; j < Troops.Count; j++)
            {
                TroopMotion b = Troops[j];
                if (!b.Active || b.Lane != a.Lane || b.S - a.S > reach) break;

                // A sent troop meeting the wave head-on: judged where both will be a moment from now, so they step
                // aside before the bodies meet, and only sideways (braking does not help against oncoming traffic).
                bool headOn = a.Sign != b.Sign;
                float sa = a.S + a.Along;
                float sb = b.S + b.Along;
                if (headOn)
                {
                    sa += a.Sign * a.Speed * lookAhead;
                    sb += b.Sign * b.Speed * lookAhead;
                }

                float dx = sb - sa;
                float dy = b.Lateral - a.Lateral;
                float r = (a.Radius + b.Radius) * settings.SeparationFactor;
                float d2 = dx * dx + dy * dy;
                if (d2 >= r * r) continue;

                float d = Mathf.Sqrt(d2);
                float overlap = 1f - d / r;
                // Sideways always, away from each other; two exactly stacked troops split by their seeds.
                float side = Mathf.Abs(dy) > 0.002f ? Mathf.Sign(dy) : (a.Tie < b.Tie ? 1f : -1f);
                float lateralPush = overlap * (headOn ? 1f : 1f - alongShare);
                float alongPush = headOn
                    ? 0f
                    : overlap * alongShare * (d > 1e-4f ? dx / d : (a.Tie < b.Tie ? 1f : -1f));

                a.PushLateral -= side * lateralPush;
                b.PushLateral += side * lateralPush;
                a.PushAlong -= alongPush;
                b.PushAlong += alongPush;
            }
        }
    }
}
