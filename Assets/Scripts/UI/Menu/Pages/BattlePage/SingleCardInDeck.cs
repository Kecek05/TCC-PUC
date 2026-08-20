using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingleCardInDeck : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private TextMeshProUGUI cardCost;
    [SerializeField] private Image cardImage;
    [SerializeField] private Image cardTypeBackground;
    [SerializeField] private Image cardLevelBackground;
    [SerializeField] private Image cardLevelFill;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button cardButton;
    [SerializeField] private CardsRarityDataSO cardsRarityData;

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

    public CardDataSO CardData => _cardData;

    /// <summary>Whether the player owns this card. Locked cards cannot be equipped.</summary>
    public bool IsOwned { get; private set; } = true;

    public void Initialize(CardDataSO cardData, DeckUIController deckUIController)
    {
        _cardData = cardData;
        _deckUIController = deckUIController;

        cardCost.text = cardData.Cost.ToString();
        cardImage.sprite = cardData.CardImage;
        cardTypeBackground.sprite = GetTypeBackground(cardData.ExistingType);

        var rarity = cardsRarityData.Get(cardData.Rarity);
        cardLevelBackground.color = rarity.mainColor;
        levelText.color = rarity.textColor;

        if (cardData.UseCustomPositionCardInMenu)
            cardImage.rectTransform.anchoredPosition = cardData.CustomPositionCardInMenu;

        if (cardData.UseCustomSizeCardInMenu)
            cardImage.rectTransform.sizeDelta = cardData.CustomSizeCardInMenu;

        InitializeButtons();
    }

    /// <summary>
    /// Shows this card's real progression. Called by <see cref="DeckUIController"/>, which owns the save
    /// lookup — the widget stays a pure view.
    /// </summary>
    /// <param name="copiesNeeded">0 means the card is at max level and no longer collects copies.</param>
    public void SetProgression(int level, int copies, int copiesNeeded, bool owned)
    {
        IsOwned = owned;

        var rarity = cardsRarityData.Get(_cardData.Rarity);
        cardImage.color = owned ? _cardData.CardColor : _cardData.CardColor * lockedTint;
        cardLevelBackground.color = owned ? rarity.mainColor : rarity.mainColor * lockedTint;

        if (!owned)
        {
            levelText.text = lockedLabel;
            quantityText.text = "-";
            cardLevelFill.fillAmount = 0f;
            return;
        }

        levelText.text = $"Level {level}";

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

    private Sprite GetTypeBackground(ExistingTypesOfCard type) => type switch
    {
        ExistingTypesOfCard.Tower => towerBackgroundType,
        ExistingTypesOfCard.Spell => spellBackgroundType,
        ExistingTypesOfCard.Enemy => enemyBackgroundType,
        _ => null
    };

    private void InitializeButtons()
    {
        cardButton.onClick.AddListener(() =>
        {
            _deckUIController.RequestActionFrame(_cardData);
        });
    }

}
