using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class DeckPage : MenuPage
{
    [Title("References")]
    [SerializeField] private ScrollRect scrollRect;

    public override void OnPageBecameActive()
    {
        base.OnPageBecameActive();
        Debug.Log("DeckPage became active!");
    }
    
    public override void OnPageBecameInactive()
    {
        base.OnPageBecameInactive();
        Debug.Log("DeckPage became inactive!");
        ResetScrollToTop();
    }

    private void ResetScrollToTop()
    {
        if (scrollRect == null) return;
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }
}
