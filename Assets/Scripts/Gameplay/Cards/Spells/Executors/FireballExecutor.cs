using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireballExecutor : ISpellExecutor
{
    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellOffensiveDataSO offensiveData)
        {
            GameLog.Error("FireballExecutor: SpellData is not SpellOffensiveDataSO");
            return;
        }
    
        GameLog.Info("FireballExecutor: Execute");
        context.CoroutineRunner.StartCoroutine(
            ApplyAoEDamageAfterDelay(context.ServerPosition, context.CasterTeam, offensiveData, context.Scale)
        );
    }

    private IEnumerator ApplyAoEDamageAfterDelay(Vector2 position, TeamType team, SpellOffensiveDataSO data,
        CardLevelScale scale)
    {
        // Resolved once, before the wait: the cast is locked in at the level it was played at.
        float damage = data.Damage * scale.Damage;
        float radius = data.Range * scale.Range;

        yield return new WaitForSeconds(data.TravelTime);

        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];

            if (enemy == null || !enemy.NetworkObject.IsSpawned) continue;
            if (enemy.Team.GetTeamType() != team) continue;

            float dist = Vector2.Distance(position, enemy.transform.position);
            if (dist <= radius)
            {
                enemy.ServerHealth.TakeDamage(new DamageInfo(damage, data.AttackColor, data.ArmorPenetration));
                GameLog.Info($"FireballExecutor: ApplyAoEDamageAfterDelay to enemy: {enemy.name}, damage: {damage}");
            }
        }
    }
}
