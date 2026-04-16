using UnityEngine;

public class ServerEndGameManager : BaseServerEndGameManager
{
    private BaseGameFlowManager _gameFlowManager;
    private BaseServerPlayerHealthManager _playerHealthManager;

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
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        WinnerTeam.Value = TeamType.None;

        if (_playerHealthManager == null)
            _playerHealthManager = ServiceLocator.Get<BaseServerPlayerHealthManager>();

        _playerHealthManager.OnPlayerDeath += ServerPlayerHealthManager_OnPlayerDeath;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && _playerHealthManager != null)
            _playerHealthManager.OnPlayerDeath -= ServerPlayerHealthManager_OnPlayerDeath;
    }

    private void ServerPlayerHealthManager_OnPlayerDeath(TeamType deathTeam)
    {
        Debug.Log($"Player from {deathTeam} team has died. Ending the game.");

        TeamType winningTeam = deathTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
        WinnerTeam.Value = winningTeam;

        //TODO:
        // Handle Trophies and rewards
        // Stop the Game and the Spawning. Stop Everything.

        _gameFlowManager.SetGameState(GameState.EndMatch);
    }
}
