using UnityEngine;

public class ServerEndGameManager : BaseServerEndGameManager
{
    private BaseGameFlowManager _gameFlowManager;
    private BaseServerPlayerHealthManager _playerHealthManager;
    private BaseServerWaveManager _waveManager;

    private void Awake()
    {
        ServiceLocator.Register<BaseServerEndGameManager>(this);
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseServerEndGameManager>();
        base.OnDestroy();
    }

    private void Start()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        _playerHealthManager = ServiceLocator.Get<BaseServerPlayerHealthManager>();
        _waveManager  = ServiceLocator.Get<BaseServerWaveManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        WinnerTeam.Value = TeamType.None;

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

    private void TeamHealthManagerOnTeamDeath(TeamType deathTeam)
    {
        Debug.Log($"Player from {deathTeam} team has died. Ending the game.");

        TeamType winnerTeam = deathTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
        WinnerTeam.Value = winnerTeam;

        //TODO:
        // Handle Trophies and rewards
        // Stop the Game and the Spawning. Stop Everything.
    }
    
    private void WaveManager_OnTeamDefeatedLastWave(TeamType winnerTeam)
    {
        WinnerTeam.Value = winnerTeam;
    }
}
