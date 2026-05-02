using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class CardContainer : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private List<CardSlot> cardSlots;
    
    private Dictionary<AbstractCard, CardSlot> occupiedSlots = new Dictionary<AbstractCard, CardSlot>();

    private void Awake()
    {
        ServiceLocator.Register<CardContainer>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<CardContainer>();
    }

    public Transform AddCardToSlot(AbstractCard  card)
    {
        CardSlot occupiedSlot = TryOccupySlot();

        if (occupiedSlot == null)
        {
            GameLog.Warn("All card slots are occupied. Cannot add card to container.");
        }
        
        occupiedSlots[card] = occupiedSlot;
        GameLog.Info($"Card added to slot: {occupiedSlot?.name ?? "None"}");
        return occupiedSlot.SlotTransform;
    }

    public void UnoccupySlot(AbstractCard card)
    {
        if (occupiedSlots.Remove(card, out CardSlot slot))
        {
            slot.Unoccupy();
            GameLog.Info($"Card removed from slot: {card.name}");
        }
        else
        {
            GameLog.Warn($"Attempted to unoccupy a slot for a card that is not in the container: {card.name}");
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
