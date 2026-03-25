using System;
using System.Collections.Generic;
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
        }
        else
        {
            _bluePlayer.Value = new PlayerTeamPair { ClientId = clientId, Team = TeamType.Blue };
        }
    }

    private void OnTeamAssigned(PlayerTeamPair previousValue, PlayerTeamPair newValue)
    {
        Debug.Log($"Time atribuído: Client {newValue.ClientId} → {newValue.Team}");
    }

    // ===== API Server-side =====

    public TeamType GetTeam(ulong clientId)
    { 
        if (_bluePlayer.Value.ClientId == clientId) return TeamType.Blue;
        if (_redPlayer.Value.ClientId == clientId) return TeamType.Red;
        
        Debug.LogError($"ClientId {clientId} não tem time atribuído!");
        return TeamType.Blue;
    }

    public bool IsOnTeam(ulong clientId, TeamType team)
    {
        return GetTeam(clientId) == team;
    }

    // ===== API Client-side =====

    public TeamType GetLocalTeam()
    {
        ulong localId = NetworkManager.LocalClientId;
        if (_bluePlayer.Value.ClientId == localId) return TeamType.Blue;
        return TeamType.Red;
    }

    public bool HasLocalTeamBeenAssigned()
    {
        ulong localId = NetworkManager.LocalClientId;
        return _bluePlayer.Value.ClientId == localId || _redPlayer.Value.ClientId == localId;
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
