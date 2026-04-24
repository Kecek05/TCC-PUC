using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerEndGameManager : BaseServerEndGameManager
{
    private bool _winnerAlreadySetted = false;
    private EndGameSnapshot _endGameSnapshot;
    
    private BaseServerPlayerHealthManager _playerHealthManager;
    private BaseServerWaveManager _waveManager;

    private void Awake()
    {
        ServiceLocator.Register<BaseServerEndGameManager>(this);
    }

    public override void OnNetworkSpawn()
    {
        _playerHealthManager = ServiceLocator.Get<BaseServerPlayerHealthManager>();
        _waveManager  = ServiceLocator.Get<BaseServerWaveManager>();
        
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _playerHealthManager.OnTeamDeath += TeamHealthManagerOnTeamDeath;
        _waveManager.OnTeamDefeatLastWave += WaveManager_OnTeamDefeatedLastWave;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            return;
        }
        
        if (_playerHealthManager != null)
            _playerHealthManager.OnTeamDeath -= TeamHealthManagerOnTeamDeath;
        
        if (_waveManager != null) 
            _waveManager.OnTeamDefeatLastWave -= WaveManager_OnTeamDefeatedLastWave;
    }
    
    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseServerEndGameManager>();
    }

    private void TeamHealthManagerOnTeamDeath(TeamType deathTeam)
    {
        GameLog.Info($"Player from {deathTeam} team has died. Ending the game.");

        TeamType winnerTeam = deathTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
        SetWinner(winnerTeam);
    }
    
    private void WaveManager_OnTeamDefeatedLastWave(TeamType winnerTeam)
    {
        SetWinner(winnerTeam);
    }

    private void SetWinner(TeamType winnerTeam)
    {
        if (winnerTeam == TeamType.None)
        {
            GameLog.Error("Setting winner to NONE. This shouldn't happen.");
            return;
        }
        
        if (_winnerAlreadySetted)
        {
            GameLog.Error($"Winner has already been set. Calling SetWinner twice, this shouldn't happen");
            return;
        }
        _winnerAlreadySetted = true;
        
        _endGameSnapshot = new EndGameSnapshot()
        {
            WinnerTeam = winnerTeam,
            RedPlayer = new PlayerEndGameData()
            {
                Health = _playerHealthManager.RedHealth.Value,
                Wave = _waveManager.RedCurrentWave.Value
            },
            BluePlayer = new PlayerEndGameData()
            {
                Health = _playerHealthManager.BlueHealth.Value,
                Wave = _waveManager.BlueCurrentWave.Value
            }
        };
        
        TriggerOnGameEnded(_endGameSnapshot);
        TriggerOnGameEndedToClientRpc(_endGameSnapshot);
        
        //TODO:
        // Handle Trophies and rewards
        // Stop the Game and the Spawning. Stop Everything.
    }

    [Rpc(SendTo.NotServer)]
    private void TriggerOnGameEndedToClientRpc(EndGameSnapshot snapshot)
    {
        TriggerOnGameEnded(snapshot);
    }
}
