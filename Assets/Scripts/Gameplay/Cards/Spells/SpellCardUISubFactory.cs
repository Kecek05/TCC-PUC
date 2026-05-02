using UnityEngine;

public class SpellCardUISubFactory : BaseCardSubFactory
{
    [SerializeField] private GhostSpellCard ghostSpellCard;
    
    public override bool CanHandle(CardDataSO data) => data is SpellCardDataSO;

    public override AbstractCard Create(CardUIFactoryData factoryData, CardDataSO cardDataSO, BaseCardContainer cardContainer)
    {
        SpellCard card = Instantiate((SpellCard)cardDataSO.CardPrefab, factoryData.CardParent);
        card.Initialize(factoryData, cardContainer, ghostSpellCard);
        return card;
    }
}
