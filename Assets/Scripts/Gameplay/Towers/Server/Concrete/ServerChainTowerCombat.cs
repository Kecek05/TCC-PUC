using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Chain tower. The bolt hits a primary target and then jumps to the nearest enemy it has not hit yet,
/// losing damage every jump. Once it is inside the wave it travels along the wave, not back to the tower, so
/// its value scales with how tightly packed the wave is — and against one lone enemy it is simply the most
/// expensive single-target shot in the set.
/// </summary>
public class ServerChainTowerCombat : BaseServerTowerCombat
{
    [Title("Chain Tower Combat References")]
    [SerializeField] private ClientCircleTowerCombat clientCircleCombat;

    private int _maxHops;
    private float _hopRadius;
    private float _falloff;

    private readonly HashSet<EnemyManager> _alreadyHit = new();

    protected override void UpdateData()
    {
        base.UpdateData();

        if (_towerData is not ChainTowerDataSO chainData)
        {
            GameLog.Error($"TowerDataSO for {GetType().Name} is not of type ChainTowerDataSO");
            return;
        }

        _maxHops = chainData.GetMaxHopsByLevel(_towerLevel.Value);
        _hopRadius = chainData.HopRadius * _cardScale.Range;
        _falloff = Mathf.Clamp01(chainData.DamageFalloffPercent);
    }

    protected override bool TryTriggerShot()
    {
        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;

        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;

        StartCoroutine(ApplyChainAfterDelay(target, _damage, travelTime));

        // Only the first leg is drawn. The jumps are server-resolved; a real chain visual is art work, and
        // faking it with one bullet per hop would cost an Rpc per jump.
        clientCircleCombat.FireBulletRpc(
            transform.position,
            _bulletSpeed,
            target.GetComponent<NetworkObject>()
        );

        return true;
    }

    private IEnumerator ApplyChainAfterDelay(EnemyManager first, float damage, float delay)
    {
        yield return new WaitForSeconds(delay);

        _alreadyHit.Clear();

        EnemyManager current = first;
        float currentDamage = damage;

        // One primary plus _maxHops jumps.
        for (int hop = 0; hop <= _maxHops; hop++)
        {
            if (!IsValidEnemy(current)) break;

            Vector2 from = current.transform.position;

            DealDamage(current, currentDamage);
            _alreadyHit.Add(current);

            currentDamage *= 1f - _falloff;
            if (currentDamage <= 0.01f) break;

            current = FindNearestUnhit(from);
        }

        _alreadyHit.Clear();
    }

    private EnemyManager FindNearestUnhit(Vector2 from)
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> active = EnemyRegistry.ActiveEnemies;

        EnemyManager closest = null;
        float closestDist = float.MaxValue;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = active[i];
            if (!IsValidEnemy(enemy)) continue;
            if (_alreadyHit.Contains(enemy)) continue;

            float dist = Vector2.Distance(from, enemy.transform.position);
            if (dist > _hopRadius || dist >= closestDist) continue;

            closest = enemy;
            closestDist = dist;
        }

        return closest;
    }
}
