using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only: handles targeting, cooldown, and damage application.
/// Uses distance-based range checks (not physics triggers) for deterministic behavior.
/// Broadcasts shot events to clients via RPC for cosmetic bullet spawning.
/// </summary>
public class ServerTowerCombat : NetworkBehaviour
{
    private float _damage;
    private float _range;
    private float _shootCooldown;
    private float _cooldownTimer;

    private ClientTowerCombat _clientCombat;

    // Cached list to avoid allocations during targeting
    private static readonly List<ServerEnemyHealth> _activeEnemies = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _clientCombat = GetComponent<ClientTowerCombat>();

        var data = GetComponent<TowerDataHolder>().TowerData;
        _damage = data.Damage;
        _range = data.Range;
        _shootCooldown = data.ShootCooldown;
        _cooldownTimer = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer > 0f) return;

        var target = FindClosestEnemy();
        if (target == null) return;

        // Apply damage immediately on server
        target.TakeDamage(_damage);
        _cooldownTimer = _shootCooldown;

        // Notify clients to play cosmetic bullet via ClientTowerCombat RPC
        _clientCombat.FireBulletRpc(
            transform.position,
            target.GetComponent<NetworkObject>()
        );
    }

    private ServerEnemyHealth FindClosestEnemy()
    {
        RefreshEnemyList();

        ServerEnemyHealth closest = null;
        float closestDist = _range;

        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = _activeEnemies[i];

            // Clean up destroyed/despawned enemies
            if (enemy == null || !enemy.NetworkObject.IsSpawned)
            {
                _activeEnemies.RemoveAt(i);
                continue;
            }

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist <= closestDist)
            {
                closestDist = dist;
                closest = enemy;
            }
        }

        return closest;
    }

    /// <summary>
    /// Registers an enemy so towers can find it via distance checks.
    /// Called by ServerEnemyHealth.OnNetworkSpawn().
    /// </summary>
    public static void RegisterEnemy(ServerEnemyHealth enemy)
    {
        if (!_activeEnemies.Contains(enemy))
            _activeEnemies.Add(enemy);
    }

    /// <summary>
    /// Unregisters an enemy when it is despawned or destroyed.
    /// Called by ServerEnemyHealth.OnNetworkDespawn().
    /// </summary>
    public static void UnregisterEnemy(ServerEnemyHealth enemy)
    {
        _activeEnemies.Remove(enemy);
    }

    private static void RefreshEnemyList()
    {
        // Remove any null entries that slipped through
        _activeEnemies.RemoveAll(e => e == null);
    }
}
