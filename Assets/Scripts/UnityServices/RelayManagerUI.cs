using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RelayManagerUI : MonoBehaviour
{
    [Header("Host")]
    [SerializeField] private Button createRelayButton;

    [Header("Join")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinButton;

    private BaseHostManager _hostManager;
    private BaseClientManager _clientManager;
    
    private void Start()
    {
        _hostManager = ServiceLocator.Get<BaseHostManager>();
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        
        createRelayButton.onClick.AddListener(CreateRelay);
        joinButton.onClick.AddListener(JoinRelay);
    }

    private async void CreateRelay()
    {
        createRelayButton.interactable = false;

        try
        {
            await _hostManager.StartHostAsync();
        } catch (System.Exception e)
        {
            GameLog.Exception(e);
            createRelayButton.interactable = true;
            return;
        }
    }

    private async void JoinRelay()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        joinButton.interactable = false;

        try
        {
            await _clientManager.JoinHost(code);
        }
        catch (Exception e)
        {
            GameLog.Error($"Failed to join relay: {e.Message}");
            joinButton.interactable = true;
            return;
        }
    }
}