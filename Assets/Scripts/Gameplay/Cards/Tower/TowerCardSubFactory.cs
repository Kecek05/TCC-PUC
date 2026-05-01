using UnityEngine;

public class TowerCardSubFactory : BaseCardSubFactory
{
    [SerializeField] private GhostTowerCard ghostTowerCard;

    public override bool CanHandle(CardDataSO data) => data is TowerCardDataSO;

    public override AbstractCard Create(CardUIFactoryData factoryData, CardDataSO cardDataSO)
    {
        TowerCard card = Instantiate((TowerCard)cardDataSO.CardPrefab, factoryData.CardParent);
        card.Initialize(factoryData, ghostTowerCard);
        return card;
    }
}
