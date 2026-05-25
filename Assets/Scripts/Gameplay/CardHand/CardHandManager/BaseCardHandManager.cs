using System;
using System.Collections.Generic;
using Unity.Netcode;

public abstract class BaseCardHandManager : NetworkBehaviour
{
    public HandData BlueHandData { get; protected set; }
    public HandData RedHandData { get; protected set; }

    public abstract void SetDeckForPlayer(TeamType teamType, List<CardType> cardsInDeck);
    
    public abstract void NotifyCardPlayed(TeamType teamType, CardType cardType);
    
    public bool TeamHasCardInHand(TeamType teamType, CardType cardType)
    {
        List<CardType> handList = teamType == TeamType.Blue ? BlueHandData.CardsTypeInHand : RedHandData.CardsTypeInHand;
        for (int i = 0; i < handList.Count; i++)
        {
            if (handList[i] == cardType) return true;
        }
        return false;
    }
}
