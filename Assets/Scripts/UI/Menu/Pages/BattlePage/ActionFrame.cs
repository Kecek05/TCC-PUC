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
            _isCardEquipped = !_isCardEquipped;
            _deckUIController.SetEquippedCard(_cardData.CardType, _isCardEquipped);
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

    public void HideActionFrame()
    {
        content.SetActive(false);
        _cardData = null;
    }
}
