using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "CardDataListSO", menuName = "Scriptable Objects/CardDataListSO")]
public class CardDataListSO : ScriptableObject
{
    [SerializeField] private List<CardDataSO> cardDataList;
    
    public CardDataSO GetCardDataById(int id)
    {
        return cardDataList.Find(card => card.CardId == id);
    }
}
