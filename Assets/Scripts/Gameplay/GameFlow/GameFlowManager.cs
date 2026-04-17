using System.Collections;
using UnityEngine;

public class GameFlowManager : BaseGameFlowManager
{
    private BaseServerEndGameManager  _endGameManager;
    
    private void Awake()
    {
        ServiceLocator.Register<BaseGameFlowManager>(this);
    }

    private void Start()
    {
        _endGameManager = ServiceLocator.Get<BaseServerEndGameManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        CurrentGameState.Value = GameState.WaitingForPlayers;
        StartCoroutine(HandleGameFlow());
        
        _endGameManager.WinnerTeam.OnValueChanged += EndGameManager_OnWinnerTeamChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            return;
        }
        
        if (_endGameManager != null)
            _endGameManager.WinnerTeam.OnValueChanged -= EndGameManager_OnWinnerTeamChanged;
    }

    private IEnumerator HandleGameFlow()
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
    
    private void SetGameState(GameState newState)
    {
        if (!IsServer) return;
        CurrentGameState.Value = newState;
        Debug.Log($"GameFlowManager: Game state changed to {newState}");
    }
    
    private void EndGameManager_OnWinnerTeamChanged(TeamType previousValue, TeamType newValue)
    {
        if (newValue == TeamType.None) return;
        
        SetGameState(GameState.EndMatch);
    }
}
