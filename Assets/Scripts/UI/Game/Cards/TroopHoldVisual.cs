using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What a troop card turns into while it is held over the board (TroopSend): its bodies lifted above the finger,
/// marching on the spot under a stream of chevrons, with how many it sends. On release it dips and flicks away
/// toward the opponent's field; taken back to the hand, it folds into the card again.
/// </summary>
/// <remarks>
/// A tower card hands over to a ghost on the board and a spell to its aim, because both have a place to land. A
/// troop has none: it goes to the other field wherever it is dropped. So its visual rides the card itself, in the
/// card's own canvas, and needs no world object, no scene reference and no factory plumbing: it is a child of the
/// card, built in code the first time the card is dragged.
/// <para/>
/// Animated by hand on unscaled time (the hand answers the finger even while the tutorial holds the world), with
/// DOTween only for the three one-shot moves. Reduce motion keeps the picture and drops the loop.
/// </remarks>
public class TroopHoldVisual : MonoBehaviour
{
    private const float BodySize = 150f;
    private const float Lift = 104f;
    private const int ChevronCount = 3;
    private const float ChevronSize = 44f;
    private const float ChevronGap = 28f;

    private RectTransform _rect;
    private CanvasGroup _group;
    private RectTransform _body;
    private Image[] _chevrons;
    private Tween _fade;
    private bool _shown;
    private bool _launched;

    /// <summary>True while the visual is up (or coming up) over the board.</summary>
    public bool IsShown => _shown;

    /// <param name="parent">The card's own animated root, so the visual leans and grows with the drag.</param>
    /// <param name="style">Any label of the card, for the game's outlined font.</param>
    public static TroopHoldVisual Create(RectTransform parent, CardDataSO card, int count, TMP_Text style)
    {
        JuiceTokensSO t = Juice.Tokens;

        GameObject go = new GameObject("TroopHold", typeof(RectTransform), typeof(CanvasGroup));
        TroopHoldVisual visual = go.AddComponent<TroopHoldVisual>();
        visual._rect = (RectTransform)go.transform;
        visual._rect.SetParent(parent, false);
        visual._rect.anchorMin = visual._rect.anchorMax = visual._rect.pivot = new Vector2(0.5f, 0.5f);
        visual._rect.sizeDelta = Vector2.one * BodySize;
        visual._rect.anchoredPosition = new Vector2(0f, Lift);

        visual._group = go.GetComponent<CanvasGroup>();
        visual._group.alpha = 0f;
        visual._group.blocksRaycasts = false;
        visual._group.interactable = false;

        // The chevrons first, so the bodies are drawn over the lowest one.
        visual._chevrons = new Image[ChevronCount];
        for (int i = 0; i < ChevronCount; i++)
        {
            Image chevron = Part(visual._rect, "Chevron" + i, JuiceSprites.ArrowDown, t.Colors.Attention, ChevronSize);
            chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            // A dark edge: attention yellow alone is lost on the mint ground.
            Outline edge = chevron.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.85f);
            edge.effectDistance = new Vector2(2.5f, 2.5f);
            visual._chevrons[i] = chevron;
        }

        Image body = Part(visual._rect, "Body", card.CardImage, card.CardColor, BodySize);
        body.preserveAspect = true;
        visual._body = body.rectTransform;

        if (count > 1) BuildCount(visual._rect, count, style);

