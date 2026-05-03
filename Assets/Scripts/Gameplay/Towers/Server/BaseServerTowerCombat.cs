using System.Collections;
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
    protected bool _setuped = false;
    protected bool _upgradingFlag = false;

    protected float _currentShootCooldown = 0f;
    protected float _shootCooldown;
    protected float _range;
    protected float _damage;
    protected float _bulletSpeed;
    protected float _currentSetupDuration;
    
    public NetworkVariable<int> TowerLevel => _towerLevel;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _towerLevel.Value = 1;
        
        UpdateData();
        StartCoroutine(SetupTimeDuration());
    }

    protected virtual IEnumerator SetupTimeDuration()
    {
        yield return new WaitForSeconds(_currentSetupDuration);
        _setuped = true;
    }

    protected void Update()
    {
        if (!IsServer) return;

        // if (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value != GameState.InMatch) return;
        
        if (!_canTickCooldown)  return;
        
        if (!_setuped) return;
        
        if (_upgradingFlag) return;
        
        if (_currentShootCooldown < _shootCooldown)
        {
            _currentShootCooldown += Time.deltaTime;
            return;
        }

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
        StartCoroutine(UpgradeSetupDurationDelay());
    }

    protected virtual IEnumerator UpgradeSetupDurationDelay()
    {
        _upgradingFlag  = true;
        yield return new WaitForSeconds(_currentSetupDuration);
        _upgradingFlag = false;
    }
    
    protected virtual void UpdateData()
    {
        _damage = _towerData.GetDamageByLevel(_towerLevel.Value);
        _range = _towerData.GetRangeByLevel(_towerLevel.Value);
        _shootCooldown = _towerData.GetShootCooldownByLevel(_towerLevel.Value);
        _bulletSpeed = _towerData.GetBulletSpeedByLevel(_towerLevel.Value);
        _currentSetupDuration = _towerData.GetSetupDurationByLevel(_towerLevel.Value);
        
        _currentShootCooldown = 0f;
    }
    
    protected EnemyManager FindClosestEnemyToEnd()
    {
        EnemyRegistry.Cleanup();

        EnemyManager closestEnemy = null;
        var activeEnemies = EnemyRegistry.ActiveEnemies;

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = activeEnemies[i];
            
            if (!IsValidEnemy(enemy)) continue;
            
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

    protected virtual bool IsValidEnemy(EnemyManager enemyManager)
    {
        if (enemyManager == null || !enemyManager.NetworkObject.IsSpawned) return false;
            
        if (enemyManager.Team.GetTeamType() != towerManager.Team.GetTeamType()) return false;
            
        if (!enemyManager.ServerMovement.IsTargetable) return false;
        
        return true;
    }
}
