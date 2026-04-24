using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "CardDataListSO", menuName = "Scriptable Objects/CardDataListSO")]
public class CardDataListSO : ScriptableObject, ICardCostProvider
{
    public List<CardDataSO> CardDataList;

    public CardDataSO GetCardDataByType(CardType cardType)
    {
        return CardDataList.Find(cardData => cardData.CardType == cardType);
    }

    public int GetCost(CardType cardType)
    {
        CardDataSO data = GetCardDataByType(cardType);
        if (data == null)
        {
            Debug.LogError($"[CardDataListSO] CardDataSO not found for {cardType}");
            return int.MaxValue;
        }
        return data.Cost;
    }
}
