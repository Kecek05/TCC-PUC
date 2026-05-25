using System.Collections.Generic;
using UnityEngine;

public class SpawnEnemyCardScorer : BaseCardScorer
{
    public override bool CanHandle(CardDataSO data) => data is SpawnEnemyCardDataSO;

    public override IEnumerable<ScoredCandidate> Score(CardDataSO data, BotContext ctx)
    {
        if (data is not SpawnEnemyCardDataSO spawnCard) yield break;

        float weight = ctx.Profile.AggressionWeight * ctx.Profile.GetCardWeight(spawnCard.CardType);

        float baseScore = 1f / Mathf.Max(1f, spawnCard.Cost);

        // Press advantage when opponent is mana-starved.
        float enemyMaxMana = ctx.World.EnemyMaxMana();
        float enemyManaFrac = enemyMaxMana > 0f ? ctx.World.EnemyMana() / enemyMaxMana : 1f;
        float manaPressure = enemyManaFrac < 0.3f ? 1.5f : 1f;

        // Back off when our own base is under heavy pressure (better to defend than attack).
        float selfHpFrac = ComputeSelfHpFrac(ctx);
        float defensiveBrake = selfHpFrac > 0.6f ? 1f : 0.5f;

        float score = baseScore * manaPressure * defensiveBrake * weight;
        if (score <= 0f) yield break;

        yield return new ScoredCandidate(spawnCard.CardType, Vector2.zero, score);
    }

    private static float ComputeSelfHpFrac(BotContext ctx)
    {
        // No "max HP" accessor on the manager. Assume starting health is a sane reference:
        // the manager initialises BlueHealth/RedHealth to the same starting value, and a fraction
        // can be inferred only relative to current opponent if we ever expose max. For now use a
        // simple ratio of self vs sum, which captures "I'm losing harder than them".
        float self = Mathf.Max(0f, ctx.World.SelfBaseHp());
        float enemy = Mathf.Max(0f, ctx.World.EnemyBaseHp());
        float total = self + enemy;
        return total > 0f ? self / total : 1f;
    }
}
