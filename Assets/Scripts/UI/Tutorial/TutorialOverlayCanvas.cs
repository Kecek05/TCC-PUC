using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Draws the tutorial: a line of copy in a box that keeps out of the highlight's way, four dim panels that
/// frame a hole around whatever the player must touch, a ring on that hole, and a hand showing the gesture.
/// </summary>
/// <remarks>
/// The hole is made of <b>four solid panels</b> rather than a cut-out shader or a mask: four rects are
/// trivially positioned around any target, need no custom material, and cost four draw calls.
/// <para>
/// The panels are decoration only. Input belongs to a <see cref="TutorialInputShield"/> built beside them
/// (not under <c>content</c>, so it keeps holding while the overlay hides for a settling beat): a step that
/// asks for something opens exactly the hole, a step that asks for nothing closes the whole board.
/// </para>
/// </remarks>
public class TutorialOverlayCanvas : BaseTutorialOverlay
{
    [Title("References")]
    [SerializeField] private GameObject content;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform canvasRect;

    [Title("Text")]
    [SerializeField] private RectTransform textBox;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Title("Dimmer")]
    [InfoBox("Four panels framing the hole: top, bottom, left, right. Together they dim everything except " +
             "the thing the player is being asked to touch.")]
    [SerializeField] private RectTransform dimTop;
    [SerializeField] private RectTransform dimBottom;
    [SerializeField] private RectTransform dimLeft;
    [SerializeField] private RectTransform dimRight;

    [Tooltip("Optional. Ring drawn on the hole.")]
    [SerializeField] private RectTransform highlightRing;

    [Tooltip("Optional. The pointing hand.")]
    [SerializeField] private RectTransform hand;

    [Title("Reward")]
    [InfoBox("Shown by the outro, and kept up while the match hands over, so the player sees what they won " +
             "rather than only reading its name. Built from the same RewardPrefab tiles as the end-of-match " +
             "screen: the card with its icon, then the gold.")]
    [FormerlySerializedAs("unlockedCardRoot")]
    [SerializeField] private GameObject rewardRoot;

    [Tooltip("The reward card's name. Several cards still share placeholder art, so the icon alone does not " +
             "say which card was won.")]
    [FormerlySerializedAs("unlockedCardName")]
    [SerializeField] private TextMeshProUGUI rewardTitle;

    [SerializeField] private Transform rewardTilesParent;
    [SerializeField] private RewardEntryUI rewardEntryPrefab;
    [SerializeField] private Sprite coinSprite;

    [Title("Layout")]
    [Tooltip("Canvas units of slack added around a highlighted target before the hole is cut.")]
    [SerializeField] private float holePadding = 24f;

    [Tooltip("Where the text box sits when the highlight is in the BOTTOM half of the screen, and where " +
             "it sits otherwise. The box moves out of the way rather than covering what it points at.")]
    [SerializeField] private Vector2 textBoxHighPosition = new(0f, 420f);
    [SerializeField] private Vector2 textBoxLowPosition = new(0f, -420f);
    [SerializeField] private float textBoxMoveDuration = 0.25f;

    [Title("Hand")]
    [SerializeField] private float tapPeriod = 1.1f;
    [SerializeField] private float dragPeriod = 1.4f;
    [SerializeField] private float swipeDistance = 260f;
    [SerializeField] private float tapPunchScale = 0.78f;

    private Camera _worldCamera;
    private Tween _textBoxTween;
    private Vector2 _textBoxTarget;
    private bool _textBoxPlaced;

    private float _hintTime;

    private TutorialInputShield _shield;

    /// <summary>Reward tiles, kept and reconfigured rather than rebuilt: the outro and the hand-off show the
    /// same payout twice in a row.</summary>
    private readonly List<RewardEntryUI> _rewardTiles = new();

    private void Awake()
    {
        ServiceLocator.Register<BaseTutorialOverlay>(this);

        if (canvasRect == null && canvas != null) canvasRect = (RectTransform)canvas.transform;

        MakeDimsDecorative();
        BuildInputShield();

        if (continueButton != null) continueButton.onClick.AddListener(RaiseContinueTapped);
        if (skipButton != null) skipButton.onClick.AddListener(RaiseSkipTapped);

        if (content != null) content.SetActive(false);
        HideReward();
    }

    private void OnDestroy()
    {
        _textBoxTween?.Kill();
        ServiceLocator.Unregister<BaseTutorialOverlay>();
    }

    /// <summary>The panels only draw. The shield is the one thing deciding what the player can touch, so a
    /// panel left as a raycast target by its prefab can never become a second, disagreeing gate.</summary>
    private void MakeDimsDecorative()
    {
        MakeDecorative(dimTop);
        MakeDecorative(dimBottom);
        MakeDecorative(dimLeft);
        MakeDecorative(dimRight);
    }

    private static void MakeDecorative(RectTransform panel)
    {
        if (panel == null) return;

        Graphic graphic = panel.GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = false;
    }

    /// <summary>
    /// Builds the input shield in code rather than the prefab: it is an implementation detail of this
    /// overlay with nothing to author, and it has two placement rules that are easy to break by hand.
    /// </summary>
    /// <remarks>
    /// It sits <b>beside</b> <c>content</c>, not under it, so hiding the overlay for a settling beat leaves the
    /// board held; and it is the <b>first</b> sibling, so everything in <c>content</c> — Continue and Skip
    /// above all — draws and raycasts in front of it. It shares <see cref="canvasRect"/>'s space, which is
    /// where the hole is computed, so the opening is the hole without any conversion.
    /// </remarks>
    private void BuildInputShield()
    {
        RectTransform parent = canvasRect != null ? canvasRect : (RectTransform)transform;

        GameObject shieldObject = new("InputShield", typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform shieldRect = (RectTransform)shieldObject.transform;
        shieldRect.SetParent(parent, false);
        shieldRect.SetAsFirstSibling();
        shieldRect.anchorMin = Vector2.zero;
        shieldRect.anchorMax = Vector2.one;
        shieldRect.offsetMin = shieldRect.offsetMax = Vector2.zero;

        _shield = shieldObject.AddComponent<TutorialInputShield>();
        _shield.Initialize(parent);
    }

    public override void SetInputMode(TutorialInputMode mode)
    {
        if (_shield != null) _shield.SetMode(mode);
    }

    /// <summary>Resolved late: Camera.main is null while the gameplay scene is still loading.</summary>
    private Camera WorldCamera
    {
        get
        {
            if (_worldCamera == null) _worldCamera = Camera.main;
            return _worldCamera;
        }
    }

    public override void Show(string text_, bool showContinue)
    {
        if (content != null) content.SetActive(true);
        SetDimsVisible(true);

        bool hasText = !string.IsNullOrWhiteSpace(text_);
        if (textBox != null) textBox.gameObject.SetActive(hasText);
        if (text != null) text.text = text_;

        if (continueButton != null) continueButton.gameObject.SetActive(showContinue && hasText);
    }

    public override void ShowTip(string tipText, TutorialHighlight highlight)
    {
        Show(tipText, showContinue: false);

        // The dims are what turn a line into an instruction: without them the board stays readable and
        // plainly the player's. Nothing else here takes a touch — every decorative graphic in this overlay
        // is non-raycast, Continue is hidden, and the shield is left in whatever mode it was given.
        SetDimsVisible(false);
        SetHighlight(highlight);
    }

    public override void HideTip() => Hide();

    private void SetDimsVisible(bool visible)
    {
        SetActive(dimTop, visible);
        SetActive(dimBottom, visible);
        SetActive(dimLeft, visible);
        SetActive(dimRight, visible);
    }

    private static void SetActive(RectTransform panel, bool active)
    {
        if (panel != null && panel.gameObject.activeSelf != active) panel.gameObject.SetActive(active);
    }

    public override void ShowReward(Reward reward, CardDataSO card)
    {
        int used = 0;

        if (rewardTilesParent != null && rewardEntryPrefab != null)
        {
            // The card leads: it is what the menu half is about to ask the player to find, equip and level.
            if (reward.HasCard) GetRewardTile(used++).SetCard(card, reward.Copies);
            if (reward.Gold > 0) GetRewardTile(used++).SetGold(reward.Gold, coinSprite);
        }

        // A layout group skips inactive children, so a spare tile leaves no gap.
        for (int i = 0; i < _rewardTiles.Count; i++)
            _rewardTiles[i].gameObject.SetActive(i < used);

        if (rewardTitle != null) rewardTitle.text = reward.HasCard && card != null ? card.CardName : string.Empty;
        if (rewardRoot != null) rewardRoot.SetActive(used > 0);
    }

    public override void HideReward()
    {
        if (rewardRoot != null) rewardRoot.SetActive(false);
    }

    private RewardEntryUI GetRewardTile(int index)
    {
        while (_rewardTiles.Count <= index)
            _rewardTiles.Add(Instantiate(rewardEntryPrefab, rewardTilesParent));

        return _rewardTiles[index];
    }

    public override void SetSkipVisible(bool visible)
    {
        if (skipButton != null) skipButton.gameObject.SetActive(visible);
    }

    public override void Hide()
    {
        _textBoxTween?.Kill();
        _textBoxPlaced = false;

        // The mode is left alone — whoever hid the overlay decides whether the board stays held — but a
        // hole nobody can see must not stay open.
        if (_shield != null) _shield.ClearOpening();

        HideReward();

        if (content != null) content.SetActive(false);
    }

    public override void SetHighlight(TutorialHighlight highlight)
    {
        if (!highlight.HasTarget)
        {
            // No target: the four panels meet in the middle and cover the screen, which is the plain dim a
            // read-this step wants.
            ApplyHole(new Rect(0f, 0f, 0f, 0f), false);
            if (highlightRing != null) highlightRing.gameObject.SetActive(false);
            if (hand != null) hand.gameObject.SetActive(false);
            PlaceTextBox(Vector2.zero, preferHigh: false);
            return;
        }

        Rect hole = ResolveHole(highlight);
        ApplyHole(hole, true);

        if (highlightRing != null)
        {
            highlightRing.gameObject.SetActive(true);
            highlightRing.anchoredPosition = hole.center;
            highlightRing.sizeDelta = hole.size;
        }

        UpdateHand(highlight, hole);

        // Keep the copy out of the way of the thing it is describing.
        PlaceTextBox(hole.center, preferHigh: hole.center.y < 0f);
    }

    /// <summary>The highlighted area in this canvas's local space, whichever kind of target it is.</summary>
    private Rect ResolveHole(TutorialHighlight highlight)
    {
        if (highlight.Rect != null) return RectToCanvas(highlight.Rect);

        Vector2 center = WorldToCanvas(highlight.WorldPosition);

        // A world radius has to become canvas units, and the honest conversion is to project a point one
        // radius away and measure the gap — that survives any camera size or canvas scale.
        Vector2 edge = WorldToCanvas(highlight.WorldPosition + Vector3.right * highlight.WorldRadius);
        float radius = Mathf.Max(24f, Mathf.Abs(edge.x - center.x));

        return new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
    }

    /// <summary>
    /// A UI target framed in this canvas, going through screen space in between.
    /// </summary>
    /// <remarks>
    /// The two cameras are different on purpose and this is the one place it matters. The card hand and
    /// the mana bar live on a <b>Screen Space - Camera</b> canvas, so their world corners only become
    /// screen points through <i>that</i> canvas's camera; this overlay is Screen Space - Overlay, so the
    /// trip back into its local space takes a null camera. Using one camera for both steps puts the hole
    /// somewhere else entirely.
    /// </remarks>
    private Rect RectToCanvas(RectTransform target)
    {
        Camera targetCamera = CameraFor(target.GetComponentInParent<Canvas>());
        Camera selfCamera = CameraFor(canvas);

        Rect own = CornersToCanvas(target, targetCamera, selfCamera);
        if (own.width > 1f && own.height > 1f) return own;

        // A target with no size of its own. Common enough to handle here rather than at every call site:
        // the deck page's card widget is a 0x0 shell whose whole appearance comes from a Content child, so
        // framing the widget itself would cut a hole with no area. Fall back to what it actually draws.
        Vector2 min = new(own.xMin, own.yMin);
        Vector2 max = new(own.xMax, own.yMax);
        bool found = false;

        foreach (RectTransform child in target.GetComponentsInChildren<RectTransform>())
        {
            if (child == target || !child.gameObject.activeInHierarchy) continue;

            Rect childRect = CornersToCanvas(child, targetCamera, selfCamera);
            if (childRect.width <= 1f || childRect.height <= 1f) continue;

            min = found ? Vector2.Min(min, childRect.min) : childRect.min;
            max = found ? Vector2.Max(max, childRect.max) : childRect.max;
            found = true;
        }

        return found ? new Rect(min.x, min.y, max.x - min.x, max.y - min.y) : own;
    }

    private Rect CornersToCanvas(RectTransform target, Camera targetCamera, Camera selfCamera)
    {
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;

        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, selfCamera, out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    /// <summary>The camera a canvas is rendered through, or null for an overlay one. Read off the ROOT
    /// canvas: a nested canvas inherits its render mode and leaves its own worldCamera empty.</summary>
    private static Camera CameraFor(Canvas target)
    {
        if (target == null) return null;

        Canvas root = target.rootCanvas != null ? target.rootCanvas : target;
        return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
    }

    private Vector2 WorldToCanvas(Vector3 world)
    {
        Camera selfCamera = CameraFor(canvas);

        Vector2 screen = WorldCamera != null
            ? (Vector2)WorldCamera.WorldToScreenPoint(world)
            : Vector2.zero;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, selfCamera, out Vector2 local);
        return local;
    }

    /// <summary>
    /// Frames <paramref name="hole"/> with the four dim panels. With no hole they meet in the middle and
    /// cover the whole screen.
    /// </summary>
    private void ApplyHole(Rect hole, bool hasHole)
    {
        if (canvasRect == null) return;

        float halfW = canvasRect.rect.width * 0.5f;
        float halfH = canvasRect.rect.height * 0.5f;

        float pad = hasHole ? holePadding : 0f;
        float left = hasHole ? hole.xMin - pad : 0f;
        float right = hasHole ? hole.xMax + pad : 0f;
        float bottom = hasHole ? hole.yMin - pad : 0f;
        float top = hasHole ? hole.yMax + pad : 0f;

        // Clamped so a target partly off-screen cannot give a panel a negative size, which would flip it
        // and leave an undimmed band on the opposite edge.
        left = Mathf.Clamp(left, -halfW, halfW);
        right = Mathf.Clamp(right, -halfW, halfW);
        bottom = Mathf.Clamp(bottom, -halfH, halfH);
        top = Mathf.Clamp(top, -halfH, halfH);

        SetPanel(dimTop, new Rect(-halfW, top, halfW * 2f, halfH - top));
        SetPanel(dimBottom, new Rect(-halfW, -halfH, halfW * 2f, bottom + halfH));
        SetPanel(dimLeft, new Rect(-halfW, bottom, left + halfW, top - bottom));
        SetPanel(dimRight, new Rect(right, bottom, halfW - right, top - bottom));

        // The opening is exactly the undimmed area — padded and clamped the same way — so what looks open
        // is open, and nothing that looks dimmed can be touched.
        if (_shield == null) return;

        if (hasHole) _shield.SetOpening(new Rect(left, bottom, right - left, top - bottom));
        else _shield.ClearOpening();
    }

    private static void SetPanel(RectTransform panel, Rect rect)
    {
        if (panel == null) return;

        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0f, 0f);
        panel.anchoredPosition = new Vector2(rect.x, rect.y);
        panel.sizeDelta = new Vector2(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
    }

    private void UpdateHand(TutorialHighlight highlight, Rect hole)
    {
        if (hand == null) return;

        if (highlight.Hint == TutorialHintKind.None)
        {
            hand.gameObject.SetActive(false);
            return;
        }

        hand.gameObject.SetActive(true);
        _hintTime += Time.unscaledDeltaTime;

        switch (highlight.Hint)
        {
            case TutorialHintKind.Drag when highlight.HasDragTarget:
            {
                float t = Mathf.Repeat(_hintTime / Mathf.Max(0.1f, dragPeriod), 1f);
                Vector2 to = WorldToCanvas(highlight.DragToWorld);
                hand.anchoredPosition = Vector2.Lerp(hole.center, to, Mathf.SmoothStep(0f, 1f, t));
                hand.localScale = Vector3.one;
                break;
            }
            case TutorialHintKind.SwipeUp:
            case TutorialHintKind.SwipeDown:
            {
                float t = Mathf.Repeat(_hintTime / Mathf.Max(0.1f, dragPeriod), 1f);
                float dir = highlight.Hint == TutorialHintKind.SwipeUp ? 1f : -1f;
                hand.anchoredPosition = hole.center + new Vector2(0f, dir * swipeDistance * Mathf.SmoothStep(0f, 1f, t));
                hand.localScale = Vector3.one;
                break;
            }
            default:
            {
                // Tap: sit on the target and pulse, so the gesture reads as a press rather than a drag.
                float t = Mathf.Repeat(_hintTime / Mathf.Max(0.1f, tapPeriod), 1f);
                float pulse = Mathf.Lerp(1f, tapPunchScale, Mathf.Sin(t * Mathf.PI));
                hand.anchoredPosition = hole.center;
                hand.localScale = Vector3.one * pulse;
                break;
            }
        }
    }

    /// <summary>
    /// Parks the copy above or below the highlight. Tweened rather than snapped, and only when the side
    /// actually changes, so a highlight that drifts a little does not make the box jitter.
    /// </summary>
    private void PlaceTextBox(Vector2 holeCenter, bool preferHigh)
    {
        if (textBox == null) return;

        Vector2 target = preferHigh ? textBoxHighPosition : textBoxLowPosition;
        if (_textBoxPlaced && target == _textBoxTarget) return;

        _textBoxTarget = target;

        if (!_textBoxPlaced)
        {
            _textBoxPlaced = true;
            textBox.anchoredPosition = target;
            return;
        }

        _textBoxTween?.Kill();
        _textBoxTween = textBox.DOAnchorPos(target, textBoxMoveDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnKill(() => _textBoxTween = null);
    }
}
