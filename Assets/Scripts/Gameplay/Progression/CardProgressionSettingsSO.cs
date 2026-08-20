using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// The single balance asset for card progression: how many levels a rarity has, what each level costs in
/// copies and gold, and how much a card's stats grow per level. Nothing here is per-player — it is pure
/// designer-authored data, read by the deck UI (costs) and by the server (stat scaling).
/// </summary>
[CreateAssetMenu(fileName = "CardProgressionSettings", menuName = "Scriptable Objects/Progression/CardProgressionSettingsSO")]
public class CardProgressionSettingsSO : ScriptableObject
{
    [Title("Level Costs")]
    [InfoBox("One entry per rarity. Steps[i] is the cost of going from level i+1 to level i+2, so a " +
             "MaxLevel of 10 needs 9 steps.")]
    [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true, ShowFoldout = true)]
#if UNITY_EDITOR
    [ValidateInput(nameof(IsComplete), "Must contain exactly one entry per CardRarityType.")]
#endif
    [SerializeField] private List<RarityProgression> rarities = new();

    [Title("Default Stat Growth")]
    [InfoBox("Applied to every card that does not tick 'Override Stat Growth'. Stats left out of this " +
             "table keep the value authored on their gameplay SO at every level.")]
    [TableList(AlwaysExpanded = true)]
    [SerializeField]
    private List<CardStatGrowth> defaultStatGrowth = new()
    {
        new CardStatGrowth(CardStatId.Damage, 0.1f),
        new CardStatGrowth(CardStatId.Health, 0.1f),
        new CardStatGrowth(CardStatId.EffectBonus, 0.05f),
        new CardStatGrowth(CardStatId.Duration, 0.03f),
    };

    [Title("Wave Enemies")]
    [MinValue(1)]
    [Tooltip("Wave enemies have no casting player, so they always use this level.")]
    public int WaveEnemyCardLevel = 1;

    private Dictionary<CardRarityType, RarityProgression> _lookup;

    private void OnEnable() => _lookup = null;

    public int GetMaxLevel(CardRarityType rarity) => Find(rarity)?.MaxLevel ?? 1;

    /// <summary>Cost of going from <paramref name="fromLevel"/> to the next level. False at max level.</summary>
    public bool TryGetStep(CardRarityType rarity, int fromLevel, out CardLevelStep step)
    {
        step = default;

        RarityProgression progression = Find(rarity);
        if (progression == null) return false;

        int index = fromLevel - 1;
        if (index < 0 || index >= progression.Steps.Count) return false;
        if (fromLevel >= progression.MaxLevel) return false;

        step = progression.Steps[index];
        return true;
    }

    /// <summary>The growth table a card actually uses: its own override, or the shared default.</summary>
    public IReadOnlyList<CardStatGrowth> GetGrowth(CardDataSO card)
    {
        if (card != null && card.OverrideStatGrowth && card.StatGrowth != null && card.StatGrowth.Count > 0)
            return card.StatGrowth;

        return defaultStatGrowth;
    }

    public CardLevelScale GetScale(CardDataSO card, int level)
    {
        if (card == null || level <= 1) return CardLevelScale.One;

        int maxLevel = GetMaxLevel(card.Rarity);
        return new CardLevelScale(Mathf.Clamp(level, 1, maxLevel), GetGrowth(card));
    }

    /// <summary>
    /// Scale from the shared default growth, for things that have no card behind them at all -
    /// wave enemies, which are authored straight into WaveDataSO.
    /// </summary>
    public CardLevelScale GetDefaultScale(int level) =>
        level <= 1 ? CardLevelScale.One : new CardLevelScale(level, defaultStatGrowth);

    /// <summary>A card's own stats at a level, for the inspector preview and the future stat panel.</summary>
    public IReadOnlyList<CardStatValue> GetStatsAtLevel(CardDataSO card, int level)
    {
        if (card == null) return Array.Empty<CardStatValue>();
        return card.GetStats(GetScale(card, level));
    }

    private RarityProgression Find(CardRarityType rarity)
    {
        _lookup ??= BuildLookup();
        return _lookup.TryGetValue(rarity, out RarityProgression progression) ? progression : null;
    }

    private Dictionary<CardRarityType, RarityProgression> BuildLookup()
    {
        Dictionary<CardRarityType, RarityProgression> map = new(rarities.Count);
        foreach (RarityProgression progression in rarities)
            if (progression != null) map[progression.Rarity] = progression;

        return map;
    }

