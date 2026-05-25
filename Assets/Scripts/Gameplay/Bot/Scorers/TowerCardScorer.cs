using System.Collections.Generic;
using UnityEngine;

public class TowerCardScorer : BaseCardScorer
{
    private const int PathSampleCount = 8;

    public override bool CanHandle(CardDataSO data) => data is TowerCardDataSO;

    public override IEnumerable<ScoredCandidate> Score(CardDataSO data, BotContext ctx)
    {
        if (data is not TowerCardDataSO towerCard) yield break;

        TowerDataSO td = ctx.World.LookupTower(towerCard.TowerType);
        if (td == null) yield break;

        WaypointPath selfPath = ctx.World.SelfPath();
        float dpsPerMana = ComputeDpsPerMana(td, towerCard.Cost);
        float range = td.GetRangeByLevel(1);
        float weight = ctx.Profile.DefenseWeight * ctx.Profile.GetCardWeight(towerCard.CardType);

        int evaluated = 0;
        foreach (IPlaceable placeable in ctx.World.FreeSelfPlaceables())
        {
            if (evaluated >= ctx.Profile.MaxCandidatePositionsPerCard) break;
            evaluated++;

            Vector2 placePos = placeable.PlaceablePoint.position;
            float closeness = LaneCloseness(placePos, selfPath);
            float pressure = LanePressure(placePos, range, ctx);

            float score = closeness * dpsPerMana * (1f + pressure) * weight;
            if (score <= 0f) continue;

            yield return new ScoredCandidate(towerCard.CardType, placePos, score);
        }
    }

    private static float ComputeDpsPerMana(TowerDataSO td, int cost)
    {
        float damage = td.GetDamageByLevel(1);
        float cooldown = td.GetShootCooldownByLevel(1);
        float dps = cooldown > 0f ? damage / cooldown : damage;
        return dps / Mathf.Max(1f, cost);
    }

    private static float LaneCloseness(Vector2 pos, WaypointPath path)
    {
        if (path == null) return 0f;

        float bestDist = float.MaxValue;
        for (int i = 0; i < PathSampleCount; i++)
        {
            float t = i / (float)(PathSampleCount - 1);
            Vector2 sample = path.SamplePosition(t);
            float d = Vector2.Distance(pos, sample);
            if (d < bestDist) bestDist = d;
        }
        return 1f / (1f + bestDist);
    }

    private static float LanePressure(Vector2 placePos, float towerRange, BotContext ctx)
    {
        float pressure = 0f;
        foreach (EnemyManager enemy in ctx.World.EnemiesOnSelfMap())
        {
            if (enemy == null) continue;
            float dist = Vector2.Distance(placePos, enemy.transform.position);
            if (dist > towerRange) continue;

            float pathRemaining = 1f - enemy.ServerMovement.PathProgress.Value;
            pressure += enemy.ServerHealth.CurrentHealth.Value * pathRemaining;
        }
        return pressure;
    }
}
