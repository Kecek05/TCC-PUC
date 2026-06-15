using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Turns a tap on the shared world surface (the full-screen CameraSlideArea, also used by CameraSlide)
/// into a tower range selection. A camera drag cancels the click, so sliding the camera never selects;
/// a genuine tap is projected to a world point and handed to <see cref="ClientTowerRangeGFX"/>, which
/// shows the tapped tower's range ring or hides it on an empty tap.
/// </summary>
public class TowerSelectionInput : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private ClientTowerRangeGFX rangeIndicator;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (rangeIndicator == null) return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 0f));
        rangeIndicator.HandleWorldTap(world);
    }
}