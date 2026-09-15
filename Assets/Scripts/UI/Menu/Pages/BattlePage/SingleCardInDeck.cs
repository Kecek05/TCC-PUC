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

    private CardDataSO _cardData;
    private DeckUIController _deckUIController;

    /// <summary>The art's authored rect, so a card without a custom one can undo the previous card's.</summary>
    private Vector2 _defaultImagePosition;
    private Vector2 _defaultImageSize;

    private bool _buttonWired;
    private bool _imageRectCaptured;

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
