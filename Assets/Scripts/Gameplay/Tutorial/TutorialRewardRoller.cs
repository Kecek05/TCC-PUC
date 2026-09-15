using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rolls the tutorial's payout: a random card the player does not own yet, plus exactly what its first
/// upgrade costs.
/// </summary>
/// <remarks>
/// Deliberately <b>not</b> an <see cref="IRewardRoller"/>. That interface is win/lose-shaped
/// (<c>Roll(bool won)</c>) because it exists for the match payout, and rarity weighting is the wrong tool
/// here anyway: this payout is shaped by the menu steps that follow it, not by value. The player is about
/// to be told to equip this card and then buy its first level, so both have to be possible the moment they
/// reach the menu — a card they already own could not be "unlocked", and a card whose upgrade they cannot
/// afford would dead-end the tutorial on its last step.
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
    /// The payout, or an empty reward when there is nothing sensible to give (no settings, or the player
    /// somehow owns every card already). The caller decides what an empty one means.
    /// </summary>
    public Reward Roll()
    {
        if (_settings == null || _settings.CardDataList == null || _save == null)
            return Reward.GoldOnly(RewardSource.Tutorial, 0);

        CardDataSO card = PickUnownedCard();

        // Owning everything is only reachable through the debug grants, but it must not throw: pay gold.
        if (card == null)
        {
            GameLog.Warn("[Tutorial] Every card is already owned; paying the tutorial reward as gold.");
            return Reward.GoldOnly(RewardSource.Tutorial, Mathf.Max(1, _settings.BonusGold));
        }

        int copies = 1;
        int gold = _settings.BonusGold;

        // The card unlocks at level 1, so the step the player is about to be walked through is 1 -> 2.
        // Granting exactly its price (plus the authored surplus) is what guarantees the menu half completes.
        if (_settings.CardProgression != null &&
            _settings.CardProgression.TryGetStep(card.Rarity, 1, out CardLevelStep step))
        {
            copies = step.CopiesRequired + _settings.BonusCopies;
            gold = step.GoldCost + _settings.BonusGold;
        }

        return Reward.WithCard(RewardSource.Tutorial, card.CardType, copies, gold);
    }

    /// <summary>Uniform over the locked cards. No rarity weighting: every card in the set is a fine first
    /// unlock, and a weighted roll would mostly hand out the Common the player probably started with.</summary>
    private CardDataSO PickUnownedCard()
    {
        List<CardDataSO> candidates = new();

        foreach (CardDataSO card in _settings.CardDataList.CardDataList)
        {
            if (card == null || card.CardType == CardType.None) continue;
            if (_save.IsCardOwned(card.CardType)) continue;

            candidates.Add(card);
        }

        if (candidates.Count == 0) return null;

        return candidates[_random.Next(candidates.Count)];
    }
}
