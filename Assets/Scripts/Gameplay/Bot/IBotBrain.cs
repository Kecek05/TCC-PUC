using UnityEngine;

/// <summary>Kind of action the bot may take on a decision tick.</summary>
public enum BotActionKind
{
    None,
    Tower,
    Spell,
    SpawnEnemy
}

/// <summary>
/// A single action the bot chose this tick. Pure data: the <see cref="IBotBrain"/> returns one and the
/// BotController executes it through the shared deployer cores, so the brain stays side-effect free.
/// Positions are server-space.
/// </summary>
public readonly struct BotDecision
{
    public readonly BotActionKind Kind;
    public readonly CardType Card;
    public readonly Vector2 ServerPosition;

    private BotDecision(BotActionKind kind, CardType card, Vector2 serverPosition)
    {
        Kind = kind;
        Card = card;
        ServerPosition = serverPosition;
    }

    public static BotDecision None => new BotDecision(BotActionKind.None, CardType.None, Vector2.zero);
    public static BotDecision Tower(CardType card, Vector2 serverPosition) => new BotDecision(BotActionKind.Tower, card, serverPosition);
    public static BotDecision Spell(CardType card, Vector2 serverPosition) => new BotDecision(BotActionKind.Spell, card, serverPosition);
    public static BotDecision SpawnEnemy(CardType card) => new BotDecision(BotActionKind.SpawnEnemy, card, Vector2.zero);
}

/// <summary>
/// Strategy for choosing the bot's next action from the live game state. Swap implementations to build
/// smarter or harder bots without touching seating or execution. Implementations must be side-effect free.
/// </summary>
public interface IBotBrain
{
    BotDecision Decide(BotContext ctx);
}
