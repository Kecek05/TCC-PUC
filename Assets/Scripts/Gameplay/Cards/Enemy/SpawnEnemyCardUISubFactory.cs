
public class SpawnEnemyCardUISubFactory : BaseCardSubFactory
{
    public override bool CanHandle(CardDataSO data) => data is SpawnEnemyCardDataSO;

    public override AbstractCard Create(CardUIFactoryData factoryData, CardDataSO cardDataSO)
    {
        SpawnEnemyCard card = Instantiate((SpawnEnemyCard)cardDataSO.CardPrefab, factoryData.CardParent);
        card.Initialize(factoryData);
        return card;
    }
}
