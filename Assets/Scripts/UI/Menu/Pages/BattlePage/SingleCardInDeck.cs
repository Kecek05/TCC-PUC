using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One card portrait: cost, art, type background and collection progress. Used twice over — as the tappable
/// card in the deck page grid, and as the static hero card inside the info panel. The panel's copy has its
/// level pill and its button stripped out, so every reference below that copy does not carry is optional.
/// </summary>
public class SingleCardInDeck : MonoBehaviour
{
    /// <summary>How the body travels to its new place when the deck page relays out (DeckEquip).</summary>
    public enum Flight
    {
        /// <summary>A collection card sliding into a gap, or opening one (ease-out).</summary>
        Slide,

        /// <summary>A deck card closing ranks after a removal: it overshoots into its slot (ease-pop).</summary>
        Snap,

        /// <summary>The card the player equipped: it lifts, grows to scale-select and arcs down onto its slot.</summary>
        Equip,

        /// <summary>The card the player removed: it travels home bowing under the line, on top of the page.</summary>
        Remove
    }

    [Title("References")]
    [SerializeField] private TextMeshProUGUI cardCost;
    [SerializeField] private Image cardImage;
    [SerializeField] private Image cardTypeBackground;

    [Tooltip("Optional. The level pill behind the level text — absent in the info panel, which prints the " +
             "level itself, larger.")]
    [SerializeField] private Image cardLevelBackground;

    [SerializeField] private Image cardLevelFill;

    [Tooltip("Optional. See cardLevelBackground.")]
    [SerializeField] private TextMeshProUGUI levelText;

    [SerializeField] private TextMeshProUGUI quantityText;

    [Tooltip("Optional. The tap target that opens the card's action popup — absent in the info panel, where " +
             "the card is a portrait rather than a control.")]
    [SerializeField] private Button cardButton;

    [SerializeField] private CardsRarityDataSO cardsRarityData;
    [Tooltip("Debug crutch: shown only when CardDataSO.ShowPlaceholderNameOverlay is on. Overlays the CardName " +
             "on top of the icon so cards that share placeholder art are still distinguishable in the deck menu.")]
    [SerializeField] private TextMeshProUGUI debugPlaceholderLabel;

    [Title("Card Types")]
    [SerializeField] private Sprite spellBackgroundType;
    [SerializeField] private Sprite towerBackgroundType;
    [SerializeField] private Sprite enemyBackgroundType;

    [Title("Locked State")]
    [Tooltip("Multiplied into the card's colours while it is still locked. A stand-in until there is a " +
             "proper lock icon to overlay.")]
    [SerializeField] private Color lockedTint = new(0.32f, 0.32f, 0.32f, 1f);

    [SerializeField] private string lockedLabel = "Locked";
    [SerializeField] private string maxLevelLabel = "MAX";

    [Title("Motion")]
    [Tooltip("Optional. The card's visible body: what flies when the deck page relays out, and what squashes " +
             "on a tap. Empty: the child named Content (the widget root is a 0x0 shell the layout moves).")]
    [SerializeField] private RectTransform visual;

    [Tooltip("Optional. The arrow beside the copies bar, which bobs while the card can be upgraded. Empty: the " +
             "'Icon' beside the bar.")]
    [SerializeField] private RectTransform upgradeArrow;

    private CardDataSO _cardData;
    private DeckUIController _deckUIController;

    /// <summary>The art's authored rect, so a card without a custom one can undo the previous card's.</summary>
    private Vector2 _defaultImagePosition;
    private Vector2 _defaultImageSize;

    private bool _buttonWired;
    private bool _imageRectCaptured;

    private Vector2 _visualRest;
    private bool _visualRestCaptured;
    private Sequence _flight;
    private Canvas _onTopCanvas;

    private bool _upgradeReady;
    private Color _fillColor;
    private bool _fillColorCaptured;
    private Vector2 _arrowRest;
    private bool _arrowRestCaptured;
    private Tween _arrowBob;

    public CardDataSO CardData => _cardData;

