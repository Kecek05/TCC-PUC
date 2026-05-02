using System.Collections.Generic;

public abstract class BasePlayersDataManager
{
    public abstract void Handle_OnPlayerConnected(OnCardPlayerConnectedEventArgs args);

    public abstract void RegisterClient(PlayerData playerData);

    public abstract void RegisterTeam(TeamType teamType, string authId);

    public abstract string GetAuthIdByClientId(ulong clientId);

    public abstract ulong GetClientIdByTeamType(TeamType teamType);
    
    public abstract Dictionary<string, PlayerData> GetAuthIdToPlayerData();
}
