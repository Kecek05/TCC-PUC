using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lance spell. Cast on the caster's own field, it hits a narrow strip measured along the lane rather than a
/// circle around the cast point, so it rewards reading where the wave is strung out and whiffs when the wave
/// has already spread.
///
/// The strip is defined in LANE space, not world space: the lane is one-dimensional here (PathProgress), so a
/// "strip along the path" is a band of progress. The cast point picks an anchor — the closest enemy — and the
/// band is centred on it, which is also what keeps the spell from reaching into a different lane.
/// </summary>
public class LanceExecutor : ISpellExecutor
{
    /// <summary>How many times Range the strip runs along the lane. Range itself is its half-width.</summary>
    private const float LengthMultiplier = 3f;

    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellOffensiveDataSO data)
        {
            GameLog.Error("LanceExecutor: SpellData is not SpellOffensiveDataSO");
            return;
        }

        context.CoroutineRunner.StartCoroutine(
            RunStrike(context.ServerPosition, context.CasterTeam, data, context.Scale));
    }

    private IEnumerator RunStrike(Vector2 position, TeamType team, SpellOffensiveDataSO data,
        CardLevelScale scale)
    {
        // Locked in before the wait: the cast resolves at the level it was played at.
        float damage = data.Damage * scale.Damage;
        float halfWidth = data.Range * scale.Range;

        yield return new WaitForSeconds(data.TravelTime);

        EnemyManager anchor = FindAnchor(position, team, halfWidth * LengthMultiplier * 0.5f);
        if (anchor == null)
        {
            GameLog.Info("LanceExecutor: no enemy near the cast point, the strip hits nothing");
            yield break;
        }

        WaypointPath path = anchor.ServerMovement.Path;
        if (path == null || path.TotalLength <= 0f) yield break;

        // World length -> fraction of the lane, so Range keeps meaning world units like every other spell.
        float halfBand = (halfWidth * LengthMultiplier) / path.TotalLength;
        float centerProgress = anchor.ServerMovement.PathProgress.Value;

        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;
            if (enemy.Team.GetTeamType() != team) continue;

            // Same lane only. Two paths can share a progress value while being nowhere near each other.
            if (enemy.ServerMovement.Path != path) continue;

            if (Mathf.Abs(enemy.ServerMovement.PathProgress.Value - centerProgress) > halfBand) continue;

            enemy.ServerHealth.TakeDamage(new DamageInfo(damage, data.AttackColor, data.ArmorPenetration));
        }
    }

    private EnemyManager FindAnchor(Vector2 position, TeamType team, float searchRadius)
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;

        EnemyManager closest = null;
        float closestDist = float.MaxValue;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;
            if (enemy.Team.GetTeamType() != team) continue;

            float dist = Vector2.Distance(position, enemy.transform.position);
            if (dist > searchRadius || dist >= closestDist) continue;

            closest = enemy;
            closestDist = dist;
        }

        return closest;
    }
}
