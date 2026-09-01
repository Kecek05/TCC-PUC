using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for the Âncora: it grabs a single enemy, drags it back down its own lane and pins it there. No
/// damage — the whole contribution is the bunching that happens behind the enemy it grabbed, which is what
/// the area towers are waiting for.
/// </summary>
/// <remarks>
/// The grab cadence is the shared <c>ShootCooldown</c> column, so the "one at a time" limit is authored the
/// same way every other tower's rate of fire is rather than through a field of its own.
/// </remarks>
[CreateAssetMenu(fileName = "AnchorTowerData", menuName = "Scriptable Objects/Data/TowerData/AnchorTowerData")]
public class AnchorTowerDataSO : TowerDataSO
{
    [Title("Hold")]
    [Unit(Units.Second)] public float HoldDurationLevel1 = 1f;
    [Unit(Units.Second)] public float HoldDurationLevel2 = 1.3f;
    [Unit(Units.Second)] public float HoldDurationLevel3 = 1.6f;

    [Title("Pull")]
    [Tooltip("World units dragged backwards along the lane at the moment of the grab. 0 = pure hold.")]
    [Min(0f)] public float PullDistanceLevel1 = 0.5f;
    [Min(0f)] public float PullDistanceLevel2 = 0.75f;
    [Min(0f)] public float PullDistanceLevel3 = 1f;

    public float GetHoldDurationByLevel(int level)
    {
        switch (level)
        {
            case 1: return HoldDurationLevel1;
            case 2: return HoldDurationLevel2;
            case 3: return HoldDurationLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 hold duration.");
                return HoldDurationLevel1;
        }
    }

    public float GetPullDistanceByLevel(int level)
    {
        switch (level)
        {
            case 1: return PullDistanceLevel1;
            case 2: return PullDistanceLevel2;
            case 3: return PullDistanceLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 pull distance.");
                return PullDistanceLevel1;
        }
    }
}
