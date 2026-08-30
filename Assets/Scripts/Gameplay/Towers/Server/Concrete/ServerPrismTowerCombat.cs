using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prism tower. It never shoots: on every cooldown tick it re-scans its radius and holds a slow on the
/// enemies inside it, releasing the slow the moment one leaves, dies or the tower is removed.
///
/// It does not buff the towers around it — it changes the enemy. That distinction is the whole design: two
/// Prisms do not multiply each other, and the aura is worth nothing against something already slow.
///
/// The applied amount is stored PER ENEMY rather than recomputed, so a placement upgrade that changes the
/// aura strength can never strand a stack the tower is unable to remove.
/// </summary>
public class ServerPrismTowerCombat : BaseServerTowerCombat
{
    private readonly Dictionary<EnemyManager, float> _slowed = new();
    private readonly List<EnemyManager> _toRelease = new();

    public override void OnNetworkDespawn()
    {
        // Release before the base unregisters us: an enemy must never outlive the aura holding its slow.
        if (IsServer) ReleaseAll();
        base.OnNetworkDespawn();
    }

    protected override bool TryTriggerShot()
    {
        if (_towerData is not SlowTowerDataSO slowData)
        {
            GameLog.Error("ServerPrismTowerCombat: TowerData is not SlowTowerDataSO");
            return false;
        }

        float slow = Mathf.Clamp01(slowData.GetSlowPercentByLevel(_towerLevel.Value) * _cardScale.EffectBonus);

        ReleaseEnemiesOutOfRange();
        AcquireEnemiesInRange(slow);

        // Always "fires", so the cooldown paces the re-scan instead of gating a shot.
        return true;
    }

    private void ReleaseEnemiesOutOfRange()
    {
        _toRelease.Clear();

        foreach (KeyValuePair<EnemyManager, float> entry in _slowed)
        {
            EnemyManager enemy = entry.Key;

            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned)
            {
                _toRelease.Add(enemy);
                continue;
            }

            if (Vector2.Distance(transform.position, enemy.transform.position) > _range)
                _toRelease.Add(enemy);
        }

        for (int i = 0; i < _toRelease.Count; i++)
        {
            EnemyManager enemy = _toRelease[i];

            // A despawned enemy already reset its own accumulator in OnNetworkSpawn, so only a live one
            // needs the explicit release.
            if (enemy != null && enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned)
                enemy.ServerMovement.RemoveSlow(_slowed[enemy]);

            _slowed.Remove(enemy);
        }

        _toRelease.Clear();
    }

    private void AcquireEnemiesInRange(float slow)
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];

            if (!IsValidEnemy(enemy)) continue;
            if (_slowed.ContainsKey(enemy)) continue;
            if (Vector2.Distance(transform.position, enemy.transform.position) > _range) continue;

            enemy.ServerMovement.AddSlow(slow);
            _slowed[enemy] = slow;
        }
    }

    private void ReleaseAll()
    {
        foreach (KeyValuePair<EnemyManager, float> entry in _slowed)
        {
            EnemyManager enemy = entry.Key;
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;

            enemy.ServerMovement.RemoveSlow(entry.Value);
        }

        _slowed.Clear();
    }
}
