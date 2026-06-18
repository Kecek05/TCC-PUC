using DG.Tweening;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ClientSlamTowerCombat : BaseClientTowerCombat
{
    [Title("Slam Bullet GFX")]
    [Tooltip("Stationary visual spawned at the tower on each slam (e.g. CircleTowerBullet1). Not pooled -- it scales to the slam radius, fades out, and self-destroys.")]
    [SerializeField] private GameObject slamBulletPrefab;
    [SerializeField] private float bulletFadeDuration = 0.5f;
    [SerializeField] private Ease bulletFadeEase = Ease.OutQuad;

    /// <summary>
    /// Server → clients: play the slam pulse. Cosmetic only — damage is applied server-side in
    /// <see cref="ServerSlamTowerCombat"/>. Spawns a stationary bullet GFX at the tower, scaled to cover the
    /// tower's current range, that smoothly fades its transparency out and is then destroyed (no pooling).
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    public void PlaySlamRpc()
    {
        SpawnSlamBulletGFX();
        TriggerOnBulletFired();
    }

    private void SpawnSlamBulletGFX()
    {
        if (slamBulletPrefab == null) return;

        GameObject bullet = Instantiate(slamBulletPrefab, transform.position, Quaternion.identity);

        SpriteRenderer spriteRenderer = bullet.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Destroy(bullet);
            return;
        }

        ScaleBulletToRange(bullet.transform, spriteRenderer);

        // Fade the existing sprite alpha down to fully transparent, then clean up. SetLink kills the
        // tween if the bullet is destroyed first (e.g. scene unload) so DOTween never touches a dead object.
        spriteRenderer.DOFade(0f, bulletFadeDuration)
            .SetEase(bulletFadeEase)
            .SetLink(bullet)
            .OnComplete(() => Destroy(bullet));
    }
    
    private void ScaleBulletToRange(Transform bulletRoot, SpriteRenderer sr)
    {
        float range = CurrentRange();
        if (range <= 0f || sr.sprite == null) return;

        float spriteDiameter = sr.sprite.bounds.size.x;
        if (spriteDiameter <= 0f) return;

        bulletRoot.localScale = Vector3.one * (range * 2f / spriteDiameter);
    }
    
    private float CurrentRange()
    {
        if (_towerManager == null) _towerManager = GetComponent<TowerManager>();
        if (_towerManager == null || _towerManager.Data == null) return 0f;

        int level = 1;
        if (serverTowerCombat != null)
            level = Mathf.Clamp(serverTowerCombat.TowerLevel.Value, 1, _towerManager.Data.MaxLevel);

        return _towerManager.Data.GetRangeByLevel(level);
    }
}
