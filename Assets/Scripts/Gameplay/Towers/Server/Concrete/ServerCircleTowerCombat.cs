using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ServerCircleTowerCombat : BaseServerTowerCombat
{
    [Title("Circle Tower Combat References")]
    [SerializeField] private ClientCircleTowerCombat clientCircleCombat;

    protected override bool TryTriggerShot()
    {
        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;
        
        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;
        
        StartCoroutine(ApplyDamageAfterDelay(target, _damage, travelTime));
        
        clientCircleCombat.FireBulletRpc(
            transform.position,
            _towerData.GetBulletSpeedByLevel(_towerLevel.Value),
            target.GetComponent<NetworkObject>()
        );
        
        return true;
    }

    private IEnumerator ApplyDamageAfterDelay(EnemyManager target, float damage, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (target != null && target.NetworkObject != null && target.NetworkObject.IsSpawned)
            target.ServerHealth.TakeDamage(damage);
    }
    
}
