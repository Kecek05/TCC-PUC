using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

/// <summary>
/// Simple object pool for CosmeticBullets to avoid GC allocations on mobile.
/// Clients use this to recycle bullet visuals instead of Instantiate/Destroy.
/// </summary>
public class CosmeticBulletPool : SerializedMonoBehaviour 
{
    public static CosmeticBulletPool Instance { get; private set; }

    [OdinSerialize] private Dictionary<CardType, CosmeticBullet> prefabs;
    [SerializeField] private int initialPoolSize = 20;
    
    private Dictionary<CardType, Queue<CosmeticBullet>> _pools = new();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            GameLog.Error("Multiple instances of CosmeticBulletPool detected. This is not allowed.");
            Destroy(this);
            return;
        }
        Prewarm();
    }

    private void Prewarm()
    {
        foreach (var kvp in prefabs)
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                CardType cardType = kvp.Key;
                CosmeticBullet bulletPrefab = kvp.Value;
                var bulletInstance = CreateInstance(bulletPrefab);
                bulletInstance.gameObject.SetActive(false);
            
                if (!_pools.ContainsKey(cardType))
                {
                    _pools.Add(cardType, new Queue<CosmeticBullet>());
                }
                _pools[cardType].Enqueue(bulletInstance);
            }
        }
    }

    public CosmeticBullet Get(CardType cardType)
    {
        if (!_pools.ContainsKey(cardType))
        {
            GameLog.Warn($"CosmeticBulletPool CardType {cardType} not found in the pool. Returning null.");
            return null;
        }

        if (!prefabs.ContainsKey(cardType))
        {
            GameLog.Warn($"CosmeticBulletPool CardType {cardType} not found in the prefabs dictionary. Returning null.");
            return null;
        }
        
        return _pools[cardType].Count > 0 ? _pools[cardType].Dequeue() : CreateInstance(prefabs[cardType]);
    }

    public void Return(CosmeticBullet bullet)
    {
        bullet.gameObject.SetActive(false);
        if (bullet.BulletCardType == CardType.None)
        {
            GameLog.Warn($"CosmeticBulletPool CardType is None. Enqueueing Skipped for:  {bullet.name}");
            return;
        }
        
        _pools[bullet.BulletCardType].Enqueue(bullet);
    }

    private CosmeticBullet CreateInstance(CosmeticBullet bulletPrefab)
    {
        var bullet = Instantiate(bulletPrefab, transform);
        bullet.gameObject.SetActive(true);
        bullet.Initialize(this);
        return bullet;
    }
}
