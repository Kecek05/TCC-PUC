using System.Collections.Generic;
using UnityEngine;

public class PlayersDataManager
{
    private Dictionary<string, ulong> _authToClientId = new(); 

    private Dictionary<ulong, string> _clientIdToAuth = new(); 
    
    private Dictionary<string, PlayerData> _authIdToPlayerData = new();

    private Dictionary<ulong, UserData> _clientIdToUserData = new();
    
    public Dictionary<ulong, UserData> ClientIdToUserData => _clientIdToUserData;
    
    public void RegisterUserData(UserData userData, ulong clientId)
    {
        _clientIdToUserData[clientId] = userData;
    }
    
    public void RegisterClient(PlayerData playerData)
    {
        _authToClientId[playerData.UserData.PlayerAuthId] = playerData.ClientId;
        _authIdToPlayerData[playerData.UserData.PlayerAuthId] = playerData;
        _clientIdToAuth[playerData.ClientId] = playerData.UserData.PlayerAuthId;

        Debug.Log($"RegisterClient, AuthId: {playerData.UserData.PlayerAuthId} ClientId: {playerData.ClientId} ");
    }
    
    public string GetAuthIdByClientId(ulong clientId)
    {
        if (_clientIdToAuth.TryGetValue(clientId, out string authId))
        {
            return authId;
        }
        return null;
    }
}
