using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCardContainer : MonoBehaviour
{
    /// <summary>The cards currently holding a hand slot. Read from outside the hand by anything that has
    /// to point at one — the tutorial asking the player to play a tower.</summary>
    public abstract IReadOnlyCollection<AbstractCard> CardsInHand { get; }

    public abstract Transform AddCardToSlot(AbstractCard card);

    public abstract void SetNextCard(Sprite image);

    public abstract void SetNextCardNone();

    public abstract void Unoccupy(AbstractCard card);
}
