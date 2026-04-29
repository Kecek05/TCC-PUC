public class EndMatchState : IGameFlowState
{
    public GameState Id => GameState.EndMatch;

    public void Enter(GameFlowContext ctx) { }
    public void Exit(GameFlowContext ctx) { }
    public void Tick(GameFlowContext ctx) { }
}