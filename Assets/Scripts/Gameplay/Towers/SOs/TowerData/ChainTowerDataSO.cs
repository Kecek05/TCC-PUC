using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for a tower whose shot jumps between nearby enemies, losing damage at every jump. Its value is a
/// function of how tightly packed the wave is, so a lone target makes it the worst-value tower in the set —
/// the declared weakness, expressed as maths rather than as a rule.
/// </summary>
[CreateAssetMenu(fileName = "ChainTowerData", menuName = "Scriptable Objects/Data/TowerData/ChainTowerData")]
public class ChainTowerDataSO : TowerDataSO
{
    [Title("Chain")]
    [MinValue(0)]
    [Tooltip("Extra enemies the bolt jumps to AFTER the primary target, per placement level.")]
    public int MaxHopsLevel1 = 2;
    [MinValue(0)] public int MaxHopsLevel2 = 3;
    [MinValue(0)] public int MaxHopsLevel3 = 4;

    [MinValue(0f)]
    [Tooltip("How far the bolt can reach from the enemy it just hit. Independent of the tower's own range: " +
             "once the bolt is in the wave it travels along the wave, not back to the tower.")]
    public float HopRadius = 0.9f;

    [PropertyRange(0f, 1f)]
    [Tooltip("Damage lost at each jump. 0.25 = every hop deals 75% of the previous one.")]
    public float DamageFalloffPercent = 0.25f;

    public int GetMaxHopsByLevel(int level)
    {
        switch (level)
        {
            case 1: return MaxHopsLevel1;
            case 2: return MaxHopsLevel2;
            case 3: return MaxHopsLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 hops.");
                return MaxHopsLevel1;
        }
    }
}
