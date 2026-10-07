using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>How a press of Battle resolved.</summary>
public enum MatchmakingOutcome
{
    /// <summary>Somebody was already waiting and this player joined them.</summary>
    Joined,

    /// <summary>Nobody was waiting, so this player is now hosting and waiting instead.</summary>
    Hosting,

    /// <summary>Neither worked. The caller re-enables its button and says so.</summary>
    Failed,

    /// <summary>The player backed out before anything was committed: nothing joined, nothing hosted.</summary>
    Cancelled,
}

/// <summary>
/// One press, one match: find an open match and join it, or become the open match. Sits above
/// <see cref="BaseHostManager"/> and <see cref="BaseClientManager"/> because it is the only thing that needs
/// both — hosting and joining stay unaware of each other, and neither learns what a queue is.
/// </summary>
public abstract class BaseMatchmaker
{
    /// <summary>
    /// Nobody was waiting, so this player is about to host and wait for an opponent instead. Raised before the
    /// host starts (and with it the scene load): the search goes on inside the match scene, so this is not a
    /// match found, and nothing can be cancelled from here on.
    /// </summary>
    public event Action HostingStarted;

    protected void RaiseHostingStarted() => HostingStarted?.Invoke();

    /// <summary>
    /// Joins an advertised match if there is one, otherwise hosts a new one. On <see cref="MatchmakingOutcome.Joined"/>
    /// and <see cref="MatchmakingOutcome.Hosting"/> the scene load is already under way, so the caller should
    /// not navigate; on <see cref="MatchmakingOutcome.Failed"/> nothing happened and the player is still in the menu.
    /// </summary>
    /// <param name="cancel">
    /// Honoured only where backing out is clean: before a join or a host has begun. Past that point a scene
    /// load is committed and the search finishes as <see cref="MatchmakingOutcome.Joined"/> or
    /// <see cref="MatchmakingOutcome.Hosting"/> even if cancel was asked for - tearing a half-started session
    /// down is riskier than letting the match load.
    /// </param>
    public abstract Task<MatchmakingOutcome> FindOrCreateMatchAsync(CancellationToken cancel = default);
}
