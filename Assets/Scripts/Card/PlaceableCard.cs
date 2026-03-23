using UnityEngine;
using UnityEngine.EventSystems;

public class PlaceableCard : BaseCard
{
    public override void ActivateCard(RaycastResult pointerRaycast)
    {
        base.ActivateCard(pointerRaycast);
        Debug.Log("Activating PlaceableCard");
    }
}
