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
            _redPlayer.Value = new PlayerTeamPair { AuthId = authIdFs, Team = TeamType.Red };
            _playersDataManager.RegisterTeam(TeamType.Red, authId);
            GameLog.Info($"TeamManager: AuthId {authId} assigned to Team RED");
        }
        else if (_bluePlayer.Value.Team == TeamType.None)
        {
            _bluePlayer.Value = new PlayerTeamPair { AuthId = authIdFs, Team = TeamType.Blue };
            _playersDataManager.RegisterTeam(TeamType.Blue, authId);
            GameLog.Info($"TeamManager: AuthId {authId} assigned to Team BLUE");
        }
        else
        {
            GameLog.Warn($"TeamManager: AuthId {authId} loaded but both teams are full!");
        }
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
}

public struct PlayerTeamPair : INetworkSerializable, System.IEquatable<PlayerTeamPair>
{
    public FixedString64Bytes AuthId;
    public TeamType Team;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref AuthId);
        serializer.SerializeValue(ref Team);
    }

    public bool Equals(PlayerTeamPair other) =>
        AuthId.Equals(other.AuthId) && Team == other.Team;
}
