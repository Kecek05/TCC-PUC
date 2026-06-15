using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellBuffData", menuName = "Scriptable Objects/Data/Spells/SpellBuffDataSO")]
public class SpellBuffDataSO : SpellEffectDataSO
{
    [Title("Buff Data")]
    [Tooltip("Attack-speed bonus granted to each ally tower in range, as a fraction (0.2 = +20%). " +
             "Overlapping zones stack additively.")]
    public float AttackSpeedBonus = 0.2f;
}
