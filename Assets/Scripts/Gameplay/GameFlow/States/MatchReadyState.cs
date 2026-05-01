using UnityEngine;

public class MatchReadyState : IGameFlowState
{
    private const float DelayBeforeMatchSeconds = 2f;

    private float _enterTime;

    public GameState Id => GameState.MatchReady;

    public void Enter(GameFlowContext ctx) => _enterTime = Time.time;
    public void Exit(GameFlowContext ctx) { }

    public void Tick(GameFlowContext ctx)
    {
        if (Time.time - _enterTime >= DelayBeforeMatchSeconds)
            ctx.RequestTransition(GameState.DrawingCards);
    }
}