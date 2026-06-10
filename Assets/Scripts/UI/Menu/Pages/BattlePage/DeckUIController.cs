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
    
    [Title("References")]
    [SerializeField] private Transform deckCardsParent;
    [SerializeField] private Transform allCardsParent;
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SingleCardInDeck singleCardInDeckPrefab;
    [SerializeField] private ActionFrame actionFramePrefab;
    [SerializeField] private CardHandSettingsSO cardHandSettingsSO;
    [SerializeField] private TextMeshProUGUI medianCostText; //placeholder
    
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
            }
            else
            {
                SingleCardInDeck cardInAllCards = Instantiate(singleCardInDeckPrefab, allCardsParent);
                cardInAllCards.Initialize(cardType, this);
                cardTypeToCardInDeckInfo.Add(cardType.CardType, new CardInDeckInfo { SingleCardInDeck = cardInAllCards, IsEquipped = false,  CardData = cardType });
            }
        }
    }

    private void InitializeActionFrame()
    {
        _spawnedActionFrame = Instantiate(actionFramePrefab, allCardsParent);
        // The action frame parents under the collection grid until a card is selected;
        // tell the layout to ignore it so it never reserves a collection cell.
        if (!_spawnedActionFrame.TryGetComponent(out LayoutElement actionFrameLayout))
            actionFrameLayout = _spawnedActionFrame.gameObject.AddComponent<LayoutElement>();
        actionFrameLayout.ignoreLayout = true;
        _spawnedActionFrame.Initialize(this);
    }

    public void RequestActionFrame(CardDataSO cardData)
    {
        _spawnedActionFrame.transform.SetParent(cardTypeToCardInDeckInfo[cardData.CardType].SingleCardInDeck.transform);
        _spawnedActionFrame.transform.localPosition = Vector3.zero;
        _spawnedActionFrame.SelectActionFrame(cardData, cardTypeToCardInDeckInfo[cardData.CardType].IsEquipped);
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
        
        info.SingleCardInDeck.transform.SetParent(isEquipped ? deckCardsParent : allCardsParent);

        if (isEquipped)
            DeckCards.Add(cardType);
        else
            DeckCards.Remove(cardType);
        
        _clientManager.UserData.SetDeckCards(DeckCards);

        UpdateMedianCost();
    }
    
    private bool IsDeckFull()
    {
        return DeckCards.Count == cardHandSettingsSO.DeckSize;
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
}