#if UNITY_EDITOR
    [Button(ButtonSizes.Medium), PropertyOrder(-1)]
    private void PopulateMissing()
    {
        foreach (CardRarityType rarity in Enum.GetValues(typeof(CardRarityType)))
            if (rarities.All(r => r.Rarity != rarity))
                rarities.Add(new RarityProgression { Rarity = rarity });

        rarities = rarities.OrderBy(r => r.Rarity).ToList();

        foreach (RarityProgression progression in rarities) progression.EnsureStepCount();

        _lookup = null;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private bool IsComplete(List<RarityProgression> list)
    {
        CardRarityType[] all = (CardRarityType[])Enum.GetValues(typeof(CardRarityType));
        return list != null
               && list.Count == all.Length
               && all.All(r => list.Count(p => p.Rarity == r) == 1);
    }
#endif
}

/// <summary>Level cap and per-level cost table for one rarity.</summary>
[Serializable]
public class RarityProgression
{
    [HorizontalGroup("Head"), LabelWidth(60)]
    public CardRarityType Rarity;

    [HorizontalGroup("Head"), LabelWidth(75), MinValue(1)]
    [OnValueChanged(nameof(EnsureStepCount))]
    public int MaxLevel = 10;

    [TableList(AlwaysExpanded = true)]
    public List<CardLevelStep> Steps = new();

    /// <summary>
    /// Fills the whole cost table from a geometric curve, so a designer can get a sane ramp in one click and
    /// then hand-tune individual rows.
    /// </summary>
    [Button(ButtonSizes.Medium), PropertyOrder(10)]
    public void AutoFill(int baseCopies = 2, float copiesGrowth = 1.7f, int baseGold = 50, float goldGrowth = 2.1f)
    {
        EnsureStepCount();

        for (int i = 0; i < Steps.Count; i++)
        {
            CardLevelStep step = Steps[i];
            step.FromLevel = i + 1;
            step.CopiesRequired = Mathf.Max(1, Mathf.RoundToInt(baseCopies * Mathf.Pow(copiesGrowth, i)));
            step.GoldCost = Mathf.Max(0, Mathf.RoundToInt(baseGold * Mathf.Pow(goldGrowth, i)));
            Steps[i] = step;
        }
    }

    /// <summary>Keeps the table exactly <c>MaxLevel - 1</c> rows long and its FromLevel column honest.</summary>
    public void EnsureStepCount()
    {
        int wanted = Mathf.Max(0, MaxLevel - 1);

        Steps ??= new List<CardLevelStep>();

        while (Steps.Count > wanted) Steps.RemoveAt(Steps.Count - 1);
        while (Steps.Count < wanted) Steps.Add(new CardLevelStep { CopiesRequired = 1, GoldCost = 0 });

        for (int i = 0; i < Steps.Count; i++)
        {
            CardLevelStep step = Steps[i];
            step.FromLevel = i + 1;
            Steps[i] = step;
        }
    }
}

/// <summary>What it costs to leave <see cref="FromLevel"/> for the next one.</summary>
[Serializable]
public struct CardLevelStep
{
    [ReadOnly, TableColumnWidth(80, Resizable = false), LabelText("Lvl")]
    public int FromLevel;

    [MinValue(1), TableColumnWidth(110)]
    public int CopiesRequired;

    [MinValue(0), TableColumnWidth(110)]
    public int GoldCost;
}
