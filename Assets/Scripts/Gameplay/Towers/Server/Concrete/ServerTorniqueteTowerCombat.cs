using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Torniquete tower. It never shoots and never deals damage: on every cooldown tick it re-scans its radius
/// and holds a color-resist clear on the enemies inside it, releasing the clear the moment one leaves, dies
/// or the tower is removed. Mirrors ServerPrismTowerCombat's acquire-and-release shape, but changes the
/// enemy's armor exposure instead of its speed.
///
/// The clear is a per-source flag (no percentage), so a per-enemy set is enough — no need to track a
/// numeric amount to remove exactly what was added, the way Prism does.
/// </summary>
public class ServerTorniqueteTowerCombat : BaseServerTowerCombat
{
    private readonly HashSet<EnemyManager> _cleared = new();
    private readonly List<EnemyManager> _toRelease = new();

    public override void OnNetworkDespawn()
    {
        // Release before the base unregisters us: an enemy must never outlive the aura holding its clear.
        if (IsServer) ReleaseAll();
        base.OnNetworkDespawn();
    }

    protected override bool TryTriggerShot()
    {
        ReleaseEnemiesOutOfRange();
        AcquireEnemiesInRange();

        // Always "fires" — the cooldown paces the re-scan instead of gating a shot.
        return true;
    }

    private void ReleaseEnemiesOutOfRange()
    {
        _toRelease.Clear();

        foreach (EnemyManager enemy in _cleared)
        {
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

            // Despawned enemies already reset their counter in OnNetworkSpawn (pooled reuse), so only a live
            // one needs the explicit release; a dead one just gets forgotten.
            if (enemy != null && enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned)
                enemy.ServerHealth.RemoveColorResistClear();

            _cleared.Remove(enemy);
        }

        _toRelease.Clear();
    }

    private void AcquireEnemiesInRange()
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = enemies[i];

            if (!IsValidEnemy(enemy)) continue;
            if (_cleared.Contains(enemy)) continue;
            if (Vector2.Distance(transform.position, enemy.transform.position) > _range) continue;

            enemy.ServerHealth.AddColorResistClear();
            _cleared.Add(enemy);
        }
    }

    private void ReleaseAll()
    {
        foreach (EnemyManager enemy in _cleared)
        {
            if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) continue;
            enemy.ServerHealth.RemoveColorResistClear();
        }

        _cleared.Clear();
    }
}
