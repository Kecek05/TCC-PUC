using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only: advances PathProgress along the assigned WaypointPath each tick.
/// Clients read PathProgress via NetworkVariable to determine visual position.
/// </summary>
public class ServerEnemyMovement : NetworkBehaviour
{
    [SerializeField] private EnemyManager enemyManager;
    
    private NetworkVariable<float> _pathProgress = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _currentSpeed = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _reversed = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _invincible = new(writePerm: NetworkVariableWritePermission.Server);

    private BaseGameFlowManager _gameFlowManager;
    
    public NetworkVariable<float> PathProgress => _pathProgress;
    public NetworkVariable<float> CurrentSpeed => _currentSpeed;
    public NetworkVariable<bool> Reversed => _reversed;
    public NetworkVariable<bool> Invincible => _invincible;

    public bool IsTargetable => !_invincible.Value;

    // Only sync PathProgress when the change exceeds this threshold.
    private const float SyncThreshold = 0.005f;

    private WaypointPath _path;
    private float _baseSpeed;
    private float _localProgress;
    private bool _reachedEnd;
    private bool _reversedLocal;
    private float _invincibilityTimer;

    // Current speed is composed from independent sources so they never clobber each other:
    //   effective = base * slowMultiplier * (1 + speedBuffPercent)
    // _slowMultiplier is a single multiplicative slow (1 = none); _speedBuffPercent is the additive sum
    // of every active speed buff (e.g. stacked Rage zones), mirroring the tower attack-speed accumulator.
    private float _slowMultiplier = 1f;
    private float _speedBuffPercent = 0f;

    /// <summary>
    /// Called by the spawner (e.g. ServerWaveManager) after instantiating to assign the path.
    /// Must be called on the server before or right after NetworkObject.Spawn().
    /// </summary>
    public void Initialize(WaypointPath path, bool reversed = false)
    {
        _path = path;
        _reversedLocal = reversed;
    }

    private void Start()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _baseSpeed = enemyManager.Data.MoveSpeed;
        _slowMultiplier = 1f;
        _speedBuffPercent = 0f;
        RecalculateSpeed();
        _reversed.Value = _reversedLocal;
        _localProgress = 0f;
        _pathProgress.Value = 0f;

        _invincibilityTimer = enemyManager.Data.SpawnDuration;
        _invincible.Value = _invincibilityTimer > 0f;
    }

    private void Update()
    {
        if (!IsServer || _path == null || _reachedEnd) return;
        
        if (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value != GameState.InMatch) return;

        if (_invincible.Value)
        {
            _invincibilityTimer -= Time.deltaTime;
            if (_invincibilityTimer <= 0f)
                _invincible.Value = false;
            return;
        }

        float totalLength = _path.TotalLength;
        if (totalLength <= 0f) return;

        // Advance local progress every frame
        _localProgress += (_currentSpeed.Value * Time.deltaTime) / totalLength;

        // Only push to NetworkVariable when change exceeds threshold (saves bandwidth)
        if (_localProgress - _pathProgress.Value >= SyncThreshold)
            _pathProgress.Value = _localProgress;

        if (_localProgress >= 1f)
        {
            _localProgress = 1f;
            _pathProgress.Value = 1f;
            _reachedEnd = true;
            OnReachedEnd();
        }

        // Update server-side transform for tower targeting distance checks
        float sampleT = _reversed.Value ? 1f - _localProgress : _localProgress;
        transform.position = _path.SamplePosition(sampleT);
    }

    private void OnReachedEnd()
    {
        // TODO: Apply damage to the player's base, then despawn
        ServiceLocator.Get<BaseServerPlayerHealthManager>().DamageBase(enemyManager.Data.Damage, enemyManager.Team.GetTeamType());
        NetworkObject.Despawn();
    }

    /// <summary>
    /// Recomputes the replicated speed from its independent contributions. Server-only.
    /// </summary>
    private void RecalculateSpeed()
    {
        if (!IsServer) return;
        _currentSpeed.Value = _baseSpeed * _slowMultiplier * (1f + _speedBuffPercent);
    }

    /// <summary>
    /// Apply a multiplicative speed modifier (e.g. a slow effect from a tower).
    /// Pass 1.0 to restore normal speed. Composes with any active speed buffs.
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        if (!IsServer) return;
        _slowMultiplier = multiplier;
        RecalculateSpeed();
    }

    /// <summary>
    /// Server-only. Adds <paramref name="percent"/> (a fraction, 0.2 = +20%) to this enemy's stacked
    /// move-speed bonus. Independent buff sources (e.g. overlapping Rage zones) stack additively. Kept
    /// separate from the base/slow speed so one source can be removed without disturbing the others.
    /// </summary>
    public void AddSpeedBuff(float percent)
    {
        if (!IsServer) return;
        _speedBuffPercent += percent;
        RecalculateSpeed();
    }

    /// <summary>
    /// Server-only. Removes a previously-applied move-speed contribution, clamped at 0 so a stray
    /// double-remove can never invert the speed.
    /// </summary>
    public void RemoveSpeedBuff(float percent)
    {
        if (!IsServer) return;
        _speedBuffPercent = Mathf.Max(0f, _speedBuffPercent - percent);
        RecalculateSpeed();
    }
}
