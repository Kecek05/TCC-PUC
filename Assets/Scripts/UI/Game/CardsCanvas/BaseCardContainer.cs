using UnityEngine;

public abstract class BaseCardContainer : MonoBehaviour
{
    public abstract Transform AddCardToSlot(AbstractCard card);

    public abstract void SetNextCard(Sprite image);

    public abstract void SetNextCardNone();

    public abstract void Unoccupy(AbstractCard card);
}
