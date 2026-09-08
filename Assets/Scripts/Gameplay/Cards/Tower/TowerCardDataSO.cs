using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "TowerCardData", menuName = "Scriptable Objects/Cards/TowerCardData")]
public class TowerCardDataSO : CardDataSO
{
    [Title("Tower Data")]
    public TowerType TowerType;
    [Tooltip("Sprite that will be used in the GhostTowerCard")]
    public Sprite TowerGhostSprite;
    public GameObject TowerPrefab;

    /// <summary>
    /// This card's stats, one row per in-match tower tier ("Damage Lvl 1/2/3"), each already scaled by the
    /// persistent card level. The two level axes are independent and both matter to the player: the card
    /// level is what they upgrade in the menu, the tier is what they pay for on the board, and this table
    /// is the only place they can see how the two compose before committing to either.
    /// </summary>
    public override IReadOnlyList<CardStatValue> GetStats(CardLevelScale scale)
    {
        TowerDataSO data = ResolveTowerData();
        if (data == null) return Array.Empty<CardStatValue>();

        List<CardStatValue> stats = new();

        // hideWhenAllZero: a support tower deals no damage at any tier, and "Damage 0" three times over
        // reads as a bug rather than as a design.
        AddPerTier(stats, data, CardStatId.Damage, "Damage", "0.#",
            level => data.GetDamageByLevel(level) * scale.Damage, hideWhenAllZero: true);

        AddPerTier(stats, data, CardStatId.Range, "Range", "0.##",
            level => data.GetRangeByLevel(level) * scale.Range);

        // PRESENTATION ONLY. The tower is authored and simulated in seconds-between-shots — cooldown
        // shrinks as attack speed grows, mirroring BaseServerTowerCombat's haste maths — but a cooldown is
        // a stat where lower is better, which reads backwards in a table whose every other row rewards a
        // bigger number. Inverting it here, at the one place the numbers are turned into text, leaves the
        // gameplay maths untouched: BaseServerTowerCombat still reads GetShootCooldownByLevel directly.
        AddPerTier(stats, data, CardStatId.AttackSpeed, "Hits Per Second", "0.00",
            level => ToRate(data.GetShootCooldownByLevel(level) / scale.AttackSpeed));

        // Walk the TowerDataSO hierarchy so each tower reports the stat it is actually bought for, the same
        // way SpellCardDataSO.GetStats walks the spell hierarchy. "0.#%" formats a raw fraction as a
        // percentage, so 0.3 reads as 30%.
        if (data is SlowTowerDataSO slow)
            AddPerTier(stats, data, CardStatId.EffectBonus, "Slow", "0.#%",
                level => slow.GetSlowPercentByLevel(level) * scale.EffectBonus);

        // Clamped because the scaled value is clamped in combat too: a maxed Torniquete strips the whole
        // resistance, and a row reading "112%" would promise something the aura cannot do.
        if (data is ResistTowerDataSO resist)
            AddPerTier(stats, data, CardStatId.EffectBonus, "Armor Break", "0.#%",
                level => Mathf.Clamp01(resist.GetResistClearPercentByLevel(level) * scale.EffectBonus));

        if (data is AuraTowerDataSO aura)
        {
            AddPerTier(stats, data, CardStatId.EffectBonus, "Ally Damage", "0.#%",
                level => aura.GetDamageBonusByLevel(level) * scale.EffectBonus);
            AddPerTier(stats, data, CardStatId.EffectBonus, "Ally Atk Speed", "0.#%",
                level => aura.GetAttackSpeedBonusByLevel(level) * scale.EffectBonus);
        }

        if (data is AnchorTowerDataSO anchor)
            AddPerTier(stats, data, CardStatId.Duration, "Hold", "0.0",
                level => anchor.GetHoldDurationByLevel(level) * scale.Duration);

        if (data is ManaTowerDataSO mana)
            AddPerTier(stats, data, CardStatId.EffectBonus, "Mana", "0.##",
                level => mana.GetManaPerTickByLevel(level) * scale.EffectBonus);

        return stats;
    }

    /// <summary>
    /// Appends one row per tower tier — or a SINGLE unlabelled row when every tier holds the same number.
    /// Three identical "Lvl 1/2/3" rows say nothing and cost three of the panel's ten slots, which is what
    /// pushes the rows that do differ out of view. Torniquete's hit rate and Espelho's range are flat by
    /// design, so this is the common case for support towers rather than an edge one.
    /// </summary>
    /// <param name="valueAt">The stat at one tier, already scaled by the card level.</param>
    /// <param name="hideWhenAllZero">Drop the stat entirely when no tier has a value.</param>
    private static void AddPerTier(List<CardStatValue> stats, TowerDataSO data, CardStatId id, string label,
        string format, Func<int, float> valueAt, bool hideWhenAllZero = false)
    {
        int maxLevel = Mathf.Max(1, data.MaxLevel);
        float first = valueAt(1);

        if (hideWhenAllZero)
        {
            bool anyValue = false;
            for (int level = 1; level <= maxLevel && !anyValue; level++) anyValue = valueAt(level) > 0f;
            if (!anyValue) return;
        }

        // The card level multiplies every tier by the same number, so whether the tiers differ is a
        // property of the authored data alone. That is what keeps the row SHAPE identical at every card
        // level, which CardProgressionSettingsSO.GetStatProgress relies on to pair current with next.
        bool varies = false;
        for (int level = 2; level <= maxLevel && !varies; level++)
            varies = !Mathf.Approximately(valueAt(level), first);

        if (!varies)
        {
            stats.Add(new CardStatValue(id, label, first, format));
            return;
        }

        for (int level = 1; level <= maxLevel; level++)
            stats.Add(new CardStatValue(id, $"{label} Lvl {level}", valueAt(level), format));
    }

    /// <summary>Shots per second from seconds per shot. Guards a 0 cooldown, which would read as infinity.</summary>
    private static float ToRate(float cooldown) => cooldown > 0f ? 1f / cooldown : 0f;

    /// <summary>Same prefab-based resolution <c>TowerCard.GetTowerDataSO()</c> uses, so there is no second
    /// reference that could disagree with what actually spawns.</summary>
    public TowerDataSO ResolveTowerData()
    {
        if (TowerPrefab == null) return null;
        return TowerPrefab.TryGetComponent(out TowerManager towerManager) ? towerManager.Data : null;
    }
}
