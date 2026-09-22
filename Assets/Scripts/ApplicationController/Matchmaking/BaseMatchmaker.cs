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
}

/// <summary>
/// One press, one match: find an open match and join it, or become the open match. Sits above
/// <see cref="BaseHostManager"/> and <see cref="BaseClientManager"/> because it is the only thing that needs
/// both — hosting and joining stay unaware of each other, and neither learns what a queue is.
/// </summary>
public abstract class BaseMatchmaker
{
    /// <summary>
    /// Joins an advertised match if there is one, otherwise hosts a new one. On <see cref="MatchmakingOutcome.Joined"/>
    /// and <see cref="MatchmakingOutcome.Hosting"/> the scene load is already under way, so the caller should
    /// not navigate; on <see cref="MatchmakingOutcome.Failed"/> nothing happened and the player is still in the menu.
    /// </summary>
    public abstract Task<MatchmakingOutcome> FindOrCreateMatchAsync();
}
