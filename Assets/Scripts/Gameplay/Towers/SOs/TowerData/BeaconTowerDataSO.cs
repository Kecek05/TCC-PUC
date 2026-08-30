using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for a beam tower: damage per tick is the base table, and the beam multiplies it the longer it
/// stays on the SAME target. Switching target resets the ramp, which is what makes the card lose to a swarm
/// and win against a boss without either being written as a special case.
/// </summary>
[CreateAssetMenu(fileName = "BeaconTowerData", menuName = "Scriptable Objects/Data/TowerData/BeaconTowerData")]
public class BeaconTowerDataSO : TowerDataSO
{
    [Title("Beam Ramp")]
    [Tooltip("How much of the base damage the beam gains per second held on one target. 0.35 = +35%/s.")]
    public float RampPerSecond = 0.35f;

    [MinValue(1f)]
    [Tooltip("Ceiling on the ramp multiplier. 3 = the beam tops out at triple damage.")]
    public float MaxRampMultiplier = 3f;
}
