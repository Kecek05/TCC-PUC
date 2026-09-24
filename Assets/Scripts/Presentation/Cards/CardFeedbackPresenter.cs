using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Makes a hand card feel physical: it lifts under the finger, grows and leans while dragged, shakes when a
/// drop is refused, punches and collapses when a play goes out, and pops back if the server refuses it.
/// </summary>
/// <remarks>
/// Pure presentation, sitting beside the <see cref="AbstractCard"/> on the card's root. Unity delivers pointer
/// events to every handler on that object, so this hears the same press and drag the card does without the
/// card calling into it; the verdicts come in through the card's events. Everything animates the
/// <c>Juice</c> node between the root and <c>GFX</c>, never the root itself — the root belongs to the drag
/// and to the slide home — and every player runs unscaled, because the tutorial freezes the clock while it
/// waits for exactly these drags.
/// <para/>
/// The Juice players are one state machine: starting one stops the last, since two MMF_Scale coroutines on
/// one transform would both write it. The shake and flash players animate <c>GFX</c> and an overlay instead,
/// so they layer on top of whatever state the Juice is in.
/// </remarks>
public class CardFeedbackPresenter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Title("References")]
    [Tooltip("Leans the Juice node while the card is dragged. Unscaled, local space.")]
    [SerializeField, Required] private MMSpringRotation tiltSpring;

    [Title("Juice States")]
    [SerializeField] private MMF_Player selectFeedback;
    [SerializeField] private MMF_Player dragFeedback;
    [SerializeField] private MMF_Player releaseFeedback;
    [SerializeField] private MMF_Player playFeedback;
    [SerializeField] private MMF_Player restoreFeedback;
    [SerializeField] private MMF_Player enterFeedback;

    [Title("Refusal Overlays")]
    [SerializeField] private MMF_Player invalidFeedback;
    [SerializeField] private MMF_Player noManaFeedback;

    [Title("Drag Lean")]
    [Tooltip("Degrees of lean per screen-width-per-second of horizontal drag speed.")]
    [SerializeField] private float leanPerSpeed = 7f;
    [SerializeField] private float maxLean = 12f;
    [Tooltip("Degrees per second the lean eases off while the finger holds still.")]
    [SerializeField] private float leanRecovery = 60f;

    private AbstractCard _card;
    private MMF_Player _state;
    private bool _dragging;
    private bool _committed;
    private float _lean;

    private void Awake()
    {
        // Looked up rather than serialized: the card component lives on each variant, not on CardBase.
        _card = GetComponent<AbstractCard>();
        if (_card == null)
        {
            enabled = false;
            return;
        }

        _card.OnDropResolved += HandleDropResolved;
        _card.OnPlayResolved += HandlePlayResolved;
        _card.OnPlacedInSlot += HandlePlacedInSlot;
    }

    private void OnDestroy()
    {
        if (_card == null) return;

        _card.OnDropResolved -= HandleDropResolved;
        _card.OnPlayResolved -= HandlePlayResolved;
        _card.OnPlacedInSlot -= HandlePlacedInSlot;
    }

    private void Update()
    {
        // OnDrag only fires on movement, so a finger held still would leave the card leaning.
        if (!_dragging || _lean == 0f) return;

        _lean = Mathf.MoveTowards(_lean, 0f, leanRecovery * Time.unscaledDeltaTime);
        tiltSpring.MoveTo(new Vector3(0f, 0f, _lean));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_committed) return;
        PlayState(selectFeedback);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Pointer-up arrives before end-drag, so a drag's release is left to the drop verdict.
        if (_dragging || _committed) return;
        PlayState(releaseFeedback);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_committed) return;

        _dragging = true;
        PlayState(dragFeedback);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        // Normalised by screen width so the lean feels the same on every device.
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float speed = eventData.delta.x / Mathf.Max(1f, Screen.width) / dt;
        float target = Mathf.Clamp(-speed * leanPerSpeed, -maxLean, maxLean);

        _lean = Mathf.Lerp(_lean, target, 0.5f);
        tiltSpring.MoveTo(new Vector3(0f, 0f, _lean));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragging = false;
        _lean = 0f;
        tiltSpring.MoveTo(Vector3.zero);
    }

    private void HandleDropResolved(CardValidation validation)
    {
        if (validation)
        {
            // The card is on its way to the server: it collapses now and is retired or restored by the answer.
            _committed = true;
            PlayState(playFeedback);
            return;
        }

        PlayState(releaseFeedback);

        // Let go over the hand: a cancel, not a mistake worth a shake.
        if (validation.Reason == CardInvalidReason.BlockedByUI) return;

        PlayRefusal(validation.Reason);
    }

    private void HandlePlayResolved(CardValidation validation)
    {
        // Accepted: the card is destroyed right after this, already collapsed.
        if (validation) return;

        _committed = false;
        PlayState(restoreFeedback);
        PlayRefusal(validation.Reason);
    }

    private void HandlePlacedInSlot()
    {
        _committed = false;
        PlayState(enterFeedback);
    }

    private void PlayRefusal(CardInvalidReason reason)
    {
        MMF_Player overlay = reason == CardInvalidReason.NotEnoughMana ? noManaFeedback : invalidFeedback;
        if (overlay != null) overlay.PlayFeedbacks();
    }

    private void PlayState(MMF_Player next)
    {
        if (next == null) return;

        if (_state != null && _state != next && _state.IsPlaying)
            _state.StopFeedbacks();

        _state = next;
        next.PlayFeedbacks();
    }
}
