using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

// [CreateAssetMenu(fileName = "CardData", menuName = "Scriptable Objects/CardData")]
public class CardDataSO : ScriptableObject
{
    [Title("Card Properties")]
    public CardType CardType;
    public ExistingTypesOfCard ExistingType;
    public CardRarityType Rarity;
    public AbstractCard CardPrefab;
    public string CardName;
    [TextArea] public string Description;
    public Sprite CardImage;
    public Color CardColor = Color.white;
    public int Cost;
    public bool UseCustomSizeCardInMenu = false;
    [ShowIf("UseCustomSizeCardInMenu")]public Vector2 CustomSizeCardInMenu;
    public bool UseCustomPositionCardInMenu = false;
    [ShowIf("UseCustomPositionCardInMenu")]public Vector2 CustomPositionCardInMenu;

    [Title("Progression")]
    [Tooltip("Off: this card grows by the default table on CardProgressionSettings. " +
             "On: it uses its own growth below and ignores the default entirely.")]
    public bool OverrideStatGrowth;

    [ShowIf(nameof(OverrideStatGrowth))]
    [TableList(AlwaysExpanded = true)]
    public List<CardStatGrowth> StatGrowth = new();

    /// <summary>
    /// This card's own stats with a level scale applied. Overridden per card family, because a tower, a
    /// troop and a spell have nothing in common statistically. One method feeds both the inspector preview
    /// and the in-game "current vs next level" panel, so the two can never drift apart.
    /// </summary>
    public virtual IReadOnlyList<CardStatValue> GetStats(CardLevelScale scale) => Array.Empty<CardStatValue>();

#if UNITY_EDITOR
    private const string ProgressionSettingsPath =
        "Assets/ScriptableObjects/Progression/CardProgressionSettings.asset";

    /// <summary>
    /// Live, read-only balance table: what this card costs and what its stats become at every level.
    /// Pure derived data — it updates as the growth percentages above are edited, so a designer never has
    /// to run the game to see the curve they just authored.
    /// </summary>
    [Title("Level Preview")]
    [ShowInInspector, ReadOnly, PropertyOrder(200)]
    [TableList(AlwaysExpanded = true, IsReadOnly = true)]
    [InfoBox("Costs come from CardProgressionSettings; stats come from this card's gameplay SO scaled by " +
             "the growth above.", InfoMessageType.None)]
    private List<LevelPreviewRow> LevelPreview => BuildLevelPreview();

    private List<LevelPreviewRow> BuildLevelPreview()
    {
        List<LevelPreviewRow> rows = new();

        CardProgressionSettingsSO progression =
            UnityEditor.AssetDatabase.LoadAssetAtPath<CardProgressionSettingsSO>(ProgressionSettingsPath);

        if (progression == null)
        {
            rows.Add(new LevelPreviewRow { Level = 0, Stats = $"No settings asset at {ProgressionSettingsPath}" });
            return rows;
        }

        int maxLevel = progression.GetMaxLevel(Rarity);

        for (int level = 1; level <= maxLevel; level++)
        {
            LevelPreviewRow row = new() { Level = level, Stats = DescribeStats(progression, level) };

            // Costs are what it takes to REACH this level, i.e. the step out of the previous one.
            if (level > 1 && progression.TryGetStep(Rarity, level - 1, out CardLevelStep step))
            {
                row.Copies = step.CopiesRequired;
                row.Gold = step.GoldCost;
            }

            rows.Add(row);
        }

        return rows;
    }

    private string DescribeStats(CardProgressionSettingsSO progression, int level)
    {
        IReadOnlyList<CardStatValue> stats = progression.GetStatsAtLevel(this, level);
        if (stats == null || stats.Count == 0) return "-";

        StringBuilder sb = new();
        for (int i = 0; i < stats.Count; i++)
        {
            if (i > 0) sb.Append("   ");
            sb.Append(stats[i].Label).Append(' ').Append(stats[i].Display);
        }

        return sb.ToString();
    }

    private class LevelPreviewRow
    {
        [TableColumnWidth(60, Resizable = false), ReadOnly] public int Level;
        [TableColumnWidth(70, Resizable = false), ReadOnly] public int Copies;
        [TableColumnWidth(80, Resizable = false), ReadOnly] public int Gold;
        [ReadOnly] public string Stats;
    }
#endif
}
