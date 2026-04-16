using System.Collections;
using UnityEngine;

public class GameFlowManager : BaseGameFlowManager
{
    private void Awake()
    {
        ServiceLocator.Register<BaseGameFlowManager>(this);
    }

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
        BaseTeamManager teamManager = ServiceLocator.Get<BaseTeamManager>();

        yield return new WaitUntil(() =>
            teamManager != null &&
            teamManager.BothTeamsAssigned());

        SetGameState(GameState.LoadingMatch);

        BaseMapTranslator mapTranslator = ServiceLocator.Get<BaseMapTranslator>();

        yield return new WaitUntil(() =>
            mapTranslator != null &&
            mapTranslator.BothPlayersInitialized);

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
