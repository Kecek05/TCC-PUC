using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

public class RelayManager : MonoBehaviour
{
    [Header("Host")]
    [SerializeField] private Button createRelayButton;

    [Header("Join")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private TextMeshProUGUI joinCodeText;

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
        
        joinCodeText.text = _hostManager.HostConnectionData.JoinCode;
        gameObject.SetActive(false);
    }

    private async void JoinRelay()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        joinButton.interactable = false;

        try
        {
            await _clientManager.JoinHost(code);
            
            gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            GameLog.Error($"Failed to join relay: {e.Message}");
            joinButton.interactable = true;
            return;
        }
    }
}