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

        return new[]
        {
            new CardStatValue(CardStatId.Damage, "Damage", data.DamageLevel1 * scale.Damage, "0.#"),
            new CardStatValue(CardStatId.Range, "Range", data.RangeLevel1 * scale.Range, "0.##"),
            // Cooldown shrinks as attack speed grows, mirroring BaseServerTowerCombat's haste maths.
            new CardStatValue(CardStatId.AttackSpeed, "Hit Speed", data.ShootCooldownLevel1 / scale.AttackSpeed, "0.00"),
        };
    }

    /// <summary>Same prefab-based resolution <c>TowerCard.GetTowerDataSO()</c> uses, so there is no second
    /// reference that could disagree with what actually spawns.</summary>
    public TowerDataSO ResolveTowerData()
    {
        if (TowerPrefab == null) return null;
        return TowerPrefab.TryGetComponent(out TowerManager towerManager) ? towerManager.Data : null;
    }
}
