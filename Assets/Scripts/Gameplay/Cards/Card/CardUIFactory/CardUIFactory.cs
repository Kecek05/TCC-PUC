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

    private IOnLocalDrawnACard _drawEvents;

    private void Start()
    {
        _drawEvents = ServiceLocator.Get<IOnLocalDrawnACard>();
        _drawEvents.OnLocalDrawACard += CreateCardUI;
    }

    private void OnDestroy()
    {
        if (_drawEvents != null)
            _drawEvents.OnLocalDrawACard -= CreateCardUI;
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
                factory.Create(cardUIFactoryData, cardDataSO);
                return;
            }
        }

        GameLog.Error($"ServerCardUIFactory: No sub-factory handles {cardDataSO.GetType().Name} for {cardType}");
    }
}
