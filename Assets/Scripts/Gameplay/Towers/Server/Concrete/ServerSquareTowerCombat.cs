using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ServerSquareTowerCombat : BaseServerTowerCombat
{
    [Title("Circle Tower Combat References")]
    [SerializeField] private ClientCircleTowerCombat clientCircleCombat;

    private float _explosionRadius;
    
    
    protected override bool TryTriggerShot()
    {
        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;
        
        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;
        
        StartCoroutine(ApplyExplosionAfterDelay(target, _damage, travelTime));
        
        clientCircleCombat.FireBulletRpc(
            transform.position,
            _towerData.GetBulletSpeedByLevel(_towerLevel.Value),
            target.GetComponent<NetworkObject>()
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
        yield return new WaitForSeconds(delay);
        
        EnemyManager[] enemiesInRange = GetEnemiesInExplosionRange(_explosionRadius, target.transform.position);

        foreach (EnemyManager targetInExplosion in enemiesInRange)
        {
            if (targetInExplosion != null && targetInExplosion.NetworkObject != null && targetInExplosion.NetworkObject.IsSpawned)
                targetInExplosion.ServerHealth.TakeDamage(damage);
        }
    }

    private EnemyManager[] GetEnemiesInExplosionRange(float explosionRange, Vector2 position)
    {
        
    }
}
