using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Explains why a control is off. A <see cref="JuicyButton"/> on the same GameObject asks it when a disabled
/// button is tapped, so the player hears the reason instead of nothing.
/// </summary>
public interface IRefusalSource
{
    RefusalReason CurrentRefusal(out string detail);
}

/// <summary>
/// Every button's answer: squash on pointer down (never on click), overshoot back on release, a click sound,
/// a Selection buzz, and a disabled button that shakes and explains itself. The PressButton recipe of the
/// Juice &amp; Feedback System.
/// </summary>
/// <remarks>
/// Scales <see cref="target"/> (itself by default). A <c>LayoutGroup</c> sets position and size, never scale, so
/// a laid-out button can be its own target. The press lands on pointer down because that is when the finger
/// lands; the click sound plays on a real click only. Works under the tutorial's frozen clock (unscaled tweens).
/// </remarks>
[DisallowMultipleComponent]
public class JuicyButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Title("Motion")]
    [Tooltip("What squashes. Empty: this transform.")]
    [SerializeField] private Transform target;

    [Tooltip("Breathe while idle, for the one call to action on a screen (Battle).")]
    [SerializeField] private bool breatheWhenIdle;

    [Title("Sound and haptics")]
    [SerializeField] private SfxId pressSfx = SfxId.Press;
    [Tooltip("Played on a real click. Confirm for a button that commits something.")]
    [SerializeField] private SfxId clickSfx = SfxId.Tap;
    [SerializeField] private bool haptic = true;

    [Title("Refusal")]
    [Tooltip("Shown when the button is tapped while disabled and nothing on it implements IRefusalSource. None: shake and buzz only.")]
    [SerializeField] private RefusalReason disabledReason = RefusalReason.None;

    private Selectable _selectable;
    private IRefusalSource _refusalSource;
    private float _restScale = 1f;
    private bool _held;
    private Tween _idle;

    private Transform Target => target != null ? target : transform;

    /// <summary>Whether this button breathes while idle: its scale is spoken for (the menu's arrival ripple leaves it).</summary>
    public bool IsBreathing => breatheWhenIdle;

    private bool Interactable => _selectable == null || _selectable.IsInteractable();

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        if (_refusalSource == null) _refusalSource = GetComponent<IRefusalSource>();

        // A button caught mid pop-in (scale ~0) would otherwise squash to nothing on every press.
        _restScale = Target.localScale.x;
        if (_restScale < 0.05f) _restScale = 1f;
    }

    /// <summary>Who explains a disabled tap, when that is not a component on this button (the popup that owns it).</summary>
    public void SetRefusalSource(IRefusalSource source) => _refusalSource = source;

    /// <summary>
    /// Turns the idle breath on or off at runtime: a "this is ready" invitation that follows state, like an
    /// Upgrade the player can now afford.
    /// </summary>
    public void SetBreathing(bool breathe)
    {
        if (breatheWhenIdle == breathe) return;
        breatheWhenIdle = breathe;

        if (breathe)
        {
            if (!_held) StartIdle();
            return;
        }

        _idle?.Kill();
        _idle = null;
        if (!_held) Target.localScale = Vector3.one * _restScale;
    }

    private void OnEnable() => StartIdle();

    private void OnDisable()
    {
        _idle?.Kill();
        Target.DOKill();
        Target.localScale = Vector3.one * _restScale;
        _held = false;
    }

    /// <summary>Configures a button built in code.</summary>
    public void Configure(SfxId click, bool breathe = false, RefusalReason refusal = RefusalReason.None)
    {
        clickSfx = click;
        breatheWhenIdle = breathe;
        disabledReason = refusal;
        if (isActiveAndEnabled) StartIdle();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (!Interactable)
        {
            Refuse();
            return;
        }

        _held = true;
        _idle?.Kill();
        JuiceFx.Press(Target, _restScale);
        Juice.Sfx(pressSfx);
        if (haptic) Juice.Haptic(HapticRole.Select);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_held) return;
        _held = false;
        JuiceFx.Release(Target, _restScale).OnComplete(StartIdle);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !Interactable) return;
        Juice.Sfx(clickSfx);
    }

    /// <summary>The disabled tap: shake and wobble, then the reason (or just the deny sound and buzz).</summary>
    public void Refuse()
    {
        if (Target is RectTransform rect) JuiceFx.Deny(rect, Juice.Tokens.Distances.ShakeBar);

        string detail = null;
        RefusalReason reason = _refusalSource != null ? _refusalSource.CurrentRefusal(out detail) : disabledReason;
        if (reason != RefusalReason.None)
        {
            Juice.Refuse(reason, detail);
            return;
        }

        Juice.Sfx(SfxId.Deny);
        Juice.Haptic(HapticRole.Warning);
    }

    private void StartIdle()
    {
        _idle?.Kill();
        _idle = null;
        if (!breatheWhenIdle || !isActiveAndEnabled) return;

        Target.localScale = Vector3.one * _restScale;
        _idle = JuiceFx.Breathe(Target, _restScale);
    }
}
