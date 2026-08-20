using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Freeze spell. Cast on the enemy field, it freezes every opponent tower within Range for
/// Duration seconds (they stop firing and tint blue). Server-authoritative; the frozen state is
/// a per-tower NetworkVariable, so each client renders the freeze GFX on its own.
/// </summary>
public class IceExecutor : ISpellExecutor
{
    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellEffectDataSO effectData)
        {
            GameLog.Error("IceExecutor: SpellData is not SpellEffectDataSO");
            return;
        }

        context.CoroutineRunner.StartCoroutine(
            FreezeTowersAfterDelay(context.ServerPosition, context.CasterTeam, effectData, context.Scale)
        );
    }

    private IEnumerator FreezeTowersAfterDelay(Vector2 position, TeamType casterTeam, SpellEffectDataSO data,
        CardLevelScale scale)
    {
        float radius = data.Range * scale.Range;
        float duration = data.Duration * scale.Duration;

        yield return new WaitForSeconds(data.TravelTime);

        TowerRegistry.Cleanup();
        IReadOnlyList<TowerManager> towers = TowerRegistry.ActiveTowers;

        for (int i = towers.Count - 1; i >= 0; i--)
        {
            TowerManager tower = towers[i];
            if (tower == null || tower.NetworkObject == null || !tower.NetworkObject.IsSpawned) continue;

            // Cast on the enemy field: only the opponent's towers are frozen, never the caster's own.
            TeamType towerTeam = tower.Team.GetTeamType();
            if (towerTeam == TeamType.None || towerTeam == casterTeam) continue;

            if (Vector2.Distance(position, tower.transform.position) <= radius)
                tower.ServerTowerCombat.Freeze(duration);
        }
    }
}
