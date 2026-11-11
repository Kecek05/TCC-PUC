#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KeceK.Capture
{
    /// <summary>
    /// A finger made of UI events. Drags and taps go through <see cref="ExecuteEvents"/> into the very handlers
    /// a real touch reaches (a hand card's OnBeginDrag/OnDrag/OnEndDrag, CameraSlide's swipe, a button's
    /// click), one event per frame, so a card visibly leaves the hand and the game validates the drop itself.
    /// The <see cref="TouchIndicator"/> follows it.
    /// </summary>
    public class VirtualFinger
    {
        private const int FingerPointerId = -1;

        private readonly MonoBehaviour _host;
        private TouchIndicator _indicator;

        private PointerEventData _held;
        private GameObject _heldTarget;

        /// <summary>Off for plays that happen out of frame: no mark is drawn.</summary>
        public bool ShowIndicator { get; set; } = true;

        public VirtualFinger(MonoBehaviour host) => _host = host;

        private TouchIndicator Indicator => _indicator != null ? _indicator : _indicator = TouchIndicator.Create();

        /// <summary>
        /// Press on <paramref name="target"/> at <paramref name="from"/>, travel to wherever <paramref name="to"/>
        /// says (re-read every frame, so a moving target is tracked), let go.
        /// </summary>
        public IEnumerator Drag(GameObject target, Vector2 from, Func<Vector2> to, float duration,
            float pressHold = 0.10f, float releaseHold = 0.05f)
        {
            if (target == null) throw new InvalidOperationException("Drag target is null");

            PointerEventData ped = NewPointer(from);
            _held = ped;
            _heldTarget = target;

            if (ShowIndicator) Indicator.Press(from);
            ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerDownHandler);
            ped.pointerPress = target;

            yield return Hold(pressHold);

            ped.pointerDrag = target;
            ExecuteEvents.Execute(target, ped, ExecuteEvents.initializePotentialDrag);
            ped.dragging = true;
            ExecuteEvents.Execute(target, ped, ExecuteEvents.beginDragHandler);

            float t = 0f;
            while (t < duration)
            {
                t += CaptureTime.Delta;
                float k = Ease(Mathf.Clamp01(t / duration));
                MoveHeld(Vector2.LerpUnclamped(from, to(), k));
                yield return null;
            }

            MoveHeld(to());
            yield return Hold(releaseHold);

            ReleaseHeld();
        }

        /// <summary>A tap: press, a short hold, release with a click.</summary>
        public IEnumerator Tap(GameObject target, Vector2 at, float hold = 0.09f)
        {
            if (target == null) throw new InvalidOperationException("Tap target is null");

            PointerEventData ped = NewPointer(at);
            if (ShowIndicator) Indicator.Press(at);

            GameObject pressed = ExecuteEvents.ExecuteHierarchy(target, ped, ExecuteEvents.pointerDownHandler);
            GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            ped.pointerPress = pressed != null ? pressed : clickHandler;
            ped.rawPointerPress = target;

            yield return Hold(hold);

            ExecuteEvents.Execute(ped.pointerPress != null ? ped.pointerPress : target, ped, ExecuteEvents.pointerUpHandler);
            if (clickHandler != null) ExecuteEvents.Execute(clickHandler, ped, ExecuteEvents.pointerClickHandler);
            if (ShowIndicator) Indicator.Lift();
        }

        /// <summary>Moves the mark without touching anything: a hand hovering to its next target.</summary>
        public IEnumerator Hover(Vector2 from, Vector2 to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += CaptureTime.Delta;
                if (ShowIndicator) Indicator.MoveTo(Vector2.LerpUnclamped(from, to, Ease(Mathf.Clamp01(t / duration))));
                yield return null;
            }
        }

        /// <summary>Lets go of anything held (abort path). Safe to call when nothing is.</summary>
        public void Release()
        {
            if (_held != null) ReleaseHeld();
            if (_indicator != null) _indicator.HideNow();
        }

        private void MoveHeld(Vector2 position)
        {
            _held.delta = position - _held.position;
            _held.position = position;
            if (ShowIndicator) Indicator.MoveTo(position);
            ExecuteEvents.Execute(_heldTarget, _held, ExecuteEvents.dragHandler);
        }

        private void ReleaseHeld()
        {
            PointerEventData ped = _held;
            GameObject target = _heldTarget;
            _held = null;
            _heldTarget = null;

            ped.delta = Vector2.zero;
            if (ped.dragging && target != null) ExecuteEvents.Execute(target, ped, ExecuteEvents.endDragHandler);
            ped.dragging = false;
            if (target != null) ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerUpHandler);
            if (ShowIndicator) Indicator.Lift();
        }

        private static PointerEventData NewPointer(Vector2 at) => new(EventSystem.current)
        {
            pointerId = FingerPointerId,
            button = PointerEventData.InputButton.Left,
            position = at,
            pressPosition = at,
            delta = Vector2.zero,
            clickCount = 1,
            eligibleForClick = true,
            useDragThreshold = false,
        };

        private static IEnumerator Hold(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += CaptureTime.Delta;
                yield return null;
            }
        }

        /// <summary>Ease in-out: a finger accelerates off the card and settles onto the spot.</summary>
        private static float Ease(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}
#endif
