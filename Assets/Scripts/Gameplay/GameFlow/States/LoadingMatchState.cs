public class LoadingMatchState : IGameFlowState
{
    public GameState Id => GameState.LoadingMatch;

    public void Enter(GameFlowContext ctx) { }
    public void Exit(GameFlowContext ctx) { }

    public void Tick(GameFlowContext ctx)
    {
        if (ctx.MapTranslator != null && ctx.MapTranslator.BothPlayersInitialized)
            ctx.RequestTransition(GameState.MatchReady);
    }
}