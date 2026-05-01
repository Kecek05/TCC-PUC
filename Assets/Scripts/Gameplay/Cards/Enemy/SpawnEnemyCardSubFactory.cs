using UnityEngine;
using UnityEngine.UI;

public class SpawnEnemyCardSubFactory : BaseCardSubFactory
{
    [SerializeField] private SpawnEnemyCard prefab;

    public override bool CanHandle(CardDataSO data) => data is SpawnEnemyCardDataSO;

    public override AbstractCard Create(
        CardDataSO data,
        Transform parent,
        Canvas canvas,
        Transform safeArea,
        GraphicRaycaster blockingRaycaster)
    {
        SpawnEnemyCard card = Instantiate(prefab, parent);
        card.Initialize(canvas, safeArea, blockingRaycaster);
        return card;
    }
}
