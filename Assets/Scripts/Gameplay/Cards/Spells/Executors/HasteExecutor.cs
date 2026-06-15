using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Haste spell. Cast on the caster's own field, it drops a buff zone that lasts Duration seconds and
/// grants +AttackSpeedBonus attack speed to every ally tower within Range — including towers placed
/// after the cast (the zone re-scans each tick). Overlapping zones stack additively. Server-authoritative;
/// the faster firing rate replicates to clients on its own. On expiry the zone removes exactly its own
/// contribution from each tower it buffed.
/// </summary>
public class HasteExecutor : ISpellExecutor
{
    private const float TickInterval = 0.25f;

    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellBuffDataSO data)
        {
            GameLog.Error("HasteExecutor: SpellData is not SpellBuffDataSO");
            return;
        }

        context.CoroutineRunner.StartCoroutine(RunBuffZone(context.ServerPosition, context.CasterTeam, data));
    }

    private IEnumerator RunBuffZone(Vector2 position, TeamType casterTeam, SpellBuffDataSO data)
    {
        yield return new WaitForSeconds(data.TravelTime);

        HashSet<TowerManager> buffed = new HashSet<TowerManager>();
        float elapsed = 0f;

        while (elapsed < data.Duration)
        {
            ApplyToNewTowersInRange(position, casterTeam, data, buffed);
            yield return new WaitForSeconds(TickInterval);
            elapsed += TickInterval;
        }

        // Zone expired: remove this zone's own contribution from every tower it buffed.
        foreach (TowerManager tower in buffed)
        {
            if (tower == null || tower.NetworkObject == null || !tower.NetworkObject.IsSpawned) continue;
            tower.ServerTowerCombat.RemoveAttackSpeedBuff(data.AttackSpeedBonus);
        }
    }

    private void ApplyToNewTowersInRange(Vector2 position, TeamType casterTeam, SpellBuffDataSO data, HashSet<TowerManager> buffed)
    {
        TowerRegistry.Cleanup();
        IReadOnlyList<TowerManager> towers = TowerRegistry.ActiveTowers;

        for (int i = 0; i < towers.Count; i++)
        {
            TowerManager tower = towers[i];
            if (tower == null || tower.NetworkObject == null || !tower.NetworkObject.IsSpawned) continue;

            // Ally towers only (own field).
            if (tower.Team.GetTeamType() != casterTeam) continue;

            // Already buffed by THIS zone — apply each zone's bonus to a tower exactly once.
            if (buffed.Contains(tower)) continue;

            if (Vector2.Distance(position, tower.transform.position) > data.Range) continue;

            tower.ServerTowerCombat.AddAttackSpeedBuff(data.AttackSpeedBonus);
            buffed.Add(tower);
        }
    }
}
