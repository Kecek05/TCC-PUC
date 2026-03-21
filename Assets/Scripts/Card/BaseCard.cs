using UnityEngine;

public class BaseCard : MonoBehaviour, ICardActivatable
{
    [SerializeField] private string cardName;
    [SerializeField] private string description;
    [SerializeField] private Sprite cardImage;
    [SerializeField] private int manaCost;
    
    
    public virtual void ActivateCard()
    {
        Debug.Log("Activating BaseCard: " + cardName);
    }
}
