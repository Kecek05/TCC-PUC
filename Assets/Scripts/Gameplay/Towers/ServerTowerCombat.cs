using System.Collections;
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
    [SerializeField] private TowerManager towerManager;
    [SerializeField] private ClientTowerCombat clientCombat;
    
    private TowerDataSO _towerData => towerManager.Data;
    
    private NetworkVariable<int> _towerLevel = new(writePerm: NetworkVariableWritePermission.Server);
    private float _damage;
    private float _range;
    private float _shootCooldown;
    private float _bulletSpeed;
    private float _cooldownTimer;


    // Cached list to avoid allocations during targeting
    private static readonly List<EnemyManager> _activeEnemies = new();

    public NetworkVariable<int> TowerLevel => _towerLevel;
    
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _towerLevel.Value = 1;
        UpdateData();
        _cooldownTimer = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer > 0f) return;

        EnemyManager target = FindClosestEnemy();
        if (target == null) return;

        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;
        _cooldownTimer = _shootCooldown;

        // Schedule damage after bullet travel time (skip if enemy dies mid-flight)
        StartCoroutine(ApplyDamageAfterDelay(target, _damage, travelTime));

        // Notify clients immediately to play cosmetic bullet
        clientCombat.FireBulletRpc(
            transform.position,
            _towerData.GetBulletSpeedByLevel(_towerLevel.Value),
            target.GetComponent<NetworkObject>()
        );
    }

    private EnemyManager FindClosestEnemy()
    {
        RefreshEnemyList();

        EnemyManager closestEnemy = null;

        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = _activeEnemies[i];

            // Clean up destroyed/despawned enemies
            if (enemy == null || !enemy.NetworkObject.IsSpawned || enemy.Team.GetTeamType() != towerManager.Team.GetTeamType())
            {
                _activeEnemies.RemoveAt(i);
                continue;
            }

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist > _range) continue;
            
            if (closestEnemy == null)
            {
                closestEnemy = enemy;
                continue;
            }
            
            if (enemy.ServerMovement.PathProgress.Value > closestEnemy.ServerMovement.PathProgress.Value)
            {
                closestEnemy = enemy;
            }
        }

        Debug.Log($"Tower {name} found target: {(closestEnemy != null ? closestEnemy.name : "None")}");
        return closestEnemy;
    }

    /// <summary>
    /// Registers an enemy so towers can find it via distance checks.
    /// Called by ServerEnemyHealth.OnNetworkSpawn().
    /// </summary>
    public static void RegisterEnemy(EnemyManager enemy)
    {
        if (!_activeEnemies.Contains(enemy))
        {
            _activeEnemies.Add(enemy);
        }
    }

    /// <summary>
    /// Unregisters an enemy when it is despawned or destroyed.
    /// Called by ServerEnemyHealth.OnNetworkDespawn().
    /// </summary>
    public static void UnregisterEnemy(EnemyManager enemy)
    {
        _activeEnemies.Remove(enemy);
    }

    private IEnumerator ApplyDamageAfterDelay(EnemyManager target, float damage, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Enemy may have died or despawned during bullet flight — skip if so
        if (target != null && target.NetworkObject != null && target.NetworkObject.IsSpawned)
            target.ServerHealth.TakeDamage(damage);
    }

    public bool CanUpgradeTower()
    {
        if (_towerLevel.Value >= _towerData.MaxLevel)
            return false;
        
        return true;
    }

    public void UpgradeTower(int newAmount)
    {
        int newLevel = _towerLevel.Value + newAmount;
        if (newLevel < 1 || newLevel > 3)
        {
            Debug.LogError("UpgradeTower: Level must be between 1 and 3");
            return;
        }

        _towerLevel.Value = newLevel;
        UpdateData();
    }

    private void UpdateData()
    {
        _damage = _towerData.GetDamageByLevel(_towerLevel.Value);
        Debug.Log($"UpdateData: Damage: {_damage} - TowerLevel: {_towerLevel.Value}");
        _range = _towerData.GetRangeByLevel(_towerLevel.Value);
        _shootCooldown = _towerData.GetShootCooldownByLevel(_towerLevel.Value);
        _bulletSpeed = _towerData.GetBulletSpeedByLevel(_towerLevel.Value);
    }
    
    private static void RefreshEnemyList()
    {
        // Remove any null entries that slipped through
        _activeEnemies.RemoveAll(enemy => enemy == null);
    }
}
