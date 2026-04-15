using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class ServerEndGameManager : NetworkBehaviour
{
    public static ServerEndGameManager Instance { get; private set; }
    
    private NetworkVariable<TeamType> _winnerTeam = new(writePerm: NetworkVariableWritePermission.Server);
    
    private BaseGameFlowManager _gameFlowManager;
    
    public NetworkVariable<TeamType> WinnerTeam => _winnerTeam;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Debug.LogError("Multiple instances of ServerEndGameManager detected. This is not allowed.");
            Destroy(this);
        }
    }

    private void Start()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _winnerTeam.Value = TeamType.None;
        
        ServerPlayerHealthManager.Instance.OnPlayerDeath += ServerPlayerHealthManager_OnPlayerDeath;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && ServerPlayerHealthManager.Instance != null)
            ServerPlayerHealthManager.Instance.OnPlayerDeath -= ServerPlayerHealthManager_OnPlayerDeath;
    }

    private void ServerPlayerHealthManager_OnPlayerDeath(TeamType deathTeam)
    {
        Debug.Log($"Player from {deathTeam} team has died. Ending the game.");

        TeamType winningTeam = deathTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
        _winnerTeam.Value = winningTeam;
        
        //TODO:
        // Handle Trophies and rewards
        // Stop the Game and the Spawning. Stop Everything.
        
        _gameFlowManager.SetGameState(GameState.EndMatch);
    }
}
