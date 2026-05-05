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
    public void FireBulletRpc(Vector3 originServerPos, float bulletSpeed, NetworkObjectReference targetRef, float delayToExplode, float explosionRadius)
    {
        if (CosmeticBulletPool.Instance == null) return;
        
        Transform targetTransform = null;
        if (targetRef.TryGet(out NetworkObject targetObj))
            targetTransform = targetObj.transform;
        
        BaseMapTranslator mapTranslator = ServiceLocator.Get<BaseMapTranslator>();
        Vector3 localOrigin = mapTranslator.ServerToLocal(originServerPos, entityTeam.GetTeamType());

        CosmeticBullet bullet = CosmeticBulletPool.Instance.Get();
        bullet.Fire(localOrigin, targetTransform, bulletSpeed);
        clientTowerGFX.FireBulletFeedback();
        
        StartCoroutine(ExplodeBulletAfterDelay(targetTransform, explosionRadius, delayToExplode));
    }
    
    private IEnumerator ExplodeBulletAfterDelay(Transform targetTransform, float explosionRadius, float delayToExplode)
    {
        yield return new WaitForSeconds(delayToExplode);
        
        GameObject explosionObject = Instantiate(explosionPrefab, targetTransform.position, Quaternion.identity);
        
        explosionObject.transform.localScale = Vector3.one * explosionRadius * 2f;
    }
}
