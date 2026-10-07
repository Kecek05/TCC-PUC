using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// An invisible hit area that reports where it was tapped: the "one tap finishes the show" layer laid over a
/// sequence (the upgrade celebration). Unlike a Button it hands over the pointer, so the owner can still act on
/// what the tap was aimed at underneath.
/// </summary>
/// <remarks>Needs a raycast-target Graphic on the same GameObject (a clear Image) to receive the tap.</remarks>
public class TapCatcher : MonoBehaviour, IPointerClickHandler
{
    public event Action<PointerEventData> Tapped;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Tapped?.Invoke(eventData);
    }
}
