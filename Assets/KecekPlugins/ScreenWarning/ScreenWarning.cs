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
        warningFeedback.PlayFeedbacks();
    }
}