    /// <summary>Whether the player owns this card. Locked cards cannot be equipped.</summary>
    public bool IsOwned { get; private set; } = true;

    /// <param name="deckUIController">Optional. Only the deck page has one; without it the card draws
    /// normally but does not open the action popup.</param>
    public void Initialize(CardDataSO cardData, DeckUIController deckUIController = null)
    {
        _cardData = cardData;
        _deckUIController = deckUIController;

        CaptureImageRect();

        cardCost.text = cardData.Cost.ToString();
        cardImage.sprite = cardData.CardImage;
        cardTypeBackground.sprite = GetTypeBackground(cardData.ExistingType);

        var rarity = cardsRarityData.Get(cardData.Rarity);
        if (cardLevelBackground != null) cardLevelBackground.color = rarity.mainColor;
        if (levelText != null) levelText.color = rarity.textColor;

        // Written on every card, not only on the ones that override: a single widget can serve card after
        // card (the info panel reuses one), so a card without an override has to undo the previous card's.
        cardImage.rectTransform.anchoredPosition = cardData.UseCustomPositionCardInMenu
            ? cardData.CustomPositionCardInMenu
            : _defaultImagePosition;

        cardImage.rectTransform.sizeDelta = cardData.UseCustomSizeCardInMenu
            ? cardData.CustomSizeCardInMenu
            : _defaultImageSize;

        if (debugPlaceholderLabel != null)
        {
            debugPlaceholderLabel.gameObject.SetActive(cardData.ShowPlaceholderNameOverlay);
            if (cardData.ShowPlaceholderNameOverlay)
                debugPlaceholderLabel.text = cardData.CardName;
        }

        InitializeButtons();
    }

    /// <summary>
    /// Shows this card's real progression. Called by <see cref="DeckUIController"/> and by the card info
    /// panel, which own the save lookup — the widget stays a pure view.
    /// </summary>
    /// <param name="copiesNeeded">0 means the card is at max level and no longer collects copies.</param>
    public void SetProgression(int level, int copies, int copiesNeeded, bool owned)
    {
        IsOwned = owned;

        var rarity = cardsRarityData.Get(_cardData.Rarity);
        cardImage.color = owned ? _cardData.CardColor : _cardData.CardColor * lockedTint;

        if (cardLevelBackground != null)
            cardLevelBackground.color = owned ? rarity.mainColor : rarity.mainColor * lockedTint;

        if (!owned)
        {
            if (levelText != null) levelText.text = lockedLabel;
            quantityText.text = "-";
            cardLevelFill.fillAmount = 0f;
            return;
        }

        if (levelText != null) levelText.text = $"Level {level}";

        if (copiesNeeded <= 0)
        {
            // Max level: there is nothing left to collect toward, so show a full bar rather than 0/0.
            quantityText.text = maxLevelLabel;
            cardLevelFill.fillAmount = 1f;
            return;
        }

        quantityText.text = $"{copies}/{copiesNeeded}";
        cardLevelFill.fillAmount = Mathf.Clamp01((float)copies / copiesNeeded);
    }

    /// <summary>
    /// Remembers the art's authored rect the first time a card is shown. Done here rather than in Awake
    /// because a widget instantiated under an inactive page would not have run Awake yet, and would then
    /// adopt (0,0) as its "default" and move every card without an override to the corner.
    /// </summary>
    private void CaptureImageRect()
    {
        if (_imageRectCaptured || cardImage == null) return;

        _imageRectCaptured = true;
        _defaultImagePosition = cardImage.rectTransform.anchoredPosition;
        _defaultImageSize = cardImage.rectTransform.sizeDelta;
    }

    /// <summary>
    /// The card's visible body. The widget root is a shell the layouts move instantly; the body is what the
    /// player sees, so it is what flies (FLIP) and what squashes on a tap.
    /// </summary>
    public RectTransform Visual
    {
        get
        {
            if (visual == null)
            {
                Transform content = transform.Find("Content");
                visual = content != null ? (RectTransform)content : (RectTransform)transform;
            }

            if (!_visualRestCaptured)
            {
                _visualRestCaptured = true;
                _visualRest = visual.anchoredPosition;
            }

            return visual;
        }
    }

