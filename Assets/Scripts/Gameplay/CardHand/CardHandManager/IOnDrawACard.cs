using System;

public interface IOnDrawACard
{
    /// <summary>
    /// Arg: the CardType drawn from the queue into the hand. Team Drawed
    /// </summary>
    event Action<TeamType, CardType> OnDrawACard;
}
