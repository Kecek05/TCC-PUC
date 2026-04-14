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

    /// <summary>
    /// Called by the spawner (e.g. ServerWaveManager) after instantiating to assign the path.
    /// Must be called on the server before or right after NetworkObject.Spawn().
    /// </summary>
    public void Initialize(WaypointPath path, bool reversed = false)
    {
        _path = path;
        _reversedLocal = reversed;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _baseSpeed = enemyManager.Data.MoveSpeed;
        _currentSpeed.Value = _baseSpeed;
        _reversed.Value = _reversedLocal;
        _localProgress = 0f;
        _pathProgress.Value = 0f;

        _invincibilityTimer = enemyManager.Data.SpawnDuration;
        _invincible.Value = _invincibilityTimer > 0f;
    }

    private void Update()
    {
        if (!IsServer || _path == null || _reachedEnd) return;

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
        ServerPlayerHealthManager.Instance.DamageBase(enemyManager.Data.Damage, enemyManager.Team.GetTeamType());
        NetworkObject.Despawn();
    }

    /// <summary>
    /// Apply a speed modifier (e.g. slow effect from a tower).
    /// Pass 1.0 to restore normal speed.
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        if (!IsServer) return;
        _currentSpeed.Value = _baseSpeed * multiplier;
    }
}
