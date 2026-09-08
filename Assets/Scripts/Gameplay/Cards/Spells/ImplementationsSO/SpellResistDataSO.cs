using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// A timed zone that strips armor instead of slowing. The spell-form twin of <see cref="ResistTowerDataSO"/>
/// and the sibling of <see cref="SpellSlowDataSO"/>: same Duration axis, but the fraction it carries is
/// off-color resistance removed. Kept as its own subclass so the card stat panel can label it correctly.
/// </summary>
[CreateAssetMenu(fileName = "SpellResistData", menuName = "Scriptable Objects/Data/Spells/SpellResistDataSO")]
public class SpellResistDataSO : SpellEffectDataSO
{
    [Title("Resist Clear Data")]
    [PropertyRange(0f, 1f)]
    [Tooltip("Fraction of off-color resistance removed while inside the zone. 1 = the armor colour stops " +
             "mattering entirely, which is what this zone used to do at every level.")]
    public float ResistClearPercent = 0.5f;
}
