using UnityEngine;

public class DeckPage : MenuPage
{
    public override void OnPageBecameActive()
    {
        base.OnPageBecameActive();
        Debug.Log("DeckPage became active!");
    }
    
    public override void OnPageBecameInactive()
    {
        base.OnPageBecameInactive();
        Debug.Log("DeckPage became inactive!");
    }
}
