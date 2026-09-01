using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Stats for the Anel: a tower whose output is a buff on the towers around it rather than anything aimed at
/// an enemy. It sits alongside <see cref="SlowTowerDataSO"/> — same per-level table, a different currency —
/// and like that one it is expected to leave the damage columns at 0.
/// </summary>
/// <remarks>
/// Both bonuses are fractions and both stack additively at the target, so two Anel rings covering the same
/// tower give it the sum. That is intentional: clustering is the card's cost, and the Aríete and Erosão
/// cards are the punishment for it.
/// </remarks>
[CreateAssetMenu(fileName = "AuraTowerData", menuName = "Scriptable Objects/Data/TowerData/AuraTowerData")]
public class AuraTowerDataSO : TowerDataSO
{
    [Title("Damage Aura")]
    [PropertyRange(0f, 2f)] public float DamageBonusLevel1 = 0.15f;
    [PropertyRange(0f, 2f)] public float DamageBonusLevel2 = 0.22f;
    [PropertyRange(0f, 2f)] public float DamageBonusLevel3 = 0.30f;

    [Title("Attack Speed Aura")]
    [PropertyRange(0f, 2f)] public float AttackSpeedBonusLevel1 = 0.12f;
    [PropertyRange(0f, 2f)] public float AttackSpeedBonusLevel2 = 0.18f;
    [PropertyRange(0f, 2f)] public float AttackSpeedBonusLevel3 = 0.25f;

    public float GetDamageBonusByLevel(int level)
    {
        switch (level)
        {
            case 1: return DamageBonusLevel1;
            case 2: return DamageBonusLevel2;
            case 3: return DamageBonusLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 damage bonus.");
                return DamageBonusLevel1;
        }
    }

    public float GetAttackSpeedBonusByLevel(int level)
    {
        switch (level)
        {
            case 1: return AttackSpeedBonusLevel1;
            case 2: return AttackSpeedBonusLevel2;
            case 3: return AttackSpeedBonusLevel3;
            default:
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 attack speed bonus.");
                return AttackSpeedBonusLevel1;
        }
    }
}
