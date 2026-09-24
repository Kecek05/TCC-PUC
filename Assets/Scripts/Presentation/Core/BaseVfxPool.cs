using UnityEngine;

/// <summary>
/// Spawns pooled visual effects by asset. Registered in <see cref="ServiceLocator"/> by the scene's instance.
/// </summary>
public abstract class BaseVfxPool : MonoBehaviour
{
    /// <summary>Plays <paramref name="vfx"/> at a world position. Returns null when the pool is exhausted and
    /// may not grow — a missing effect is cosmetic and never worth an exception.</summary>
    public abstract PooledVfx Spawn(PooledVfxSO vfx, Vector3 position, float scale = 1f);

    /// <summary>As <see cref="Spawn(PooledVfxSO, Vector3, float)"/>, tinting the effect's tintable systems.</summary>
    public abstract PooledVfx Spawn(PooledVfxSO vfx, Vector3 position, Color tint, float scale = 1f);
}
