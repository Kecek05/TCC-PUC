using System;
using System.Collections.Generic;

/// <summary>
/// Owns everything persistent about the player: deck slots, collection-sort preference, gold, and which
/// cards are owned and how far each has levelled. It is the single source of truth for the rules too —
/// deck size, no duplicates, whether an upgrade is affordable — so the UI only ever presents what this
/// exposes. <c>UserData.DeckCards</c> is a mirror of <see cref="ActiveDeck"/>, refreshed from
/// <see cref="OnActiveDeckContentChanged"/>.
/// </summary>
public abstract class BasePlayerSaveManager
{
    /// <summary>The selected slot changed. The deck page rebuilds its layout from the new deck.</summary>
    public event Action<int> OnActiveDeckSlotChanged;

    /// <summary>The active deck's cards changed — by a slot switch or by an edit. <c>UserData</c> mirrors this.</summary>
    public event Action<DeckSaveData> OnActiveDeckContentChanged;

    /// <summary>Gold moved: spent on an upgrade or earned from a match.</summary>
    public event Action OnGoldChanged;

    /// <summary>One card's level or copy count moved, or it was just unlocked.</summary>
    public event Action<CardType> OnCardProgressChanged;

    public abstract int DeckSlotCount { get; }

    /// <summary>Cards a deck must hold to be playable (from <c>CardHandSettingsSO.DeckSize</c>).</summary>
    public abstract int DeckSize { get; }

    public abstract int ActiveDeckIndex { get; }

    public abstract DeckSaveData ActiveDeck { get; }

    public abstract bool IsActiveDeckFull { get; }

    public abstract CardSortKey SortKey { get; }

    public abstract bool SortAscending { get; }

    public abstract int Gold { get; }

    /// <summary>Hydrates from storage (or creates a default save) and raises
    /// <see cref="OnActiveDeckContentChanged"/> once. Must run before the Main Menu scene loads.</summary>
    public abstract void Load();

    public abstract DeckSaveData GetDeck(int index);

    public abstract void SetActiveDeck(int index);

    /// <summary>Adds a card to the active deck. False when unowned, already in the deck, or the deck is full.</summary>
    public abstract bool TryEquipCard(CardType cardType);

    public abstract void UnequipCard(CardType cardType);

    /// <summary>Replaces a slot's cards wholesale (used by the debug-hand override).</summary>
    public abstract void SetDeckCards(int index, List<CardType> cards);

    public abstract void SetSortPreference(CardSortKey key, bool ascending);

    // ---- Card progression ----------------------------------------------------------------------

    /// <summary>A card is owned exactly when the save holds progress for it.</summary>
    public abstract bool IsCardOwned(CardType cardType);

    /// <summary>Null when the card is still locked.</summary>
    public abstract CardProgressSaveData GetCardProgress(CardType cardType);

    /// <summary>0 when the card is still locked, otherwise 1..max.</summary>
    public abstract int GetCardLevel(CardType cardType);

    /// <summary>Copies needed to reach the next level, 0 at max level. For the <c>owned/needed</c> label.</summary>
    public abstract int GetCopiesRequired(CardType cardType);

    /// <summary>Whether the next level is affordable right now, and what it would cost.</summary>
    public abstract CardUpgradeValidation CanUpgradeCard(CardType cardType);

    /// <summary>Spends the copies and gold and raises the level. False if <see cref="CanUpgradeCard"/> refuses.</summary>
    public abstract bool TryUpgradeCard(CardType cardType);

    /// <summary>Banks a finished match: gold, copies, and unlocking a brand-new card at level 1.</summary>
    public abstract void GrantReward(MatchReward reward);

    /// <summary>Editor/debug affordance. Negative amounts are clamped at 0 gold.</summary>
    public abstract void AddGold(int amount);

    /// <summary>Editor/debug affordance: hand a card copies, unlocking it if it was locked.</summary>
    public abstract void AddCardCopies(CardType cardType, int copies);

    protected void RaiseActiveDeckSlotChanged(int index) => OnActiveDeckSlotChanged?.Invoke(index);

    protected void RaiseActiveDeckContentChanged(DeckSaveData deck) => OnActiveDeckContentChanged?.Invoke(deck);

    protected void RaiseGoldChanged() => OnGoldChanged?.Invoke();

    protected void RaiseCardProgressChanged(CardType cardType) => OnCardProgressChanged?.Invoke(cardType);
}