    /// <summary>
    /// The "you can upgrade this" invitation (CardUpgrade's ready state): the copies bar turns upgrade green
    /// and its arrow bobs. Shown on the deck page and on the info panel's portrait alike.
    /// </summary>
    public void SetUpgradeReady(bool ready)
    {
        if (cardLevelFill == null) return;

        if (!_fillColorCaptured)
        {
            _fillColorCaptured = true;
            _fillColor = cardLevelFill.color;
        }

        if (ready == _upgradeReady) return;
        _upgradeReady = ready;

        cardLevelFill.color = ready ? Juice.Tokens.Colors.Upgrade : _fillColor;

        RectTransform arrow = UpgradeArrow;
        if (arrow == null) return;

        _arrowBob?.Kill();
        _arrowBob = null;
        arrow.anchoredPosition = _arrowRest;

        if (ready && !Juice.ReduceMotion)
            _arrowBob = arrow.DOAnchorPosY(_arrowRest.y + 7f, 0.42f).SetEase(Juice.Tokens.Eases.Breathe)
                .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
    }

    private RectTransform UpgradeArrow
    {
        get
        {
            if (upgradeArrow == null && cardLevelFill != null)
            {
                // Fill sits in the bar's Background; the arrow is that background's sibling.
                Transform bar = cardLevelFill.transform.parent != null ? cardLevelFill.transform.parent.parent : null;
                if (bar != null) upgradeArrow = bar.Find("Icon") as RectTransform;
            }

            if (upgradeArrow != null && !_arrowRestCaptured)
            {
                _arrowRestCaptured = true;
                _arrowRest = upgradeArrow.anchoredPosition;
            }

            return upgradeArrow;
        }
    }

    /// <summary>Whether the body is flying to its place right now.</summary>
    public bool IsFlying => _flight != null && _flight.IsActive();

    /// <summary>Where the card is drawn right now, mid-flight included.</summary>
    public Vector3 VisualPosition => Visual.position;

    /// <summary>
    /// The middle of the card's top edge where the card rests, in world space: where the action popup is pinned. A
    /// flight or a squash in progress does not move it.
    /// </summary>
    public Vector3 TopCenterWorld
    {
        get
        {
            RectTransform body = Visual;

            // localPosition minus anchoredPosition is the anchor's reference point, which no flight moves.
            Vector3 anchor = body.localPosition - (Vector3)body.anchoredPosition;
            Vector3 top = anchor + (Vector3)_visualRest + new Vector3(0f, body.rect.yMax, 0f);
            return body.parent.TransformPoint(top);
        }
    }

