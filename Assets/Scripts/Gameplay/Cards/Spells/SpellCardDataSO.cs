using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellCardData", menuName = "Scriptable Objects/Cards/SpellCardDataSO")]
public class SpellCardDataSO : CardDataSO
{
    [Title("Spell Data")]
    public SpellType SpellType;
    [Space(10f)]

    [Title("Placement Settings")]
    public bool CanUseInEnemyMap = false;
    public bool CanUseInLocalMap = true;
    [Space(10f)]

    [Title("Others")]
    [Tooltip("Sprite that will be used in the GhostSpellCard")]
    public Sprite SpellGhostSprite;
    public SpellDataSO SpellData;

    /// <summary>
    /// The team whose field this spell lands on, given the caster's team: the opponent for
    /// enemy-only spells (e.g. freeze), otherwise the caster's own team. Used to map both the
    /// cast point and the spell visual to the correct server-space side for translating players.
    /// </summary>
    public TeamType GetTargetFieldTeam(TeamType casterTeam)
    {
        bool enemyField = CanUseInEnemyMap && !CanUseInLocalMap;
        if (!enemyField) return casterTeam;
        return casterTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
    }

    /// <summary>Walks the SpellDataSO hierarchy so each spell reports only the stats it actually has.</summary>
    public override IReadOnlyList<CardStatValue> GetStats(CardLevelScale scale)
    {
        if (SpellData == null) return Array.Empty<CardStatValue>();

        List<CardStatValue> stats = new()
        {
            new CardStatValue(CardStatId.Range, "Radius", SpellData.Range * scale.Range, "0.##")
        };

        if (SpellData is SpellOffensiveDataSO offensive)
            stats.Add(new CardStatValue(CardStatId.Damage, "Damage", offensive.Damage * scale.Damage, "0.#"));

        if (SpellData is SpellEffectDataSO effect)
            stats.Add(new CardStatValue(CardStatId.Duration, "Duration", effect.Duration * scale.Duration, "0.0"));

        // "0.#%" formats the raw fraction as a percentage, so 0.22 reads as 22%.
        if (SpellData is SpellBuffDataSO buff)
            stats.Add(new CardStatValue(CardStatId.EffectBonus, "Atk Speed",
                buff.AttackSpeedBonus * scale.EffectBonus, "0.#%"));

        if (SpellData is SpellRageDataSO rage)
            stats.Add(new CardStatValue(CardStatId.EffectBonus, "Move Speed",
                rage.MoveSpeedBonus * scale.EffectBonus, "0.#%"));

        if (SpellData is SpellSlowDataSO slowData)
            stats.Add(new CardStatValue(CardStatId.EffectBonus, "Slow",
                slowData.SlowPercent * scale.EffectBonus, "0.#%"));

        return stats;
    }
}
