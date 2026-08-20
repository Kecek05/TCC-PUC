using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class ScreenWarning : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject content;
    [SerializeField] private TextMeshProUGUI warningText;
    [SerializeField] private MMF_Player warningFeedback;
    
    private void Awake()
    {
        content.gameObject.SetActive(false);
        ServiceLocator.Register(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<ScreenWarning>();
    }

    public void ShowWarning(string message)
    {
        content.gameObject.SetActive(false);
        warningText.SetText(message);
        content.gameObject.SetActive(true);
        warningFeedback.StopFeedbacks();
        warningFeedback.PlayFeedbacks();
    }
}

public static class WarningMessages
{
    public static string CannotEquipCard = "Cannot equip card. Deck is full.";
    public static string DeckNotFull = "Cannot play. Deck is not full.";
    public static string CardLocked = "You don't own this card yet.";
    public static string UpgradeNotEnoughCards = "Not enough cards to upgrade.";
    public static string UpgradeNotEnoughGold = "Not enough gold to upgrade.";
    public static string UpgradeMaxLevel = "This card is already at max level.";
}