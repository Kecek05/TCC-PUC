using System;
using System.Collections.Generic;

/// <summary>
/// Drives <see cref="IGameFlowState"/> transitions. The owner subscribes to
/// <see cref="OnStateChanged"/> to mirror the active state onto a NetworkVariable
/// or any other transport — the FSM itself stays transport-agnostic and testable.
/// </summary>
public class GameFlowFsm
{
    public event Action<GameState> OnStateChanged;

    private readonly Dictionary<GameState, IGameFlowState> _states = new();
    private readonly GameFlowContext _ctx;
    private IGameFlowState _current;

    public GameState Current => _current?.Id ?? GameState.None;

    public GameFlowFsm(GameFlowContext ctx, params IGameFlowState[] states)
    {
        _ctx = ctx;
        foreach (IGameFlowState state in states)
            _states[state.Id] = state;

        _ctx.RequestTransition = ChangeState;
    }

    public void Start(GameState initial) => ChangeState(initial);

    public void Tick() => _current?.Tick(_ctx);

    public void ChangeState(GameState next)
    {
        if (!_states.TryGetValue(next, out IGameFlowState newState))
        {
            GameLog.Error($"GameFlowFsm: state {next} not registered");
            return;
        }

        if (_current == newState) return;

        _current?.Exit(_ctx);
        _current = newState;
        OnStateChanged?.Invoke(next);
        _current.Enter(_ctx);
    }
}