using Sirenix.OdinInspector;
using UnityEngine;

public class BaseCard : MonoBehaviour, ICardActivatable
{
    [Title("Card Properties")]
    [SerializeField] private string cardName;
    [SerializeField] private string description;
    [SerializeField] private Sprite cardImage;
    [SerializeField] private int manaCost;
    
    
    public virtual void ActivateCard()
    {
        Debug.Log("Activating BaseCard: " + cardName);
    }
}
