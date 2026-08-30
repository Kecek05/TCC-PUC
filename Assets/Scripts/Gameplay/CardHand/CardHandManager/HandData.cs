using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Random = UnityEngine.Random;

[Serializable]
public class HandData
{
    /// <summary>
    /// Cards currently displayed in the player's hand (data, source of truth for draw cycle)
    /// </summary>
    public List<CardType> CardsTypeInHand;
    
    /// <summary>
    /// List of all Cards in the player's deck (full set, unchanged after distribution)
    /// </summary>
    public List<CardType> CardsTypeInDeck;
    
    /// <summary>
    /// List of cards that the Cost is higher than the current maximum mana
    /// </summary>
    public List<CardType> LockedCardsType;
    
    /// <summary>
    /// Queue of the next cards available to be drawn, based on the current deck, hand and maximum mana
    /// </summary>
    [ShowInInspector] public Queue<CardType> QueuedCardsType;

    /// <summary>
    /// Builds a fresh <see cref="HandData"/>: locks cards above <paramref name="maxMana"/>,
    /// shuffles the remainder, and enqueues all of them. The hand starts empty — call
    /// <see cref="Draw"/> N times to populate it (each draw is observable to listeners).
    /// </summary>
    public static HandData Distribute(List<CardType> deck, float maxMana, ICardCostProvider costs)
    {
        HandData data = new HandData
        {
            CardsTypeInHand = new List<CardType>(),
            CardsTypeInDeck = new List<CardType>(deck),
            LockedCardsType = new List<CardType>(),
            QueuedCardsType = new Queue<CardType>(),
        };

        List<CardType> unlockedPool = new List<CardType>(deck.Count);
        foreach (CardType card in deck)
        {
            if (costs.GetCost(card) > maxMana)
                data.LockedCardsType.Add(card);
            else
                unlockedPool.Add(card);
        }

        Shuffle(unlockedPool);

        for (int i = 0; i < unlockedPool.Count; i++)
            data.QueuedCardsType.Enqueue(unlockedPool[i]);

        return data;
    }

    /// <summary>
    /// Dequeues the front of the queue into the hand. Returns false if the queue is empty.
    /// </summary>
    public bool Draw(out CardType drawnCard)
    {
        drawnCard = CardType.None;
        if (QueuedCardsType.Count == 0) return false;

        drawnCard = QueuedCardsType.Dequeue();
        CardsTypeInHand.Add(drawnCard);
        return true;
    }

    /// <summary>
    /// Resolves a "card played" event: takes the card out of the hand and puts it at the back of the
    /// queue. Drawing the replacement is deliberately left to the caller — one rule decides how full a
    /// hand is (see <c>ServerCardHandManager.RefillHand</c>), instead of this method topping up by
    /// exactly one and a separate loop topping up the rest. Queueing the played card first also keeps
    /// the degenerate case working: even when it was the only drawable card, there is one to draw.
    /// Returns false if the card wasn't actually in the hand.
    /// </summary>
    public bool Play(CardType cardType)
    {
        if (!CardsTypeInHand.Remove(cardType)) return false;

        QueuedCardsType.Enqueue(cardType);
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
        for (int i = LockedCardsType.Count - 1; i >= 0; i--)
        {
            CardType card = LockedCardsType[i];
            if (costs.GetCost(card) <= newMaxMana)
            {
                LockedCardsType.RemoveAt(i);
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
        List<CardType> tail = new List<CardType>(QueuedCardsType.Count + unlocked.Count);

        CardType? next = null;
        if (QueuedCardsType.Count > 0)
        {
            next = QueuedCardsType.Dequeue();
            while (QueuedCardsType.Count > 0)
                tail.Add(QueuedCardsType.Dequeue());
        }

        tail.AddRange(unlocked);
        Shuffle(tail);

        QueuedCardsType.Clear();
        if (next.HasValue)
            QueuedCardsType.Enqueue(next.Value);
        for (int i = 0; i < tail.Count; i++)
            QueuedCardsType.Enqueue(tail[i]);
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
