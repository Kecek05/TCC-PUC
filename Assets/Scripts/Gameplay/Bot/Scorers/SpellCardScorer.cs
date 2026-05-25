using System.Collections.Generic;
using UnityEngine;

public class SpellCardScorer : BaseCardScorer
{
    public override bool CanHandle(CardDataSO data) => data is SpellCardDataSO;

    public override IEnumerable<ScoredCandidate> Score(CardDataSO data, BotContext ctx)
    {
        if (data is not SpellCardDataSO spellCard) yield break;
        SpellDataSO spell = spellCard.SpellData;
        if (spell == null) yield break;

        float perCardWeight = ctx.Profile.GetCardWeight(spellCard.CardType);

        if (spellCard.CanUseInLocalMap)
        {
            foreach (var c in ScoreOverMap(spellCard, spell, ctx.World.EnemiesOnSelfMap(), ctx, isDefense: true, perCardWeight))
                yield return c;
        }

        if (spellCard.CanUseInEnemyMap)
        {
            foreach (var c in ScoreOverMap(spellCard, spell, ctx.World.EnemiesOnEnemyMap(), ctx, isDefense: false, perCardWeight))
                yield return c;
        }
    }

    private static IEnumerable<ScoredCandidate> ScoreOverMap(
        SpellCardDataSO spellCard,
        SpellDataSO spell,
        IEnumerable<EnemyManager> enemies,
        BotContext ctx,
        bool isDefense,
        float perCardWeight)
    {
        float weight = perCardWeight * (isDefense ? ctx.Profile.DefenseWeight : ctx.Profile.AggressionWeight);
        int budget = ctx.Profile.MaxCandidatePositionsPerCard;

        int yielded = 0;
        foreach (EnemyManager pivot in enemies)
        {
            if (yielded >= budget) yield break;
            if (pivot == null) continue;

            Vector2 center = pivot.transform.position;
            float score = ScoreAt(center, spell, spellCard.Cost, enemies);
            score *= weight;
            if (score <= 0f) continue;

            yield return new ScoredCandidate(spellCard.CardType, center, score);
            yielded++;
        }
    }

    private static float ScoreAt(Vector2 center, SpellDataSO spell, int cost, IEnumerable<EnemyManager> enemies)
    {
        float range = Mathf.Max(0.01f, spell.Range);

        if (spell is SpellOffensiveDataSO offensive)
        {
            float totalHpInRange = 0f;
            float killBonus = 0f;
            foreach (EnemyManager enemy in enemies)
            {
                if (enemy == null) continue;
                float dist = Vector2.Distance(center, enemy.transform.position);
                if (dist > range) continue;

                float hp = enemy.ServerHealth.CurrentHealth.Value;
                totalHpInRange += hp;
                if (hp <= offensive.Damage) killBonus += hp; // bonus for outright kills
            }
            float damagePerMana = offensive.Damage / Mathf.Max(1f, cost);
            return (totalHpInRange + killBonus) * damagePerMana;
        }

        if (spell is SpellEffectDataSO effect)
        {
            float pressureSlowed = 0f;
            foreach (EnemyManager enemy in enemies)
            {
                if (enemy == null) continue;
                float dist = Vector2.Distance(center, enemy.transform.position);
                if (dist > range) continue;

                float speed = enemy.ServerMovement.CurrentSpeed.Value;
                float progress = enemy.ServerMovement.PathProgress.Value;
                pressureSlowed += speed * progress;
            }
            return pressureSlowed * effect.Duration / Mathf.Max(1f, cost);
        }

        // Plain SpellDataSO (no damage / no duration): count enemies in range, fall-through value.
        int count = 0;
        foreach (EnemyManager enemy in enemies)
        {
            if (enemy == null) continue;
            if (Vector2.Distance(center, enemy.transform.position) <= range) count++;
        }
        return count;
    }
}
