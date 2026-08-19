using System;
using System.Collections.Generic;

/// <summary>
/// Owns the player's persistent deck slots and collection-sort preference, and is the single source of
/// truth for deck rules (slot count, deck size, no duplicates). The deck UI only presents what this
/// exposes; <c>UserData.DeckCards</c> is a mirror of <see cref="ActiveDeck"/>, refreshed from
/// <see cref="OnActiveDeckContentChanged"/>.
/// </summary>
public abstract class BasePlayerSaveManager
{
    /// <summary>The selected slot changed. The deck page rebuilds its layout from the new deck.</summary>
    public event Action<int> OnActiveDeckSlotChanged;

    /// <summary>The active deck's cards changed — by a slot switch or by an edit. <c>UserData</c> mirrors this.</summary>
    public event Action<DeckSaveData> OnActiveDeckContentChanged;

    public abstract int DeckSlotCount { get; }

    /// <summary>Cards a deck must hold to be playable (from <c>CardHandSettingsSO.DeckSize</c>).</summary>
    public abstract int DeckSize { get; }

    public abstract int ActiveDeckIndex { get; }

    public abstract DeckSaveData ActiveDeck { get; }

    public abstract bool IsActiveDeckFull { get; }

    public abstract CardSortKey SortKey { get; }

    public abstract bool SortAscending { get; }

    /// <summary>Hydrates from storage (or creates a default save) and raises
    /// <see cref="OnActiveDeckContentChanged"/> once. Must run before the Main Menu scene loads.</summary>
    public abstract void Load();

    public abstract DeckSaveData GetDeck(int index);

    public abstract void SetActiveDeck(int index);

    /// <summary>Adds a card to the active deck. Returns false when the deck is full or already holds it.</summary>
    public abstract bool TryEquipCard(CardType cardType);

    public abstract void UnequipCard(CardType cardType);

    /// <summary>Replaces a slot's cards wholesale (used by the debug-hand override).</summary>
    public abstract void SetDeckCards(int index, List<CardType> cards);

    public abstract void SetSortPreference(CardSortKey key, bool ascending);

    protected void RaiseActiveDeckSlotChanged(int index) => OnActiveDeckSlotChanged?.Invoke(index);

    protected void RaiseActiveDeckContentChanged(DeckSaveData deck) => OnActiveDeckContentChanged?.Invoke(deck);
}
