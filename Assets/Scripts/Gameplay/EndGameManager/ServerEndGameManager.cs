using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ServerEndGameManager : BaseServerEndGameManager
{
    [Title("Rewards")]
    [SerializeField, Required] private RewardSettingsSO rewardSettings;

    private bool _winnerAlreadySetted = false;
    private EndGameSnapshot _endGameSnapshot;
    private IRewardRoller _rewardRoller;
    
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
                Wave = _waveManager.RedCurrentWave.Value,
                WaveProgress = _waveManager.RedCurrentWaveProgressNormalized.Value,
            },
            BluePlayer = new PlayerEndGameData()
            {
                Health = _playerHealthManager.BlueHealth.Value,
                Wave = _waveManager.BlueCurrentWave.Value,
                WaveProgress = _waveManager.BlueCurrentWaveProgressNormalized.Value,
            }
        };
        
        TriggerOnGameEnded(_endGameSnapshot);
        TriggerOnGameEndedToClientRpc(_endGameSnapshot);

        GrantRewards(winnerTeam);

        // OnGameEnded drives the FSM to GameState.EndMatch (see InMatchState),
        // which freezes the simulation: ServerWaveManager stops spawning,
        // ServerEnemyMovement stops moving, BaseServerTowerCombat stops firing.
        // The network connection is torn down later, per-player, from
        // ClientEndGameCanvas -> ClientManager.LeaveMatchAsync().
        //
        // TODO: Handle trophies before/while showing the end screen.
    }

    /// <summary>
    /// Rolls one reward per seated player and sends each to that player alone. The save lives on the
    /// client, so the server decides and the client banks it - a targeted Rpc rather than the broadcast
    /// snapshot, because a reward is nobody else's business.
    /// </summary>
    private void GrantRewards(TeamType winnerTeam)
    {
        if (rewardSettings == null)
        {
            GameLog.Error("[ServerEndGameManager] No RewardSettingsSO assigned; no rewards were granted.");
            return;
        }

        _rewardRoller ??= new WeightedRewardRoller(rewardSettings);

        BasePlayersDataManager playersData = ServiceLocator.Get<BasePlayersDataManager>();
        BaseTeamManager teamManager = ServiceLocator.Get<BaseTeamManager>();

        foreach (KeyValuePair<string, PlayerData> entry in playersData.GetAuthIdToPlayerData())
        {
            // The bot is a virtual player with no network client (ClientId sentinel), so there is nobody
            // to pay. Reading ClientId directly also avoids GetClientIdByTeamType's error log for it.
            ulong clientId = entry.Value.ClientId;
            if (clientId == ulong.MaxValue) continue;

            TeamType team = teamManager.GetTeam(entry.Key);
            if (team == TeamType.None) continue;

            Reward reward = _rewardRoller.Roll(team == winnerTeam);
            GameLog.Info($"[ServerEndGameManager] {team} reward: {reward}");

            SendRewardRpc(reward, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SendRewardRpc(Reward reward, RpcParams rpcParams)
    {
        TriggerOnRewardGranted(reward);
    }

    // ---- Debug ---------------------------------------------------------------------------------
    // Entry points for the Quantum Console reward commands (see RewardDebugCommands). They exist so a
    // payout can be exercised without playing a match to its end; nothing in the game calls them.

    /// <summary>Rolls and delivers a payout to every seated player exactly as a finished match would,
    /// without ending the match.</summary>
    public void DebugGrantRewards(TeamType winnerTeam) => GrantRewards(winnerTeam);

    /// <summary>Delivers one exact, unrolled reward to a single team — the way to test a specific card
    /// unlock without fighting the roller. False when that team has nobody to pay.</summary>
    public bool DebugSendRewardTo(TeamType team, Reward reward)
    {
        if (!TryGetClientIdForTeam(team, out ulong clientId)) return false;

        SendRewardRpc(reward, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        return true;
    }

    /// <summary>Walks the seated players rather than asking <c>GetClientIdByTeamType</c>, for the same
    /// reason <see cref="GrantRewards"/> does: the bot has no clientId and that lookup logs an error for it.</summary>
    private bool TryGetClientIdForTeam(TeamType team, out ulong clientId)
    {
        clientId = ulong.MaxValue;

        BasePlayersDataManager playersData = ServiceLocator.Get<BasePlayersDataManager>();
        BaseTeamManager teamManager = ServiceLocator.Get<BaseTeamManager>();

        foreach (KeyValuePair<string, PlayerData> entry in playersData.GetAuthIdToPlayerData())
        {
            if (teamManager.GetTeam(entry.Key) != team) continue;

            clientId = entry.Value.ClientId;
            return clientId != ulong.MaxValue;
        }

        return false;
    }

    [Rpc(SendTo.NotServer)]
    private void TriggerOnGameEndedToClientRpc(EndGameSnapshot snapshot)
    {
        TriggerOnGameEnded(snapshot);
    }
}
