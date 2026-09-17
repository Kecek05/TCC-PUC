using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rolls the tutorial's payout: always a card, plus exactly what that card's next upgrade costs.
/// </summary>
/// <remarks>
/// Deliberately <b>not</b> an <see cref="IRewardRoller"/>. That interface is win/lose-shaped
/// (<c>Roll(bool won)</c>) because it exists for the match payout, and rarity weighting is the wrong tool
/// here anyway: this payout is shaped by the menu steps that follow it, not by value. The player is about
/// to be told to equip this card and then buy its next level, so both have to be possible the moment they
/// reach the menu — a card already in their deck could not be "equipped", and a card whose upgrade they
/// cannot afford would dead-end the tutorial on its last step.
/// <para>
/// A first-time player always gets a card they have never owned. A save that already owns every card
/// (the tutorial replayed on a developed save) still gets a card rather than a pile of gold: one it owns
/// but keeps out of its deck, preferring one that can still level so the upgrade beat has something to buy.
/// </para>
/// </remarks>
public class TutorialRewardRoller
{
    private readonly TutorialSettingsSO _settings;
    private readonly BasePlayerSaveManager _save;
    private readonly System.Random _random;

    public TutorialRewardRoller(TutorialSettingsSO settings, BasePlayerSaveManager save, System.Random random = null)
    {
        _settings = settings;
        _save = save;
        _random = random ?? new System.Random();
    }

    /// <summary>
    /// The payout, or a gold-only one when no card fits (no settings, or every card the game has is already
    /// in the player's deck). The caller decides what an empty one means.
    /// </summary>
    public Reward Roll()
    {
        if (_settings == null || _settings.CardDataList == null || _save == null)
            return Reward.GoldOnly(RewardSource.Tutorial, 0);

        CardDataSO card = PickCard();

        // Only reachable with a deck that holds the whole collection, but it must not throw: pay gold.
        if (card == null)
        {
            GameLog.Warn("[Tutorial] No card outside the player's deck to give; paying the tutorial reward as gold.");
            return Reward.GoldOnly(RewardSource.Tutorial, Mathf.Max(1, _settings.BonusGold));
        }

        int copies = _settings.BonusCopies;
        int gold = _settings.BonusGold;

        // The level the menu half is about to ask the player to buy: 1 -> 2 for a card being unlocked, the
        // next one up for a card a replayed save already owns. Granting exactly its price (plus the authored
        // surplus) is what guarantees that step can be completed.
        if (TryGetNextStep(card, out CardLevelStep step))
        {
            copies += step.CopiesRequired;
            gold += step.GoldCost;
        }

        return Reward.WithCard(RewardSource.Tutorial, card.CardType, copies, gold);
    }

    /// <summary>
    /// Uniform within the best tier that has anything in it: a locked card (the unlock the tutorial's copy
    /// talks about), then an owned card outside the deck that can still level, then any card outside the
    /// deck. No rarity weighting — every card in the set is a fine first unlock, and a weighted roll would
    /// mostly hand out the Common the player probably started with.
    /// </summary>
    private CardDataSO PickCard()
    {
        List<CardDataSO> locked = new();
        List<CardDataSO> levelable = new();
        List<CardDataSO> outsideDeck = new();

        List<CardType> deck = _save.ActiveDeck?.Cards;

        foreach (CardDataSO card in _settings.CardDataList.CardDataList)
        {
            if (card == null || card.CardType == CardType.None) continue;

            if (!_save.IsCardOwned(card.CardType))
            {
                locked.Add(card);
                continue;
            }

            // The menu half teaches equipping this card; one already in the deck would skip that beat.
            if (deck != null && deck.Contains(card.CardType)) continue;

            outsideDeck.Add(card);
            if (TryGetNextStep(card, out _)) levelable.Add(card);
        }

        return PickFrom(locked) ?? PickFrom(levelable) ?? PickFrom(outsideDeck);
    }

    private CardDataSO PickFrom(List<CardDataSO> candidates) =>
        candidates.Count > 0 ? candidates[_random.Next(candidates.Count)] : null;

    /// <summary>The cost of the card's next level from where the save has it. A locked card counts as level
    /// 1, the level it unlocks at. False at max level, or without a progression table.</summary>
    private bool TryGetNextStep(CardDataSO card, out CardLevelStep step)
    {
        step = default;
        if (_settings.CardProgression == null) return false;

        int level = Mathf.Max(1, _save.GetCardLevel(card.CardType));
        return _settings.CardProgression.TryGetStep(card.Rarity, level, out step);
    }
}
