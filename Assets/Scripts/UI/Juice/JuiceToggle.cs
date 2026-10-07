using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// An on/off switch in the game's look: a pill track that turns from <c>stat</c> grey to the upgrade green,
/// and a knob that slides across on ease-pop with a tick and a Selection buzz.
/// </summary>
public class JuiceToggle : MonoBehaviour, IPointerClickHandler
{
    // Darker than the stat row it sits on, so an off switch still reads as a switch.
    private static readonly Color OffColor = JuiceTokensSO.Hex("#4f4f4f");

    private Image _track;
    private RectTransform _knob;
    private float _travel;
    private bool _value;
    private Action<bool> _onChanged;

    public bool Value => _value;

    /// <summary>Builds the switch under <paramref name="parent"/>.</summary>
    public static JuiceToggle Create(Transform parent, Sprite trackSprite, Sprite knobSprite, Vector2 size)
    {
        GameObject root = new GameObject("Switch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = size;

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = layout.minWidth = size.x;
        layout.preferredHeight = layout.minHeight = size.y;

        Image track = root.GetComponent<Image>();
        track.sprite = trackSprite;
        track.type = trackSprite != null && trackSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        track.pixelsPerUnitMultiplier = 4f;

        GameObject knob = new GameObject("Knob", typeof(RectTransform), typeof(Image));
        knob.transform.SetParent(root.transform, false);
        RectTransform knobRect = (RectTransform)knob.transform;
        float k = size.y - 14f;
        knobRect.sizeDelta = new Vector2(k, k);
        knobRect.anchorMin = knobRect.anchorMax = new Vector2(0.5f, 0.5f);

        Image knobImage = knob.GetComponent<Image>();
        knobImage.sprite = knobSprite;
        knobImage.color = Juice.Tokens.Colors.Ink;
        knobImage.raycastTarget = false;

        JuiceToggle toggle = root.AddComponent<JuiceToggle>();
        toggle._track = track;
        toggle._knob = knobRect;
        toggle._travel = (size.x - size.y) * 0.5f;
        return toggle;
    }

    public void Bind(bool value, Action<bool> onChanged)
    {
        _onChanged = onChanged;
        Set(value, false);
    }

    public void Set(bool value, bool animated)
    {
        _value = value;
        Color color = value ? Juice.Tokens.Colors.Upgrade : OffColor;
        float x = value ? _travel : -_travel;

        _knob.DOKill();
        _knob.localScale = Vector3.one; // a punch killed half way would otherwise leave the knob resized
        _track.DOKill();
        if (!animated)
        {
            _knob.anchoredPosition = new Vector2(x, 0f);
            _track.color = color;
            return;
        }

        JuiceTokensSO t = Juice.Tokens;
        _knob.DOAnchorPosX(x, t.Durations.Select).SetEase(t.Eases.Pop).SetUpdate(true).SetLink(gameObject);
        _track.DOColor(color, t.Durations.Select).SetEase(t.Eases.Press).SetUpdate(true).SetLink(gameObject);
        JuiceFx.Punch(_knob, t.Scales.PunchSoft, t.Durations.Pop);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Set(!_value, true);
        Juice.Sfx(SfxId.Tick);
        Juice.Haptic(HapticRole.Select);
        _onChanged?.Invoke(_value);
    }
}
