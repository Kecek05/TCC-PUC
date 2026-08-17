using UnityEngine;

public class WaitingForPlayersState : IGameFlowState
{
    public GameState Id => GameState.WaitingForPlayers;

    private float _elapsed;

    public void Enter(GameFlowContext ctx) { _elapsed = 0f; }

    public void Exit(GameFlowContext ctx)
    {
        // The match is starting (a 2nd human joined or the bot seated): stop admitting new players and
        // close the discovery lobby so nobody connects into an in-progress match.
        ctx.CommitMatch?.Invoke();
    }

    public void Tick(GameFlowContext ctx)
    {
        if (ctx.TeamManager != null && ctx.TeamManager.BothTeamsAssigned())
        {
            ctx.RequestTransition(GameState.LoadingMatch);
            return;
        }

        // No second human yet: after the timeout, seat a bot to fill the empty slot. Seating assigns the
        // bot its team, which flips BothTeamsAssigned() true on the next tick -> normal match start.
        BaseBotController bot = ctx.BotController;
        if (bot == null || !bot.BotFallbackEnabled || bot.IsSeated) return;

        _elapsed += Time.deltaTime;
        if (_elapsed >= bot.FillTimeoutSeconds)
            bot.SeatBot();
    }
}
