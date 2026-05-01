using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class ServerCardUIFactory : BaseCardUIFactory
{
    [Title("References")]
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private Canvas cardsCanvas;
    [SerializeField] private Transform safeAreaParent;
    [SerializeField] private GraphicRaycaster graphicRaycaster;

    private IOnDrawACard _drawEvents;

    private void Start()
    {
        _drawEvents = ServiceLocator.Get<IOnDrawACard>();
        _drawEvents.OnDrawACard += CreateCardUI;
    }

    private void OnDestroy()
    {
        if (_drawEvents != null)
            _drawEvents.OnDrawACard -= CreateCardUI;
    }

    public override void CreateCardUI(CardType cardType)
    {
        CardDataSO cardDataSO = cardDataListSO.GetCardDataByType(cardType);

        if (cardDataSO == null) return;
        
        cardDataSO.CardPrefab.Initialize(cardsCanvas, safeAreaParent, graphicRaycaster);
        
    }
}
