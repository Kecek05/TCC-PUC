using UnityEngine;
using UnityEngine.UI;

public class SpellCardSubFactory : BaseCardSubFactory
{
    [SerializeField] private SpellCard prefab;

    public override bool CanHandle(CardDataSO data) => data is SpellCardDataSO;

    public override AbstractCard Create(
        CardDataSO data,
        Transform parent,
        Canvas canvas,
        Transform safeArea,
        GraphicRaycaster blockingRaycaster)
    {
        SpellCard card = Instantiate(prefab, parent);
        card.Initialize(canvas, safeArea, blockingRaycaster);
        return card;
    }
}
