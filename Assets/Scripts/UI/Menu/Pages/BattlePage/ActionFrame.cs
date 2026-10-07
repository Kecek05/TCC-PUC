using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ActionFrame : MonoBehaviour, IRefusalSource
{
    [Title("References")]
    [SerializeField] private GameObject content;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionButtonText;
    [SerializeField] private Button infoButton;

    [Title("Upgrade")]
    [InfoBox("Optional. Until the Upgrade button exists in the prefab these can stay empty, and the popup " +
             "keeps working with just Use/Remove and Info.")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;

    [SerializeField] private string maxLevelLabel = "MAX";
    [SerializeField] private string lockedLabel = "Locked";

    [Title("Placement")]
    [Tooltip("The panel slides sideways to stay at least this far inside the page's left and right edges.")]
    [SerializeField] private float edgeMargin = 16f;

    private static readonly Vector3[] Corners = new Vector3[4];
    private static readonly List<RaycastResult> Hits = new();
    private float _contentRestX;
    private bool _contentRestCaptured;

    private BaseInfoPanelService _infoPanelService;
    private DeckUIController _deckUIController;
    private bool _isCardEquipped;

    private CardDataSO _cardData;

    /// <summary>Pops the popup in and out (the Popup recipe, small). Added in code: the prefab needs nothing.</summary>
    private PopupAnimator _popup;

    /// <summary>Open as far as the player is concerned: true from the tap that opens it, false from the one
    /// that closes it, even while the close is still animating out.</summary>
    private bool _shown;

    /// <summary>Whether the popup is on screen. The tutorial waits for it before pointing inside it.</summary>
    public bool IsVisible => _shown && content != null && content.activeInHierarchy;

    /// <summary>The card the popup is currently open for, or null when it is closed.</summary>
    public CardDataSO ShownCard => _cardData;

    /// <summary>The popup's buttons as rects, so the tutorial can frame one of them. Exposed rather than
    /// driven: the tutorial points at them, it never presses them for the player.</summary>
    public RectTransform InfoButtonRect => infoButton != null ? (RectTransform)infoButton.transform : null;

    /// <summary>The Use / Remove button — one button whose label follows whether the card is equipped.</summary>
    public RectTransform ActionButtonRect => actionButton != null ? (RectTransform)actionButton.transform : null;

    public RectTransform UpgradeButtonRect => upgradeButton != null ? (RectTransform)upgradeButton.transform : null;

    public void Initialize(DeckUIController  deckUIController)
    {
        _infoPanelService = ServiceLocator.Get<BaseInfoPanelService>();

        _deckUIController = deckUIController;
        InitializeButtons();

        _popup = gameObject.AddComponent<PopupAnimator>();
        _popup.Configure((RectTransform)content.transform, null, null, true);

        // A greyed-out Upgrade (max level, locked) explains itself when tapped instead of doing nothing.
        if (upgradeButton != null && upgradeButton.TryGetComponent(out JuicyButton juicy)) juicy.SetRefusalSource(this);
        content.SetActive(false);
        UpdateActionButton();
    }

    public void ActivateActionFrame(CardDataSO cardData, bool isCardEquipped)
    {
        _isCardEquipped = isCardEquipped;
        UpdateActionButton();
        ToggleVisibility(cardData);
        _cardData = cardData;

        RefreshUpgradeButton();
    }

    private void ToggleVisibility(CardDataSO newCardData)
    {
        bool isVisible = _shown;
        if (newCardData == _cardData)
            isVisible = !isVisible;
        else
            isVisible = true;

        SetShown(isVisible);
    }

    /// <summary>
    /// Pops the popup open, or closes it quickly and quietly. A popup re-opened on another card replays its
    /// pop, so the player sees it move to the card they just tapped.
    /// </summary>
    private void SetShown(bool shown)
    {
        _shown = shown;

        if (shown)
        {
            content.SetActive(true);
            KeepInsidePage();
            _popup.Open();
            return;
        }

        // Already hidden, or under a page that is off screen: nothing to watch close.
        if (!content.activeInHierarchy)
        {
            _popup.Complete();
            content.SetActive(false);
            return;
        }

        _popup.Close(() => content.SetActive(false));
    }

    /// <summary>
    /// The popup is pinned above the card it opened on; on an edge column a wide panel would hang off the screen, so
    /// it slides sideways until it fits inside the page. Measured at rest size, before the pop shrinks it.
    /// </summary>
    private void KeepInsidePage()
    {
        RectTransform panel = (RectTransform)content.transform;
        if (!_contentRestCaptured)
        {
            _contentRestCaptured = true;
            _contentRestX = panel.anchoredPosition.x;
        }

        panel.anchoredPosition = new Vector2(_contentRestX, panel.anchoredPosition.y);
        panel.localScale = Vector3.one;

        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        RectTransform page = scroll != null ? scroll.viewport : null;
        if (page == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        panel.GetWorldCorners(Corners);
        float left = page.InverseTransformPoint(Corners[0]).x;
        float right = page.InverseTransformPoint(Corners[2]).x;
        Rect area = page.rect;

        float shift = 0f;
        if (right - left > area.width - 2f * edgeMargin) shift = area.center.x - (left + right) * 0.5f;
        else if (left < area.xMin + edgeMargin) shift = area.xMin + edgeMargin - left;
        else if (right > area.xMax - edgeMargin) shift = area.xMax - edgeMargin - right;
        if (Mathf.Abs(shift) < 0.01f) return;

        // Measured in the page's units; the panel moves in its parent's.
        Vector3 local = panel.parent.InverseTransformVector(page.TransformVector(new Vector3(shift, 0f, 0f)));
        panel.anchoredPosition += new Vector2(local.x, 0f);
    }

    private void InitializeButtons()
    {
        actionButton.onClick.AddListener(() =>
        {
            // A second tap while the popup is closing: the card it was open for has already been let go.
            if (_cardData == null) return;

            bool wantEquipped = !_isCardEquipped;

            // Only adopt the new state when the controller accepted it: a refused equip (deck full) must
            // not leave this popup reading "Remove" for a card that never entered the deck.
            if (_deckUIController.SetEquippedCard(_cardData.CardType, wantEquipped))
                _isCardEquipped = wantEquipped;

            UpdateActionButton();
            HideActionFrame();
        });

        infoButton.onClick.AddListener(() =>
        {
            if (_cardData == null) return;

            // Only the identity travels: the panel resolves the level, the copies and the stat table from
            // the save itself, because it can upgrade the card it is showing.
            _infoPanelService.ShowInfoPanel(InfoPanelData.ForCard(_cardData));
            HideActionFrame();
        });

        if (upgradeButton == null) return;

        upgradeButton.onClick.AddListener(() =>
        {
            if (_cardData == null) return;

            // The controller owns the rules and reports the failure through the refusal toast, so this popup
            // only has to decide whether to close.
            if (_deckUIController.TryUpgradeCard(_cardData.CardType)) HideActionFrame();
            else RefreshUpgradeButton();
        });
    }

    private void UpdateActionButton()
    {
        switch (_isCardEquipped)
        {
            case true:
                actionButtonText.text = "Remove";
                break;
            case false:
                actionButtonText.text = "Use";
                break;
        }
    }

    /// <summary>Shows what the next level costs, or why there is nothing to buy.</summary>
    private void RefreshUpgradeButton()
    {
        if (upgradeButton == null) return;

        if (_cardData == null)
        {
            upgradeButton.interactable = false;
            return;
        }

        CardUpgradeValidation upgrade = _deckUIController.GetUpgradeState(_cardData.CardType);

        // Missing copies or gold stays clickable on purpose: tapping is how the player finds out which.
        upgradeButton.interactable = upgrade.Reason != CardUpgradeInvalidReason.MaxLevel &&
                                     upgrade.Reason != CardUpgradeInvalidReason.NotOwned;

        if (upgradeButtonText == null) return;

        upgradeButtonText.text = upgrade.Reason switch
        {
            CardUpgradeInvalidReason.MaxLevel => maxLevelLabel,
            CardUpgradeInvalidReason.NotOwned => lockedLabel,
            _ => upgrade.GoldCost.ToString()
        };
    }

    /// <summary>Why the Upgrade button is off, for its disabled tap.</summary>
    public RefusalReason CurrentRefusal(out string detail)
    {
        detail = null;
        if (_cardData == null || _deckUIController == null) return RefusalReason.None;

        CardUpgradeValidation upgrade = _deckUIController.GetUpgradeState(_cardData.CardType);
        detail = upgrade.RefusalDetail;
        return upgrade.Refusal;
    }

    public void HideActionFrame()
    {
        SetShown(false);
        _cardData = null;
    }

    /// <summary>
    /// Popup: a press anywhere outside the popup closes it, quietly, with a tap. A press on the popup is its own,
    /// and a press on a card is left to the card, which moves the popup there (or closes it, if it is that card).
    /// The tutorial's input shield is not "outside": it is holding the screen around a step pointing into here.
    /// </summary>
    private void Update()
    {
        if (!_shown || content == null || !content.activeInHierarchy) return;

        Pointer pointer = Pointer.current;
        EventSystem events = EventSystem.current;
        if (pointer == null || events == null || !pointer.press.wasPressedThisFrame) return;

        Hits.Clear();
        events.RaycastAll(new PointerEventData(events) { position = pointer.position.ReadValue() }, Hits);
        if (Hits.Count > 0)
        {
            Transform top = Hits[0].gameObject.transform;
            if (top.IsChildOf(content.transform) || top.GetComponentInParent<SingleCardInDeck>() != null ||
                top.GetComponent<TutorialInputShield>() != null)
                return;
        }

        Juice.Sfx(SfxId.Tap, 0, 0.6f);
        HideActionFrame();
    }
}
