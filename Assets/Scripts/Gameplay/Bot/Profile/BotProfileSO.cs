using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "BotProfile", menuName = "Scriptable Objects/Bot/BotProfileSO")]
public class BotProfileSO : ScriptableObject
{
    [Title("Cadence")]
    [Tooltip("Seconds between bot decision ticks. Higher = bot 'thinks' slower.")]
    [MinValue(0.1f)] public float DecisionTickRate = 0.5f;

    [Tooltip("Extra delay between picking an action and executing it. Humanises the bot.")]
    [MinValue(0f)] public float ReactionDelaySeconds = 0.3f;

    [Title("Spending")]
    [Tooltip("Don't spend if remaining mana after the spend would dip below this. Keeps a reserve.")]
    [MinValue(0)] public int ManaSpendFloor = 2;

    [Title("Scoring")]
    [Tooltip("Below this score, the bot waits instead of playing.")]
    [MinValue(0f)] public float MinScoreThreshold = 0.05f;

    [Tooltip("Per-candidate score noise, applied multiplicatively. 0.15 = ±15% jitter.")]
    [Range(0f, 1f)] public float ScoreNoise = 0.15f;

    [Tooltip("Biases SpawnEnemy + offensive spell scores. >1 = more aggressive.")]
    [MinValue(0f)] public float AggressionWeight = 1f;

    [Tooltip("Biases tower placement + defensive spell scores. >1 = more defensive.")]
    [MinValue(0f)] public float DefenseWeight = 1f;

    [Title("Per-card overrides")]
    [Tooltip("Multipliers applied per CardType on top of the role weights. Default (no entry) = 1.")]
    public List<CardWeightOverride> PerCardWeightOverrides = new();

    [Title("Combinatorics")]
    [Tooltip("Cap on candidate positions evaluated per card per tick. Bounds CPU work.")]
    [MinValue(1)] public int MaxCandidatePositionsPerCard = 8;

    public float GetCardWeight(CardType card)
    {
        for (int i = 0; i < PerCardWeightOverrides.Count; i++)
        {
            if (PerCardWeightOverrides[i].Card == card)
                return PerCardWeightOverrides[i].Weight;
        }
        return 1f;
    }
}

[Serializable]
public struct CardWeightOverride
{
    public CardType Card;
    [MinValue(0f)] public float Weight;
}
