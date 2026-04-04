using UnityEngine;

public interface ICardActivatable
{
    /// <summary>
    /// Used for without context (in-hand, cooldown, etc)
    /// </summary>
    CardValidation CanPlayCard();

    /// <summary>
    /// Used with placement context (world-space position from camera projection)
    /// </summary>
    CardValidation CanPlayCardAt(Vector2 worldPosition);

    void ActivateCard(Vector2 worldPosition);
}
