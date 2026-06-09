using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class MenuNavController : MonoBehaviour
{
    [Title("References")]
    [InfoBox("buttons[i] selects strip.Pages[i]. Both lists are index-aligned.")]
    [SerializeField] private HorizontalPageStrip strip;
    [SerializeField] private List<MenuNavButton> buttons = new();

    [Title("Initial State")]
    [SerializeField, MinValue(0)] private int startingPageIndex = 0;

    private int _activePageIndex = -1;
    private int _pendingInactiveIndex = -1;

    private void Start()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            int capturedIndex = i;
            buttons[i].Button.onClick.AddListener(() => strip.GoToPage(capturedIndex, animated: true));
            buttons[i].SetSelected(false, animated: false);
        }
        
        for (int i = 0; i < strip.PageCount; i++)
        {
            if (strip.Pages[i] != null) strip.Pages[i].OnPageBecameInactive();
        }

        strip.OnPageChanged += HandlePageChanged;
        strip.OnPageSettled += HandlePageSettled;

        int clampedStart = Mathf.Clamp(startingPageIndex, 0, Mathf.Max(0, strip.PageCount - 1));
        strip.GoToPage(clampedStart, animated: false);
    }

    private void OnDestroy()
    {
        if (strip != null)
        {
            strip.OnPageChanged -= HandlePageChanged;
            strip.OnPageSettled -= HandlePageSettled;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null && buttons[i].Button != null)
                buttons[i].Button.onClick.RemoveAllListeners();
        }
    }

    // Fires at the start of the snap: activate the incoming page and update button highlights
    // immediately so the nav bar tracks the swipe. The outgoing page's OnPageBecameInactive is
    // deferred to HandlePageSettled so it runs only once the page has slid off-screen.
    private void HandlePageChanged(int newIndex)
    {
        if (newIndex == _activePageIndex) return;

        // Swiping back to a page that is still waiting to be deactivated: it never actually
        // left, so cancel its pending deactivation and don't re-activate it.
        bool returningToPendingPage = newIndex == _pendingInactiveIndex;

        // Any other not-yet-settled page is already off-screen; deactivate it now.
        if (_pendingInactiveIndex >= 0 && _pendingInactiveIndex != newIndex)
            SetPageInactive(_pendingInactiveIndex);
        _pendingInactiveIndex = -1;

        if (_activePageIndex >= 0)
        {
            SetButtonSelected(_activePageIndex, false);
            _pendingInactiveIndex = _activePageIndex; // OnPageBecameInactive runs on settle
        }

        _activePageIndex = newIndex;
        SetButtonSelected(_activePageIndex, true);

        if (!returningToPendingPage)
            SetPageActive(_activePageIndex);
    }

    // Fires when the strip finishes moving: now the outgoing page is off-screen, so deactivate it.
    private void HandlePageSettled(int settledIndex)
    {
        if (settledIndex != _activePageIndex) return;
        if (_pendingInactiveIndex >= 0 && _pendingInactiveIndex != _activePageIndex)
            SetPageInactive(_pendingInactiveIndex);
        _pendingInactiveIndex = -1;
    }

    private void SetButtonSelected(int index, bool selected)
    {
        if (index >= 0 && index < buttons.Count && buttons[index] != null)
            buttons[index].SetSelected(selected, animated: true);
    }

    private void SetPageActive(int index)
    {
        if (index >= 0 && index < strip.PageCount && strip.Pages[index] != null)
            strip.Pages[index].OnPageBecameActive();
    }

    private void SetPageInactive(int index)
    {
        if (index >= 0 && index < strip.PageCount && strip.Pages[index] != null)
            strip.Pages[index].OnPageBecameInactive();
    }

    private void OnValidate()
    {
        if (strip != null && buttons.Count != strip.PageCount)
        {
            Debug.LogWarning($"[{nameof(MenuNavController)}] buttons ({buttons.Count}) and strip.PageCount ({strip.PageCount}) differ on '{name}'.", this);
        }
    }
}
