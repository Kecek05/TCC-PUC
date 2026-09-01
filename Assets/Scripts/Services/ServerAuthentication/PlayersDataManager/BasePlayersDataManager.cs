using System.Collections.Generic;

public abstract class BasePlayersDataManager
{
    public abstract void Handle_OnPlayerConnected(OnCardPlayerConnectedEventArgs args);

    public abstract void RegisterClient(PlayerData playerData);

    /// <summary>
    /// Registers a client-less "virtual" player (a bot): its <see cref="PlayerData"/> becomes visible to
    /// <see cref="GetAuthIdToPlayerData"/> (so the deck pipeline deals its hand) without a clientId mapping.
    /// Default no-op so debug/stand-in managers that never host bots don't need to implement it.
    /// </summary>
    public virtual void RegisterBot(string authId, PlayerData playerData) { }

    public abstract void RegisterTeam(TeamType teamType, string authId);

    public abstract string GetAuthIdByClientId(ulong clientId);

    public abstract ulong GetClientIdByTeamType(TeamType teamType);

    /// <summary>
    /// Silent variant of <see cref="GetClientIdByTeamType"/>: returns false when the team has no network
    /// client mapped (a bot, or a not-yet-seated slot) instead of logging an error. Use this on paths that
    /// legitimately fire for a bot — hand-sync RPCs, per-client UI notifications — so the guard doesn't
    /// paint the console red every card play.
    /// </summary>
    public abstract bool TryGetClientIdByTeamType(TeamType teamType, out ulong clientId);

    public abstract Dictionary<string, PlayerData> GetAuthIdToPlayerData();
}
