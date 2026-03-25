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

    private ServerEnemyMovement _serverMovement;
    private WaypointPath _path;
    private bool _initialized;

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
        // Wait until the path has been assigned and MapTranslator is ready
        yield return new WaitUntil(() =>
            _path != null &&
            MapTranslator.Instance != null &&
            MapTranslator.Instance.IsInitialized);

        // Snap to initial position
        Vector3 serverPos = _path.SamplePosition(_serverMovement.PathProgress.Value);
        transform.position = MapTranslator.Instance.ServerToLocal(serverPos, entityTeam.GetTeamType());
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || _path == null) return;

        float progress = _serverMovement.PathProgress.Value;
        Vector3 serverPos = _path.SamplePosition(progress);
        Vector3 localPos = MapTranslator.Instance.ServerToLocal(serverPos, entityTeam.GetTeamType());

        // Smooth interpolation to avoid snapping between network updates
        transform.position = Vector3.Lerp(
            transform.position,
            localPos,
            interpolationSpeed * Time.deltaTime
        );
    }
}
