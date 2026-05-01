using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class TowerCardSubFactory : BaseCardSubFactory
{
    [Title("Prefab")]
    [SerializeField] private TowerCard prefab;

    [Title("Scene Refs")]
    [SerializeField] private GhostTowerCard ghostTowerCard;

    public override bool CanHandle(CardDataSO data) => data is TowerCardDataSO;

    public override AbstractCard Create(
        CardDataSO data,
        Transform parent,
        Canvas canvas,
        Transform safeArea,
        GraphicRaycaster blockingRaycaster)
    {
        TowerCard card = Instantiate(prefab, parent);
        card.Initialize(canvas, safeArea, blockingRaycaster, ghostTowerCard);
        return card;
    }
}
