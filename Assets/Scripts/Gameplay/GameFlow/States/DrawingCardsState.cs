
public class DrawingCardsState : IGameFlowState
{
    public GameState Id => GameState.DrawingCards;
    public void Enter(GameFlowContext ctx)
    {
        BasePlayersDataManager playersDataManager = ServiceLocator.Get<BasePlayersDataManager>();
        BaseCardHandManager cardHandManager = ServiceLocator.Get<BaseCardHandManager>();

        foreach (var data in playersDataManager.GetAuthIdToPlayerData())
        {
            TeamType team = ctx.TeamManager.GetTeam(data.Key);
            cardHandManager.SetDeckForPlayer(team, data.Value.UserData.DeckCards);
        }
        
        ctx.RequestTransition(GameState.InMatch);
    }

    public void Tick(GameFlowContext ctx) { }

    public void Exit(GameFlowContext ctx) { }
}
