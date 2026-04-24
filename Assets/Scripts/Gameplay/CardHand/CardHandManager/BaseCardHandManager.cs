using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCardHandManager : MonoBehaviour
{
    public abstract void SetHandForPlayer(TeamType teamType, List<CardType> cardsInDeck);
}
