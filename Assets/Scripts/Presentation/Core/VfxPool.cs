using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Scene service that spawns pooled effects by asset, one Feel <see cref="MMSimpleObjectPooler"/> per
/// <see cref="PooledVfxSO"/>. A spawn is a scan for an inactive instance plus a position write — no
/// allocation — and the instance hands itself back by disabling itself when its particles finish.
/// </summary>
public class VfxPool : BaseVfxPool
{
    [Title("Prewarm")]
    [Tooltip("Effects whose pools are built at scene start, so the first one in a match does not hitch.")]
    [SerializeField] private List<PooledVfxSO> prewarm = new();

    private readonly Dictionary<PooledVfxSO, MMSimpleObjectPooler> _pools = new();

    private void Awake()
    {
        ServiceLocator.Register<BaseVfxPool>(this);

        foreach (PooledVfxSO vfx in prewarm)
            GetPool(vfx);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<BaseVfxPool>();
    }

    public override PooledVfx Spawn(PooledVfxSO vfx, Vector3 position, float scale = 1f)
        => SpawnInternal(vfx, position, scale, false, default);

    public override PooledVfx Spawn(PooledVfxSO vfx, Vector3 position, Color tint, float scale = 1f)
        => SpawnInternal(vfx, position, scale, true, tint);

    private PooledVfx SpawnInternal(PooledVfxSO vfx, Vector3 position, float scale, bool applyTint, Color tint)
    {
        MMSimpleObjectPooler pool = GetPool(vfx);
        if (pool == null) return null;

        GameObject instance = pool.GetPooledGameObject();
        if (instance == null) return null;

        Transform t = instance.transform;
        t.SetPositionAndRotation(position, Quaternion.identity);
        t.localScale = new Vector3(scale, scale, scale);

        PooledVfx effect = instance.GetComponent<PooledVfx>();
        if (applyTint && effect != null) effect.Tint(tint);

        instance.SetActive(true);
        return effect;
    }

    private MMSimpleObjectPooler GetPool(PooledVfxSO vfx)
    {
        if (vfx == null || vfx.Prefab == null) return null;
        if (_pools.TryGetValue(vfx, out MMSimpleObjectPooler pool)) return pool;

        // Built inactive: the pooler fills itself in Awake, which must only run once it knows what to pool.
        GameObject holder = new GameObject(vfx.name);
        holder.SetActive(false);
        holder.transform.SetParent(transform, false);

        pool = holder.AddComponent<MMSimpleObjectPooler>();
        pool.GameObjectToPool = vfx.Prefab;
        pool.PoolSize = vfx.PrewarmCount;
        pool.PoolCanExpand = vfx.CanExpand;
        pool.NestWaitingPool = true;
        pool.NestUnderThis = true;

        holder.SetActive(true);

        _pools.Add(vfx, pool);
        return pool;
    }
}
