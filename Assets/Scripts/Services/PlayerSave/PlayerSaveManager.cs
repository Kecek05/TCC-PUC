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

    /// <summary>CardType -> progress, rebuilt whenever the owned set changes. Ownership is checked on
    /// every equip and every widget refresh, so a linear scan of the list would not do.</summary>
    private readonly Dictionary<CardType, CardProgressSaveData> _progressByCard = new();

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

    public override int Gold => Data.Gold;

    public override void Load()
    {
        if (_repository == null || !_repository.TryLoad(out _data) || _data == null)
        {
            _data = CreateDefaultSave();
            Persist();
        }
        else if (Normalize(_data))
        {
            // The save drifted from current content (a card was removed, the slot count changed, an older
            // version had no card progression at all). Rewrite it now so the file always matches what the
            // game is actually using.
            Persist();
        }

        RebuildProgressLookup();
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
        if (!IsCardOwned(cardType)) return false;

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

        // The debug hand can name cards the player has not unlocked; grant them rather than silently
        // dropping them, so "use this exact deck" keeps meaning what it says.
        foreach (CardType cardType in deck.Cards) EnsureOwned(cardType);

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

    // ---- Card progression ----------------------------------------------------------------------

    public override bool IsCardOwned(CardType cardType) => GetCardProgress(cardType) != null;

    public override CardProgressSaveData GetCardProgress(CardType cardType)
    {
        if (_progressByCard.Count != Data.Cards.Count) RebuildProgressLookup();
        return _progressByCard.TryGetValue(cardType, out CardProgressSaveData progress) ? progress : null;
    }

    public override int GetCardLevel(CardType cardType) => GetCardProgress(cardType)?.Level ?? 0;

    public override int GetCopiesRequired(CardType cardType)
    {
        CardProgressSaveData progress = GetCardProgress(cardType);
        if (progress == null) return 0;

        return TryGetNextStep(cardType, progress.Level, out CardLevelStep step) ? step.CopiesRequired : 0;
    }

    public override CardUpgradeValidation CanUpgradeCard(CardType cardType)
    {
        CardProgressSaveData progress = GetCardProgress(cardType);
        if (progress == null) return CardUpgradeValidation.Invalid(CardUpgradeInvalidReason.NotOwned);

        if (!TryGetNextStep(cardType, progress.Level, out CardLevelStep step))
            return CardUpgradeValidation.Invalid(CardUpgradeInvalidReason.MaxLevel);

        if (progress.Copies < step.CopiesRequired)
            return CardUpgradeValidation.Invalid(CardUpgradeInvalidReason.NotEnoughCopies,
                step.CopiesRequired, step.GoldCost);

        if (Data.Gold < step.GoldCost)
            return CardUpgradeValidation.Invalid(CardUpgradeInvalidReason.NotEnoughGold,
                step.CopiesRequired, step.GoldCost);

        return CardUpgradeValidation.Valid(step.CopiesRequired, step.GoldCost);
    }

    public override bool TryUpgradeCard(CardType cardType)
    {
        CardUpgradeValidation validation = CanUpgradeCard(cardType);
        if (!validation) return false;

        CardProgressSaveData progress = GetCardProgress(cardType);

        // Spend exactly what the step costs; surplus copies carry over to the next level.
        progress.Copies -= validation.CopiesRequired;
        progress.Level++;
        Data.Gold -= validation.GoldCost;

        Persist();

        RaiseCardProgressChanged(cardType);
        if (validation.GoldCost != 0) RaiseGoldChanged();

        return true;
    }

    public override void GrantReward(MatchReward reward)
    {
        bool goldMoved = false;

        if (reward.Gold != 0)
        {
            Data.Gold = Mathf.Max(0, Data.Gold + reward.Gold);
            goldMoved = true;
        }

        if (reward.HasCard) GrantCopies(reward.Card, reward.Copies);

        Persist();

        if (goldMoved) RaiseGoldChanged();
        if (reward.HasCard) RaiseCardProgressChanged(reward.Card);

        GameLog.Info($"[PlayerSaveManager] Reward banked - {reward}. Gold is now {Data.Gold}.");
    }

    public override void AddGold(int amount)
    {
        if (amount == 0) return;

        Data.Gold = Mathf.Max(0, Data.Gold + amount);
        Persist();
        RaiseGoldChanged();
    }

    public override void AddCardCopies(CardType cardType, int copies)
    {
        if (cardType == CardType.None || copies <= 0) return;
        if (!IsKnownCard(cardType)) return;

        GrantCopies(cardType, copies);
        Persist();
        RaiseCardProgressChanged(cardType);
    }

    /// <summary>Unlocks the card at level 1 if it was locked, then banks the copies. No copy is spent
    /// unlocking, so the first drop of a card is pure upside.</summary>
    private void GrantCopies(CardType cardType, int copies)
    {
        CardProgressSaveData progress = EnsureOwned(cardType);
        if (progress == null) return;

        progress.Copies = Mathf.Max(0, progress.Copies + copies);
    }

    /// <summary>Returns the card's progress, creating a level-1 entry if it was still locked.</summary>
    private CardProgressSaveData EnsureOwned(CardType cardType)
    {
        if (cardType == CardType.None || !IsKnownCard(cardType)) return null;

        CardProgressSaveData progress = GetCardProgress(cardType);
        if (progress != null) return progress;

        progress = new CardProgressSaveData { CardType = cardType, Level = 1, Copies = 0 };
        Data.Cards.Add(progress);
        _progressByCard[cardType] = progress;

        GameLog.Info($"[PlayerSaveManager] Unlocked {cardType} at level 1.");
        return progress;
    }

    private bool TryGetNextStep(CardType cardType, int fromLevel, out CardLevelStep step)
    {
        step = default;

        CardProgressionSettingsSO progression = _settings != null ? _settings.CardProgression : null;
        if (progression == null)
        {
            GameLog.Error("[PlayerSaveManager] No CardProgressionSettingsSO assigned; upgrades are disabled.");
            return false;
        }

        CardDataSO card = ResolveCard(cardType);
        if (card == null) return false;

        return progression.TryGetStep(card.Rarity, fromLevel, out step);
    }

    private CardDataSO ResolveCard(CardType cardType) =>
        _settings != null && _settings.CardDataList != null
            ? _settings.CardDataList.GetCardDataByType(cardType)
            : null;

    private void RebuildProgressLookup() => RebuildProgressLookupFor(Data);

    private PlayerSaveData Data
    {
        get
        {
            if (_data == null)
            {
                GameLog.Warn("[PlayerSaveManager] Accessed before Load(); falling back to a default save.");
                _data = CreateDefaultSave();
                RebuildProgressLookup();
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

        // Slot 1 starts playable - a fresh player would otherwise be unable to press Battle at all - and
        // those same cards are the ones they own. Everything else starts locked.
        if (_settings != null && _settings.StarterDeck != null)
        {
            data.Decks[0].Cards = new List<CardType>(_settings.StarterDeck);
            foreach (CardType cardType in _settings.StarterDeck)
                data.Cards.Add(new CardProgressSaveData { CardType = cardType, Level = 1, Copies = 0 });
        }

        data.Gold = _settings != null ? Mathf.Max(0, _settings.StartingGold) : 0;

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
    /// Forces the save into a shape the game can trust: a valid owned-card set first (decks depend on it),
    /// then exactly <c>DeckSlotCount</c> named slots holding only owned, non-duplicated cards, an in-range
    /// active index and non-negative gold. Returns true when anything moved.
    /// </summary>
    private bool Normalize(PlayerSaveData data)
    {
        bool changed = NormalizeCards(data);

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

        if (data.Gold < 0)
        {
            data.Gold = 0;
            changed = true;
        }

        if (data.SaveVersion != PlayerSaveData.CurrentVersion)
        {
            data.SaveVersion = PlayerSaveData.CurrentVersion;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Drops progress for cards that no longer exist, de-duplicates it, clamps levels to the rarity cap,
    /// and migrates a v1 (deck-only) save by granting whatever its decks already reference.
    /// </summary>
    private bool NormalizeCards(PlayerSaveData data)
    {
        bool changed = false;

        if (data.Cards == null)
        {
            data.Cards = new List<CardProgressSaveData>();
            changed = true;
        }

        HashSet<CardType> seen = new();
        int write = 0;

        for (int read = 0; read < data.Cards.Count; read++)
        {
            CardProgressSaveData progress = data.Cards[read];

            if (progress == null) continue;
            if (progress.CardType == CardType.None) continue;
            if (!seen.Add(progress.CardType)) continue;
            if (!IsKnownCard(progress.CardType)) continue;

            int maxLevel = GetMaxLevel(progress.CardType);
            int clampedLevel = Mathf.Clamp(progress.Level, 1, maxLevel);
            if (clampedLevel != progress.Level)
            {
                progress.Level = clampedLevel;
                changed = true;
            }

            if (progress.Copies < 0)
            {
                progress.Copies = 0;
                changed = true;
            }

            data.Cards[write++] = progress;
        }

        if (write != data.Cards.Count)
        {
            data.Cards.RemoveRange(write, data.Cards.Count - write);
            changed = true;
        }

        // Migration / recovery: a v1 save (or a corrupted one) has no ownership at all. Grant the starter
        // deck plus anything the player's saved decks already reference, so nobody loses a deck they built.
        if (data.Cards.Count == 0)
        {
            foreach (CardType cardType in CollectSeedOwnership(data))
            {
                if (!seen.Add(cardType)) continue;
                data.Cards.Add(new CardProgressSaveData { CardType = cardType, Level = 1, Copies = 0 });
                changed = true;
            }

            if (data.SaveVersion < PlayerSaveData.CurrentVersion && data.Gold == 0 && _settings != null)
            {
                data.Gold = Mathf.Max(0, _settings.StartingGold);
                changed = true;
            }
        }

        // Ownership drives every later check, so the lookup has to be current before decks are sanitized.
        RebuildProgressLookupFor(data);

        return changed;
    }

    private IEnumerable<CardType> CollectSeedOwnership(PlayerSaveData data)
    {
        if (_settings != null && _settings.StarterDeck != null)
            foreach (CardType cardType in _settings.StarterDeck)
                if (IsKnownCard(cardType)) yield return cardType;

        if (data.Decks == null) yield break;

        foreach (DeckSaveData deck in data.Decks)
        {
            if (deck?.Cards == null) continue;

            foreach (CardType cardType in deck.Cards)
                if (IsKnownCard(cardType)) yield return cardType;
        }
    }

    private void RebuildProgressLookupFor(PlayerSaveData data)
    {
        _progressByCard.Clear();

        foreach (CardProgressSaveData progress in data.Cards)
            if (progress != null) _progressByCard[progress.CardType] = progress;
    }

    private int GetMaxLevel(CardType cardType)
    {
        CardProgressionSettingsSO progression = _settings != null ? _settings.CardProgression : null;
        if (progression == null) return 1;

        CardDataSO card = ResolveCard(cardType);
        return card == null ? 1 : progression.GetMaxLevel(card.Rarity);
    }

    /// <summary>Drops None, duplicates, unowned cards and cards that no longer exist, then trims to
    /// <see cref="DeckSize"/>.</summary>
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
            if (!_progressByCard.ContainsKey(card)) continue;
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
