
public class DrawingCardsState : IGameFlowState
{
    public GameState Id => GameState.DrawingCards;
    public void Enter(GameFlowContext ctx)
    {
        PlayersDataManager playersDataManager = ServiceLocator.Get<PlayersDataManager>();
        BaseCardHandManager cardHandManager = ServiceLocator.Get<BaseCardHandManager>();

        foreach (var data in playersDataManager.AuthIdToPlayerData)
        {
            TeamType team = ctx.TeamManager.GetTeam(data.Key);
            cardHandManager.SetHandForPlayer(team, data.Value.UserData.DeckCards);
        }
        
        ctx.RequestTransition(GameState.InMatch);
    }

    public void Tick(GameFlowContext ctx) { }

    public void Exit(GameFlowContext ctx) { }
}
