public class WaitingForPlayersState : IGameFlowState
{
    public GameState Id => GameState.WaitingForPlayers;

    public void Enter(GameFlowContext ctx) { }
    public void Exit(GameFlowContext ctx) { }

    public void Tick(GameFlowContext ctx)
    {
        if (ctx.TeamManager != null && ctx.TeamManager.BothTeamsAssigned())
            ctx.RequestTransition(GameState.LoadingMatch);
    }
}