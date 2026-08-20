using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The multipliers a card's persistent level applies to its own stats, resolved once and then read many
/// times. Semantic names rather than a keyed bag, so gameplay reads like
/// <c>damage = baseDamage * scale.Damage</c> and no call site has to know about <see cref="CardStatId"/>.
/// </summary>
/// <remarks>
/// A readonly struct of seven floats: no allocation per spawn, and safe to copy into a coroutine or an
/// <see cref="SpellExecutionContext"/>. <see cref="AttackSpeed"/> is the odd one out — it <b>divides</b> a
/// tower's shoot cooldown, matching how <c>BaseServerTowerCombat</c> already applies its haste buff.
/// </remarks>
public readonly struct CardLevelScale
{
    public readonly int Level;

    public readonly float Damage;
    public readonly float Health;
    public readonly float Range;
    public readonly float AttackSpeed;
    public readonly float MoveSpeed;
    public readonly float Duration;
    public readonly float EffectBonus;

    /// <summary>Level 1: every stat at its authored value. The safe fallback everywhere.</summary>
    public static readonly CardLevelScale One = new(1);

    private CardLevelScale(int level)
    {
        Level = Mathf.Max(1, level);
        Damage = Health = Range = AttackSpeed = MoveSpeed = Duration = EffectBonus = 1f;
    }

    public CardLevelScale(int level, IReadOnlyList<CardStatGrowth> growth)
    {
        Level = Mathf.Max(1, level);

        Damage = Health = Range = AttackSpeed = MoveSpeed = Duration = EffectBonus = 1f;
        if (growth == null) return;

        int steps = Level - 1;
        if (steps <= 0) return;

        for (int i = 0; i < growth.Count; i++)
        {
            CardStatGrowth entry = growth[i];
            if (Mathf.Approximately(entry.PercentPerLevel, 0f)) continue;

            float multiplier = Mathf.Pow(1f + entry.PercentPerLevel, steps);

            switch (entry.Stat)
            {
                case CardStatId.Damage: Damage = multiplier; break;
                case CardStatId.Health: Health = multiplier; break;
                case CardStatId.Range: Range = multiplier; break;
                case CardStatId.AttackSpeed: AttackSpeed = multiplier; break;
                case CardStatId.MoveSpeed: MoveSpeed = multiplier; break;
                case CardStatId.Duration: Duration = multiplier; break;
                case CardStatId.EffectBonus: EffectBonus = multiplier; break;
            }
        }
    }

    /// <summary>Keyed access, for generic code such as the inspector preview.</summary>
    public float Get(CardStatId stat) => stat switch
    {
        CardStatId.Damage => Damage,
        CardStatId.Health => Health,
        CardStatId.Range => Range,
        CardStatId.AttackSpeed => AttackSpeed,
        CardStatId.MoveSpeed => MoveSpeed,
        CardStatId.Duration => Duration,
        CardStatId.EffectBonus => EffectBonus,
        _ => 1f
    };
}
