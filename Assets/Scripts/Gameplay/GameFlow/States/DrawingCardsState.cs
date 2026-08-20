public class DrawingCardsState : IGameFlowState
{
    public GameState Id => GameState.DrawingCards;

    public void Enter(GameFlowContext ctx)
    {
        BasePlayersDataManager playersDataManager = ServiceLocator.Get<BasePlayersDataManager>();
        BaseCardHandManager cardHandManager = ServiceLocator.Get<BaseCardHandManager>();

        // Optional: debug scenes have no MatchCardLevels, and the correct behaviour there is "level 1".
        ServiceLocator.TryGet(out MatchCardLevels matchCardLevels);

        foreach (var data in playersDataManager.GetAuthIdToPlayerData())
        {
            TeamType team = ctx.TeamManager.GetTeam(data.Key);
            cardHandManager.SetDeckForPlayer(team, data.Value.UserData.DeckCards);

            // Same source of truth as the deck itself, so a player's levels can never describe a
            // different deck from the one they are about to play.
            matchCardLevels?.SetLevels(team, data.Value.UserData.DeckCards, data.Value.UserData.DeckCardLevels);
        }

        ctx.RequestTransition(GameState.InMatch);
    }

    public void Tick(GameFlowContext ctx) { }

    public void Exit(GameFlowContext ctx) { }
}
