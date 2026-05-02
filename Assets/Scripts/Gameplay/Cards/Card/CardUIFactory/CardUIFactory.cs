using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct CardUIFactoryData
{
    public Canvas CardsCanvas;
    public Transform SafeAreaParent;
    public GraphicRaycaster BlockCardsCanvas;
    public Transform CardParent;
}

public class CardUIFactory : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private CardUIFactoryData cardUIFactoryData;

    [Title("Sub-Factories")]
    [SerializeField] private List<BaseCardSubFactory> subFactories = new();

    private IOnLocalDrawnACard _OnLocalDrawnACard;
    private IOnLocalNextCardChanged _OnLocalNextCardChanged;
    private BaseCardContainer _cardContainer;

    private void Start()
    {
        _OnLocalDrawnACard = ServiceLocator.Get<IOnLocalDrawnACard>();
        _OnLocalDrawnACard.OnLocalDrawACard += CreateCardUI;
        
        _OnLocalNextCardChanged  = ServiceLocator.Get<IOnLocalNextCardChanged>();
        _OnLocalNextCardChanged.OnLocalNextCardChanged += OnNextCardChanged;
        
        _cardContainer = ServiceLocator.Get<BaseCardContainer>();
    }

    private void OnDestroy()
    {
        if (_OnLocalDrawnACard != null)
            _OnLocalDrawnACard.OnLocalDrawACard -= CreateCardUI;
        
        if (_OnLocalNextCardChanged != null)
            _OnLocalNextCardChanged.OnLocalNextCardChanged -= CreateCardUI;
    }

    private void CreateCardUI(CardType cardType)
    {
        CardDataSO cardDataSO = cardDataListSO.GetCardDataByType(cardType);
        if (cardDataSO == null)
        {
            GameLog.Error($"ServerCardUIFactory: No CardDataSO for {cardType}");
            return;
        }

        foreach (BaseCardSubFactory factory in subFactories)
        {
            if (factory.CanHandle(cardDataSO))
            {
                factory.Create(cardUIFactoryData, cardDataSO, _cardContainer);
                return;
            }
        }

        GameLog.Error($"ServerCardUIFactory: No sub-factory handles {cardDataSO.GetType().Name} for {cardType}");
    }

    private void OnNextCardChanged(CardType cardType)
    {
        if (cardType == CardType.None)
        {
            _cardContainer.SetNextCardNone();
            return;
        }
        
        _cardContainer.SetNextCard(cardDataListSO.GetCardDataByType(cardType).CardImage);
    }
}
