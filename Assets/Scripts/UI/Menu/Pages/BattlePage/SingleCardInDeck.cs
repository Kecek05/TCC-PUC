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
    
    private CardDataSO _cardData;
    private DeckUIController _deckUIController;

    public void Initialize(CardDataSO cardData, DeckUIController deckUIController)
    {
        _cardData = cardData;
        _deckUIController = deckUIController;
        
        cardCost.text = cardData.Cost.ToString();
        cardImage.sprite = cardData.CardImage;
        
        InitializeButtons();
    }

    private void InitializeButtons()
    {
        cardButton.onClick.AddListener(() =>
        {
            _deckUIController.RequestActionFrame(_cardData);
        });
    }
}
