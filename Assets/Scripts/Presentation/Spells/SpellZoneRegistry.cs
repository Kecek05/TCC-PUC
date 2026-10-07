using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A spell's area while it works, as this client draws it: where, how big, whose, and until when. The unit
/// statuses (raged, slowed, armour broken) and a frozen tower's frost clock are read off these, so no status has
/// to cross the network.
/// </summary>
/// <remarks>
/// Each zone applies the server's own rule for who it touches (see <see cref="SpellStatusPresenter"/>), which is
/// what keeps the inferred statuses honest: a Rift slows everything that has touched it until it seals, a Rage
/// bonus lingers 1 s after a troop leaves. Timed on scaled time, like the gameplay it mirrors.
/// </remarks>
public sealed class SpellZone
{
    public readonly SpellType Spell;
    public readonly Vector3 Center;
    public readonly float Radius;
    public readonly TeamType CasterTeam;
    public readonly float StartTime;
    public readonly float EndTime;

    /// <summary>How long a unit keeps the status after it stops being covered (Rage's LingerDuration).</summary>
    public readonly float Linger;

    /// <summary>The effect's size at the caster's level, as a fraction (0.5 = +50%), for the float that announces it.</summary>
    public readonly float Magnitude;

    /// <summary>
    /// Per unit: for a lingering zone, when its status runs out; for the others, simply "touched". Keyed by the
    /// network id rather than the EnemyManager: enemies are pooled, and a body recycled into a new enemy mid-zone
    /// must not inherit the old one's status (the server's per-zone sets treat it as already handled, so it is not
    /// slowed or broken there either).
    /// </summary>
    public readonly Dictionary<ulong, float> Units = new();

    public SpellZone(SpellType spell, Vector3 center, float radius, TeamType casterTeam, float startTime, float duration,
        float linger, float magnitude)
    {
        Spell = spell;
        Center = new Vector3(center.x, center.y, 0f);
        Radius = radius;
        CasterTeam = casterTeam;
        StartTime = startTime;
        EndTime = startTime + Mathf.Max(0f, duration);
        Linger = Mathf.Max(0f, linger);
        Magnitude = magnitude;
    }

    public bool IsAlive(float now) => now >= StartTime && now < EndTime;

    /// <summary>Whether anything could still be wearing this zone's status.</summary>
    public bool IsSpent(float now) => now >= EndTime + Linger + 0.05f;

    public bool Covers(Vector3 position) => ((Vector2)(position - Center)).sqrMagnitude <= Radius * Radius;
}

/// <summary>The zones this client is drawing. Visuals add theirs on landing and take them away when they finish.</summary>
public static class SpellZoneRegistry
{
    private static readonly List<SpellZone> Active = new();
    private static bool _hooked;

    public static IReadOnlyList<SpellZone> Zones => Active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active.Clear();
        _hooked = false;
    }

    public static void Add(SpellZone zone)
    {
        if (zone == null || Active.Contains(zone)) return;

        // A zone holds enemies by reference; a scene change must not carry them into the next match.
        if (!_hooked)
        {
            _hooked = true;
            SceneManager.sceneLoaded += (_, mode) => { if (mode == LoadSceneMode.Single) Active.Clear(); };
        }

        Active.Add(zone);
        SpellStatusPresenter.EnsureRunning();
    }

    public static void Remove(SpellZone zone) => Active.Remove(zone);

    /// <summary>The newest zone of <paramref name="spell"/> covering <paramref name="position"/> and still working.</summary>
    public static bool TryFindCovering(SpellType spell, Vector3 position, out SpellZone zone)
    {
        float now = Time.time;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            SpellZone candidate = Active[i];
            if (candidate.Spell != spell || now >= candidate.EndTime) continue;
            if (!candidate.Covers(position)) continue;

            zone = candidate;
            return true;
        }

        zone = null;
        return false;
    }
}
