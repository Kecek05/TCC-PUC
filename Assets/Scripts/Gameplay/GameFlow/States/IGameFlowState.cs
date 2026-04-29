/// <summary>
/// One node in the game-flow FSM. Each state owns its own entry/exit
/// side-effects and decides its own transitions via <see cref="GameFlowContext.RequestTransition"/>.
/// </summary>
public interface IGameFlowState
{
    GameState Id { get; }
    void Enter(GameFlowContext ctx);
    void Tick(GameFlowContext ctx);
    void Exit(GameFlowContext ctx);
}