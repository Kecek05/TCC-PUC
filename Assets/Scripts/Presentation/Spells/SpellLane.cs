using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The lanes as a spell visual sees them: an enemy's path in this client's view, a stretch of it sampled as a
/// polyline, and Lance's anchor found the way <see cref="LanceExecutor"/> finds it.
/// </summary>
/// <remarks>
/// Paths are authored in server space; every point goes through <see cref="BaseMapTranslator.ServerToLocal"/> with
/// the enemy's team, exactly as <see cref="ClientEnemyMovement"/> places the enemy itself, so a strip drawn here
/// lies under the bodies it covers on both clients.
/// </remarks>
public static class SpellLane
{
    /// <summary>The path <paramref name="enemy"/> walks, the same instance its movement samples.</summary>
    public static bool TryGetPath(EnemyManager enemy, out WaypointPath path)
    {
        path = null;
        if (enemy == null || enemy.PathAssignment == null) return false;
        if (!ServiceLocator.TryGet(out BaseServerWaveManager waves) || waves == null) return false;

        path = waves.GetPath(enemy.PathAssignment.TargetMap);
        return path != null && path.TotalLength > 0f;
    }

    /// <summary>Where along its path the enemy stands, 0 at the path's start (reversed troops walk it backwards).</summary>
    public static float PhysicalProgress(EnemyManager enemy)
    {
        float progress = enemy.ServerMovement.PathProgress.Value;
        return enemy.ServerMovement.Reversed.Value ? 1f - progress : progress;
    }

    /// <summary>
    /// Fills <paramref name="into"/> with the path between two physical progress values, in local space, a point
    /// every ~0.12 world units so a bend reads as a bend.
    /// </summary>
    public static void Sample(WaypointPath path, float from, float to, TeamType team, List<Vector3> into)
    {
        into.Clear();
        if (path == null || !ServiceLocator.TryGet(out BaseMapTranslator translator) || translator == null) return;

        from = Mathf.Clamp01(from);
        to = Mathf.Clamp01(to);
        int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(to - from) * path.TotalLength / 0.12f) + 1, 2, 64);
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(from, to, i / (count - 1f));
            Vector3 local = translator.ServerToLocal(path.SamplePosition(t), team);
            into.Add(new Vector3(local.x, local.y, 0f));
        }
    }

    /// <summary>
    /// LanceExecutor's anchor: the enemy walking at <paramref name="fieldTeam"/>'s field closest to
    /// <paramref name="point"/>, no further than <paramref name="searchRadius"/>.
    /// </summary>
    public static EnemyManager FindAnchor(Vector3 point, TeamType fieldTeam, float searchRadius, IEnumerable<EnemyManager> candidates)
    {
        EnemyManager closest = null;
        float best = searchRadius * searchRadius;

        foreach (EnemyManager enemy in candidates)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.Team == null) continue;
            if (enemy.Team.GetTeamType() != fieldTeam) continue;

            float d = ((Vector2)(enemy.transform.position - point)).sqrMagnitude;
            if (d > best) continue;
            best = d;
            closest = enemy;
        }

        return closest;
    }

    /// <summary>The direction of the lane nearest <paramref name="point"/> on <paramref name="fieldTeam"/>'s field, if any.</summary>
    public static bool TryLaneDirection(Vector3 point, TeamType fieldTeam, out Vector3 direction)
    {
        direction = Vector3.up;
        if (!ServiceLocator.TryGet(out BaseServerWaveManager waves) || waves == null) return false;
        if (!ServiceLocator.TryGet(out BaseMapTranslator translator) || translator == null) return false;

        WaypointPath path = waves.GetPath(fieldTeam);
        if (path == null || path.TotalLength <= 0f) return false;

        const int steps = 120;
        float best = float.MaxValue;
        Vector3 previous = translator.ServerToLocal(path.SamplePosition(0f), fieldTeam);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 current = translator.ServerToLocal(path.SamplePosition(i / (float)steps), fieldTeam);
            Vector3 mid = (previous + current) * 0.5f;
            float d = ((Vector2)(mid - point)).sqrMagnitude;
            if (d < best && (current - previous).sqrMagnitude > 0.000001f)
            {
                best = d;
                direction = (current - previous).normalized;
            }
            previous = current;
        }

        return best < float.MaxValue;
    }
}
