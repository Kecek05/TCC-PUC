using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A vertical <see cref="ScrollRect"/> that hands horizontal drags off to a parent drag
/// handler (e.g. <see cref="HorizontalPageStrip"/>) so a page-swiper sitting above it keeps
/// receiving horizontal swipes, while this rect still scrolls vertically.
///
/// The axis is locked in at the start of each drag from the initial finger direction:
/// a horizontal-dominant gesture is routed up to the nearest parent drag handler; a
/// vertical-dominant gesture is handled by the base ScrollRect.
/// </summary>
public class DirectionalScrollRect : ScrollRect
{
    private bool _routeToParent;

    public override void OnBeginDrag(PointerEventData eventData)
    {
        // Lock the axis from the gesture's initial direction (press -> current position).
        Vector2 gesture = eventData.position - eventData.pressPosition;
        _routeToParent = Mathf.Abs(gesture.x) > Mathf.Abs(gesture.y);

        if (_routeToParent)
            RouteToParent(eventData, ExecuteEvents.beginDragHandler);
        else
            base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (_routeToParent)
            RouteToParent(eventData, ExecuteEvents.dragHandler);
        else
            base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (_routeToParent)
            RouteToParent(eventData, ExecuteEvents.endDragHandler);
        else
            base.OnEndDrag(eventData);

        _routeToParent = false;
    }

    /// <summary>
    /// Forwards the pointer event to the first ancestor that implements <typeparamref name="T"/>.
    /// Starts at the parent (not self) so this ScrollRect's own handlers are skipped.
    /// </summary>
    private void RouteToParent<T>(PointerEventData eventData, ExecuteEvents.EventFunction<T> handler)
        where T : IEventSystemHandler
    {
        if (transform.parent != null)
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, handler);
    }
}
