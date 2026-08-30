using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mortar tower. Fires a magazine of heavy area shells in a quick burst, then reloads for a long time. Its
/// real cadence is the whole cycle, so it can erase a wave that arrives while it is loaded and does nothing
/// whatsoever for the several seconds after — the window the card is balanced around.
/// </summary>
/// <remarks>
/// The reload is timed inside <see cref="TryTriggerShot"/> on purpose. The base only resets its cooldown
/// when a shot returns true, so while this returns false the cooldown stays ready and TryTriggerShot is
/// called every frame — which makes it a valid per-frame tick, and avoids a second Update racing the first.
/// </remarks>
public class ServerMortarTowerCombat : BaseServerTowerCombat
{
    [Title("Mortar Tower Combat References")]
    [SerializeField] private ClientSquareTowerCombat clientSquareCombat;

    private float _explosionRadius;
    private int _magazineSize = 1;
    private float _reloadDuration;

    private int _shotsLeft;
    private float _reloadTimer;

    protected override void UpdateData()
    {
        base.UpdateData();

        if (_towerData is not MortarTowerDataSO mortarData)
        {
            GameLog.Error($"TowerDataSO for {GetType().Name} is not of type MortarTowerDataSO");
            return;
        }

        _explosionRadius = mortarData.GetExplosionRangeByLevel(_towerLevel.Value) * _cardScale.Range;
        _magazineSize = Mathf.Max(1, mortarData.GetMagazineSizeByLevel(_towerLevel.Value));
        _reloadDuration = mortarData.GetReloadDurationByLevel(_towerLevel.Value);

        // Reloaded by the upgrade. The placement upgrade already pauses the tower for its setup duration,
        // so this reads as the crew re-arming rather than as a free burst.
        _shotsLeft = _magazineSize;
        _reloadTimer = 0f;
    }

    protected override bool TryTriggerShot()
    {
        if (_shotsLeft <= 0)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer > 0f) return false;

            _shotsLeft = _magazineSize;
        }

        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;

        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;

        StartCoroutine(ApplyExplosionAfterDelay(target, _damage, travelTime));

        clientSquareCombat.FireBulletRpc(
            transform.position,
            _bulletSpeed,
            target.GetComponent<NetworkObject>(),
            travelTime,
            _explosionRadius
        );

        _shotsLeft--;
        if (_shotsLeft <= 0) _reloadTimer = _reloadDuration;

        return true;
    }

    /// <summary>Tracks the target while the shell is in the air and detonates at its last known position,
    /// mirroring <see cref="ServerSquareTowerCombat"/> so both AoE towers miss a fast mover the same way.</summary>
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

        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> active = EnemyRegistry.ActiveEnemies;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = active[i];
            if (!IsValidEnemy(enemy)) continue;
            if (Vector2.Distance(lastKnownPosition, enemy.transform.position) > _explosionRadius) continue;

            DealDamage(enemy, damage);
        }
    }
}
