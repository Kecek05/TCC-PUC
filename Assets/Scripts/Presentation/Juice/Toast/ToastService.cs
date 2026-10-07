using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The single way the game says no: a toast with the reason and what to do, plus the deny sound and a Warning
/// buzz. Callers shake their own element; this owns the words and the channel.
/// </summary>
public abstract class BaseToastService : MonoBehaviour
{
    /// <summary>Shows the copy for <paramref name="reason"/>; <paramref name="detail"/> fills the hint's {0}.</summary>
    /// <param name="feedback">Play the deny sound and the Warning haptic. Off when the caller already did.</param>
    public abstract void Show(RefusalReason reason, string detail = null, bool feedback = true);

    /// <summary>Shows free text in the same toast, for a refusal that has no reason in the table yet.</summary>
    public abstract void ShowMessage(string title, string hint = null, bool feedback = true);
}

/// <summary>
/// A top-of-screen pill on its own overlay canvas, built in code so it exists in every scene without a prefab.
/// One toast at a time: a repeat restarts it with a punch instead of stacking.
/// </summary>
/// <remarks>
/// The canvas has no <see cref="GraphicRaycaster"/> and every graphic is non-raycast, so a toast can never eat
/// a touch: it sits above the tutorial overlay (sorting 20) only to stay readable there. Tweens run on
/// unscaled time, so a toast still animates under the tutorial's frozen clock.
/// </remarks>
public class ToastService : BaseToastService
{
    private RefusalCopySO _copy;
    private Canvas _canvas;
    private RectTransform _pill;
    private CanvasGroup _group;
    private TextMeshProUGUI _label;
    private Sequence _sequence;
    private bool _visible;
    private string _hintColor;

    public static ToastService Create(RefusalCopySO copy)
    {
        GameObject host = new GameObject("[Juice] Toast");
        DontDestroyOnLoad(host);

        ToastService service = host.AddComponent<ToastService>();
        service.Build(copy != null ? copy : ScriptableObject.CreateInstance<RefusalCopySO>());
        return service;
    }

    private void Build(RefusalCopySO copy)
    {
        _copy = copy;
        JuiceTokensSO tokens = JuiceTokensSO.Instance;
        _hintColor = "#" + ColorUtility.ToHtmlStringRGB(tokens.Colors.InkMuted);

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = copy.SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject pill = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image),
            typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        pill.transform.SetParent(transform, false);
        _pill = (RectTransform)pill.transform;
        _pill.anchorMin = _pill.anchorMax = new Vector2(0.5f, 1f);
        _pill.pivot = new Vector2(0.5f, 1f);

        Image background = pill.GetComponent<Image>();
        background.sprite = copy.PillSprite;
        background.type = copy.PillSprite != null && copy.PillSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        background.color = tokens.Colors.Sheet;
        background.raycastTarget = false;

        HorizontalLayoutGroup layout = pill.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 40, 20, 20);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = pill.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _group = pill.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // The cross: a deny disc with the sheet colour cut through it.
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(pill.transform, false);
        Image disc = icon.GetComponent<Image>();
        disc.sprite = copy.IconSprite;
        disc.color = tokens.Colors.Deny;
        disc.raycastTarget = false;
        LayoutElement iconSize = icon.GetComponent<LayoutElement>();
        iconSize.preferredWidth = iconSize.preferredHeight = 54f;
        iconSize.minWidth = iconSize.minHeight = 54f;

        TextMeshProUGUI cross = CreateText("Cross", icon.transform, copy.Font, 40f);
        cross.text = "×";
        cross.color = tokens.Colors.Sheet;
        cross.alignment = TextAlignmentOptions.Center;
        RectTransform crossRect = cross.rectTransform;
        crossRect.anchorMin = Vector2.zero;
        crossRect.anchorMax = Vector2.one;
        crossRect.offsetMin = crossRect.offsetMax = Vector2.zero;

        _label = CreateText("Label", pill.transform, copy.Font, 44f);
        _label.color = tokens.Colors.Ink;
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, TMP_FontAsset font, float size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    public override void Show(RefusalReason reason, string detail = null, bool feedback = true)
    {
        if (reason == RefusalReason.None) return;
        _copy.Resolve(reason, detail, out string title, out string hint);
        ShowMessage(title, hint, feedback);
    }

    public override void ShowMessage(string title, string hint = null, bool feedback = true)
    {
        // The hint goes on a second, smaller line: one line for both ran off a phone's width on the longer ones.
        _label.text = string.IsNullOrEmpty(hint)
            ? title
            : $"{title}\n<size=78%><color={_hintColor}>{hint}</color></size>";

        if (feedback)
        {
            Juice.Sfx(SfxId.Deny);
            Juice.Haptic(HapticRole.Warning);
        }

        Animate();
    }

    private void Animate()
    {
        JuiceTokensSO t = JuiceTokensSO.Instance;
        float restY = -(SafeTopInset() + _copy.TopOffset);
        float rise = t.Distances.RiseToast;
        float hold = Mathf.Max(0.2f, t.Durations.Toast - t.Durations.Pop - t.Durations.Select);

        _sequence?.Kill();

        if (_visible)
        {
            // A repeat restarts the hold and punches, instead of stacking a second toast.
            _pill.localScale = Vector3.one;
            _group.alpha = 1f;
            _pill.anchoredPosition = new Vector2(0f, restY);
            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(_pill.DOPunchScale(Vector3.one * t.Scales.PunchSoft, t.Durations.Pop, 6, 0.6f))
                .AppendInterval(hold);
        }
        else
        {
            _visible = true;
            _group.alpha = 0f;
            _pill.anchoredPosition = new Vector2(0f, restY + rise);
            _pill.localScale = Vector3.one * (Juice.ReduceMotion ? 1f : 0.9f);
            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(_group.DOFade(1f, t.Durations.Select))
                .Join(_pill.DOAnchorPosY(restY, t.Durations.Pop).SetEase(t.Eases.Pop))
                .Join(_pill.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Pop))
                .AppendInterval(hold);
        }

        _sequence
            .Append(_group.DOFade(0f, t.Durations.Select).SetEase(t.Eases.In))
            .Join(_pill.DOAnchorPosY(restY + rise * 0.5f, t.Durations.Select).SetEase(t.Eases.In))
            .OnComplete(() => _visible = false);
    }

    // The notch / status bar, in canvas units, so the toast never hides behind it.
    private float SafeTopInset()
    {
        float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        return Mathf.Max(0f, Screen.height - Screen.safeArea.yMax) / scale;
    }
}
