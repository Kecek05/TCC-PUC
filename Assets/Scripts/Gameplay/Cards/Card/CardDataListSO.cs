using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "CardDataListSO", menuName = "Scriptable Objects/CardDataListSO")]
public class CardDataListSO : ScriptableObject
{
    public List<CardDataSO> CardDataList;
    
    public CardDataSO GetCardDataByType(CardType cardType)
    {
        return CardDataList.Find(cardData => cardData.CardType == cardType);
    }
}

