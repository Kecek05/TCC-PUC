using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// One pooled visual effect: which prefab, and how many instances to keep warm. Presenters reference the asset
/// rather than a scene object, which is what lets a presenter on a pooled enemy or tower prefab spawn effects
/// at all — a prefab cannot hold a reference into the scene. <see cref="BaseVfxPool"/> builds the pool for
/// an asset the first time it is asked for it (or at scene start, if listed there to be prewarmed).
/// </summary>
/// <remarks>
/// The prefab returns itself to the pool: its root <see cref="ParticleSystem"/> is authored with
/// <c>Stop Action = Disable</c>, and a disabled instance is exactly what the pool hands out next.
/// </remarks>
[CreateAssetMenu(fileName = "PooledVfx", menuName = "Scriptable Objects/Presentation/Pooled VFX")]
public class PooledVfxSO : ScriptableObject
{
    [Required, AssetsOnly]
    [Tooltip("Effect prefab. Its root ParticleSystem must use Stop Action = Disable so it returns to the pool.")]
    public GameObject Prefab;

    [Min(1)]
    [Tooltip("Instances created up front. Size it to the most that can be alive at once in a busy wave.")]
    public int PrewarmCount = 8;

    [Tooltip("Grow past the prewarm count instead of skipping the effect when every instance is busy.")]
    public bool CanExpand = true;
}
