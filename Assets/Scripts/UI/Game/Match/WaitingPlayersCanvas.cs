using System;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class WaitingPlayersCanvas : NetworkBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject waitingCanvas;
    [SerializeField] private TextMeshProUGUI  joinCodeText;

    private BaseGameFlowManager _gameFlowManager;

    private void Awake()
    {
        waitingCanvas.SetActive(true);
    }

    public override void OnNetworkSpawn()
    {
        if (IsHost)
        {
            var hostManager = ServiceLocator.Get<BaseHostManager>();
            joinCodeText.text = hostManager.CurrentHostConnectionData.JoinCode;
        }
        
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        _gameFlowManager.CurrentGameState.OnValueChanged += OnGameStateChanged;
        UpdateCanvasVisibility(_gameFlowManager.CurrentGameState.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (_gameFlowManager != null)
            _gameFlowManager.CurrentGameState.OnValueChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        UpdateCanvasVisibility(newState);
    }

    private void UpdateCanvasVisibility(GameState newState)
    {
        if (newState == GameState.InMatch)
        {
            waitingCanvas.SetActive(false);
        }
    }
}
