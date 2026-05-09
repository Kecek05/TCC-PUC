using UnityEngine;

public class ShopPage : MenuPage
{
    public override void OnPageBecameActive()
    {
        base.OnPageBecameActive();
        Debug.Log("ShopPage became active!");
    }
    
    public override void OnPageBecameInactive()
    {
        base.OnPageBecameInactive();
        Debug.Log("ShopPage became inactive!");
    }
}
