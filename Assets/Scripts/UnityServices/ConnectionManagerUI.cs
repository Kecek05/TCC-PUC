using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionManagerUI : MonoBehaviour
{
    [Header("Create")]
    [SerializeField] private Button createRelayButton;
    [SerializeField] private Button createDedicatedServerButton;
    
    [Header("Join")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button joinDedicatedServerButton;

    private BaseHostManager _hostManager;
    private BaseClientManager _clientManager;
    
    private void Start()
    {
        _hostManager = ServiceLocator.Get<BaseHostManager>();
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        
        createRelayButton.onClick.AddListener(CreateRelay);
        joinButton.onClick.AddListener(JoinRelay);
        
        createDedicatedServerButton.onClick.AddListener(CreateDedicatedServer);
        joinDedicatedServerButton.onClick.AddListener(JoinDedicatedServer);
    }

    private async void CreateRelay()
    {
        createRelayButton.interactable = false;
        try
        {
            if (await _hostManager.StartHostAsync()) return;
            
        } catch (System.Exception e)
        {
            GameLog.Exception(e);
        }
        createRelayButton.interactable = true;
    }

    private async void JoinRelay()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        joinButton.interactable = false;

        try
        {
            if (await _clientManager.JoinHost(code)) return;
        }
        catch (Exception e)
        {
            GameLog.Error($"Failed to join relay: {e.Message}");
        }
        
        joinButton.interactable = true;
    }

    private async void CreateDedicatedServer()
    {
        
    }

    private async void JoinDedicatedServer()
    {
        
    }
}