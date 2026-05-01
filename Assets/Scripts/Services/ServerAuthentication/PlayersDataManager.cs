using System.Collections.Generic;
using UnityEngine;

public class PlayersDataManager
{
    private Dictionary<string, ulong> _authToClientId = new(); 

    private Dictionary<ulong, string> _clientIdToAuth = new(); 
    
    private Dictionary<string, PlayerData> _authIdToPlayerData = new();

    public Dictionary<string, PlayerData> AuthIdToPlayerData => _authIdToPlayerData;
    
    public void Handle_OnPlayerConnected(OnCardPlayerConnectedEventArgs args)
    {
        PlayerData newPlayerData = new PlayerData()
        {
            UserData = args.UserData,
            ClientId = args.ClientId,
        };

        RegisterClient(newPlayerData);
    }
    
    public void RegisterClient(PlayerData playerData)
    {
        _authToClientId[playerData.UserData.PlayerAuthId] = playerData.ClientId;
        _authIdToPlayerData[playerData.UserData.PlayerAuthId] = playerData;
        _clientIdToAuth[playerData.ClientId] = playerData.UserData.PlayerAuthId;

        GameLog.Info($"Registered player: {playerData.UserData.PlayerName}, AuthId: {playerData.UserData.PlayerAuthId}, ClientId: {playerData.ClientId}");
    }
    
    public string GetAuthIdByClientId(ulong clientId)
    {
        if (_clientIdToAuth.TryGetValue(clientId, out string authId))
        {
            return authId;
        }
        GameLog.Warn("Trying to get auth id for client ID: " + clientId);
        return null;
    }
    
    public PlayerData GetPlayerDataByAuthId(string authId)
    {
        if (_authIdToPlayerData.TryGetValue(authId, out PlayerData playerData))
        {
            return playerData;
        }
        return null;
    }
}
