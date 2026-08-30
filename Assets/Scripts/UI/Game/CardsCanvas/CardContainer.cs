using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class CardContainer : BaseCardContainer
{
    [Title("References")]
    [SerializeField] private NextCardSlot nextCardSlot;
    [SerializeField] private List<CardSlot> cardSlots;
    
    private Dictionary<AbstractCard, CardSlot> occupiedSlots = new();

    // Cards drawn while every slot was still taken, oldest first. Drawn -> parked -> placed as soon as
    // the card they are replacing releases its slot.
    private readonly List<AbstractCard> pendingCards = new();

    private void Awake()
    {
        ServiceLocator.Register<BaseCardContainer>(this);
    }
    
    private void OnDestroy()
    {
        ServiceLocator.Unregister<CardContainer>();
    }

    /// <summary>
    /// Assigns a slot to a freshly drawn card, or null when they are all taken. Null is a normal state,
    /// not a failure: the server sends the replacement card before the result that retires the one being
    /// played, so the newcomer waits here and is placed by <see cref="Unoccupy"/>.
    /// </summary>
    public override Transform AddCardToSlot(AbstractCard card)
    {
        CardSlot occupiedSlot = TryOccupySlot();

        if (occupiedSlot == null)
        {
            pendingCards.Add(card);
            GameLog.Info($"No free card slot yet; {card.name} waits for one.");
            return null;
        }

        occupiedSlots[card] = occupiedSlot;
        GameLog.Info($"Card added to slot: {occupiedSlot.name}");
        return occupiedSlot.SlotTransform;
    }

    public override void SetNextCard(Sprite image)
    {
        nextCardSlot.SetNextCardImage(image);
        nextCardSlot.gameObject.SetActive(true);
    }

    public override void SetNextCardNone()
    {
        nextCardSlot.SetNextCardImage(null);
        nextCardSlot.gameObject.SetActive(false);
    }

    public override void Unoccupy(AbstractCard card)
    {
        if (occupiedSlots.Remove(card, out CardSlot slot))
        {
            if (slot != null)
                slot.Unoccupy();
            GameLog.Info($"Card removed from slot: {card.name}");

            PlaceNextPendingCard();
            return;
        }

        // A card that never got a slot can still be retired; it just has nothing to release.
        if (pendingCards.Remove(card)) return;

        GameLog.Warn($"Attempted to unoccupy a slot for a card that is not in the container: {card.name}");
    }

    /// <summary>Hands the slot that just opened to the card that has been waiting longest, if any.</summary>
    private void PlaceNextPendingCard()
    {
        while (pendingCards.Count > 0)
        {
            AbstractCard card = pendingCards[0];
            pendingCards.RemoveAt(0);

            if (card == null) continue; // destroyed while it waited

            CardSlot slot = TryOccupySlot();
            if (slot == null)
            {
                pendingCards.Insert(0, card);
                return;
            }

            occupiedSlots[card] = slot;
            card.PlaceInSlot(slot.SlotTransform);
            return;
        }
    }

    private CardSlot TryOccupySlot()
    {
        foreach (CardSlot slot in cardSlots)
        {
            if (slot.IsOccupied) continue;
            
            if (slot.TryOccupy())
            {
                return slot;
            }
        }
        return null;
    }
}
