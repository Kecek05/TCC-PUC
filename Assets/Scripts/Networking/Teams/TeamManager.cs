using Unity.Netcode;
using UnityEngine;

public class TeamManager : BaseTeamManager
{
    private NetworkVariable<PlayerTeamPair> _bluePlayer = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<PlayerTeamPair> _redPlayer = new(writePerm: NetworkVariableWritePermission.Server);

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
            NetworkManager.OnClientConnectedCallback += OnClientConnected;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                OnClientConnected(clientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        _bluePlayer.OnValueChanged -= OnTeamAssigned;
        _redPlayer.OnValueChanged -= OnTeamAssigned;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }
    }
    
    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseTeamManager>();
    }

    private bool HasTeam(ulong clientId)
    {
        return (_redPlayer.Value.Team != TeamType.None && _redPlayer.Value.ClientId == clientId) ||
               (_bluePlayer.Value.Team != TeamType.None && _bluePlayer.Value.ClientId == clientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (HasTeam(clientId)) return;

        if (_redPlayer.Value.Team == TeamType.None)
        {
            _redPlayer.Value = new PlayerTeamPair { ClientId = clientId, Team = TeamType.Red };
            Debug.Log($"TeamManager: Client {clientId} connected to Team RED");
        }
        else if (_bluePlayer.Value.Team == TeamType.None)
        {
            _bluePlayer.Value = new PlayerTeamPair { ClientId = clientId, Team = TeamType.Blue };
            Debug.Log($"TeamManager: Client {clientId} connected to Team BLUE");
        }
        else
        {
            Debug.LogWarning($"TeamManager: Client {clientId} connected but both teams are full!");
        }
    }

    public override bool BothTeamsAssigned()
    {
        if (_redPlayer.Value.Team != TeamType.None && _bluePlayer.Value.Team != TeamType.None)
        {
            Debug.Log("TeamManager: Both teams are now full!");
            return true;
        }

        return false;
    }

    private void OnTeamAssigned(PlayerTeamPair previousValue, PlayerTeamPair newValue)
    {
        Debug.Log($"Team Assigned: Client {newValue.ClientId} -> {newValue.Team}");
    }

    // Server-side

    public override TeamType GetTeam(ulong clientId)
    {
        if (_bluePlayer.Value.ClientId == clientId) return TeamType.Blue;
        if (_redPlayer.Value.ClientId == clientId) return TeamType.Red;

        Debug.LogError($"ClientId {clientId} dont have team!");
        return TeamType.None;
    }

    public override bool IsOnTeam(ulong clientId, TeamType team)
    {
        return GetTeam(clientId) == team;
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
            Debug.LogWarning("Trying to get local team on a dedicated server, returning None");
            return TeamType.None;
        }
        
        ulong localId = NetworkManager.LocalClientId;
        if  (_redPlayer.Value.ClientId == localId && _redPlayer.Value.Team != TeamType.None) return isLocal ? TeamType.Red : TeamType.Blue;
        if (_bluePlayer.Value.ClientId == localId && _bluePlayer.Value.Team != TeamType.None) return isLocal ? TeamType.Blue  : TeamType.Red;
        Debug.LogError($"LocalId {localId} dont have team! Returning None");
        return TeamType.None;
    }

    public override bool HasLocalTeamBeenAssigned()
    {
        if (IsServer && !IsClient)
        {
            Debug.LogWarning("Trying to check local team assignment on a dedicated server, returning false");
            return false;
        }
        
        ulong localId = NetworkManager.LocalClientId;
        return (_bluePlayer.Value.ClientId == localId || _redPlayer.Value.ClientId == localId) && NetworkManager.IsConnectedClient;
    }
}

public struct PlayerTeamPair : INetworkSerializable, System.IEquatable<PlayerTeamPair>
{
    public ulong ClientId;
    public TeamType Team;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Team);
    }

    public bool Equals(PlayerTeamPair other) =>
        ClientId == other.ClientId && Team == other.Team;
}
