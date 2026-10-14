using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A spell's area while it works, as this client draws it: where, how big, whose, and until when. A frozen tower's
/// frost clock is read off these: the replicated frozen flag says a tower is frozen, not for how much longer.
/// </summary>
/// <remarks>
/// Unit statuses used to be inferred from these as well; they are replicated now (see
/// <see cref="UnitStatusPresenter"/>), since a tower's aura or an Ancora's grip cannot be guessed from a zone.
/// Timed on scaled time, like the gameplay it mirrors.
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

        // A scene change must not carry one match's zones into the next.
        if (!_hooked)
        {
            _hooked = true;
            SceneManager.sceneLoaded += (_, mode) => { if (mode == LoadSceneMode.Single) Active.Clear(); };
        }

        Active.Add(zone);
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
