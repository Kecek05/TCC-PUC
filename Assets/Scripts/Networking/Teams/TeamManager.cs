using System;
using Unity.Collections;
using Unity.Netcode;

public class TeamManager : BaseTeamManager
{
    private NetworkVariable<PlayerTeamPair> _bluePlayer = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<PlayerTeamPair> _redPlayer = new(writePerm: NetworkVariableWritePermission.Server);

    private BasePlayersDataManager _playersDataManager;
    private IOnPlayerLoaded _connectionEvents;
    
    private void Awake()
    {
        ServiceLocator.Register<BaseTeamManager>(this);
    }

    public override void OnNetworkSpawn()
    {
        _bluePlayer.OnValueChanged += OnTeamAssigned;
        _redPlayer.OnValueChanged += OnTeamAssigned;

        if (IsServer)
        {
            _connectionEvents = ServiceLocator.Get<IOnPlayerLoaded>();
            _playersDataManager = ServiceLocator.Get<BasePlayersDataManager>();
            _connectionEvents.OnPlayerLoaded += AssignTeam;
        }
    }

    public override void OnNetworkDespawn()
    {
        _bluePlayer.OnValueChanged -= OnTeamAssigned;
        _redPlayer.OnValueChanged -= OnTeamAssigned;

        if (IsServer && _connectionEvents != null)
        {
            _connectionEvents.OnPlayerLoaded -= AssignTeam;
        }
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseTeamManager>();
    }

    private bool HasTeam(FixedString64Bytes authId)
    {
        return (_redPlayer.Value.Team != TeamType.None && _redPlayer.Value.AuthId == authId) ||
               (_bluePlayer.Value.Team != TeamType.None && _bluePlayer.Value.AuthId == authId);
    }

    // Server-side seam for seating a bot: reuses the exact first-free-slot assignment a real player
    // gets. The host (Red) is already assigned, so this deterministically lands the bot on Blue.
    public override void AssignTeamForAuthId(string authId) => AssignTeam(authId);

    private void AssignTeam(string authId)
    {
        if (string.IsNullOrEmpty(authId))
        {
            GameLog.Error("TeamManager: AssignTeam received null/empty authId");
            return;
        }

        FixedString64Bytes authIdFs = authId;

        if (HasTeam(authIdFs)) return;

        if (_redPlayer.Value.Team == TeamType.None)
        {
            _redPlayer.Value = new PlayerTeamPair { AuthId = authIdFs, Team = TeamType.Red, PlayerName = GetPlayerNameByAuthId(authId) };
            _playersDataManager.RegisterTeam(TeamType.Red, authId);
            GameLog.Info($"TeamManager: AuthId {authId} assigned to Team RED");
        }
        else if (_bluePlayer.Value.Team == TeamType.None)
        {
            _bluePlayer.Value = new PlayerTeamPair { AuthId = authIdFs, Team = TeamType.Blue, PlayerName = GetPlayerNameByAuthId(authId) };
            _playersDataManager.RegisterTeam(TeamType.Blue, authId);
            GameLog.Info($"TeamManager: AuthId {authId} assigned to Team BLUE");
        }
        else
        {
            GameLog.Warn($"TeamManager: AuthId {authId} loaded but both teams are full!");
        }
    }

    // Server-side: resolves the player's display name from the connection payload
    // so it can be synced inside PlayerTeamPair (clients have no PlayersDataManager).
    private FixedString128Bytes GetPlayerNameByAuthId(string authId)
    {
        var players = _playersDataManager.GetAuthIdToPlayerData();
        if (players != null && players.TryGetValue(authId, out var data) && data?.UserData?.PlayerName != null)
            return data.UserData.PlayerName;

        return default;
    }

    public override bool BothTeamsAssigned()
    {
        if (_redPlayer.Value.Team != TeamType.None && _bluePlayer.Value.Team != TeamType.None)
        {
            GameLog.Info("TeamManager: Both teams are now full!");
            return true;
        }

        return false;
    }

    private void OnTeamAssigned(PlayerTeamPair previousValue, PlayerTeamPair newValue)
    {
        GameLog.Info($"Team Assigned: AuthId {newValue.AuthId} -> {newValue.Team}");
    }

    // Server-side

    public override TeamType GetTeam(string authId)
    {
        FixedString64Bytes authIdFs = authId;
        if (_bluePlayer.Value.AuthId == authIdFs) return TeamType.Blue;
        if (_redPlayer.Value.AuthId == authIdFs) return TeamType.Red;

        GameLog.Error($"AuthId {authId} dont have team!");
        return TeamType.None;
    }

    public override bool IsOnTeam(string authId, TeamType team)
    {
        return GetTeam(authId) == team;
    }

    // Client-side

    public override TeamType GetEnemyTeam()
    {
        return GetLocalTeam(false);
    }

    public override TeamType GetLocalTeam(bool isLocal = true)
    {
        if (IsServer && !IsClient)
        {
            GameLog.Warn("Trying to get local team on a dedicated server, returning None");
            return TeamType.None;
        }

        FixedString64Bytes localAuthId = ServiceLocator.Get<BaseClientManager>().UserData.PlayerAuthId;
        if (_redPlayer.Value.AuthId == localAuthId && _redPlayer.Value.Team != TeamType.None) return isLocal ? TeamType.Red : TeamType.Blue;
        if (_bluePlayer.Value.AuthId == localAuthId && _bluePlayer.Value.Team != TeamType.None) return isLocal ? TeamType.Blue : TeamType.Red;
        GameLog.Error($"Local AuthId {localAuthId} dont have team! Returning None");
        return TeamType.None;
    }

    public override bool HasLocalTeamBeenAssigned()
    {
        if (IsServer && !IsClient)
        {
            GameLog.Warn("Trying to check local team assignment on a dedicated server, returning false");
            return false;
        }

        FixedString64Bytes localAuthId = ServiceLocator.Get<BaseClientManager>().UserData.PlayerAuthId;
        return (_bluePlayer.Value.AuthId == localAuthId || _redPlayer.Value.AuthId == localAuthId) && NetworkManager.IsConnectedClient;
    }

    public override string GetPlayerName(TeamType team)
    {
        if (team == TeamType.None) return string.Empty;
        if (_bluePlayer.Value.Team == team) return _bluePlayer.Value.PlayerName.ToString();
        if (_redPlayer.Value.Team == team) return _redPlayer.Value.PlayerName.ToString();
        return string.Empty;
    }
}

public struct PlayerTeamPair : INetworkSerializable, System.IEquatable<PlayerTeamPair>
{
    public FixedString64Bytes AuthId;
    public TeamType Team;
    public FixedString128Bytes PlayerName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref AuthId);
        serializer.SerializeValue(ref Team);
        serializer.SerializeValue(ref PlayerName);
    }

    public bool Equals(PlayerTeamPair other) =>
        AuthId.Equals(other.AuthId) && Team == other.Team && PlayerName.Equals(other.PlayerName);
}
