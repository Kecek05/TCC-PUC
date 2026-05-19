using UnityEngine;

public class BattlePage : MenuPage
{
    public override void OnPageBecameActive()
    {
        base.OnPageBecameActive();
        Debug.Log("BattlePage became active!");
    }
    
    public override void OnPageBecameInactive()
    {
        base.OnPageBecameInactive();
        Debug.Log("BattlePage became inactive!");
    }
}
