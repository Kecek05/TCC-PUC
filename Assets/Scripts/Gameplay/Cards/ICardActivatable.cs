using UnityEngine;
using UnityEngine.EventSystems;

public interface ICardActivatable
{

    /// <summary>
    /// Used for without context (in-hand, cooldown, etc)
    /// </summary>
    CardValidation CanPlayCard();
    
    /// <summary>
    /// Used with placement context
    /// </summary>
    CardValidation CanPlayCardAt(RaycastResult pointerRaycast);

    public void ActivateCard(RaycastResult target);
}
