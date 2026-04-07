using System;
using Unity.Netcode;
using UnityEngine;

public class WaitingPlayersCanvas : NetworkBehaviour
{
    [SerializeField] private GameObject waitingCanvas;

    private void Awake()
    {
        waitingCanvas.SetActive(true);
    }

    public override void OnNetworkSpawn()
    {
        GameFlowManager.Instance.CurrentGameState.OnValueChanged += OnGameStateChanged;
        UpdateCanvasVisibility(GameFlowManager.Instance.CurrentGameState.Value);
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
