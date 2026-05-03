using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ServerSquareTowerCombat : BaseServerTowerCombat
{
    [Title("Circle Tower Combat References")]
    [SerializeField] private ClientSquareTowerCombat clientSquareCombat;

    private float _explosionRadius;
    
    
    protected override bool TryTriggerShot()
    {
        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;
        
        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;
        
        StartCoroutine(ApplyExplosionAfterDelay(target, _damage, travelTime));
        
        clientSquareCombat.FireBulletRpc(
            transform.position,
            _towerData.GetBulletSpeedByLevel(_towerLevel.Value),
            target.GetComponent<NetworkObject>(),
            travelTime
        );
        
        return true;
    }

    protected override void UpdateData()
    {
        base.UpdateData();
        if (_towerData is not ExplosionTowerDataSO explosionTowerDataSO)
        {
            GameLog.Error($"TowerDataSO for {GetType().Name} is not of type ExplosionTowerDataSO");
            return;
        }
        
        _explosionRadius = explosionTowerDataSO.GetExplosionRangeByLevel(_towerLevel.Value);
    }

    private IEnumerator ApplyExplosionAfterDelay(EnemyManager target, float damage, float delay)
    {
        Vector2 lastKnownPosition = target != null ? (Vector2)target.transform.position : (Vector2)transform.position;
        float elapsed = 0f;

        while (elapsed < delay)
        {
            if (target != null && target.NetworkObject != null && target.NetworkObject.IsSpawned)
                lastKnownPosition = target.transform.position;

            elapsed += Time.deltaTime;
            yield return null;
        }
        
        EnemyManager[] enemiesInRange = GetEnemiesInExplosionRange(_explosionRadius, lastKnownPosition);

        foreach (EnemyManager enemy in enemiesInRange)
        {
            if (!IsValidEnemy(enemy)) continue;
            enemy.ServerHealth.TakeDamage(damage);
        }
    }

    private EnemyManager[] GetEnemiesInExplosionRange(float explosionRange, Vector2 position)
    {
        EnemyRegistry.Cleanup();
        var active = EnemyRegistry.ActiveEnemies;
        List<EnemyManager> result = new List<EnemyManager>();

        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = active[i];
            if (enemy == null) continue;

            float dist = Vector2.Distance(position, enemy.transform.position);
            if (dist <= explosionRange)
                result.Add(enemy);
        }

        return result.ToArray();
    }
}
