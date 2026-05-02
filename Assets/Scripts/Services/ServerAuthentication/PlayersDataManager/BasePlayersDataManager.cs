using System.Collections.Generic;

public abstract class BasePlayersDataManager
{
    public Dictionary<string, PlayerData> AuthIdToPlayerData { get; protected set; } = new();

    public abstract void Handle_OnPlayerConnected(OnCardPlayerConnectedEventArgs args);

    public abstract void RegisterClient(PlayerData playerData);

    public abstract void RegisterTeam(TeamType teamType, string authId);

    public abstract string GetAuthIdByClientId(ulong clientId);

    public abstract ulong GetClientIdByTeamType(TeamType teamType);
}
