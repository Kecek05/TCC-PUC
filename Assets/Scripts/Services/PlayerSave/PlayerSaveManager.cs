using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Default <see cref="BasePlayerSaveManager"/>: keeps the save in memory, persists every mutation through
/// an <see cref="IPlayerSaveRepository"/> (the file is a few hundred bytes and edits are rare, so there is
/// nothing to gain from batching), and normalizes whatever it reads back so the rest of the game can trust
/// the shape of the data.
/// </summary>
public class PlayerSaveManager : BasePlayerSaveManager
{
    private const int FallbackDeckSize = 8;

    private readonly PlayerSaveSettingsSO _settings;
    private readonly IPlayerSaveRepository _repository;

    private PlayerSaveData _data;

    public PlayerSaveManager(PlayerSaveSettingsSO settings, IPlayerSaveRepository repository)
    {
        _settings = settings;
        _repository = repository;

        if (_settings == null) GameLog.Error("[PlayerSaveManager] No PlayerSaveSettingsSO assigned.");
        if (_repository == null) GameLog.Error("[PlayerSaveManager] No IPlayerSaveRepository assigned.");
    }

    public override int DeckSlotCount => Data.Decks.Count;

    public override int DeckSize =>
        _settings != null && _settings.CardHandSettings != null
            ? _settings.CardHandSettings.DeckSize
            : FallbackDeckSize;

    public override int ActiveDeckIndex => Data.ActiveDeckIndex;

    public override DeckSaveData ActiveDeck => Data.Decks[Data.ActiveDeckIndex];

    public override bool IsActiveDeckFull => ActiveDeck.Cards.Count >= DeckSize;

    public override CardSortKey SortKey => Data.SortKey;

    public override bool SortAscending => Data.SortAscending;

    public override void Load()
    {
        if (_repository == null || !_repository.TryLoad(out _data) || _data == null)
        {
            _data = CreateDefaultSave();
            Persist();
        }
        else if (Normalize(_data))
        {
            // The save drifted from current content (a card was removed, the slot count changed, ...).
            // Rewrite it now so the file always matches what the game is actually using.
            Persist();
        }

        RaiseActiveDeckContentChanged(ActiveDeck);
    }

    public override DeckSaveData GetDeck(int index)
    {
        if (index < 0 || index >= Data.Decks.Count)
        {
            GameLog.Error($"[PlayerSaveManager] Deck slot {index} out of range (0..{Data.Decks.Count - 1}).");
            return null;
        }

        return Data.Decks[index];
    }

    public override void SetActiveDeck(int index)
    {
        int clamped = Mathf.Clamp(index, 0, Data.Decks.Count - 1);
        if (clamped != index)
            GameLog.Warn($"[PlayerSaveManager] Deck slot {index} out of range; clamped to {clamped}.");

        if (clamped == Data.ActiveDeckIndex) return;

        Data.ActiveDeckIndex = clamped;
        Persist();

        RaiseActiveDeckSlotChanged(clamped);
        RaiseActiveDeckContentChanged(ActiveDeck);
    }

    public override bool TryEquipCard(CardType cardType)
    {
        if (cardType == CardType.None) return false;

        DeckSaveData deck = ActiveDeck;
        if (deck.Cards.Count >= DeckSize) return false;
        if (deck.Cards.Contains(cardType)) return false;

        deck.Cards.Add(cardType);
        Persist();

        RaiseActiveDeckContentChanged(deck);
        return true;
    }

    public override void UnequipCard(CardType cardType)
    {
        DeckSaveData deck = ActiveDeck;
        if (!deck.Cards.Remove(cardType)) return;

        Persist();
        RaiseActiveDeckContentChanged(deck);
    }

    public override void SetDeckCards(int index, List<CardType> cards)
    {
        DeckSaveData deck = GetDeck(index);
        if (deck == null) return;

        // Copy: the save must never alias a caller's list (a ScriptableObject's, in the debug-hand case).
        deck.Cards = cards == null ? new List<CardType>() : new List<CardType>(cards);
        SanitizeCards(deck.Cards);
        Persist();

        if (index == Data.ActiveDeckIndex) RaiseActiveDeckContentChanged(deck);
    }

