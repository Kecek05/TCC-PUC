using Unity.Netcode;

/// <summary>
/// Server-side controller for the fallback bot opponent. Exposes just what the game-flow FSM needs to
/// trigger seating; the concrete <see cref="BotController"/> owns identity, seating and the decision loop.
/// </summary>
public abstract class BaseBotController : NetworkBehaviour
{
    public abstract bool IsSeated { get; }
    public abstract bool BotFallbackEnabled { get; }
    public abstract float FillTimeoutSeconds { get; }

    /// <summary>Seat the bot into the empty (Blue) slot and start it playing. Server-only, idempotent.</summary>
    public abstract void SeatBot();
}
