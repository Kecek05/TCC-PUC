using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class MenuNavController : MonoBehaviour
{
    [Title("References")]
    [InfoBox("Pages and buttons must be index-aligned: buttons[i] selects pages[i].")]
    [SerializeField] private HorizontalPageScroller scroller;
    [SerializeField] private List<MenuNavButton> buttons = new List<MenuNavButton>();
    [SerializeField] private List<MenuPage> pages = new List<MenuPage>();

    private int _activePageIndex = -1;

    private void Start()
    {
        scroller.OnPageChanged += Scroller_OnPageChanged;

        for (int i = 0; i < buttons.Count; i++)
        {
            int capturedIndex = i;
            buttons[i].Button.onClick.AddListener(() => scroller.GoToPage(capturedIndex, true));
            buttons[i].SetSelected(false, animated: false);
        }

        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].OnPageBecameInactive();
        }

        ApplyPageChange(scroller.CurrentPageIndex, animated: false);
    }

    private void OnDestroy()
    {
        if (scroller != null)
        {
            scroller.OnPageChanged -= Scroller_OnPageChanged;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null && buttons[i].Button != null)
            {
                buttons[i].Button.onClick.RemoveAllListeners();
            }
        }
    }

    private void Scroller_OnPageChanged(int newIndex)
    {
        ApplyPageChange(newIndex, animated: true);
    }

    private void ApplyPageChange(int newIndex, bool animated)
    {
        if (newIndex == _activePageIndex) return;

        if (_activePageIndex >= 0 && _activePageIndex < buttons.Count)
        {
            buttons[_activePageIndex].SetSelected(false, animated);
        }
        if (_activePageIndex >= 0 && _activePageIndex < pages.Count)
        {
            pages[_activePageIndex].OnPageBecameInactive();
        }

        _activePageIndex = newIndex;

        if (_activePageIndex >= 0 && _activePageIndex < buttons.Count)
        {
            buttons[_activePageIndex].SetSelected(true, animated);
        }
        if (_activePageIndex >= 0 && _activePageIndex < pages.Count)
        {
            pages[_activePageIndex].OnPageBecameActive();
        }
    }

    private void OnValidate()
    {
        if (buttons.Count != pages.Count)
        {
            Debug.LogWarning($"[{nameof(MenuNavController)}] buttons ({buttons.Count}) and pages ({pages.Count}) counts differ on '{name}'. They must match.", this);
        }
    }
}
