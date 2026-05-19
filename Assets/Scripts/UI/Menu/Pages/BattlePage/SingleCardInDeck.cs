using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingleCardInDeck : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private TextMeshProUGUI cardCost;
    [SerializeField] private Image cardImage;
    [SerializeField] private Button cardButton;
    [SerializeField] private GameObject actionsFrame;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionButtonText;
    
    private bool _isShowingActions = false;
    private bool _isEquipped = false;
    
    private CardDataSO _cardData;
    private DeckUIController _deckUIController;

    private void Awake()
    {
        actionsFrame.SetActive(false);
    }

    public void Initialize(CardDataSO cardData, DeckUIController deckUIController, bool isEquipped)
    {
        _cardData = cardData;
        _deckUIController = deckUIController;
        _isEquipped = isEquipped;
        
        cardCost.text = cardData.Cost.ToString();
        cardImage.sprite = cardData.CardImage;
        
        UpdateActionButton();
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