        go.SetActive(false);
        return visual;
    }

    /// <summary>Over the board: the visual pops up and starts marching.</summary>
    public void Show()
    {
        if (_shown || _launched) return;
        _shown = true;

        gameObject.SetActive(true);
        _fade?.Kill();
        _fade = _group.DOFade(1f, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.Out).SetUpdate(true).SetLink(gameObject);
        JuiceFx.PopIn(_rect, 0.6f);
    }

    /// <summary>Back over the hand, or the drag is over with nothing sent: it folds away.</summary>
    public void Hide()
    {
        if (!_shown || _launched) return;
        _shown = false;

        _fade?.Kill();
        _fade = _group.DOFade(0f, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.In).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => gameObject.SetActive(false));
    }

    /// <summary>
    /// The card was played: the troops dip, then flick up and away toward the other field. The visual leaves the
    /// card for this (the card is about to slide home and be retired) and removes itself when it is gone.
    /// </summary>
    public void Launch()
    {
        if (_launched) return;
        _launched = true;
        _shown = false;

        JuiceTokensSO t = Juice.Tokens;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) _rect.SetParent(canvas.rootCanvas.transform, true);

        _fade?.Kill();
        _rect.DOKill();
        _group.alpha = 1f;
        _rect.localRotation = Quaternion.identity;

        float rest = _rect.localScale.x;
        float away = Juice.ReduceMotion ? 260f : 900f;
        Vector2 from = _rect.anchoredPosition;

        DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Append(_rect.DOScale(new Vector3(rest * 1.12f, rest * 0.86f, 1f), t.Durations.Press).SetEase(t.Eases.Press))
            .Join(_rect.DOAnchorPosY(from.y - 16f, t.Durations.Press).SetEase(t.Eases.Press))
            .Append(_rect.DOAnchorPosY(from.y + away, t.Durations.Travel).SetEase(t.Eases.Anticipate))
            .Join(_rect.DOScale(new Vector3(rest * 0.8f, rest * 1.25f, 1f), t.Durations.Travel).SetEase(t.Eases.In))
            .Insert(t.Durations.Press + t.Durations.Travel * 0.55f, _group.DOFade(0f, t.Durations.Travel * 0.45f))
            .OnComplete(() => Destroy(gameObject));
    }

    private void Update()
    {
        if (_launched || _chevrons == null) return;

        bool calm = Juice.ReduceMotion;
        float time = Time.unscaledTime;

        // The bodies hop on the spot: up quickly, down with a small squash, about three steps a second.
        float hop = calm ? 0f : Mathf.Abs(Mathf.Sin(time * 9.5f));
        _body.anchoredPosition = new Vector2(0f, hop * 12f);
        float squash = 1f - (1f - hop) * 0.07f;
        _body.localScale = new Vector3(2f - squash, squash, 1f);

        // The chevrons climb away from the bodies one after another and fade as they go: where the troops are headed.
        float start = BodySize * 0.42f;
        for (int i = 0; i < _chevrons.Length; i++)
        {
            float phase = calm ? 0.5f : Mathf.Repeat(time * 1.5f - i * 0.2f, 1f);
            _chevrons[i].rectTransform.anchoredPosition = new Vector2(0f, start + i * ChevronGap + phase * ChevronGap);
            Color c = _chevrons[i].color;
            c.a = Mathf.Sin(phase * Mathf.PI) * (1f - i * 0.22f);
            _chevrons[i].color = c;
        }
    }

    private static Image Part(RectTransform parent, string name, Sprite sprite, Color color, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * size;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>"x3" on a dark pill at the bodies' lower right: how many the card sends.</summary>
    private static void BuildCount(RectTransform parent, int count, TMP_Text style)
    {
        JuiceTokensSO t = Juice.Tokens;

        Image pill = Part(parent, "Count", JuiceSprites.Pill, t.Colors.Sheet, 1f);
        pill.type = Image.Type.Sliced;
        pill.pixelsPerUnitMultiplier = 32f / 19f;
        pill.rectTransform.sizeDelta = new Vector2(70f, 38f);
        pill.rectTransform.anchoredPosition = new Vector2(BodySize * 0.36f, -BodySize * 0.3f);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(pill.rectTransform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        if (style != null)
        {
            label.font = style.font;
            label.fontSharedMaterial = style.fontSharedMaterial;
        }

        label.fontSize = 24f;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = "x" + count;
        label.color = t.Colors.Ink;
    }
}
