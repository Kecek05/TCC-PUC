using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameFlowManager : BaseGameFlowManager
{
    
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentGameState.Value = GameState.WaitingForPlayers;
            StartCoroutine(HandleGameFlow());
        }
    }

    protected override IEnumerator HandleGameFlow()
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
    
    public override void SetGameState(GameState newState)
    {
        if (!IsServer) return;
        CurrentGameState.Value = newState;
        Debug.Log($"GameFlowManager: Game state changed to {newState}");
    }
}
