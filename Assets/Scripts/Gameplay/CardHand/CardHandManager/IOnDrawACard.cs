using System;

public interface IOnDrawACard
{
    /// <summary>
    /// Arg: the CardType drawn from the queue into the hand.
    /// </summary>
    event Action<CardType> OnDrawACard;
}
