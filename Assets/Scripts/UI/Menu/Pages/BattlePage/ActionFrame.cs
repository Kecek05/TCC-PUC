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

    private DeckUIController _deckUIController;
    private bool _isEquipped;
    private bool _isVisible;
    
    private CardDataSO _cardData;
    
    public void Initialize(DeckUIController  deckUIController)
    {
        _deckUIController = deckUIController;
        InitializeButtons();
        content.SetActive(false);
    }

    public void SelectActionFrame(CardDataSO cardData, bool isEquipped)
    {
        _isEquipped = isEquipped;
        UpdateActionButton();
        ToggleVisibility(cardData);
        _cardData = cardData;
    }

    private void ToggleVisibility(CardDataSO newCardData)
    {
        if (newCardData == _cardData)
        {
            _isVisible = !_isVisible;
        }
        else
        {
            _isVisible = true;
        }
        content.SetActive(_isVisible);
    }
    
    private void InitializeButtons()
    {
        actionButton.onClick.AddListener(() =>
        {
            _isEquipped = !_isEquipped;
            _deckUIController.SetEquippedCard(_cardData.CardType, _isEquipped);
            UpdateActionButton();
        });

        infoButton.onClick.AddListener(() =>
        {
            GameLog.Info($"Card Info: Name: {_cardData.CardName}, Cost: {_cardData.Cost}, Description: {_cardData.Description}");
        });
    }
    
    private void UpdateActionButton()
    {
        switch (_isEquipped)
        {
            case true:
                actionButtonText.text = "Remove";
                break;
            case false:
                actionButtonText.text = "Use";
                break;
        }
    }
}
