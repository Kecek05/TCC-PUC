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

    public Action<GameState> RequestTransition;
}