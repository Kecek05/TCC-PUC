using UnityEngine;

public abstract class MenuPage : MonoBehaviour
{
    public virtual void OnPageBecameActive() { }

    public virtual void OnPageBecameInactive() { }
}
