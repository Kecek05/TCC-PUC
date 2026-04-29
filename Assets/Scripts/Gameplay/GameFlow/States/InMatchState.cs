public class InMatchState : IGameFlowState
{
    private GameFlowContext _ctx;

    public GameState Id => GameState.InMatch;

    public void Enter(GameFlowContext ctx)
    {
        _ctx = ctx;
        if (ctx.EndGameManager != null)
            ctx.EndGameManager.OnGameEnded += OnGameEnded;
    }

    public void Exit(GameFlowContext ctx)
    {
        if (ctx.EndGameManager != null)
            ctx.EndGameManager.OnGameEnded -= OnGameEnded;
        _ctx = null;
    }

    public void Tick(GameFlowContext ctx) { }

    private void OnGameEnded(EndGameSnapshot snapshot)
        => _ctx?.RequestTransition(GameState.EndMatch);
}