using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionFrame : MonoBehaviour
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

    private BaseInfoPanelService _infoPanelService;
    private DeckUIController _deckUIController;
    private bool _isCardEquipped;

    private CardDataSO _cardData;

    public void Initialize(DeckUIController  deckUIController)
    {
        _infoPanelService = ServiceLocator.Get<BaseInfoPanelService>();

        _deckUIController = deckUIController;
        InitializeButtons();
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
        bool isVisible = content.activeInHierarchy;
        if (newCardData == _cardData)
            isVisible = !isVisible;
        else
            isVisible = true;

        content.SetActive(isVisible);
    }

    private void InitializeButtons()
    {
        actionButton.onClick.AddListener(() =>
        {
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
            InfoPanelData infoPanelData = new InfoPanelData
            {
                Title = _cardData.CardName,
                Description = _cardData.Description,
                Icon = _cardData.CardImage
            };
            _infoPanelService.ShowInfoPanel(infoPanelData);
            HideActionFrame();
        });

        if (upgradeButton == null) return;

        upgradeButton.onClick.AddListener(() =>
        {
            if (_cardData == null) return;

            // The controller owns the rules and reports the failure through ScreenWarning, so this popup
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

    public void HideActionFrame()
    {
        content.SetActive(false);
        _cardData = null;
    }
}
