using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Torniquete tower. It never shoots and never deals damage: on every cooldown tick it re-scans its radius
/// and holds a color-resist clear on the enemies inside it, releasing the clear the moment one leaves, dies
/// or the tower is removed. Mirrors ServerPrismTowerCombat's acquire-and-release shape, but changes the
/// enemy's armor exposure instead of its speed.
///
/// The applied fraction is stored PER ENEMY rather than recomputed, exactly as the Prism stores its slow: a
/// placement upgrade that changes the aura strength mid-hold would otherwise strand a contribution the
/// tower is unable to remove, and the enemy would keep part of its armor stripped forever.
/// </summary>
public class ServerTorniqueteTowerCombat : BaseServerTowerCombat
{
    private readonly Dictionary<EnemyManager, float> _cleared = new();
    private readonly List<EnemyManager> _toRelease = new();

    public override void OnNetworkDespawn()
    {
        // Release before the base unregisters us: an enemy must never outlive the aura holding its clear.
        if (IsServer) ReleaseAll();
        base.OnNetworkDespawn();
    }

    protected override bool TryTriggerShot()
    {
        if (_towerData is not ResistTowerDataSO resistData)
        {
            GameLog.Error("ServerTorniqueteTowerCombat: TowerData is not ResistTowerDataSO");
            return false;
        }

        float clear = Mathf.Clamp01(
            resistData.GetResistClearPercentByLevel(_towerLevel.Value) * _cardScale.EffectBonus);

        ReleaseEnemiesOutOfRange();
        AcquireEnemiesInRange(clear);

        // Always "fires" — the cooldown paces the re-scan instead of gating a shot.
        return true;
    }

    private void ReleaseEnemiesOutOfRange()
    {
        _toRelease.Clear();

        foreach (KeyValuePair<EnemyManager, float> entry in _cleared)
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

            // Despawned enemies already reset their accumulator in OnNetworkSpawn (pooled reuse), so only a
            // live one needs the explicit release; a dead one just gets forgotten.
            if (enemy != null && enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned)
                enemy.ServerHealth.RemoveColorResistClear(_cleared[enemy]);

            _cleared.Remove(enemy);
        }

        _toRelease.Clear();
    }

    private void AcquireEnemiesInRange(float clear)
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];

            if (!IsValidEnemy(enemy)) continue;
            if (_cleared.ContainsKey(enemy)) continue;
            if (Vector2.Distance(transform.position, enemy.transform.position) > _range) continue;

            enemy.ServerHealth.AddColorResistClear(clear);
            _cleared[enemy] = clear;
        }
    }

    private void ReleaseAll()
    {
        foreach (KeyValuePair<EnemyManager, float> entry in _cleared)
        {
            EnemyManager enemy = entry.Key;
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;

            enemy.ServerHealth.RemoveColorResistClear(entry.Value);
        }

        _cleared.Clear();
    }
}
