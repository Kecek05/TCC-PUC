using Sirenix.OdinInspector;
using UnityEngine;

// [CreateAssetMenu(fileName = "CardData", menuName = "Scriptable Objects/CardData")]
public class CardDataSO : ScriptableObject
{
    [Title("Card Properties")]
    public CardType CardType;
    public ExistingTypesOfCard ExistingType;
    public CardRarityType Rarity;
    public AbstractCard CardPrefab;
    public string CardName;
    public string Description;
    public Sprite CardImage;
    public int Cost;
    public bool UseCustomSizeCardInMenu = false;
    [ShowIf("UseCustomSizeCardInMenu")]public Vector2 CustomSizeCardInMenu;
    public bool UseCustomPositionCardInMenu = false;
    [ShowIf("UseCustomPositionCardInMenu")]public Vector2 CustomPositionCardInMenu;
}
