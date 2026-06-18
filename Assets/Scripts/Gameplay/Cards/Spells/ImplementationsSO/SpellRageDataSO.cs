using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellRageData", menuName = "Scriptable Objects/Data/Spells/SpellRageDataSO")]
public class SpellRageDataSO : SpellEffectDataSO
{
    [Title("Rage Data")]
    [PropertyRange(0f, 2f)]
    [Tooltip("Move-speed bonus granted to each troop inside the zone, as a fraction (0.2 = +20%). " +
             "Applied once per troop per cast; independent Rage casts stack additively.")]
    public float MoveSpeedBonus = 0.2f;

    [PropertyRange(0f, 5f)]
    [Tooltip("Seconds the buff lingers on a troop after it leaves the zone radius (or after the zone expires).")]
    public float LingerDuration = 1f;
}
