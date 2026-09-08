using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ferrugem spell. Cast on the caster's own field, it drops a zone that lasts Duration seconds and strips
/// the off-color armor resistance of every inbound enemy inside Range — including enemies that walk in
/// after the cast, since the zone re-scans each tick. It deals no damage; the zone's whole contribution is
/// making the towers around it hit closer to full damage regardless of color. Independent Ferrugem casts
/// stack through the color-resist accumulator on ServerEnemyHealth, so overlapping zones never interfere,
/// and on expiry the zone removes exactly its own contribution from each enemy it touched.
///
/// This is the disposable, spell-form twin of the Torniquete tower's aura: same acquire-and-release shape,
/// bounded in time instead of by the tower's presence.
/// </summary>
public class FerrugemExecutor : ISpellExecutor
{
    private const float TickInterval = 0.25f;

    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellResistDataSO data)
        {
            GameLog.Error("FerrugemExecutor: SpellData is not SpellResistDataSO");
            return;
        }

        context.CoroutineRunner.StartCoroutine(
            RunClearZone(context.ServerPosition, context.CasterTeam, data, context.Scale));
    }

    private IEnumerator RunClearZone(Vector2 position, TeamType casterTeam, SpellResistDataSO data,
        CardLevelScale scale)
    {
        // Resolved ONCE for the whole cast, the same way Haste and Rage resolve their bonus: every enemy
        // this zone touches must be handed the identical number, because the release below pairs against
        // it. A clear recomputed per tick would strand a contribution the zone could never remove.
        float radius = data.Range * scale.Range;
        float duration = data.Duration * scale.Duration;
        float clear = Mathf.Clamp01(data.ResistClearPercent * scale.EffectBonus);

        yield return new WaitForSeconds(data.TravelTime);

        HashSet<EnemyManager> cleared = new HashSet<EnemyManager>();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ApplyToNewEnemiesInRange(position, casterTeam, radius, clear, cleared);
            yield return new WaitForSeconds(TickInterval);
            elapsed += TickInterval;
        }

        foreach (EnemyManager enemy in cleared)
        {
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;
            enemy.ServerHealth.RemoveColorResistClear(clear);
        }
    }

    private void ApplyToNewEnemiesInRange(Vector2 position, TeamType casterTeam, float radius, float clear,
        HashSet<EnemyManager> cleared)
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;

            // Defensive spell — only ever touches enemies walking toward the caster.
            if (enemy.Team.GetTeamType() != casterTeam) continue;

            // Each zone contributes to an enemy exactly once, so we can pair the RemoveColorResistClear
            // above with the AddColorResistClear here and never leak a stack.
            if (cleared.Contains(enemy)) continue;

            if (Vector2.Distance(position, enemy.transform.position) > radius) continue;

            enemy.ServerHealth.AddColorResistClear(clear);
            cleared.Add(enemy);
        }
    }
}
