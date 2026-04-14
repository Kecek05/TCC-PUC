using Sirenix.OdinInspector;
using UnityEngine;

// [CreateAssetMenu(fileName = "CardData", menuName = "Scriptable Objects/CardData")]
public class CardDataSO : ScriptableObject
{
    [Title("Card Properties")]
    public CardType CardType;
    public string CardName;
    public string Description;
    public Sprite CardImage;
    public int Cost;
}
