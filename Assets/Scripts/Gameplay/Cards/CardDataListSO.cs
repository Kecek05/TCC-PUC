using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "CardDataListSO", menuName = "Scriptable Objects/CardDataListSO")]
public class CardDataListSO : ScriptableObject
{
    [SerializeField] private List<CardDataSO> cardDataList;
    
    public CardDataSO GetCardDataByType(CardType cardType)
    {
        return cardDataList.Find(cardData => cardData.CardType == cardType);
    }
}

public enum CardType
{
    None,
    TowerCircle,
    TowerSquare,
    SpellFireball
}