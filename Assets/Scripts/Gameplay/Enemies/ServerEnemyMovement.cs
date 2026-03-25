using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only: advances PathProgress along the assigned WaypointPath each tick.
/// Clients read PathProgress via NetworkVariable to determine visual position.
/// </summary>
public class ServerEnemyMovement : NetworkBehaviour
{
    private NetworkVariable<float> _pathProgress = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> _currentSpeed = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> PathProgress => _pathProgress;
    public NetworkVariable<float> CurrentSpeed => _currentSpeed;

    private WaypointPath _path;
    private float _baseSpeed;
    private bool _reachedEnd;

    /// <summary>
    /// Called by the spawner (e.g. ServerWaveManager) after instantiating to assign the path.
    /// Must be called on the server before or right after NetworkObject.Spawn().
    /// </summary>
    public void Initialize(WaypointPath path)
    {
        _path = path;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        var data = GetComponent<EnemyDataHolder>().EnemyData;
        _baseSpeed = data.MoveSpeed;
        _currentSpeed.Value = _baseSpeed;
        _pathProgress.Value = 0f;
    }

    private void Update()
    {
        if (!IsServer || _path == null || _reachedEnd) return;

        float totalLength = _path.TotalLength;
        if (totalLength <= 0f) return;

        // Advance progress based on speed and path length
        _pathProgress.Value += (_currentSpeed.Value * Time.deltaTime) / totalLength;

        if (_pathProgress.Value >= 1f)
        {
            _pathProgress.Value = 1f;
            _reachedEnd = true;
            OnReachedEnd();
        }

        // Update server-side transform for physics queries (tower targeting)
        transform.position = _path.SamplePosition(_pathProgress.Value);
    }

    private void OnReachedEnd()
    {
        // TODO: Apply damage to the player's base, then despawn
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
