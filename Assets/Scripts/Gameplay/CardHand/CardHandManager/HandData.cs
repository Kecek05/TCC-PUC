using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class HandData
{
    // List of Instance Cards in the players hand (populated by UI layer)
    public List<AbstractCard> CardsInHand;
    // Cards currently displayed in the player's hand (data, source of truth for draw cycle)
    public List<CardType> HandCards;
    // List of all Cards in the player's deck (full set, unchanged after distribution)
    public List<CardType> CardsInDeck;
    // List of cards that the Cost is higher than the current maximum mana
    public List<CardType> LockedCards;
    // Queue of the next cards available to be drawn, based on the current deck, hand and maximum mana
    public Queue<CardType> QueuedCards;

    /// <summary>
    /// Builds a fresh <see cref="HandData"/>: starts with every card unlocked, locks
    /// those above <paramref name="maxMana"/>, shuffles the remainder, then draws the
    /// first <paramref name="handSize"/> into the hand with the rest forming the queue.
    /// </summary>
    public static HandData Distribute(List<CardType> deck, int handSize, float maxMana, ICardCostProvider costs)
    {
        HandData data = new HandData
        {
            CardsInHand = new List<AbstractCard>(handSize),
            HandCards = new List<CardType>(handSize),
            CardsInDeck = new List<CardType>(deck),
            LockedCards = new List<CardType>(),
            QueuedCards = new Queue<CardType>(),
        };

        List<CardType> unlockedPool = new List<CardType>(deck.Count);
        foreach (CardType card in deck)
        {
            if (costs.GetCost(card) > maxMana)
                data.LockedCards.Add(card);
            else
                unlockedPool.Add(card);
        }

        Shuffle(unlockedPool);

        int drawCount = Mathf.Min(handSize, unlockedPool.Count);
        for (int i = 0; i < drawCount; i++)
            data.HandCards.Add(unlockedPool[i]);
        for (int i = drawCount; i < unlockedPool.Count; i++)
            data.QueuedCards.Enqueue(unlockedPool[i]);

        return data;
    }

    /// <summary>
    /// Resolves a "card played" event: moves the played card to the back of the queue,
    /// then draws the front of the queue into the hand. Returns false if the played
    /// card wasn't actually in the hand.
    /// </summary>
    public bool Play(CardType cardType)
    {
        if (!HandCards.Remove(cardType)) return false;

        // Played -> back of queue -> then draw the front. Guarantees a draw even in
        // the degenerate case where the played card is the only drawable one.
        QueuedCards.Enqueue(cardType);
        HandCards.Add(QueuedCards.Dequeue());
        return true;
    }

    /// <summary>
    /// Promotes any locked cards whose cost is now &lt;= <paramref name="newMaxMana"/>
    /// into the queue. Preserves the "Next" slot (the head of the queue) unchanged and
    /// shuffles the newcomers into the remainder. Returns true if anything moved.
    /// Cards never move from queue/hand back into locked — locking is a one-way exit.
    /// </summary>
    public bool Unlock(float newMaxMana, ICardCostProvider costs)
    {
        List<CardType> unlocked = null;
        for (int i = LockedCards.Count - 1; i >= 0; i--)
        {
            CardType card = LockedCards[i];
            if (costs.GetCost(card) <= newMaxMana)
            {
                LockedCards.RemoveAt(i);
                unlocked ??= new List<CardType>();
                unlocked.Add(card);
            }
        }

        if (unlocked == null || unlocked.Count == 0) return false;

        MergeUnlockedIntoQueue(unlocked);
        return true;
    }

    private void MergeUnlockedIntoQueue(List<CardType> unlocked)
    {
        List<CardType> tail = new List<CardType>(QueuedCards.Count + unlocked.Count);

        CardType? next = null;
        if (QueuedCards.Count > 0)
        {
            next = QueuedCards.Dequeue();
            while (QueuedCards.Count > 0)
                tail.Add(QueuedCards.Dequeue());
        }

        tail.AddRange(unlocked);
        Shuffle(tail);

        QueuedCards.Clear();
        if (next.HasValue)
            QueuedCards.Enqueue(next.Value);
        for (int i = 0; i < tail.Count; i++)
            QueuedCards.Enqueue(tail[i]);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
