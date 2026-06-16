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

    private void UpdateCardCostGfx()
    {
        float remaining =  Mathf.Clamp01((_cardCost - _clientManaManager.PredictedMana) / _cardCost);
        GameLog.Info($"Cost: {_cardCost} - Mana: {_clientManaManager.PredictedMana} Remaining: {remaining}");
    }
}
