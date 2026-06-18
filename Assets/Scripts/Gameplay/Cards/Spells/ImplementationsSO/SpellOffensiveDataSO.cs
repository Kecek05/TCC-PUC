using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellOffensiveData", menuName = "Scriptable Objects/Data/Spells/SpellOffensiveDataSO")]
public class SpellOffensiveDataSO : SpellDataSO
{
    [Title("Offensive Data")]
    public float Damage = 5f;

    [Title("Color / Armor")]
    [Tooltip("This spell deals full damage to enemies of the same color, and reduced damage to others.")]
    public ArmorColor AttackColor = ArmorColor.None;
    [PropertyRange(0f, 1f)]
    [Tooltip("Fraction of an enemy's off-color resistance this spell ignores. 0 = normal, 1 = ignores armor entirely.")]
    public float ArmorPenetration = 0f;
}
