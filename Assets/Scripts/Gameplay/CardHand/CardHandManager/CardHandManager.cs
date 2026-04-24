using System;
using System.Collections.Generic;
using UnityEngine;

public class CardHandManager : BaseCardHandManager
{
    [SerializeField] private CardDataListSO  _cardDataListSO;
    
    private HandData _redHandData;
    private HandData _blueHandData;
    private void Awake()
    {
        ServiceLocator.Register<BaseCardHandManager>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<BaseCardHandManager>();
    }

    public override void SetHandForPlayer(TeamType teamType, List<CardType> cardsInDeck)
    {
        //TODO: Set the corresponding HandData based on cardsInDeck and teamType
    }
}

public class HandData
{
    // List of Instance Cards in the players hand
    public List<AbstractCard> CardsInHand;
    // List of all Cards in the deck
    public List<CardType> CardsInDeck;
    // List of cards that the Cost is higher than the current maximum mana  
    public List<CardType> LockedCards;
    // Queue of the next cards available to be drawn, based on the current deck, hand and maximum mana
    public Queue<CardType> QueuedCards;
}