using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Shared component on enemy prefab. Server writes the target map,
/// clients read it to look up the correct WaypointPath for ClientEnemyMovement.
/// This bridges the gap between server path assignment and client path lookup.
/// </summary>
public class EnemyPathAssignment : NetworkBehaviour
{
    private NetworkVariable<TeamType> _targetMap = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    public TeamType TargetMap => _targetMap.Value;

    /// <summary>
    /// Called by ServerWaveManager before Spawn() to set which map this enemy belongs to.
    /// </summary>
    public void SetTargetMap(TeamType map)
    {
        _targetMap.Value = map;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            StartCoroutine(InitializeClientPath());
    }

    private IEnumerator InitializeClientPath()
    {
        // Wait for ServerWaveManager to be available (it holds the path references)
        yield return new WaitUntil(() => ServerWaveManager.Instance != null);

        WaypointPath path = ServerWaveManager.Instance.GetPath(_targetMap.Value);
        var clientMovement = GetComponent<ClientEnemyMovement>();
        if (clientMovement != null)
            clientMovement.Initialize(path);
    }
}
