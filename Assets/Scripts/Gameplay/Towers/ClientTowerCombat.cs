using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client-only: receives shot events from the server and spawns cosmetic bullets.
/// The RPC is defined here so the server can call it via GetComponent from ServerTowerCombat.
/// </summary>
public class ClientTowerCombat : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }
    }

    /// <summary>
    /// Called by the server (via ServerTowerCombat) to notify clients of a shot.
    /// Spawns a local-only CosmeticBullet that lerps from tower to target.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    public void FireBulletRpc(Vector3 originServerPos, NetworkObjectReference targetRef)
    {
        if (CosmeticBulletPool.Instance == null) return;

        // Resolve the target NetworkObject on this client
        Transform targetTransform = null;
        if (targetRef.TryGet(out NetworkObject targetObj))
            targetTransform = targetObj.transform;

        // Convert server-space origin to local-space
        Vector3 localOrigin = MapTranslator.Instance != null && MapTranslator.Instance.IsInitialized
            ? MapTranslator.Instance.ServerToLocal(originServerPos)
            : originServerPos;

        var bullet = CosmeticBulletPool.Instance.Get();
        bullet.Fire(localOrigin, targetTransform);
    }
}
