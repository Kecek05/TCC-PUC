using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class DeckUIController : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Transform deckCardsParent;
    [SerializeField] private Transform allCardsParent;
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SingleCardInDeck singleCardInDeckPrefab;
    
    private List<CardType> DeckCards;

    private BaseClientManager _clientManager;

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        DeckCards = _clientManager.UserData.DeckCards;
        if (DeckCards == null || DeckCards.Count == 0) {
            DeckCards = new List<CardType>();
        }
        InitializeDeckCards();
    }

    private void InitializeDeckCards()
    {
        foreach (CardDataSO cardType in cardDataListSO.CardDataList)
        {
            if (DeckCards.Contains(cardType.CardType))
            {
                SingleCardInDeck cardInDeck = Instantiate(singleCardInDeckPrefab, deckCardsParent);
                cardInDeck.Initialize(cardType, this, true);
            }
            else
            {
                SingleCardInDeck cardInAllCards = Instantiate(singleCardInDeckPrefab, allCardsParent);
                cardInAllCards.Initialize(cardType, this, false);
            }
        }
    }
}
