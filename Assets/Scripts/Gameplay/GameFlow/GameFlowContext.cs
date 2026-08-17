using System;

/// <summary>
/// Services and callbacks made available to every <see cref="IGameFlowState"/>.
/// States never touch <see cref="ServiceLocator"/> directly — they read from the context.
/// </summary>
public class GameFlowContext
{
    public BaseTeamManager TeamManager;
    public BaseMapTranslator MapTranslator;
    public BaseServerEndGameManager EndGameManager;

    /// <summary>Optional fallback-bot controller. Null when no bot is present (e.g. debug scenes).</summary>
    public BaseBotController BotController;

    /// <summary>Commit the match — stop admitting new players (approval + lobby). Invoked on leaving WaitingForPlayers.</summary>
    public Action CommitMatch;

    public Action<GameState> RequestTransition;
}