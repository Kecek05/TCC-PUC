using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class MenuNavController : MonoBehaviour
{
    [Title("References")]
    [InfoBox("Pages and buttons must be index-aligned: buttons[i] selects pages[i].")]
    [SerializeField] private List<MenuNavButton> buttons = new List<MenuNavButton>();
    [SerializeField] private List<MenuPage> pages = new List<MenuPage>();

    [Title("Initial State")]
    [SerializeField] private int startingPageIndex = 0;

    private int _activePageIndex = -1;

    private void Start()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            int capturedIndex = i;
            buttons[i].Button.onClick.AddListener(() => GoToPage(capturedIndex));
            buttons[i].SetSelected(false, animated: false);
        }

        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].gameObject.SetActive(false);
        }

        GoToPage(Mathf.Clamp(startingPageIndex, 0, Mathf.Max(0, pages.Count - 1)));
    }

    private void OnDestroy()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null && buttons[i].Button != null)
                buttons[i].Button.onClick.RemoveAllListeners();
        }
    }

    public void GoToPage(int index)
    {
        if (index < 0 || index >= pages.Count) return;
        if (index == _activePageIndex) return;

        if (_activePageIndex >= 0)
        {
            if (_activePageIndex < buttons.Count)
                buttons[_activePageIndex].SetSelected(false, animated: true);
            if (_activePageIndex < pages.Count)
            {
                pages[_activePageIndex].OnPageBecameInactive();
                pages[_activePageIndex].gameObject.SetActive(false);
            }
        }

        _activePageIndex = index;

        if (_activePageIndex < buttons.Count)
            buttons[_activePageIndex].SetSelected(true, animated: true);

        pages[_activePageIndex].gameObject.SetActive(true);
        pages[_activePageIndex].OnPageBecameActive();
    }

    private void OnValidate()
    {
        if (buttons.Count != pages.Count)
        {
            Debug.LogWarning($"[{nameof(MenuNavController)}] buttons ({buttons.Count}) and pages ({pages.Count}) counts differ on '{name}'. They must match.", this);
        }
    }
}
