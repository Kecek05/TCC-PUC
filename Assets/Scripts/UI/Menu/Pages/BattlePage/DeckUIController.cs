using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class DeckUIController : MonoBehaviour
{
    private class CardInDeckInfo
    {
        public SingleCardInDeck SingleCardInDeck;
        public bool IsEquipped;
    }
    
    [Title("References")]
    [SerializeField] private Transform deckCardsParent;
    [SerializeField] private Transform allCardsParent;
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SingleCardInDeck singleCardInDeckPrefab;
    [SerializeField] private ActionFrame actionFramePrefab;
    
    private List<CardType> DeckCards;
    private Dictionary<CardType, CardInDeckInfo> cardTypeToCardInDeckInfo = new();

    private BaseClientManager _clientManager;
    private ActionFrame _spawnedActionFrame;

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        DeckCards = _clientManager.UserData.DeckCards;
        if (DeckCards == null || DeckCards.Count == 0) {
            DeckCards = new List<CardType>();
        }
        InitializeDeckCards();
        InitializeActionFrame();
    }

    private void InitializeDeckCards()
    {
        foreach (CardDataSO cardType in cardDataListSO.CardDataList)
        {
            if (DeckCards.Contains(cardType.CardType))
            {
                SingleCardInDeck cardInDeck = Instantiate(singleCardInDeckPrefab, deckCardsParent);
                cardInDeck.Initialize(cardType, this, true);
                cardTypeToCardInDeckInfo.Add(cardType.CardType, new CardInDeckInfo { SingleCardInDeck = cardInDeck, IsEquipped = true });
            }
            else
            {
                SingleCardInDeck cardInAllCards = Instantiate(singleCardInDeckPrefab, allCardsParent);
                cardInAllCards.Initialize(cardType, this, false);
                cardTypeToCardInDeckInfo.Add(cardType.CardType, new CardInDeckInfo { SingleCardInDeck = cardInAllCards, IsEquipped = false });
            }
        }
    }

    private void InitializeActionFrame()
    {
        _spawnedActionFrame = Instantiate(actionFramePrefab, allCardsParent);
        _spawnedActionFrame.Initialize(this);
    }

    public void RequestActionFrame(CardDataSO cardData, bool isEquipped)
    {
        _spawnedActionFrame.transform.SetParent(cardTypeToCardInDeckInfo[cardData.CardType].SingleCardInDeck.transform);
        _spawnedActionFrame.transform.localPosition = Vector3.zero;
        _spawnedActionFrame.SelectActionFrame(cardData, isEquipped);
    }

    public void SetEquippedCard(CardType cardType, bool isEquipped)
    {
        if (!cardTypeToCardInDeckInfo.TryGetValue(cardType, out CardInDeckInfo info))
        {
            GameLog.Warn($"CardType {cardType} not found in cardTypeToCardInDeckInfo.");
            return;
        }
        
        info.IsEquipped = isEquipped;
        
        info.SingleCardInDeck.transform.SetParent(isEquipped ? deckCardsParent : allCardsParent);

        if (isEquipped)
            DeckCards.Add(cardType);
        else
            DeckCards.Remove(cardType);
        
        _clientManager.UserData.SetDeckCards(DeckCards);
    }
}
