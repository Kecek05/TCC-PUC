using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client-only: receives shot events from the server and spawns cosmetic bullets.
/// The RPC is defined here so the server can call it via GetComponent from ServerTowerCombat.
/// </summary>
public class ClientTowerCombat : NetworkBehaviour
{
    [SerializeField] private EntityTeam entityTeam;
    [SerializeField] private ServerTowerCombat serverTowerCombat;
    [SerializeField] private ClientTowerGFX clientTowerGFX;
    
    private int _towerLevel = 1;
    
    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }
        
        serverTowerCombat.TowerLevel.OnValueChanged += OnTowerLevelChanged;
        OnTowerLevelChanged(0, serverTowerCombat.TowerLevel.Value);
    }

    private void OnTowerLevelChanged(int previousValue, int newValue)
    {
        clientTowerGFX.UpgradeTower(newValue);
    }

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
        Vector3 localOrigin = MapTranslator.Instance != null && MapTranslator.Instance.IsInitialized
            ? MapTranslator.Instance.ServerToLocal(originServerPos, entityTeam.GetTeamType())
            : originServerPos;

        CosmeticBullet bullet = CosmeticBulletPool.Instance.Get();
        bullet.Fire(localOrigin, targetTransform, bulletSpeed);
        clientTowerGFX.FireBulletFeedback();
    }
}
