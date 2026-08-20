using System;
using System.Collections.Generic;
using UnityEngine;
// Both System and UnityEngine define Random; this roller deliberately uses the seedable System one.
using Random = System.Random;

/// <summary>
/// Default payout: gold for everyone (jittered so it is not identical every match) and, for the winner, a
/// card drawn by rarity weight. Rarity is picked first and a card uniformly within it, so adding a new
/// Common never dilutes the Legendary chance.
/// </summary>
/// <remarks>
/// Takes a <see cref="System.Random"/> rather than <c>UnityEngine.Random</c>: seedable, so the distribution
/// can be asserted in a test, and it never disturbs the global Unity random sequence mid-match.
/// </remarks>
public class WeightedRewardRoller : IRewardRoller
{
    private readonly RewardSettingsSO _settings;
    private readonly Random _random;

    private readonly List<CardDataSO> _rarityBucket = new();

    public WeightedRewardRoller(RewardSettingsSO settings, Random random = null)
    {
        _settings = settings;
        _random = random ?? new Random();

        if (_settings == null) GameLog.Error($"[{nameof(WeightedRewardRoller)}] No RewardSettingsSO assigned.");
    }

    public MatchReward Roll(bool won)
    {
        if (_settings == null) return MatchReward.GoldOnly(won, 0);

        int gold = RollGold(won);
        if (!won) return MatchReward.GoldOnly(false, gold);

        CardType card = RollCard();
        if (card == CardType.None) return MatchReward.GoldOnly(true, gold);

        return new MatchReward
        {
            Won = true,
            Gold = gold,
            Card = card,
            Copies = Mathf.Max(1, _settings.WinCardCopies)
        };
    }

    private int RollGold(bool won)
    {
        int baseGold = won ? _settings.WinGold : _settings.LoseGold;
        int variance = Mathf.Max(0, _settings.GoldVariance);
        if (variance == 0) return Mathf.Max(0, baseGold);

        // Next(min, maxExclusive) -> +variance must be reachable, hence the +1.
        return Mathf.Max(0, baseGold + _random.Next(-variance, variance + 1));
    }

    private CardType RollCard()
    {
        if (_settings.CardDataList == null || _settings.CardDataList.CardDataList == null) return CardType.None;

        CardRarityType rarity = RollRarity();
        if (rarity == CardRarityType.None) return CardType.None;

        _rarityBucket.Clear();
        foreach (CardDataSO card in _settings.CardDataList.CardDataList)
            if (card != null && card.Rarity == rarity) _rarityBucket.Add(card);

        if (_rarityBucket.Count == 0) return CardType.None;

        return _rarityBucket[_random.Next(_rarityBucket.Count)].CardType;
    }

    /// <summary>Weighted pick over the rarities that actually have at least one card in the pool.</summary>
    private CardRarityType RollRarity()
    {
        float total = 0f;

        foreach (CardRarityType rarity in Enum.GetValues(typeof(CardRarityType)))
        {
            if (!PoolHas(rarity)) continue;
            total += _settings.GetWeight(rarity);
        }

        if (total <= 0f) return CardRarityType.None;

        float roll = (float)_random.NextDouble() * total;

        foreach (CardRarityType rarity in Enum.GetValues(typeof(CardRarityType)))
        {
            if (!PoolHas(rarity)) continue;

            float weight = _settings.GetWeight(rarity);
            if (weight <= 0f) continue;

            roll -= weight;
            if (roll <= 0f) return rarity;
        }

        return CardRarityType.None;
    }

    private bool PoolHas(CardRarityType rarity)
    {
        foreach (CardDataSO card in _settings.CardDataList.CardDataList)
            if (card != null && card.Rarity == rarity) return true;

        return false;
    }
}
