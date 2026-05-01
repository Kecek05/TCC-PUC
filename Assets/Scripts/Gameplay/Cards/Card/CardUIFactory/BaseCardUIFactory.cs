using UnityEngine;

public abstract class BaseCardUIFactory : MonoBehaviour
{
    public abstract void CreateCardUI(CardType cardType);
}
