using System.Collections.Generic;

/// <summary>
/// Enumerates hand × scorer candidates, applies score noise, returns the best above threshold.
/// </summary>
public class BotDecisionMaker
{
    private readonly BotScorerRegistry _scorers;

    public BotDecisionMaker(BotScorerRegistry scorers)
    {
        _scorers = scorers;
    }

    public bool TryPickAction(BotContext ctx, out ScoredCandidate best, out CardDataSO bestData)
    {
        best = default;
        bestData = null;
        float bestScore = 0f;

        IReadOnlyList<CardType> hand = ctx.World.Hand();
        float currentMana = ctx.World.SelfMana();
        float noise = ctx.Profile.ScoreNoise;

        for (int i = 0; i < hand.Count; i++)
        {
            CardType card = hand[i];
            CardDataSO data = ctx.World.LookupCard(card);
            if (data == null) continue;
            if (data.Cost > currentMana) continue;

            BaseCardScorer scorer = _scorers.Resolve(data);
            if (scorer == null) continue;

            foreach (ScoredCandidate cand in scorer.Score(data, ctx))
            {
                float jitter = noise > 0f ? ctx.NextFloat(-noise, noise) : 0f;
                float noisy = cand.Score * (1f + jitter);
                if (noisy > bestScore)
                {
                    bestScore = noisy;
                    best = cand.WithScore(noisy);
                    bestData = data;
                }
            }
        }

        return bestScore >= ctx.Profile.MinScoreThreshold;
    }
}
