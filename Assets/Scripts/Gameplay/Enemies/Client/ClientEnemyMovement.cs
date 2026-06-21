using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client-only: reads PathProgress from the server, samples the shared WaypointPath,
/// applies MapTranslator coordinate conversion, and smoothly interpolates the visual position.
/// This ensures both clients always reflect the server's authoritative state with no drift.
/// </summary>
public class ClientEnemyMovement : NetworkBehaviour
{
    [SerializeField] private float interpolationSpeed = 10f;
    [SerializeField] private EntityTeam entityTeam;
    [SerializeField] private GameObject GFXObject;
    
    private ServerEnemyMovement _serverMovement;
    private WaypointPath _path;
    private bool _initialized;
    private Vector3 _prevSamplePos;

    private BaseMapTranslator _mapTranslator;

    /// <summary>
    /// Called after spawn to assign the path reference on the client.
    /// The path must be the same WaypointPath instance the server uses.
    /// </summary>
    public void Initialize(WaypointPath path)
    {
        _path = path;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        _serverMovement = GetComponent<ServerEnemyMovement>();
        StartCoroutine(WaitForInitialization());
    }

    private IEnumerator WaitForInitialization()
    {
        _mapTranslator = ServiceLocator.Get<BaseMapTranslator>();

        // Wait until the path has been assigned and MapTranslator is ready
        yield return new WaitUntil(() =>
            _path != null &&
            _mapTranslator != null &&
            _mapTranslator.IsInitialized);

        // Snap to initial position
        float progress = _serverMovement.PathProgress.Value;
        float sampleT = _serverMovement.Reversed.Value ? 1f - progress : progress;
        Vector3 serverPos = _path.SamplePosition(sampleT);
        Vector3 localPos = _mapTranslator.ServerToLocal(serverPos, entityTeam.GetTeamType());
        transform.position = localPos;
        _prevSamplePos = localPos;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || _path == null) return;

        float progress = _serverMovement.PathProgress.Value;
        float sampleT = _serverMovement.Reversed.Value ? 1f - progress : progress;
        Vector3 serverPos = _path.SamplePosition(sampleT);
        Vector3 localPos = _mapTranslator.ServerToLocal(serverPos, entityTeam.GetTeamType());

        // Face the actual travel direction, derived from successive path samples — NOT from
        // (localPos - transform.position). On the host the server's ServerEnemyMovement also
        // writes transform.position every frame using the un-throttled _localProgress, which
        // LEADS the throttled PathProgress this reads, so there the direction pointed backward;
        // pure clients (no server component) pointed forward. With the fixed -90 sprite offset
        // those two used to cancel differently, leaving clients facing 180 off. Sampling
        // progress deltas is identical on host and client.
        Vector3 direction = localPos - _prevSamplePos;
        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GFXObject.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
        _prevSamplePos = localPos;

        // Smooth interpolation to avoid snapping between network updates
        transform.position = Vector3.Lerp(
            transform.position,
            localPos,
            interpolationSpeed * Time.deltaTime
        );
    }
}
