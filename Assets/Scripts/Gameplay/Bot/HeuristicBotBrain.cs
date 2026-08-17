using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Default competent bot. Each tick it defends its own lane first (place/upgrade a tower at the biggest
/// threat, Fireball a dangerous cluster, Haste its towers when under pressure), then attacks with spare
/// mana (send troops, Ice the opponent's towers, Rage the opponent's lane), otherwise keeps a mana
/// reserve. Pure decision logic — all mutation happens in the BotController via the shared deployer cores.
/// </summary>
public class HeuristicBotBrain : IBotBrain
{
    private readonly BotSettingsSO _settings;

    // Base HP only decreases, so the highest value seen approximates the starting max. Used to tell how
    // badly the bot is losing without needing the health-settings SO.
    private float _maxOwnHealthSeen = -1f;

    public HeuristicBotBrain(BotSettingsSO settings)
    {
        _settings = settings;
    }

    public BotDecision Decide(BotContext ctx)
    {
        IReadOnlyList<CardType> hand = ctx.HandCards;
        if (hand == null || hand.Count == 0)
            return BotDecision.None;

        float ownHp = ctx.OwnBaseHealth;
        if (ownHp > _maxOwnHealthSeen) _maxOwnHealthSeen = ownHp;
        bool lowHealth = _maxOwnHealthSeen > 0f && ownHp <= _maxOwnHealthSeen * 0.4f;

        // Threats on my own lane, ranked by how close they are to my base.
        List<EnemyManager> threats = ctx.EnemiesOnLane(ctx.Team);
        EnemyManager worstThreat = null;
        float worstProgress = -1f;
        foreach (EnemyManager e in threats)
        {
            float p = e.ServerMovement != null ? e.ServerMovement.PathProgress.Value : 0f;
            if (p > worstProgress) { worstProgress = p; worstThreat = e; }
        }
        bool underPressure = threats.Count >= 3 || worstProgress >= 0.5f || lowHealth;

        // ---------- 1) DEFENCE ----------
        if (threats.Count > 0)
        {
            CardType tower = FindTowerPlay(ctx, worstThreat, out Vector2 towerPos);
            if (tower != CardType.None)
                return BotDecision.Tower(tower, towerPos);

            if (underPressure && worstThreat != null)
            {
                CardType fireball = FindSpell(ctx, SpellType.Fireball);
                if (fireball != CardType.None)
                    return BotDecision.Spell(fireball, worstThreat.transform.position);
            }

            if (underPressure)
            {
                List<TowerManager> ownTowers = ctx.TowersOf(ctx.Team);
                if (ownTowers.Count > 0)
                {
                    CardType haste = FindSpell(ctx, SpellType.Haste);
                    if (haste != CardType.None)
                        return BotDecision.Spell(haste, ownTowers[0].transform.position);
                }
            }
        }

        // ---------- 2) OFFENCE (keep a mana reserve) ----------
        if (ctx.CurrentMana >= _settings.ManaReserve)
        {
            CardType troop = FindCheapestSpawnEnemy(ctx);
            if (troop != CardType.None)
                return BotDecision.SpawnEnemy(troop);

            List<TowerManager> enemyTowers = ctx.TowersOf(ctx.EnemyTeam);
            if (enemyTowers.Count > 0)
            {
                CardType ice = FindSpell(ctx, SpellType.Ice);
                if (ice != CardType.None)
                    return BotDecision.Spell(ice, enemyTowers[0].transform.position);
            }

            List<EnemyManager> enemyLane = ctx.EnemiesOnLane(ctx.EnemyTeam);
            if (enemyLane.Count > 0)
            {
                CardType rage = FindSpell(ctx, SpellType.Rage);
                if (rage != CardType.None)
                    return BotDecision.Spell(rage, enemyLane[0].transform.position);
            }
        }

        // ---------- 3) ECONOMY: near max mana, spend so it doesn't overflow ----------
        if (ctx.CurrentMana >= ctx.MaxMana - 0.5f)
        {
            CardType tower = FindTowerPlay(ctx, worstThreat, out Vector2 towerPos);
            if (tower != CardType.None)
                return BotDecision.Tower(tower, towerPos);

            CardType troop = FindCheapestSpawnEnemy(ctx);
            if (troop != CardType.None)
                return BotDecision.SpawnEnemy(troop);
        }

        return BotDecision.None;
    }

    // Prefers placing a new tower on the free slot nearest the threat; falls back to upgrading a
    // same-type tower when no slot is free.
    private CardType FindTowerPlay(BotContext ctx, EnemyManager focus, out Vector2 pos)
    {
        pos = default;
        Vector2? near = focus != null ? (Vector2?)(Vector2)focus.transform.position : null;

        AbstractPlaceable freeSlot = ctx.NearestFreePlaceable(near);
        if (freeSlot != null)
        {
            foreach (CardType card in ctx.HandCards)
            {
                if (ctx.Cards.GetCardDataByType(card) is TowerCardDataSO tc && ctx.CanAfford(tc.Cost))
                {
                    pos = freeSlot.PlaceablePoint.position;
                    return card;
                }
            }
        }

        // Upgrade path: play a tower card whose type matches an upgradeable own tower.
        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is not TowerCardDataSO tc || !ctx.CanAfford(tc.Cost))
                continue;
            if (ctx.OwnPlaceables == null) continue;

            foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
            {
                if (pl == null || !pl.IsOccupied() || pl.OccupiedTower == null) continue;
                if (pl.OccupiedTower.Data.TowerType != tc.TowerType) continue;
                if (pl.OccupiedTower.ServerTowerCombat == null || !pl.OccupiedTower.ServerTowerCombat.CanUpgradeTower()) continue;

                pos = pl.PlaceablePoint.position;
                return card;
            }
        }

        return CardType.None;
    }

    private CardType FindSpell(BotContext ctx, SpellType type)
    {
        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is SpellCardDataSO sc && sc.SpellType == type && ctx.CanAfford(sc.Cost))
                return card;
        }
        return CardType.None;
    }

    private CardType FindCheapestSpawnEnemy(BotContext ctx)
    {
        CardType best = CardType.None;
        int bestCost = int.MaxValue;
        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is SpawnEnemyCardDataSO se && ctx.CanAfford(se.Cost) && se.Cost < bestCost)
            {
                best = card;
                bestCost = se.Cost;
            }
        }
        return best;
    }
}
