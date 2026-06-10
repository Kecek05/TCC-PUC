using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckUIController : MonoBehaviour
{
    private class CardInDeckInfo
    {
        public SingleCardInDeck SingleCardInDeck;
        public bool IsEquipped;
        public CardDataSO  CardData;
    }

    [Serializable]
    private class CardPosition
    {
        public Transform PositionTransform;
        [ReadOnly] public CardType CardTypeOccupied = CardType.None;
    }
    
    [Title("References")]
    [SerializeField] private Transform deckCardsParent;
    [SerializeField] private Transform allCardsParent;
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SingleCardInDeck singleCardInDeckPrefab;
    [SerializeField] private ActionFrame actionFramePrefab;
    [SerializeField] private CardHandSettingsSO cardHandSettingsSO;
    [SerializeField] private TextMeshProUGUI medianCostText; //placeholder
    [SerializeField] private List<CardPosition> cardPositions;
    
    private List<CardType> DeckCards;
    private Dictionary<CardType, CardInDeckInfo> cardTypeToCardInDeckInfo = new();

    private BaseClientManager _clientManager;
    private ActionFrame _spawnedActionFrame;
    private ScreenWarning _screenWarning;

    private UserData _userData => _clientManager.UserData;

    //PLACEHOLDER
    private float totalCost;
    
    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        _screenWarning = ServiceLocator.Get<ScreenWarning>();
        DeckCards = _clientManager.UserData.DeckCards;
        if (DeckCards == null || DeckCards.Count == 0) {
            DeckCards = new List<CardType>();
        }
        InitializeDeckCards();
        InitializeActionFrame();

        UpdateMedianCost();
    }

    private void InitializeDeckCards()
    {
        foreach (CardDataSO cardType in cardDataListSO.CardDataList)
        {
            if (DeckCards.Contains(cardType.CardType))
            {
                SingleCardInDeck cardInDeck = Instantiate(singleCardInDeckPrefab, deckCardsParent);
                cardInDeck.Initialize(cardType, this);
                cardTypeToCardInDeckInfo.Add(cardType.CardType, new CardInDeckInfo { SingleCardInDeck = cardInDeck, IsEquipped = true, CardData = cardType });
                EquipCardIntoPosition(cardInDeck);
            }
            else
            {
                SingleCardInDeck cardInAllCards = Instantiate(singleCardInDeckPrefab, allCardsParent);
                cardInAllCards.Initialize(cardType, this);
                cardTypeToCardInDeckInfo.Add(cardType.CardType, new CardInDeckInfo { SingleCardInDeck = cardInAllCards, IsEquipped = false,  CardData = cardType });
                cardInAllCards.transform.SetParent(allCardsParent);
                cardInAllCards.transform.localPosition = Vector3.zero;
            }
        }
    }

    private void InitializeActionFrame()
    {
        _spawnedActionFrame = Instantiate(actionFramePrefab, allCardsParent);
        _spawnedActionFrame.Initialize(this);
    }

    public void RequestActionFrame(CardDataSO cardData)
    {
        _spawnedActionFrame.transform.SetParent(cardTypeToCardInDeckInfo[cardData.CardType].SingleCardInDeck.transform);
        _spawnedActionFrame.transform.localPosition = Vector3.zero;
        _spawnedActionFrame.ActivateActionFrame(cardData, cardTypeToCardInDeckInfo[cardData.CardType].IsEquipped);
    }

    public void SetEquippedCard(CardType cardType, bool isEquipped)
    {
        if (!cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info))
        {
            GameLog.Warn($"CardType {cardType} not found in cardTypeToCardInDeckInfo.");
            return;
        }
        
        if (IsDeckFull() && isEquipped)
        {
            _screenWarning.ShowWarning(WarningMessages.CannotEquipCard);
            return;
        }
        
        info.IsEquipped = isEquipped;
        
        if (isEquipped)
        {
            DeckCards.Add(cardType);
            EquipCardIntoPosition(info.SingleCardInDeck);
        }
        else
        {
            DeckCards.Remove(cardType);
            UnequipCardIntoPosition(info.SingleCardInDeck);
        }
        
        _clientManager.UserData.SetDeckCards(DeckCards);

        UpdateMedianCost();
    }

    private void UpdateMedianCost()
    {
        totalCost = 0f;
        foreach (var cardEntry in cardTypeToCardInDeckInfo)
        {
            if (!cardEntry.Value.IsEquipped)
            {
                continue;
            }
            
            totalCost += cardEntry.Value.CardData.Cost;
        }
        totalCost /= cardTypeToCardInDeckInfo.Count;
        
        medianCostText.text = $"{totalCost:0.0}";
    }

    private void EquipCardIntoPosition(SingleCardInDeck cardInDeck)
    {
        CardPosition cardPosition = GetFirstEmptyPosition();
        if (cardPosition == null) 
            return;

        cardPosition.CardTypeOccupied = cardInDeck.CardData.CardType;
        
        cardInDeck.transform.SetParent(cardPosition.PositionTransform);
        cardInDeck.transform.localPosition = Vector3.zero;
    }

    private void UnequipCardIntoPosition(SingleCardInDeck cardInDeck)
    {
        CardPosition cardPosition = GetCardPositionByType(cardInDeck.CardData.CardType);
        if (cardPosition == null)
            return;
        
        cardPosition.CardTypeOccupied = CardType.None;
        
        cardInDeck.transform.SetParent(allCardsParent);
        cardInDeck.transform.localPosition = Vector3.zero;
    }

    private CardPosition GetFirstEmptyPosition()
    {
        foreach (CardPosition cardPosition in cardPositions)
        {
            if (cardPosition.CardTypeOccupied == CardType.None) return cardPosition;
        }
        
        GameLog.Warn("Get First Empty Position not found");
        return null;
    }

    private CardPosition GetCardPositionByType(CardType cardType)
    {
        foreach (CardPosition cardPosition in cardPositions)
        {
            if (cardPosition.CardTypeOccupied == cardType) return cardPosition;
        }
        
        GameLog.Warn("Get Card Position ByType not found");
        return null;
    }
    
    private bool IsDeckFull()
    {
        return DeckCards.Count == cardHandSettingsSO.DeckSize;
    }
}
