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
    /// Stats of a freshly placed tower (in-match level 1) at this card level. The in-match placement
    /// upgrade is a separate axis and multiplies on top of these at runtime.
    /// </summary>
    public override IReadOnlyList<CardStatValue> GetStats(CardLevelScale scale)
    {
        TowerDataSO data = ResolveTowerData();
        if (data == null) return Array.Empty<CardStatValue>();

        List<CardStatValue> stats = new()
        {
            new CardStatValue(CardStatId.Range, "Range", data.RangeLevel1 * scale.Range, "0.##"),
            // Cooldown shrinks as attack speed grows, mirroring BaseServerTowerCombat's haste maths.
            new CardStatValue(CardStatId.AttackSpeed, "Hit Speed", data.ShootCooldownLevel1 / scale.AttackSpeed, "0.00"),
        };

        // A support tower's damage column is 0 by design, and printing "Damage 0" reads as a bug rather
        // than as a design. Show the number only when the tower actually deals some.
        if (data.DamageLevel1 > 0f)
            stats.Insert(0, new CardStatValue(CardStatId.Damage, "Damage", data.DamageLevel1 * scale.Damage, "0.#"));

        // Walk the TowerDataSO hierarchy so each tower reports the stat it is actually bought for, the same
        // way SpellCardDataSO.GetStats walks the spell hierarchy. "0.#%" formats a raw fraction as a
        // percentage, so 0.3 reads as 30%.
        if (data is SlowTowerDataSO slow)
            stats.Add(new CardStatValue(CardStatId.EffectBonus, "Slow",
                slow.SlowPercentLevel1 * scale.EffectBonus, "0.#%"));

        if (data is AuraTowerDataSO aura)
        {
            stats.Add(new CardStatValue(CardStatId.EffectBonus, "Ally Damage",
                aura.DamageBonusLevel1 * scale.EffectBonus, "0.#%"));
            stats.Add(new CardStatValue(CardStatId.EffectBonus, "Ally Atk Speed",
                aura.AttackSpeedBonusLevel1 * scale.EffectBonus, "0.#%"));
        }

        if (data is AnchorTowerDataSO anchor)
            stats.Add(new CardStatValue(CardStatId.Duration, "Hold",
                anchor.HoldDurationLevel1 * scale.Duration, "0.0"));

        if (data is ManaTowerDataSO mana)
            stats.Add(new CardStatValue(CardStatId.EffectBonus, "Mana",
                mana.ManaPerTickLevel1 * scale.EffectBonus, "0.##"));

        return stats;
    }

    /// <summary>Same prefab-based resolution <c>TowerCard.GetTowerDataSO()</c> uses, so there is no second
    /// reference that could disagree with what actually spawns.</summary>
    public TowerDataSO ResolveTowerData()
    {
        if (TowerPrefab == null) return null;
        return TowerPrefab.TryGetComponent(out TowerManager towerManager) ? towerManager.Data : null;
    }
}