    public override void SetSortPreference(CardSortKey key, bool ascending)
    {
        if (Data.SortKey == key && Data.SortAscending == ascending) return;

        Data.SortKey = key;
        Data.SortAscending = ascending;
        Persist();
    }

    private PlayerSaveData Data
    {
        get
        {
            if (_data == null)
            {
                GameLog.Warn("[PlayerSaveManager] Accessed before Load(); falling back to a default save.");
                _data = CreateDefaultSave();
            }

            return _data;
        }
    }

    private void Persist() => _repository?.Save(_data);

    private PlayerSaveData CreateDefaultSave()
    {
        PlayerSaveData data = new PlayerSaveData();

        int slots = _settings != null ? Mathf.Max(1, _settings.DeckSlotCount) : 1;
        for (int i = 0; i < slots; i++) data.Decks.Add(CreateEmptyDeck(i));

        // Slot 1 starts playable - a fresh player would otherwise be unable to press Battle at all.
        if (_settings != null && _settings.StarterDeck != null)
            data.Decks[0].Cards = new List<CardType>(_settings.StarterDeck);

        Normalize(data);
        return data;
    }

    private DeckSaveData CreateEmptyDeck(int index) => new DeckSaveData { Name = DeckName(index) };

    private string DeckName(int index)
    {
        string format = _settings != null && !string.IsNullOrEmpty(_settings.DeckNameFormat)
            ? _settings.DeckNameFormat
            : "Deck {0}";

        return string.Format(format, index + 1);
    }

    /// <summary>
    /// Forces the save into a shape the game can trust: exactly <c>DeckSlotCount</c> named slots, no unknown
    /// or duplicated cards, no over-long deck, and an in-range active index. True when anything moved.
    /// </summary>
    private bool Normalize(PlayerSaveData data)
    {
        bool changed = false;

        if (data.Decks == null)
        {
            data.Decks = new List<DeckSaveData>();
            changed = true;
        }

        int slots = _settings != null ? Mathf.Max(1, _settings.DeckSlotCount) : Mathf.Max(1, data.Decks.Count);

        while (data.Decks.Count > slots)
        {
            data.Decks.RemoveAt(data.Decks.Count - 1);
            changed = true;
        }

        while (data.Decks.Count < slots)
        {
            data.Decks.Add(CreateEmptyDeck(data.Decks.Count));
            changed = true;
        }

        for (int i = 0; i < data.Decks.Count; i++)
        {
            DeckSaveData deck = data.Decks[i];

            if (deck == null)
            {
                data.Decks[i] = CreateEmptyDeck(i);
                changed = true;
                continue;
            }

            if (string.IsNullOrEmpty(deck.Name))
            {
                deck.Name = DeckName(i);
                changed = true;
            }

            if (deck.Cards == null)
            {
                deck.Cards = new List<CardType>();
                changed = true;
            }

            changed |= SanitizeCards(deck.Cards);
        }

        int clampedIndex = Mathf.Clamp(data.ActiveDeckIndex, 0, data.Decks.Count - 1);
        if (clampedIndex != data.ActiveDeckIndex)
        {
            data.ActiveDeckIndex = clampedIndex;
            changed = true;
        }

        if (data.SaveVersion != PlayerSaveData.CurrentVersion)
        {
            data.SaveVersion = PlayerSaveData.CurrentVersion;
            changed = true;
        }

        return changed;
    }

    /// <summary>Drops None, duplicates and cards that no longer exist, then trims to <see cref="DeckSize"/>.</summary>
    private bool SanitizeCards(List<CardType> cards)
    {
        HashSet<CardType> seen = new HashSet<CardType>();
        int write = 0;

        for (int read = 0; read < cards.Count; read++)
        {
            CardType card = cards[read];

            if (card == CardType.None) continue;
            if (!seen.Add(card)) continue;
            if (!IsKnownCard(card)) continue;
            if (write >= DeckSize) continue;

            cards[write++] = card;
        }

        if (write == cards.Count) return false;

        cards.RemoveRange(write, cards.Count - write);
        return true;
    }

    private bool IsKnownCard(CardType cardType)
    {
        // Unwired settings must not silently wipe the player's decks.
        if (_settings == null || _settings.CardDataList == null) return true;

        return _settings.CardDataList.GetCardDataByType(cardType) != null;
    }
}
