using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Fast, low-damage single-target shooter. Behaviour currently mirrors <see cref="ServerCircleTowerCombat"/>,
/// but lives in its own type so the Dart tower can diverge (e.g. multi-shot, pierce) without touching Circle.
/// </summary>
public class ServerDartTowerCombat : BaseServerTowerCombat
{
    [Title("Dart Tower Combat References")]
    [SerializeField] private ClientDartTowerCombat clientDartCombat;

    protected override bool TryTriggerShot()
    {
        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;

        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;

        StartCoroutine(ApplyDamageAfterDelay(target, _damage, travelTime));

        clientDartCombat.FireBulletRpc(
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
