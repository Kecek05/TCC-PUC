using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameFlowManager : NetworkBehaviour
{
    public static GameFlowManager Instance { get; private set; }
    
    public NetworkVariable<GameState> CurrentGameState = new NetworkVariable<GameState>(writePerm: NetworkVariableWritePermission.Server);
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Debug.LogError("Multiple instances of GameFlowManager detected. This is not allowed.");
            Destroy(this);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentGameState.Value = GameState.WaitingForPlayers;
            StartCoroutine(HandleGameFlow());
        }
    }

    private IEnumerator HandleGameFlow()
    {
        yield return new WaitUntil(() => 
            TeamManager.Instance != null && 
            TeamManager.Instance.BothTeamsAssigned());
        
        SetGameState(GameState.LoadingMatch);
        
        yield return new WaitUntil(() => 
            MapTranslator.Instance != null && 
            MapTranslator.Instance.BothPlayersInitialized);
        
        SetGameState(GameState.MatchReady);
        
        yield return new WaitForSeconds(2f); // Short delay before starting the match
        
        SetGameState(GameState.InMatch);
    }
    
    public void SetGameState(GameState newState)
    {
        if (!IsServer) return;
        CurrentGameState.Value = newState;
        Debug.Log($"GameFlowManager: Game state changed to {newState}");
    }
}
