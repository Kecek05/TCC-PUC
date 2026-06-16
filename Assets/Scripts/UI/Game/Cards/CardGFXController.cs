using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardGFXController : MonoBehaviour
{
    [Title("References")] 
    [SerializeField] private TextMeshProUGUI costLabel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private Image cardIconImage;
    [SerializeField] private Image cardIconBackground;
    [SerializeField] private Image cardCostGfx;
    [SerializeField] private Color costDefaultColor;
    [SerializeField] private Color costNoManaColor;
    
    private BaseClientManaManager _clientManaManager;
    private int _cardCost;
    
    public void Initialize(CardDataSO cardDataSo, BaseClientManaManager clientManaManager)
    {
        _clientManaManager  = clientManaManager;
        _cardCost = cardDataSo.Cost;
        
        costLabel.text = _cardCost.ToString();
        titleLabel.text = cardDataSo.CardName;
        cardIconImage.sprite = cardDataSo.CardImage;
        cardIconBackground.sprite = cardDataSo.CardImage;

        UpdateCardCostGfx();
    }

    private void Update()
    {
        if (_clientManaManager != null)
            UpdateCardCostGfx();
    }
    
    private void UpdateCardCostGfx()
    {
        float remaining =  Mathf.Clamp01((_cardCost - _clientManaManager.PredictedMana) / _cardCost);
        cardIconBackground.fillAmount = remaining;

        cardCostGfx.color = costNoManaColor;
        cardCostGfx.color = Color.Lerp(cardCostGfx.color, costDefaultColor, (1f - remaining));

        /*if (remaining <= 0)
            cardCostGfx.color = Color.Lerp(cardCostGfx.color, costDefaultColor, (1f - remaining));
        else
            cardCostGfx.color = Color.Lerp(cardCostGfx.color, costNoManaColor, (1f - remaining));*/
    }
}
