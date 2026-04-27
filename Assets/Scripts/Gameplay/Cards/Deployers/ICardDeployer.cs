using System;

/// <summary>
/// Implemented by anything that can deploy a card on the server. Listeners
/// (CardHand, analytics, quests, UI) depend on this abstraction instead of a
/// concrete deployer type, so deployers can be added/removed/swapped freely.
/// </summary>
public interface ICardDeployer
{
    event Action<CardDeployedEventArgs> OnCardDeployed;
}

public struct CardDeployedEventArgs
{
    public TeamType TeamDeployed;
    public CardType CardDeployed;
}
