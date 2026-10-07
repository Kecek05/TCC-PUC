using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class MenuNavController : MonoBehaviour
{
    [Title("References")]
    [InfoBox("buttons[i] selects strip.Pages[i]. Both lists are index-aligned.")]
    [SerializeField] private HorizontalPageStrip strip;
    [SerializeField] private List<MenuNavButton> buttons = new();

    [Title("Initial State")]
    [SerializeField, MinValue(0)] private int startingPageIndex = 0;

    [Title("Plate")]
    [InfoBox("A rounded plate that slides under the selected button (NavTabs). Built at runtime behind the " +
             "buttons' layout, so the layout never knows it is there. No sprite: no plate.")]
    [SerializeField] private Sprite plateSprite;
    [SerializeField] private Color plateColor = new(1f, 1f, 1f, 0.08f);
    [Tooltip("Added to the selected button's size.")]
    [SerializeField] private Vector2 platePadding = new(70f, 10f);

    [Title("Page Arrival")]
    [Tooltip("When a switch lands, the page's cards and controls ripple from top to bottom: each one bumps up a " +
             "little in size and trembles (NavTabs' arrival). The page stays fully drawn the whole time.")]
    [SerializeField] private bool rippleOnArrival = true;
    [SerializeField, MinValue(1)] private int maxRippled = 40;
    [Tooltip("How long the ripple takes to run from the top of the page to the bottom.")]
    [SerializeField, Min(0f)] private float rippleSeconds = 0.24f;

    private int _activePageIndex = -1;
    private int _pendingInactiveIndex = -1;

    private RectTransform _plate;
    private Tween _plateSlide;

    /// <summary>A switch the player made is under way, so its arrival gets the tick.</summary>
    private bool _switching;

    private readonly List<RectTransform> _rippled = new();
    private readonly List<Tween> _ripples = new();
    private static readonly Vector3[] Corners = new Vector3[4];

    private void Start()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            int capturedIndex = i;
            buttons[i].Button.onClick.AddListener(() =>
            {
                // The page already on screen: nothing to switch, but the tap still answers (a punch and a tick).
                if (capturedIndex == strip.CurrentPageIndex && strip.IsSettled)
                {
                    Juice.Sfx(SfxId.Tick);
                    JuiceFx.Punch(buttons[capturedIndex].transform, Juice.Tokens.Scales.PunchSoft);
                    return;
                }

                // The icon's own pop is MenuNavButton's; the press is the sound and the buzz.
                Juice.Sfx(SfxId.Press);
                Juice.Haptic(HapticRole.Select);
                strip.GoToPage(capturedIndex, animated: true);
            });
            buttons[i].SetSelected(false, animated: false);
        }

        BuildPlate();
        
        for (int i = 0; i < strip.PageCount; i++)
        {
            if (strip.Pages[i] != null) strip.Pages[i].OnPageBecameInactive();
        }

        strip.OnPageChanged += HandlePageChanged;
        strip.OnPageSettled += HandlePageSettled;
        strip.OnPageSwitchStarted += HandlePageSwitchStarted;

        int clampedStart = Mathf.Clamp(startingPageIndex, 0, Mathf.Max(0, strip.PageCount - 1));
        strip.GoToPage(clampedStart, animated: false);
    }

    private void OnDestroy()
    {
        if (strip != null)
        {
            strip.OnPageChanged -= HandlePageChanged;
            strip.OnPageSettled -= HandlePageSettled;
            strip.OnPageSwitchStarted -= HandlePageSwitchStarted;
        }

        _plateSlide?.Kill();
        KillRipples();

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null && buttons[i].Button != null)
                buttons[i].Button.onClick.RemoveAllListeners();
        }
    }

    // Fires at the start of the snap: activate the incoming page and update button highlights
    // immediately so the nav bar tracks the swipe. The outgoing page's OnPageBecameInactive is
    // deferred to HandlePageSettled so it runs only once the page has slid off-screen.
    private void HandlePageChanged(int newIndex)
    {
        if (newIndex == _activePageIndex) return;

        // Swiping back to a page that is still waiting to be deactivated: it never actually
        // left, so cancel its pending deactivation and don't re-activate it.
        bool returningToPendingPage = newIndex == _pendingInactiveIndex;

        // Any other not-yet-settled page is already off-screen; deactivate it now.
        if (_pendingInactiveIndex >= 0 && _pendingInactiveIndex != newIndex)
            SetPageInactive(_pendingInactiveIndex);
        _pendingInactiveIndex = -1;

        if (_activePageIndex >= 0)
        {
            SetButtonSelected(_activePageIndex, false);
            _pendingInactiveIndex = _activePageIndex; // OnPageBecameInactive runs on settle
        }

        _activePageIndex = newIndex;
        SetButtonSelected(_activePageIndex, true);

        if (!returningToPendingPage)
            SetPageActive(_activePageIndex);
    }

    // Fires when the strip finishes moving: now the outgoing page is off-screen, so deactivate it.
    private void HandlePageSettled(int settledIndex)
    {
        if (settledIndex != _activePageIndex) return;

        if (_switching)
        {
            _switching = false;
            Juice.Sfx(SfxId.Tick);
            Juice.Haptic(HapticRole.Select);
            RippleIn(settledIndex);
        }
        if (_pendingInactiveIndex >= 0 && _pendingInactiveIndex != _activePageIndex)
            SetPageInactive(_pendingInactiveIndex);
        _pendingInactiveIndex = -1;
    }

    /// <summary>
    /// A tap or a committed swipe started moving the strip (raised right after <see cref="HandlePageChanged"/>,
    /// so the active index is already the new one): the page whooshes, the plate slides, and the arrival ticks.
    /// </summary>
    private void HandlePageSwitchStarted(int index)
    {
        _switching = true;
        Juice.Sfx(SfxId.Whoosh);
        SlidePlate();

        // The page leaving stops wobbling at once; nothing is hidden, the next page slides in fully drawn.
        KillRipples();
    }

    /// <summary>
    /// NavTabs' arrival: the page lands with weight. Its cards and controls ripple from the top of the page down,
    /// each one bumping up a little in size and trembling, staggered by how far down it sits. Lighter things move
    /// more than heavy ones (the amount scales with the element's size), and an element whose scale is spoken for
    /// (a breathing call to action, a nav tab) only trembles.
    /// </summary>
    private void RippleIn(int index)
    {
        KillRipples();
        if (!rippleOnArrival || Juice.ReduceMotion) return;
        if (index < 0 || index >= strip.PageCount || strip.Pages[index] == null) return;

        Rect view = WorldRect((RectTransform)strip.transform);
        CollectRippled((RectTransform)strip.Pages[index].transform, view);

        for (int i = 0; i < _rippled.Count; i++)
        {
            RectTransform target = _rippled[i];
            Rect area = WorldRect(target);
            float depth = Mathf.Clamp01((view.yMax - area.center.y) / Mathf.Max(1f, view.height));

            float size = Mathf.Max(1f, SizeOf(target));
            float scale = OwnsScale(target) ? 0f : Mathf.Clamp(26f / size, 0.02f, 0.07f);
            float degrees = Mathf.Clamp(300f / size, 0.6f, 3.5f) * (i % 2 == 0 ? 1f : -1f);

            Tween ripple = JuiceFx.Jiggle(target, scale, degrees, depth * rippleSeconds + Random.Range(0f, 0.03f));
            if (ripple != null) _ripples.Add(ripple);
        }
    }

    private void KillRipples()
    {
        foreach (Tween ripple in _ripples) ripple?.Kill();
        _ripples.Clear();
        _rippled.Clear();
    }

    /// <summary>
    /// What ripples: every card and control on screen, and the page's top-level sections that hold neither (the
    /// player name strip, the shop's sign), so no part of the page lands dead. Never a parent and its child both:
    /// the wobbles would compound.
    /// </summary>
    private void CollectRippled(RectTransform page, Rect view)
    {
        _rippled.Clear();

        foreach (SingleCardInDeck card in page.GetComponentsInChildren<SingleCardInDeck>(false))
            TryRipple(card.transform, view);

        foreach (Selectable control in page.GetComponentsInChildren<Selectable>(false))
        {
            // A card's own hit area moves with the card, and a scrollbar is no element to the eye.
            if (control is Scrollbar || control.GetComponentInParent<SingleCardInDeck>() != null) continue;
            TryRipple(control.transform, view);
        }

        Transform root = ContentRoot(page);
        for (int i = 0; i < root.childCount; i++)
        {
            Transform section = root.GetChild(i);
            if (!HoldsRippled(section)) TryRipple(section, view);
        }
    }

    private void TryRipple(Transform target, Rect view)
    {
        if (_rippled.Count >= maxRippled || !(target is RectTransform rect) || !target.gameObject.activeInHierarchy) return;

        // Off screen (below the scroll's fold) there is nothing to see; and a full-page hit area or wrapper would
        // shake the whole page.
        Rect area = WorldRect(rect);
        if (!area.Overlaps(view)) return;
        if (area.width > view.width * 0.8f && area.height > view.height * 0.5f) return;

        foreach (RectTransform added in _rippled)
            if (target.IsChildOf(added)) return;

        _rippled.Add(rect);
    }

    private bool HoldsRippled(Transform section)
    {
        foreach (RectTransform added in _rippled)
            if (added.IsChildOf(section)) return true;
        return false;
    }

    /// <summary>A breathing button and a nav tab animate their own scale: the ripple only tilts them.</summary>
    private static bool OwnsScale(Transform target) =>
        target.GetComponent<MenuNavButton>() != null ||
        (target.TryGetComponent(out JuicyButton juicy) && juicy.IsBreathing);

    /// <summary>The element's larger side in canvas units: a card counts its visible body, not its empty shell.</summary>
    private static float SizeOf(RectTransform target)
    {
        if (target.TryGetComponent(out SingleCardInDeck card)) target = card.Visual;
        Rect rect = target.rect;
        return Mathf.Max(rect.width, rect.height);
    }

    private static Rect WorldRect(RectTransform rect)
    {
        rect.GetWorldCorners(Corners);
        return Rect.MinMaxRect(Corners[0].x, Corners[0].y, Corners[2].x, Corners[2].y);
    }

    /// <summary>
    /// Where a page's real content lives: down through single-child wrappers and into a scroll view's content, so
    /// the deck page's sections are what ripple, not the one scroll view holding them.
    /// </summary>
    private static Transform ContentRoot(Transform page)
    {
        Transform node = page;
        for (int depth = 0; depth < 6; depth++)
        {
            ScrollRect scroll = node.GetComponent<ScrollRect>();
            if (scroll != null && scroll.content != null)
            {
                node = scroll.content;
                continue;
            }

            Transform only = null;
            int active = 0;
            for (int i = 0; i < node.childCount; i++)
            {
                if (!node.GetChild(i).gameObject.activeSelf) continue;
                active++;
                only = node.GetChild(i);
            }

            if (active != 1) break;
            node = only;
        }

        return node;
    }

    /// <summary>
    /// Creates the plate as the sibling just behind the buttons' container, so it draws over the bar and under
    /// every icon without ever entering the buttons' layout.
    /// </summary>
    private void BuildPlate()
    {
        if (plateSprite == null || buttons.Count == 0 || buttons[0] == null) return;

        Transform row = buttons[0].transform.parent;
        if (row == null || row.parent == null) return;

        GameObject plate = new GameObject("NavPlate", typeof(RectTransform), typeof(Image));
        _plate = (RectTransform)plate.transform;
        _plate.SetParent(row.parent, false);
        _plate.SetSiblingIndex(row.GetSiblingIndex());
        _plate.anchorMin = _plate.anchorMax = _plate.pivot = new Vector2(0.5f, 0.5f);

        Image image = plate.GetComponent<Image>();
        image.sprite = plateSprite;
        image.type = plateSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = plateColor;
        image.raycastTarget = false;
    }

    /// <summary>
    /// Keeps the plate on the selected button whenever it is not sliding. That covers the start-up selection,
    /// the first frames before the layout has placed anything, and a resolution change.
    /// </summary>
    private void LateUpdate()
    {
        if (_plate == null || (_plateSlide != null && _plateSlide.IsActive())) return;
        if (!TryGetPlateTarget(out Vector3 target, out Vector2 size)) return;

        _plate.localPosition = target;
        _plate.sizeDelta = size;
    }

    private void SlidePlate()
    {
        if (_plate == null || !TryGetPlateTarget(out Vector3 target, out Vector2 size)) return;

        JuiceTokensSO t = Juice.Tokens;
        _plateSlide?.Kill();
        _plate.sizeDelta = size;
        _plateSlide = _plate.DOLocalMove(target, t.Durations.Pop).SetEase(t.Eases.Out).SetUpdate(true)
            .SetLink(_plate.gameObject);
    }

    private bool TryGetPlateTarget(out Vector3 localPosition, out Vector2 size)
    {
        localPosition = default;
        size = default;
        if (_activePageIndex < 0 || _activePageIndex >= buttons.Count || buttons[_activePageIndex] == null) return false;

        RectTransform button = (RectTransform)buttons[_activePageIndex].transform;
        localPosition = _plate.parent.InverseTransformPoint(button.TransformPoint(button.rect.center));
        localPosition.z = 0f;
        size = button.rect.size + platePadding;
        return size.x > 0f && size.y > 0f;
    }

    private void SetButtonSelected(int index, bool selected)
    {
        if (index >= 0 && index < buttons.Count && buttons[index] != null)
            buttons[index].SetSelected(selected, animated: true);
    }

    private void SetPageActive(int index)
    {
        if (index >= 0 && index < strip.PageCount && strip.Pages[index] != null)
            strip.Pages[index].OnPageBecameActive();
    }

    private void SetPageInactive(int index)
    {
        if (index >= 0 && index < strip.PageCount && strip.Pages[index] != null)
            strip.Pages[index].OnPageBecameInactive();
    }

    private void OnValidate()
    {
        if (strip != null && buttons.Count != strip.PageCount)
        {
            Debug.LogWarning($"[{nameof(MenuNavController)}] buttons ({buttons.Count}) and strip.PageCount ({strip.PageCount}) differ on '{name}'.", this);
        }
    }
}
