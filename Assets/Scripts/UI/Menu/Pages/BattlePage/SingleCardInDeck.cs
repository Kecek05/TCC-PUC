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
    
    private CardDataSO _cardData;
    private DeckUIController _deckUIController;

    public CardDataSO CardData => _cardData;
    
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

        SetPlaceholderProgression();
        
        if (cardData.UseCustomPositionCardInMenu)
            cardImage.rectTransform.anchoredPosition = cardData.CustomPositionCardInMenu;
        
        if (cardData.UseCustomSizeCardInMenu)
            cardImage.rectTransform.sizeDelta = cardData.CustomSizeCardInMenu;
        
        InitializeButtons();
    }

    private Sprite GetTypeBackground(ExistingTypesOfCard type) => type switch
    {
        ExistingTypesOfCard.Tower => towerBackgroundType,
        ExistingTypesOfCard.Spell => spellBackgroundType,
        ExistingTypesOfCard.Enemy => enemyBackgroundType,
        _ => null
    };

    private void SetPlaceholderProgression()
    {
        // TODO: placeholder only — replace with real player-progression data.
        int level = Random.Range(1, 10);
        int max = level * 7;
        int owned = Mathf.RoundToInt(max * Random.Range(0.3f, 0.8f));

        levelText.text = $"Level {level}";
        quantityText.text = $"{owned}/{max}";
        cardLevelFill.fillAmount = (float)owned / max;
    }

    private void InitializeButtons()
    {
        cardButton.onClick.AddListener(() =>
        {
            _deckUIController.RequestActionFrame(_cardData);
        });
    }
    
}
