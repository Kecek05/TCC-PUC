using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ClientSquareTowerCombat : BaseClientTowerCombat
{
    [Title("Square Client Tower References")]
    [SerializeField] private EntityTeam entityTeam;

    [SerializeField] private GameObject explosionPrefab;
    
    [Rpc(SendTo.ClientsAndHost)]
    public void FireBulletRpc(Vector3 originServerPos, float bulletSpeed, NetworkObjectReference targetRef, float delayToExplode)
    {
        if (CosmeticBulletPool.Instance == null) return;
        
        Transform targetTransform = null;
        if (targetRef.TryGet(out NetworkObject targetObj))
            targetTransform = targetObj.transform;
        
        BaseMapTranslator mapTranslator = ServiceLocator.Get<BaseMapTranslator>();
        Vector3 localOrigin = mapTranslator != null && mapTranslator.IsInitialized
            ? mapTranslator.ServerToLocal(originServerPos, entityTeam.GetTeamType())
            : originServerPos;

        CosmeticBullet bullet = CosmeticBulletPool.Instance.Get();
        bullet.Fire(localOrigin, targetTransform, bulletSpeed);
        clientTowerGFX.FireBulletFeedback();
        
        StartCoroutine(ExplodeBulletAfterDelay(localOrigin, delayToExplode));
    }
    
    private IEnumerator ExplodeBulletAfterDelay(Vector2 explosionCenter, float delayToExplode)
    {
        yield return new WaitForSeconds(delayToExplode);
        
        Instantiate(explosionPrefab, explosionCenter, Quaternion.identity);
    }
}
