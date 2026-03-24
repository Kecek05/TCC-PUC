using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Scriptable Objects/CardData")]
public class CardDataSO : ScriptableObject
{
    [Title("Card Properties")]
    [SerializeField] private int cardId;
    [SerializeField] private string cardName;
    [SerializeField] private string description;
    [SerializeField] private Sprite cardImage;
    [SerializeField] private int cost;
    
    [Title("Card References")]
    [SerializeField] private GameObject cardPrefab;
    
    public int CardId => cardId;
    public string CardName => cardName;
    public string Description => description;
    public Sprite CardImage => cardImage;
    public int Cost => cost;
    public GameObject CardPrefab => cardPrefab;
}
