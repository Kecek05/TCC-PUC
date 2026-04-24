using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple object pool for CosmeticBullets to avoid GC allocations on mobile.
/// Clients use this to recycle bullet visuals instead of Instantiate/Destroy.
/// </summary>
public class CosmeticBulletPool : MonoBehaviour
{
    public static CosmeticBulletPool Instance { get; private set; }

    [SerializeField] private CosmeticBullet prefab;
    [SerializeField] private int initialPoolSize = 20;

    private readonly Queue<CosmeticBullet> _pool = new();

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
        for (int i = 0; i < initialPoolSize; i++)
        {
            var bullet = CreateInstance();
            bullet.gameObject.SetActive(false);
            _pool.Enqueue(bullet);
        }
    }

    public CosmeticBullet Get()
    {
        var bullet = _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();
        bullet.gameObject.SetActive(true);
        return bullet;
    }

    public void Return(CosmeticBullet bullet)
    {
        bullet.gameObject.SetActive(false);
        _pool.Enqueue(bullet);
    }

    private CosmeticBullet CreateInstance()
    {
        var bullet = Instantiate(prefab, transform);
        bullet.Initialize(this);
        return bullet;
    }
}
