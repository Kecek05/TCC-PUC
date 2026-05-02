using System.Collections.Generic;
using UnityEngine;

public class PlayersDataManager : BasePlayersDataManager
{
    private Dictionary<string, ulong> _authToClientId = new(); 
    private Dictionary<ulong, string> _clientIdToAuthId = new(); 
    private Dictionary<string, PlayerData> _authIdToPlayerData = new();
    private Dictionary<TeamType, string> _teamDataToAuthId = new();
    public Dictionary<string, PlayerData> AuthIdToPlayerData => _authIdToPlayerData;
    
    public override void Handle_OnPlayerConnected(OnCardPlayerConnectedEventArgs args)
    {
        PlayerData newPlayerData = new PlayerData()
        {
            UserData = args.UserData,
            ClientId = args.ClientId,
        };

        RegisterClient(newPlayerData);
    }
    
    public override void RegisterClient(PlayerData playerData)
    {
        _authToClientId[playerData.UserData.PlayerAuthId] = playerData.ClientId;
        _authIdToPlayerData[playerData.UserData.PlayerAuthId] = playerData;
        _clientIdToAuthId[playerData.ClientId] = playerData.UserData.PlayerAuthId;

        GameLog.Info($"Registered player: {playerData.UserData.PlayerName}, AuthId: {playerData.UserData.PlayerAuthId}, ClientId: {playerData.ClientId}");
    }

    public override void RegisterTeam(TeamType teamType, string authId)
    {
        if (_teamDataToAuthId.ContainsKey(teamType))
        {
            GameLog.Error($"Team {teamType} already registered");
            return;
        }
        _teamDataToAuthId[teamType] = authId;
    }
    
    public override string GetAuthIdByClientId(ulong clientId)
    {
        if (_clientIdToAuthId.TryGetValue(clientId, out string authId))
        {
            return authId;
        }
        GameLog.Warn("Trying to get auth id for client ID: " + clientId);
        return null;
    }
    
    public override ulong GetClientIdByTeamType(TeamType teamType)
    {
        if (_teamDataToAuthId.TryGetValue(teamType, out string authId))
        {
            if (_authToClientId.TryGetValue(authId, out ulong clientId))
            {
                return clientId;
            }
        }
        
        GameLog.Error("Error Trying to get client ID for team type: " + teamType);
        return ulong.MaxValue;
    }
}
