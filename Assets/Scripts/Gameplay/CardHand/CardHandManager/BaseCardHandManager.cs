using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;

public abstract class BaseCardHandManager : NetworkBehaviour
{
    [ShowInInspector, ReadOnly, FoldoutGroup("Hands"), HideReferenceObjectPicker]
    public HandData BlueHandData { get; protected set; }

    [ShowInInspector, ReadOnly, FoldoutGroup("Hands"), HideReferenceObjectPicker]
    public HandData RedHandData { get; protected set; }

    public abstract void SetDeckForPlayer(TeamType teamType, List<CardType> cardsInDeck);
    
    public abstract void NotifyCardPlayed(TeamType teamType, CardType cardType);
    
    public bool TeamHasCardInHand(TeamType teamType, CardType cardType)
    {
        List<CardType> handList = teamType == TeamType.Blue ? BlueHandData.CardsTypeInHand : RedHandData.CardsTypeInHand;
        foreach (var cardTypeInHand in handList)
        {
            if (cardTypeInHand == cardType) return true;
        }
        return false;
    }
}
