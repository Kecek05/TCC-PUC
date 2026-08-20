using System;
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
    protected NetworkVariable<bool> _isFrozen = new(writePerm: NetworkVariableWritePermission.Server);
    protected NetworkVariable<bool> _isHasted = new(writePerm: NetworkVariableWritePermission.Server);
    protected float _attackSpeedBuffPercent = 0f;
    protected bool _canTickCooldown = true;
    protected bool _setuped = false;
    protected bool _upgradingFlag = false;

    /// <summary>
    /// Multipliers from the deploying player's persistent CARD level. Set once before the tower spawns and
    /// never changes afterwards. Entirely separate from <see cref="_towerLevel"/>, the in-match placement
    /// upgrade: the two compose, card level scaling whatever tier the placement upgrade selected.
    /// </summary>
    protected CardLevelScale _cardScale = CardLevelScale.One;

    protected float _currentShootCooldown = 0f;
    protected float _shootCooldown;
    protected float _range;
    protected float _damage;
    protected float _bulletSpeed;
    protected float _currentSetupDuration;

    private Coroutine _freezeCoroutine;

    protected BaseGameFlowManager _gameFlowManager;

    public NetworkVariable<int> TowerLevel => _towerLevel;
    public NetworkVariable<bool> IsFrozen => _isFrozen;
    public NetworkVariable<bool> IsHasted => _isHasted;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();

        _towerLevel.Value = 1;

        UpdateData();
        _currentShootCooldown = 0f;
        StartCoroutine(SetupTimeDuration());

        TowerRegistry.Register(towerManager);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (!IsServer) return;

        TowerRegistry.Unregister(towerManager);
    }

    /// <summary>
    /// Server-only. Must be called before <c>NetworkObject.Spawn</c>, because <see cref="OnNetworkSpawn"/>
    /// is what first reads the stats through <see cref="UpdateData"/>.
    /// </summary>
    public void SetCardLevelScale(CardLevelScale scale)
    {
        _cardScale = scale;
        if (IsSpawned) UpdateData();
    }

    protected virtual IEnumerator SetupTimeDuration()
    {
        yield return new WaitForSeconds(_currentSetupDuration);
        _setuped = true;
    }

    protected void Update()
    {
        if (!IsServer) return;

        // Freeze firing outside an active match (e.g. on GameState.EndMatch), the
        // same gate ServerEnemyMovement uses to halt the simulation.
        if (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value != GameState.InMatch) return;

        if (!_canTickCooldown)  return;

        if (!_setuped) return;

        if (_upgradingFlag) return;

        // Frozen by a spell: stop firing and pause the cooldown until it thaws.
        if (_isFrozen.Value) return;

        // Effective cooldown shrinks with stacked attack-speed buffs (e.g. +40% -> cooldown / 1.4).
        float effectiveCooldown = _shootCooldown / (1f + _attackSpeedBuffPercent);
        if (_currentShootCooldown < effectiveCooldown)
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

    /// <summary>
    /// Server-only. Freezes the tower for <paramref name="duration"/> seconds: it stops firing and
    /// its shoot cooldown is paused. Re-calling while already frozen refreshes the timer. The frozen
    /// flag is a NetworkVariable, so clients react to it to render the freeze GFX.
    /// </summary>
    public void Freeze(float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        if (_freezeCoroutine != null)
            StopCoroutine(_freezeCoroutine);

        _freezeCoroutine = StartCoroutine(FreezeRoutine(duration));
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        _isFrozen.Value = true;
        yield return new WaitForSeconds(duration);
        _isFrozen.Value = false;
        _freezeCoroutine = null;
    }

    /// <summary>
    /// Server-only. Adds <paramref name="percent"/> (a fraction, 0.2 = +20%) to this tower's stacked
    /// attack-speed bonus. Overlapping buff zones stack additively. Kept separate from the base shoot
    /// cooldown so upgrades (which recompute base stats) never wipe an active buff.
    /// </summary>
    public void AddAttackSpeedBuff(float percent)
    {
        if (!IsServer) return;
        _attackSpeedBuffPercent += percent;
        _isHasted.Value = _attackSpeedBuffPercent > 0.0001f;
    }

    /// <summary>
    /// Server-only. Removes a previously-applied attack-speed contribution, clamped at 0 so a stray
    /// double-remove can never invert the cooldown.
    /// </summary>
    public void RemoveAttackSpeedBuff(float percent)
    {
        if (!IsServer) return;
        _attackSpeedBuffPercent = Mathf.Max(0f, _attackSpeedBuffPercent - percent);
        _isHasted.Value = _attackSpeedBuffPercent > 0.0001f;
    }

    /// <summary>
    /// The one place tower stats are read. Card-level multipliers are applied here so every stat, and every
    /// subclass that calls <c>base.UpdateData()</c>, gets them for free.
    /// </summary>
    protected virtual void UpdateData()
    {
        _damage = _towerData.GetDamageByLevel(_towerLevel.Value) * _cardScale.Damage;
        _range = _towerData.GetRangeByLevel(_towerLevel.Value) * _cardScale.Range;

        // Attack speed shrinks the cooldown, the same way the haste buff does in Update().
        _shootCooldown = _towerData.GetShootCooldownByLevel(_towerLevel.Value) / Mathf.Max(0.01f, _cardScale.AttackSpeed);

        _bulletSpeed = _towerData.GetBulletSpeedByLevel(_towerLevel.Value);
        _currentSetupDuration = _towerData.GetSetupDurationByLevel(_towerLevel.Value);
    }

    /// <summary>
    /// Server-only. Deals damage to an enemy tagged with this tower's attack color and armor penetration, so
    /// the enemy resolves off-color resistance. Every tower combat funnels through here so a new tower can
    /// never forget to carry its color.
    /// </summary>
    protected void DealDamage(EnemyManager enemy, float damage)
    {
        enemy.ServerHealth.TakeDamage(new DamageInfo(damage, _towerData.AttackColor, _towerData.ArmorPenetration));
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
    
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _range);
    }
#endif
}
