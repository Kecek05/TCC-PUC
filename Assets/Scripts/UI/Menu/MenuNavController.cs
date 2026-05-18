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

        int clampedStart = Mathf.Clamp(startingPageIndex, 0, Mathf.Max(0, strip.PageCount - 1));
        strip.GoToPage(clampedStart, animated: false);
    }

    private void OnDestroy()
    {
        if (strip != null) strip.OnPageChanged -= HandlePageChanged;

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null && buttons[i].Button != null)
                buttons[i].Button.onClick.RemoveAllListeners();
        }
    }

    private void HandlePageChanged(int newIndex)
    {
        if (newIndex == _activePageIndex) return;

        if (_activePageIndex >= 0)
        {
            if (_activePageIndex < buttons.Count)
                buttons[_activePageIndex].SetSelected(false, animated: true);
            if (_activePageIndex < strip.PageCount && strip.Pages[_activePageIndex] != null)
                strip.Pages[_activePageIndex].OnPageBecameInactive();
        }

        _activePageIndex = newIndex;

        if (_activePageIndex < buttons.Count)
            buttons[_activePageIndex].SetSelected(true, animated: true);
        if (_activePageIndex < strip.PageCount && strip.Pages[_activePageIndex] != null)
            strip.Pages[_activePageIndex].OnPageBecameActive();
    }

    private void OnValidate()
    {
        if (strip != null && buttons.Count != strip.PageCount)
        {
            Debug.LogWarning($"[{nameof(MenuNavController)}] buttons ({buttons.Count}) and strip.PageCount ({strip.PageCount}) differ on '{name}'.", this);
        }
    }
}
