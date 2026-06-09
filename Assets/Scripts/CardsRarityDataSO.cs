using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "CardsRarityData", menuName = "Scriptable Objects/Data/CardsRarityDataSO")]
public class CardsRarityDataSO : ScriptableObject
{
    [TableList(AlwaysExpanded = true)]
    [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true)]
    [ValidateInput(nameof(IsComplete), "Must contain exactly one entry per CardRarityType.")]
    [SerializeField] private List<RarityData> rarityData = new();

    private Dictionary<CardRarityType, RarityData> _lookup;

    private void OnEnable() => _lookup = BuildLookup();

    private Dictionary<CardRarityType, RarityData> BuildLookup()
    {
        var map = new Dictionary<CardRarityType, RarityData>(rarityData.Count);
        foreach (var data in rarityData) map[data.rarityType] = data;
        return map;
    }

    public RarityData Get(CardRarityType rarity)
    {
        _lookup ??= BuildLookup();
        return _lookup[rarity];
    }

#if UNITY_EDITOR
    [Button(ButtonSizes.Medium), PropertyOrder(-1)]
    private void PopulateMissing()
    {
        foreach (CardRarityType rarity in Enum.GetValues(typeof(CardRarityType)))
            if (rarityData.All(d => d.rarityType != rarity))
                rarityData.Add(new RarityData { rarityType = rarity });

        rarityData = rarityData.OrderBy(d => d.rarityType).ToList();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private bool IsComplete(List<RarityData> list)
    {
        var all = (CardRarityType[])Enum.GetValues(typeof(CardRarityType));
        return list != null
            && list.Count == all.Length
            && all.All(r => list.Count(d => d.rarityType == r) == 1);
    }
#endif
}

[Serializable]
public struct RarityData
{
    public CardRarityType rarityType;
    public Color mainColor;
    public Color secondaryColor;
    public Color textColor;
}