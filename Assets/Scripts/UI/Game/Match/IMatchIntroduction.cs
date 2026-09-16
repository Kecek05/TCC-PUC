/// <summary>
/// The screen that covers the board while a match loads - both players' names. Registered in the
/// ServiceLocator by whatever draws it, so a script that must not begin until the player can actually see
/// the board can wait for it without knowing which canvas that is.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="GameState.InMatch"/>: the server reaches that while the names are still on
/// screen (MatchReady holds for its own delay, the intro for a longer one plus a fade), so "the match has
/// started" and "the player can see it" are two different moments.
/// </remarks>
public interface IMatchIntroduction
{
    /// <summary>True once the intro has fully faded out and stopped blocking input. Never goes back.</summary>
    bool IsFinished { get; }
}
