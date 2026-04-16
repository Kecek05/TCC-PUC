using Unity.Netcode;
using UnityEngine;

public class WaitingPlayersCanvas : NetworkBehaviour
{
    [SerializeField] private GameObject waitingCanvas;

    private BaseGameFlowManager _gameFlowManager;

    private void Awake()
    {
        waitingCanvas.SetActive(true);
    }

    public override void OnNetworkSpawn()
    {
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
