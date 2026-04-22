using UnityEngine;

public abstract class BaseClientManaManager : MonoBehaviour
{
    public float PredictedMana { get; protected set; }

    public abstract bool CanAffordLocally(int cost);

    public abstract void PredictSpend(int cost);

    public abstract void ConfirmSpend(int cost);

    public abstract void RevertSpend(int cost);
}
