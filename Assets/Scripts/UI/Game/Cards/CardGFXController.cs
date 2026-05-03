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

    public void Initialize(CardDataSO cardDataSo)
    {
        costLabel.text = cardDataSo.Cost.ToString();
        titleLabel.text = cardDataSo.CardName;
        cardIconImage.sprite = cardDataSo.CardImage;
    }
}
