using UnityEngine;
using UnityEngine.EventSystems;

public interface ICardActivatable
{
    public void ActivateCard(RaycastResult pointerRaycast);
}
