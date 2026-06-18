using UnityEngine;

/// <summary>
/// An immutable description of a single hit: how much damage, of what color, and how much of the
/// target's off-color armor it ignores. Passed to <see cref="IDamageable.TakeDamage"/> so the target
/// (which owns its armor) resolves the final amount in one place — see <see cref="ArmorResistance"/>.
/// </summary>
public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly ArmorColor Color;

    /// <summary>0 = obeys the target's off-color resistance fully; 1 = ignores it entirely (true off-color damage).</summary>
    public readonly float ArmorPenetration;

    public DamageInfo(float amount, ArmorColor color, float armorPenetration = 0f)
    {
        Amount = amount;
        Color = color;
        ArmorPenetration = Mathf.Clamp01(armorPenetration);
    }

    /// <summary>Colorless (true) damage — bypasses armor entirely regardless of the target's color.</summary>
    public static DamageInfo Neutral(float amount) => new(amount, ArmorColor.None);
}