    /// <summary>
    /// The last half of a FLIP: the layout has already moved the widget, so draw the body back where it was
    /// (<paramref name="fromWorld"/>) and fly it home. Returns false when it had not actually moved.
    /// </summary>
    /// <param name="flight">How it travels. The card the player moved arcs over the page on top of everything (so it
    /// never passes under the headers between the deck and the collection); a reflowing card just slides.</param>
    /// <param name="onLanded">Called when it arrives. Not called when the flight is cut short (a page switch).</param>
    public bool FlyFrom(Vector3 fromWorld, float delay, Flight flight, Action onLanded = null)
    {
        RectTransform body = Visual;
        StopFlight();

        Vector3 home = body.position;
        float minimum = 2f * body.lossyScale.x;
        if ((home - fromWorld).sqrMagnitude < minimum * minimum) return false;

        body.position = fromWorld;
        bool travels = flight is Flight.Equip or Flight.Remove;
        SetOnTop(travels);

        JuiceTokensSO t = Juice.Tokens;
        float duration = travels ? t.Durations.Travel : t.Durations.Pop;
        _flight = DOTween.Sequence().SetUpdate(true).SetLink(gameObject).SetTarget(this).SetDelay(delay);

        // The arcs are the design's: up and over onto a slot, under the line on the way home (DOJump's power is
        // the bow at mid-flight, in canvas units).
        bool grows = flight == Flight.Equip && !Juice.ReduceMotion;
        switch (flight)
        {
            case Flight.Equip:
                _flight.Append(body.DOJumpAnchorPos(_visualRest, 90f, 1, duration).SetEase(t.Eases.InOut));
                break;
            case Flight.Remove:
                _flight.Append(body.DOJumpAnchorPos(_visualRest, -60f, 1, duration).SetEase(t.Eases.InOut));
                break;
            case Flight.Snap:
                _flight.Append(body.DOAnchorPos(_visualRest, duration).SetEase(t.Eases.Pop));
                break;
            default:
                _flight.Append(body.DOAnchorPos(_visualRest, duration).SetEase(t.Eases.Out));
                break;
        }

        // Lifted off the page: it peaks at scale-select half way and is back to size as it touches down.
        if (grows)
        {
            _flight.Insert(0f, body.DOScale(t.Scales.Select, duration * 0.5f).SetEase(t.Eases.Out));
            _flight.Insert(duration * 0.5f, body.DOScale(1f, duration * 0.5f).SetEase(t.Eases.In));
        }

        bool landed = false;
        _flight.OnComplete(() =>
        {
            landed = true;
            onLanded?.Invoke();
        });
        _flight.OnKill(() =>
        {
            if (body != null)
            {
                body.anchoredPosition = _visualRest;

                // A landing's squash may already own the scale; only a flight cut short puts it back.
                if (grows && !landed) body.localScale = Vector3.one;
            }

            SetOnTop(false);
            _flight = null;
        });
        return true;
    }

    /// <summary>Puts the body back at rest at once, ending any flight.</summary>
    public void StopFlight()
    {
        _flight?.Kill();
        Visual.anchoredPosition = _visualRest;
    }

    /// <summary>Enter Pop: from 0.6 through the pop peak to rest, after <paramref name="delay"/>.</summary>
    public void PopIn(float delay)
    {
        RectTransform body = Visual;
        JuiceTokensSO t = Juice.Tokens;
        body.DOKill();
        body.localScale = Vector3.one * 0.6f;

        float d = t.Durations.Pop;
        float peak = Juice.ReduceMotion ? 1f : t.Scales.PopPeak;
        DOTween.Sequence().SetUpdate(true).SetLink(gameObject).SetTarget(body).SetDelay(delay)
            .Append(body.DOScale(peak, d * 0.6f).SetEase(t.Eases.Out))
            .Append(body.DOScale(1f, d * 0.4f).SetEase(t.Eases.Press))
            .OnKill(() => { if (body != null && body.localScale.x < 0.9f) body.localScale = Vector3.one; });
    }

    private void SetOnTop(bool onTop)
    {
        if (!onTop)
        {
            if (_onTopCanvas != null) Destroy(_onTopCanvas);
            _onTopCanvas = null;
            return;
        }

        if (_onTopCanvas != null) return;
        Canvas parent = GetComponentInParent<Canvas>();
        _onTopCanvas = Visual.gameObject.AddComponent<Canvas>();
        _onTopCanvas.overrideSorting = true;
        _onTopCanvas.sortingOrder = parent != null ? parent.rootCanvas.sortingOrder + 1 : 1;
    }

    private void OnDisable()
    {
        // A page switched away mid-flight must not come back with a card stranded half way.
        if (_flight != null) _flight.Kill();
    }

    private Sprite GetTypeBackground(ExistingTypesOfCard type) => type switch
    {
        ExistingTypesOfCard.Tower => towerBackgroundType,
        ExistingTypesOfCard.Spell => spellBackgroundType,
        ExistingTypesOfCard.Enemy => enemyBackgroundType,
        _ => null
    };

    private void InitializeButtons()
    {
        // Once only: the deck page initialises each widget a single time, but the info panel re-initialises
        // its one widget on every card it shows, and a listener added there would stack up per open.
        if (_buttonWired || cardButton == null || _deckUIController == null) return;

        _buttonWired = true;
        cardButton.onClick.AddListener(() =>
        {
            _deckUIController.RequestActionFrame(_cardData);
        });
    }

}
