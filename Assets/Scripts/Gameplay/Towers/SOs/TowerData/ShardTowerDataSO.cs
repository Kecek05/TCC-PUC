using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for a tower whose damage cascades: the shot itself is ordinary, but a kill sprays fragments at the
/// neighbours. It extends <see cref="ExplosionTowerDataSO"/> because the fragment burst is geometrically the
/// same thing as an explosion — a radius around a point — so the per-level radius table is reused instead of
/// duplicated.
/// </summary>
[CreateAssetMenu(fileName = "ShardTowerData", menuName = "Scriptable Objects/Data/TowerData/ShardTowerData")]
public class ShardTowerDataSO : ExplosionTowerDataSO
{
    [Title("Shard")]
    [PropertyRange(0f, 2f)]
    [Tooltip("Fragment damage as a fraction of the shot that killed. 0.5 = each neighbour takes half. " +
             "It rides the shot's damage, so it scales with tower level and card level for free.")]
    public float FragmentDamagePercent = 0.5f;
}
