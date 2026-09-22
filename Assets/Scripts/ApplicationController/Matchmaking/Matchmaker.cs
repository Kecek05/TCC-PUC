using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

/// <summary>
/// Quick match over the discovery lobby the host already publishes. Nothing new is advertised: a host's
/// lobby carries its Relay join code, so matchmaking is "ask the lobby service for any open one, read the
/// code out of it, join that Relay" — and hosting is simply what happens when the answer is "none".
/// </summary>
/// <remarks>
/// Plain C#, created beside the save and the reward service in <see cref="ClientManager"/>, because a press
/// of Battle has to work from the Main Menu and nothing scene-bound would outlive the load it starts.
/// </remarks>
public class Matchmaker : BaseMatchmaker
{
    /// <summary>
    /// Key the host writes its Relay join code under. Must stay in step with <see cref="HostManager"/>.
    /// </summary>
    public const string JoinCodeKey = "JoinCode";

    private readonly BaseClientManager _clientManager;

    public Matchmaker(BaseClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    public override async Task<MatchmakingOutcome> FindOrCreateMatchAsync()
    {
        if (await TryJoinOpenMatchAsync()) return MatchmakingOutcome.Joined;

        return await TryHostAsync() ? MatchmakingOutcome.Hosting : MatchmakingOutcome.Failed;
    }

    /// <summary>
    /// Every failure here returns false rather than throwing, because none of them is an error the player
    /// should see: "nobody is waiting" and "the one who was waiting has since gone" both mean the same
    /// thing to a press of Battle, and the answer to both is to host.
    /// </summary>
    private async Task<bool> TryJoinOpenMatchAsync()
    {
        Lobby lobby;
        try
        {
            lobby = await LobbyService.Instance.QuickJoinLobbyAsync();
        }
        catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.NoOpenLobbies)
        {
            GameLog.Info("[Matchmaker] No open match waiting; hosting one.");
            return false;
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
            return false;
        }

        if (lobby == null) return false;

        string joinCode = ReadJoinCode(lobby);
        if (string.IsNullOrEmpty(joinCode))
        {
            GameLog.Error($"[Matchmaker] Lobby {lobby.Id} advertised no join code. Leaving it and hosting.");
            await LeaveLobbySafeAsync(lobby.Id);
            return false;
        }

        bool joined = false;
        try
        {
            joined = await _clientManager.JoinHost(joinCode);
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
        }

        if (joined)
        {
            // Stay in the lobby on purpose: the seat this player now occupies is what stops a third one
            // quick-joining into a match that is already full. The host deletes the whole lobby when the
            // match commits (BaseHostManager.CloseLobbyToNewPlayers), which releases it.
            GameLog.Info($"[Matchmaker] Joined open match {lobby.Id}.");
            return true;
        }

        // A host that crashed leaves its lobby advertised until the heartbeat lapses, so quick join will
        // hand out a join code whose Relay allocation is already gone. That is not an error either - give
        // the seat back so the dead lobby is not held open by a phantom member, and host instead.
        GameLog.Info($"[Matchmaker] Lobby {lobby.Id} was stale; leaving it and hosting instead.");
        await LeaveLobbySafeAsync(lobby.Id);
        return false;
    }

    private async Task<bool> TryHostAsync()
    {
        // Resolved per call, never cached: HostManager registers itself in its own Awake, and nothing
        // orders that against ClientManager's, where this service is constructed.
        if (!ServiceLocator.TryGet(out BaseHostManager hostManager))
        {
            GameLog.Error("[Matchmaker] No BaseHostManager registered; cannot host.");
            return false;
        }

        try
        {
            return await hostManager.StartHostAsync();
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
            return false;
        }
    }

    /// <summary>
    /// The host publishes the code with <see cref="DataObject.VisibilityOptions.Member"/>, which is exactly
    /// what makes this work: by the time we read it we have already quick-joined and are a member. Browsing
    /// the public lobby list could never have seen it.
    /// </summary>
    private static string ReadJoinCode(Lobby lobby)
    {
        if (lobby.Data == null) return null;
        return lobby.Data.TryGetValue(JoinCodeKey, out DataObject entry) ? entry?.Value : null;
    }

    private static async Task LeaveLobbySafeAsync(string lobbyId)
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
        }
    }
}
