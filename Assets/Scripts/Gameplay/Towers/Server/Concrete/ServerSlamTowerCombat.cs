using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ServerSlamTowerCombat : BaseServerTowerCombat
{
    [Title("Slam Tower Combat References")]
    [SerializeField] private ClientSlamTowerCombat clientSlamCombat;

    /// <summary>
    /// Ground slam: an instant AoE centred on the tower. Every cooldown, if at least one valid enemy is
    /// within range, it damages all of them at once — no projectile, no travel time. Returns false (so the
    /// cooldown stays ready) when nothing is in range, so the slam lands the instant an enemy steps in.
    /// </summary>
    protected override bool TryTriggerShot()
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> active = EnemyRegistry.ActiveEnemies;

        bool hitAny = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = active[i];
            if (!IsValidEnemy(enemy)) continue;
            if (Vector2.Distance(transform.position, enemy.transform.position) > _range) continue;

            enemy.ServerHealth.TakeDamage(_damage);
            hitAny = true;
        }

        if (!hitAny) return false;

        if (clientSlamCombat != null) clientSlamCombat.PlaySlamRpc();
        return true;
    }
}
