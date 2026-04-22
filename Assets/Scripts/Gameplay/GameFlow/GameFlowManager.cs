using System.Collections;
using UnityEngine;

public class GameFlowManager : BaseGameFlowManager
{
    private BaseServerEndGameManager  _endGameManager;
    [SerializeField] private DebugSettingsSO  _debugSettings;
    
    private void Awake()
    {
        ServiceLocator.Register<BaseGameFlowManager>(this);
    }

    public override void OnNetworkSpawn()
    {
        _endGameManager = ServiceLocator.Get<BaseServerEndGameManager>();
        
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        CurrentGameState.Value = GameState.WaitingForPlayers;
        StartCoroutine(HandleGameFlow());
        
        _endGameManager.OnGameEnded += EndGameManager_OnGameEnded;
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseGameFlowManager>();
        base.OnDestroy();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            return;
        }
        
        if (_endGameManager != null)
            _endGameManager.OnGameEnded -= EndGameManager_OnGameEnded;
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
    
    private void EndGameManager_OnGameEnded(EndGameSnapshot endGameSnapshot)
    {
        SetGameState(GameState.EndMatch);
    }
}
