using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseServerTowerCombat : NetworkBehaviour
{
    [Title("References")]
    [SerializeField] protected TowerManager towerManager;
    protected TowerDataSO _towerData => towerManager.Data;
    protected NetworkVariable<int> _towerLevel = new(writePerm: NetworkVariableWritePermission.Server);
    protected bool _canTickCooldown = true;

    protected float _currentShootCooldown = 0f;
    protected float _shootCooldown;
    protected float _range;
    protected float _damage;
    protected float _bulletSpeed;
    
    public NetworkVariable<int> TowerLevel => _towerLevel;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _currentShootCooldown = 0f;
        _towerLevel.Value = 1;
        UpdateData();
    }

    protected void Update()
    {
        if (!IsServer) return;
        
        // if (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value != GameState.InMatch) return;
        
        if (!_canTickCooldown)  return;
        
        _currentShootCooldown += Time.deltaTime;
        if (_currentShootCooldown < _shootCooldown) return;

        if (TryTriggerShot())
            _currentShootCooldown =  0f; 
        
    }

    protected abstract bool TryTriggerShot();
    
    public bool CanUpgradeTower()
    {
        if (_towerLevel.Value >= _towerData.MaxLevel)
            return false;
        
        return true;
    }

    public void IncrementTowerLevel(int newAmount)
    {
        int newLevel = _towerLevel.Value + newAmount;
        if (newLevel < 1 || newLevel > 3)
        {
            GameLog.Error("UpgradeTower: Level must be between 1 and 3");
            return;
        }

        _towerLevel.Value = newLevel;
        UpdateData();
    }
    
    protected virtual void UpdateData()
    {
        _damage = _towerData.GetDamageByLevel(_towerLevel.Value);
        _range = _towerData.GetRangeByLevel(_towerLevel.Value);
        _shootCooldown = _towerData.GetShootCooldownByLevel(_towerLevel.Value);
        _bulletSpeed = _towerData.GetBulletSpeedByLevel(_towerLevel.Value);
    }
    
    protected EnemyManager FindClosestEnemyToEnd()
    {
        EnemyRegistry.Cleanup();

        EnemyManager closestEnemy = null;
        var activeEnemies = EnemyRegistry.ActiveEnemies;

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = activeEnemies[i];

            if (enemy == null || !enemy.NetworkObject.IsSpawned)
                continue;
            
            if (enemy.Team.GetTeamType() != towerManager.Team.GetTeamType())
                continue;
            
            if (!enemy.ServerMovement.IsTargetable)
                continue;

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

        return closestEnemy;
    }
}
