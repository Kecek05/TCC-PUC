using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Balance for what a finished match pays out. The winner gets gold plus a random card; the loser gets
/// gold only. Which card is drawn is weighted by rarity, so Legendaries stay rare without any per-card
/// authoring.
/// </summary>
[CreateAssetMenu(fileName = "RewardSettings", menuName = "Scriptable Objects/Progression/RewardSettingsSO")]
public class RewardSettingsSO : ScriptableObject
{
    [Title("Gold")]
    [MinValue(0)] public int WinGold = 120;
    [MinValue(0)] public int LoseGold = 40;

    [MinValue(0)]
    [Tooltip("Gold is rolled in +/- this range around the values above, so payouts are not identical every match.")]
    public int GoldVariance = 10;

    [Title("Card Reward")]
    [MinValue(1)]
    [Tooltip("Copies of the drawn card the winner receives.")]
    public int WinCardCopies = 3;

    [Title("Rarity Weights")]
    [InfoBox("Relative chance of drawing from each rarity. A rarity with no cards in CardDataList, or a " +
             "weight of 0, is skipped.")]
    [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true)]
#if UNITY_EDITOR
    [ValidateInput(nameof(HasPositiveWeight), "At least one rarity needs a weight above 0.")]
#endif
    [SerializeField] private List<RarityWeight> rarityWeights = new();

    [Title("Content")]
    [Required, Tooltip("Pool the card reward is drawn from.")]
    public CardDataListSO CardDataList;

    public IReadOnlyList<RarityWeight> RarityWeights => rarityWeights;

    public float GetWeight(CardRarityType rarity)
    {
        foreach (RarityWeight entry in rarityWeights)
            if (entry.Rarity == rarity) return Mathf.Max(0f, entry.Weight);

        return 0f;
    }

#if UNITY_EDITOR
    [Button(ButtonSizes.Medium), PropertyOrder(-1)]
    private void PopulateMissing()
    {
        foreach (CardRarityType rarity in Enum.GetValues(typeof(CardRarityType)))
            if (rarityWeights.All(w => w.Rarity != rarity))
                rarityWeights.Add(new RarityWeight { Rarity = rarity, Weight = DefaultWeight(rarity) });

        rarityWeights = rarityWeights.OrderBy(w => w.Rarity).ToList();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static float DefaultWeight(CardRarityType rarity) => rarity switch
    {
        CardRarityType.Common => 60f,
        CardRarityType.Rare => 25f,
        CardRarityType.Epic => 12f,
        CardRarityType.Legendary => 3f,
        _ => 0f // None is not a real rarity, so it never drops.
    };

    private bool HasPositiveWeight(List<RarityWeight> list) => list != null && list.Any(w => w.Weight > 0f);
#endif
}

[Serializable]
public struct RarityWeight
{
    [TableColumnWidth(140, Resizable = false)]
    public CardRarityType Rarity;

    [MinValue(0f)]
    public float Weight;
}
