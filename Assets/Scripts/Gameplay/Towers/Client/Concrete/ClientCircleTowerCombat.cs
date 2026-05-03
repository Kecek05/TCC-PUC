using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ClientCircleTowerCombat : BaseClientTowerCombat
{
    [Title("Circle Client Tower References")]
    [SerializeField] private EntityTeam entityTeam;

    /// <summary>
    /// Called by the server (via ServerTowerCombat) to notify clients of a shot.
    /// Spawns a local-only CosmeticBullet that lerps from tower to target.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    public void FireBulletRpc(Vector3 originServerPos, float bulletSpeed, NetworkObjectReference targetRef)
    {
        if (CosmeticBulletPool.Instance == null) return;

        // Resolve the target NetworkObject on this client
        Transform targetTransform = null;
        if (targetRef.TryGet(out NetworkObject targetObj))
            targetTransform = targetObj.transform;

        // Convert server-space origin to local-space
        BaseMapTranslator mapTranslator = ServiceLocator.Get<BaseMapTranslator>();
        Vector3 localOrigin = mapTranslator != null && mapTranslator.IsInitialized
            ? mapTranslator.ServerToLocal(originServerPos, entityTeam.GetTeamType())
            : originServerPos;

        CosmeticBullet bullet = CosmeticBulletPool.Instance.Get();
        bullet.Fire(localOrigin, targetTransform, bulletSpeed);
        clientTowerGFX.FireBulletFeedback();
    }
}
