using System;
using UnityEngine;

public interface IOnLocalDrawnACard
{
    /// <summary>
    /// Arg: the CardType drawn from the queue into the hand to the local player.
    /// </summary>
    event Action<CardType> OnLocalDrawACard;
}
