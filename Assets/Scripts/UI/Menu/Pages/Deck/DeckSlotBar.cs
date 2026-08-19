using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// The row of deck-slot buttons (DeckEntry1..N) above the deck area. Selection visuals reuse
/// <see cref="MenuNavButton"/>, the same component the bottom nav bar uses to highlight the active page.
/// The bar never decides which slot is active - it reports taps and is told what to highlight.
/// </summary>
public class DeckSlotBar : MonoBehaviour
{
    [Title("References")]
    [InfoBox("deckSlotButtons[i] selects deck slot i. Order must match DeckEntry1..N in the hierarchy.")]
    [SerializeField] private List<MenuNavButton> deckSlotButtons = new();

    public event Action<int> OnSlotSelected;

    public int SlotButtonCount => deckSlotButtons.Count;

    public void Initialize(int slotCount, int activeIndex)
    {
        if (deckSlotButtons.Count != slotCount)
            GameLog.Error(
                $"[{nameof(DeckSlotBar)}] {deckSlotButtons.Count} slot buttons wired but the save holds {slotCount} decks.",
                this);

        for (int i = 0; i < deckSlotButtons.Count; i++)
        {
            if (deckSlotButtons[i] == null)
            {
                GameLog.Error($"[{nameof(DeckSlotBar)}] Slot button {i} is not assigned.", this);
                continue;
            }

            int capturedIndex = i;
            deckSlotButtons[i].Button.onClick.AddListener(() => OnSlotSelected?.Invoke(capturedIndex));
        }

        SetSelected(activeIndex, animated: false);
    }

    public void SetSelected(int index, bool animated = true)
    {
        for (int i = 0; i < deckSlotButtons.Count; i++)
        {
            if (deckSlotButtons[i] == null) continue;
            deckSlotButtons[i].SetSelected(i == index, animated);
        }
    }

    private void OnDestroy()
    {
        foreach (MenuNavButton slotButton in deckSlotButtons)
        {
            if (slotButton != null && slotButton.Button != null)
                slotButton.Button.onClick.RemoveAllListeners();
        }
    }
}
