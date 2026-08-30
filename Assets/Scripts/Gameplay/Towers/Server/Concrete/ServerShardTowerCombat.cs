using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Shard tower. An ordinary single-target shot that, when it KILLS, sprays fragments over the neighbours.
/// Its output is therefore a function of how often it finishes something off, not of raw damage: it wants a
/// cheap tower beside it softening the wave, and it does nothing at all against one durable target.
/// </summary>
/// <remarks>
/// The kill is detected from the shot itself rather than from <c>ServerEnemyHealth.OnDeath</c>. That event is
/// static and fires for every despawn, a leak into the base included, so it cannot tell whose kill it was.
/// Reading the target's health straight after <see cref="BaseServerTowerCombat.DealDamage"/> can.
/// </remarks>
public class ServerShardTowerCombat : BaseServerTowerCombat
{
    [Title("Shard Tower Combat References")]
    [SerializeField] private ClientCircleTowerCombat clientCircleCombat;

    private float _fragmentRadius;
    private float _fragmentPercent;

    protected override void UpdateData()
    {
        base.UpdateData();

        if (_towerData is not ShardTowerDataSO shardData)
        {
            GameLog.Error($"TowerDataSO for {GetType().Name} is not of type ShardTowerDataSO");
            return;
        }

        _fragmentRadius = shardData.GetExplosionRangeByLevel(_towerLevel.Value) * _cardScale.Range;
        _fragmentPercent = shardData.FragmentDamagePercent;
    }

    protected override bool TryTriggerShot()
    {
        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;

        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;

        StartCoroutine(ApplyShotAfterDelay(target, _damage, travelTime));

        clientCircleCombat.FireBulletRpc(
            transform.position,
            _bulletSpeed,
            target.GetComponent<NetworkObject>()
        );

        return true;
    }

    private IEnumerator ApplyShotAfterDelay(EnemyManager target, float damage, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (target == null || target.NetworkObject == null || !target.NetworkObject.IsSpawned) yield break;

        // Captured BEFORE the hit: a kill despawns the enemy, and a pooled instance is recycled, so its
        // transform is not a reliable place to read the burst origin from afterwards.
        Vector2 impact = target.transform.position;

        DealDamage(target, damage);

        bool killed = target == null || target.ServerHealth.CurrentHealth.Value <= 0f;
        if (!killed) yield break;

        SprayFragments(impact, damage * _fragmentPercent);
    }

    /// <summary>The dead enemy is already despawned, so <see cref="BaseServerTowerCombat.IsValidEnemy"/>
    /// filters it out on its own — fragments can never double-hit the corpse that produced them.</summary>
    private void SprayFragments(Vector2 origin, float fragmentDamage)
    {
        if (fragmentDamage <= 0f || _fragmentRadius <= 0f) return;

        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> active = EnemyRegistry.ActiveEnemies;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = active[i];
            if (!IsValidEnemy(enemy)) continue;
            if (Vector2.Distance(origin, enemy.transform.position) > _fragmentRadius) continue;

            DealDamage(enemy, fragmentDamage);
        }
    }
}
