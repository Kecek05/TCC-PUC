using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// A number that rolls rather than jumps: gold in the header, the deck's average cost, a reward's amount.
/// The CurrencyCounter recipe of the Juice &amp; Feedback System. It owns the label's text, so a caller hands
/// it values and never writes the text itself.
/// </summary>
/// <remarks>
/// Added in code to the label it drives (<see cref="On"/>). Every roll starts from the value on screen, not
/// from the last target, so a second change during a roll continues smoothly instead of snapping back.
/// </remarks>
[DisallowMultipleComponent]
public class CounterAnimator : MonoBehaviour
{
    private TMP_Text _label;
    private Func<int, string> _format = DefaultFormat;
    private int _shown;
    private bool _hasValue;
    private Tween _roll;

    /// <summary>The number on screen right now, mid-roll included.</summary>
    public int Shown => _shown;

    public bool HasValue => _hasValue;

    private static string DefaultFormat(int value) => value.ToString();

    /// <summary>The counter on this label, created on first use.</summary>
    public static CounterAnimator On(TMP_Text label, Func<int, string> format = null)
    {
        CounterAnimator counter = label.GetComponent<CounterAnimator>();
        if (counter == null) counter = label.gameObject.AddComponent<CounterAnimator>();
        counter._label = label;
        if (format != null) counter._format = format;
        return counter;
    }

    /// <summary>Shows a value at once, cancelling any roll.</summary>
    public void Set(int value)
    {
        _roll?.Kill();
        _shown = value;
        _hasValue = true;
        _label.text = _format(value);
    }

    /// <summary>
    /// Rolls from the value on screen to <paramref name="value"/> over <paramref name="duration"/> (dur-count
    /// by default), calling <paramref name="onTick"/> each time the shown number changes.
    /// </summary>
    public Tween RollTo(int value, float duration = -1f, Action<int> onTick = null)
    {
        if (!_hasValue || Juice.ReduceMotion)
        {
            Set(value);
            return null;
        }

        _roll?.Kill();
        float d = duration > 0f ? duration : Juice.Tokens.Durations.Count;
        _roll = DOVirtual.Int(_shown, value, d, v =>
            {
                if (v == _shown) return;
                _shown = v;
                _label.text = _format(v);
                onTick?.Invoke(v);
            })
            .SetEase(Juice.Tokens.Eases.Out).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => Set(value));
        _hasValue = true;
        return _roll;
    }

    /// <summary>
    /// A "+N" or "-N" that rises off the label and fades: what just changed, in <c>gain</c> or <c>deny</c>.
    /// Drawn with the label's own font and material so it reads as part of the same widget.
    /// </summary>
    public void Float(int delta, Color? color = null, string suffix = null)
    {
        if (delta == 0 || _label == null) return;

        JuiceTokensSO t = Juice.Tokens;
        GameObject go = new GameObject(delta > 0 ? "Gain" : "Spend", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_label.transform.parent, false);
        RectTransform labelRect = _label.rectTransform;
        rect.anchorMin = labelRect.anchorMin;
        rect.anchorMax = labelRect.anchorMax;
        rect.pivot = labelRect.pivot;
        rect.sizeDelta = labelRect.sizeDelta;
        rect.anchoredPosition = labelRect.anchoredPosition;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = _label.font;
        text.fontSharedMaterial = _label.fontSharedMaterial;
        text.fontSize = _label.fontSize * 0.8f;
        text.enableAutoSizing = false;
        text.alignment = _label.alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = (delta > 0 ? $"+{delta}" : delta.ToString()) + suffix;
        text.color = color ?? (delta > 0 ? t.Colors.Gain : t.Colors.Deny);

        // A gain rises toward the player; a spend sinks away from the number it left.
        float rise = t.Distances.RiseToast * 2f * (delta > 0 ? 1f : -1f);
        float d = t.Durations.Banner * 0.45f;
        DOTween.Sequence().SetUpdate(true).SetLink(go)
            .Join(rect.DOAnchorPosY(labelRect.anchoredPosition.y + rise, d).SetEase(t.Eases.Out))
            .Insert(d * 0.5f, text.DOFade(0f, d * 0.5f).SetEase(t.Eases.In))
            .OnComplete(() => Destroy(go));
    }

    private void OnDestroy() => _roll?.Kill();
}
