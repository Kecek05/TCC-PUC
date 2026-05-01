using UnityEngine;

public class SpellCardUISubFactory : BaseCardSubFactory
{
    [SerializeField] private GhostSpellCard ghostSpellCard;
    
    public override bool CanHandle(CardDataSO data) => data is SpellCardDataSO;

    public override AbstractCard Create(CardUIFactoryData factoryData, CardDataSO cardDataSO)
    {
        SpellCard card = Instantiate((SpellCard)cardDataSO.CardPrefab, factoryData.CardParent);
        card.Initialize(factoryData, ghostSpellCard);
        return card;
    }
}
