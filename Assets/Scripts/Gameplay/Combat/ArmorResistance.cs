using UnityEngine;

/// <summary>
/// Pure, server-side damage policy: resolves how much of a <see cref="DamageInfo"/> actually lands on a
/// target with a given armor color and off-color resistance. Kept separate from health state so it can be
/// reused (e.g. UI damage previews) and unit-tested without a live NetworkObject.
/// </summary>
public static class ArmorResistance
{
    /// <summary>
    /// Full damage when the attack is neutral (<see cref="ArmorColor.None"/>), the armor is neutral, or the
    /// colors match. Otherwise the target's off-color resistance applies, reduced by the attack's penetration.
    /// </summary>
    public static float Resolve(in DamageInfo hit, ArmorColor armorColor, float offColorResistance)
    {
        if (hit.Color == ArmorColor.None || armorColor == ArmorColor.None || hit.Color == armorColor)
            return hit.Amount;

        float effectiveResistance = Mathf.Clamp01(offColorResistance) * (1f - hit.ArmorPenetration);
        return hit.Amount * (1f - effectiveResistance);
    }
}
