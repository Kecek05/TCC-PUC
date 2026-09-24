using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ClientSquareTowerCombat : BaseClientTowerCombat
{
    [Title("Square Client Tower References")]
    [SerializeField] private EntityTeam entityTeam;

    [Tooltip("Legacy, unpooled explosion. Used only when no pooled one is set or the scene has no VFX pool.")]
    [SerializeField] private GameObject explosionPrefab;

    [Title("Explosion Feedback")]
    [Tooltip("Pooled explosion, scaled to the blast's diameter. Preferred over the legacy prefab.")]
    [SerializeField] private PooledVfxSO explosionVfx;
    [Tooltip("A small camera shake per blast, only when it lands on screen. 0 disables it.")]
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.05f;
    [SerializeField, Min(0f)] private float shakeDuration = 0.12f;

    [Rpc(SendTo.ClientsAndHost)]
    public void FireBulletRpc(Vector3 originServerPos, float bulletSpeed, NetworkObjectReference targetRef, float delayToExplode, float explosionRadius)
    {
        if (CosmeticBulletPool.Instance == null) return;

        Transform targetTransform = null;
        if (targetRef.TryGet(out NetworkObject targetObj))
            targetTransform = targetObj.transform;

        BaseMapTranslator mapTranslator = ServiceLocator.Get<BaseMapTranslator>();
        Vector3 localOrigin = mapTranslator.ServerToLocal(originServerPos, entityTeam.GetTeamType());

        CosmeticBullet bullet = GetPooledBullet();
        bullet?.Fire(localOrigin, targetTransform, bulletSpeed);
        TriggerOnBulletFired(targetTransform);

        StartCoroutine(ExplodeBulletAfterDelay(targetTransform, explosionRadius, delayToExplode));
    }

    private IEnumerator ExplodeBulletAfterDelay(Transform targetTransform, float explosionRadius, float delayToExplode)
    {
        Vector3 lastKnownPosition = targetTransform != null && targetTransform.gameObject.activeInHierarchy ? targetTransform.position : transform.position;
        float elapsed = 0f;

        while (elapsed < delayToExplode)
        {
            if (targetTransform != null && targetTransform.gameObject.activeInHierarchy)
                lastKnownPosition = targetTransform.position;

            elapsed += Time.deltaTime;
            yield return null;
        }

        SpawnExplosion(lastKnownPosition, explosionRadius);

        if (shakeAmplitude > 0f && CameraFeedback.IsOnScreen(lastKnownPosition))
            CameraFeedback.Shake(shakeDuration, shakeAmplitude, 35f);
    }

    private void SpawnExplosion(Vector3 position, float explosionRadius)
    {
        if (explosionVfx != null && ServiceLocator.TryGet(out BaseVfxPool pool))
        {
            pool.Spawn(explosionVfx, position, explosionRadius * 2f);
            return;
        }

        if (explosionPrefab == null) return;

        GameObject explosionObject = Instantiate(explosionPrefab, position, Quaternion.identity);
        explosionObject.transform.localScale = Vector3.one * explosionRadius * 2f;
    }
}
