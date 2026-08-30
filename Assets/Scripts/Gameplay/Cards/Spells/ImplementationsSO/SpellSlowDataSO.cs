using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// A timed zone that slows enemies instead of buffing them. It is the enemy-side twin of
/// <see cref="SpellBuffDataSO"/>: same Duration axis, but the stat it carries is a fraction of move speed
/// removed rather than a bonus added. Kept as its own subclass so the card stat panel can label it
/// correctly and so a slow can never be mistaken for a Rage.
/// </summary>
[CreateAssetMenu(fileName = "SpellSlowData", menuName = "Scriptable Objects/Data/Spells/SpellSlowDataSO")]
public class SpellSlowDataSO : SpellEffectDataSO
{
    [Title("Slow Data")]
    [PropertyRange(0f, 1f)]
    [Tooltip("Fraction of move speed removed while inside the zone. 0.4 = the enemy walks at 60% speed.")]
    public float SlowPercent = 0.4f;
}
