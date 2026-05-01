using Sirenix.OdinInspector;
using UnityEngine;

public class ServerCardUIFactory : BaseCardUIFactory
{
    [Title("References")]
    [SerializeField] private AbstractCard cardPrefab;
    [SerializeField] private Transform cardParent;
    
    public override void CreateCardUI(CardType cardType)
    {
        //TODO: Instance a new object, initialize it and move it to the parent.
    }
}
