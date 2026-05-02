using System.Collections.Generic;
using Unity.Netcode;

public class PlayersDataManager_SinglePlayerDEBUG : BasePlayersDataManager
{
    private string debugID = "ID";
    public DebugHand DebugHand;
    
    public override void Handle_OnPlayerConnected(OnCardPlayerConnectedEventArgs args)
    {
        
    }

    public override void RegisterClient(PlayerData playerData)
    {
        
    }

    public override void RegisterTeam(TeamType teamType, string authId)
    {
        
    }

    public override string GetAuthIdByClientId(ulong clientId)
    {
        return debugID;
    }

    public override ulong GetClientIdByTeamType(TeamType teamType) => NetworkManager.Singleton.LocalClientId;

    public override Dictionary<string, PlayerData> GetAuthIdToPlayerData()
    {
        Dictionary<string, PlayerData> authIdToPlayerDataDEBUG = new();
        
        authIdToPlayerDataDEBUG.Add("ID", new PlayerData()
        {
            UserData = new UserData()
            {
                PlayerName = "DebugPlayer",
                PlayerAuthId = debugID,
                UserTrophies = -1,
                DeckCards = DebugHand.Deck,
            },
        });
        
        return authIdToPlayerDataDEBUG;
    }
}
