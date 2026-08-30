using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for a tower whose output is a slow aura rather than damage, mirroring how
/// <see cref="ExplosionTowerDataSO"/> adds an AoE radius on top of the shared per-level table. The damage
/// columns stay on the asset because the base reads them, but a slow tower is expected to leave them at 0 —
/// its whole contribution is time, not damage.
/// </summary>
[CreateAssetMenu(fileName = "SlowTowerData", menuName = "Scriptable Objects/Data/TowerData/SlowTowerData")]
public class SlowTowerDataSO : TowerDataSO
{
    [Title("Slow Aura")]
    [PropertyRange(0f, 1f)] public float SlowPercentLevel1 = 0.30f;
    [PropertyRange(0f, 1f)] public float SlowPercentLevel2 = 0.40f;
    [PropertyRange(0f, 1f)] public float SlowPercentLevel3 = 0.50f;

    public float GetSlowPercentByLevel(int level)
    {
        switch (level)
        {
            case 1: return SlowPercentLevel1;
            case 2: return SlowPercentLevel2;
            case 3: return SlowPercentLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 slow percent.");
                return SlowPercentLevel1;
        }
    }
}
