using Unity.Netcode;
using UnityEngine;

public class TeamManager : NetworkBehaviour
{ 
    public static TeamManager Instance { get; private set; }
    
    private NetworkVariable<PlayerTeamPair> _bluePlayer = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<PlayerTeamPair> _redPlayer = new(writePerm: NetworkVariableWritePermission.Server);

    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        _bluePlayer.OnValueChanged += OnTeamAssigned;
        _redPlayer.OnValueChanged += OnTeamAssigned; 
          
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
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

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == 1)
        {
            _redPlayer.Value = new PlayerTeamPair { ClientId = clientId, Team = TeamType.Red };
            Debug.Log($"TeamManager: Client {clientId} connected to Team RED");
        }
        else
        {
            _bluePlayer.Value = new PlayerTeamPair { ClientId = clientId, Team = TeamType.Blue };
            Debug.Log($"TeamManager: Client {clientId} connected to Team BLUE");
        }
    }

    private void OnTeamAssigned(PlayerTeamPair previousValue, PlayerTeamPair newValue)
    {
        Debug.Log($"Team Assigned: Client {newValue.ClientId} -> {newValue.Team}");
    }

    // Server-side

    public TeamType GetTeam(ulong clientId)
    { 
        if (_bluePlayer.Value.ClientId == clientId) return TeamType.Blue;
        if (_redPlayer.Value.ClientId == clientId) return TeamType.Red;
        
        Debug.LogError($"ClientId {clientId} dont have team!");
        return TeamType.None;
    }

    public bool IsOnTeam(ulong clientId, TeamType team)
    {
        return GetTeam(clientId) == team;
    }

    // Client-side 

    public TeamType GetLocalTeam()
    {
        ulong localId = NetworkManager.LocalClientId;
        if (_bluePlayer.Value.ClientId == localId) return TeamType.Blue;
        if  (_redPlayer.Value.ClientId == localId) return TeamType.Red;
        Debug.LogError($"LocalId {localId} dont have team! Returning None");
        return TeamType.None;
    }

    public bool HasLocalTeamBeenAssigned()
    {
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
