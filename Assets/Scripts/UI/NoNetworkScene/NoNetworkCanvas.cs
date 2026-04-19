using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class NoNetworkCanvas : MonoBehaviour
{
    [Title("References")] 
    [SerializeField] private Button retryButton;

    private void Awake()
    {
        retryButton.onClick.AddListener(OnRetryButtonClicked);
    }

    private void OnRetryButtonClicked()
    {
        // Application.Restart();
    }
}
