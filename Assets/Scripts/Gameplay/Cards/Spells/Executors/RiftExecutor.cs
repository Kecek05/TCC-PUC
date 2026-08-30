using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rift spell. Cast on the caster's own field, it drops a zone that lasts Duration seconds and slows every
/// inbound enemy inside Range — including enemies that walk in after the cast, since the zone re-scans each
/// tick. Overlapping zones stack additively on the enemy's slow accumulator, and on expiry the zone removes
/// exactly its own contribution from each enemy it slowed. Server-authoritative; the reduced speed reaches
/// clients for free through the existing PathProgress sync.
///
/// This is the defensive twin of <see cref="HasteExecutor"/>: same re-scan-and-release shape, applied to
/// enemies instead of towers.
/// </summary>
public class RiftExecutor : ISpellExecutor
{
    private const float TickInterval = 0.25f;

    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellSlowDataSO data)
        {
            GameLog.Error("RiftExecutor: SpellData is not SpellSlowDataSO");
            return;
        }

        context.CoroutineRunner.StartCoroutine(
            RunSlowZone(context.ServerPosition, context.CasterTeam, data, context.Scale));
    }

    private IEnumerator RunSlowZone(Vector2 position, TeamType casterTeam, SpellSlowDataSO data,
        CardLevelScale scale)
    {
        // Resolved ONCE for the whole cast. AddSlow and RemoveSlow must be handed the identical value or the
        // enemy keeps a stack it can never shed — the same invariant the Haste and Rage zones rely on.
        float slow = Mathf.Clamp01(data.SlowPercent * scale.EffectBonus);
        float radius = data.Range * scale.Range;
        float duration = data.Duration * scale.Duration;

        yield return new WaitForSeconds(data.TravelTime);

        HashSet<EnemyManager> slowed = new HashSet<EnemyManager>();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ApplyToNewEnemiesInRange(position, casterTeam, radius, slow, slowed);
            yield return new WaitForSeconds(TickInterval);
            elapsed += TickInterval;
        }

        foreach (EnemyManager enemy in slowed)
        {
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;
            enemy.ServerMovement.RemoveSlow(slow);
        }
    }

    private void ApplyToNewEnemiesInRange(Vector2 position, TeamType casterTeam, float radius, float slow,
        HashSet<EnemyManager> slowed)
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;

            // Enemies walking at the caster: a defensive spell only ever touches its own lane.
            if (enemy.Team.GetTeamType() != casterTeam) continue;

            // Already slowed by THIS zone — each zone contributes to an enemy exactly once.
            if (slowed.Contains(enemy)) continue;

            if (Vector2.Distance(position, enemy.transform.position) > radius) continue;

            enemy.ServerMovement.AddSlow(slow);
            slowed.Add(enemy);
        }
    }
}
