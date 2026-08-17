/// <summary>
/// Server-side switch for whether the match still admits new network clients. Implemented by the
/// connection layer and registered in the ServiceLocator; flipped off (e.g. by the BotController) once
/// the match is committed, so a late joiner can't connect into an already-running bot match.
/// </summary>
public interface IMatchAdmission
{
    void StopAcceptingPlayers();
}
