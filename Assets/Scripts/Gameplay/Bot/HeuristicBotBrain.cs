using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Default competent bot. Each tick it works its own lane first — build or level a tower, then Fireball
/// a dangerous cluster or Haste its towers when the lane is under real pressure — and only spends what
/// is left on the offence (troops, Ice on the opponent's towers, Rage on their lane). A tower it wants
/// but cannot afford yet makes it hold the mana rather than leak it into a cheap troop. Pure decision
/// logic — all mutation happens in the BotController via the shared deployer cores.
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

        // ---------- 1) THE LANE: build and level the defence, threat or no threat ----------
        // Not gated on being attacked. Towers are the bot's whole defence and cost 4-6 while a troop
        // costs 2, so leaving them below the offence meant the bot spent every 2 mana it had on the
        // cheapest troop, never banked a tower card, and left its lane bare for the whole match.
        CardType tower = FindTowerPlay(ctx, worstThreat, ctx.CurrentMana, out Vector2 towerPos);
        if (tower != CardType.None)
            return BotDecision.Tower(tower, towerPos);

        // ---------- 2) DEFENCE: spells, once the lane is under real pressure ----------
        if (underPressure && threats.Count > 0)
        {
            CardType fireball = FindSpell(ctx, SpellType.Fireball);
            if (fireball != CardType.None)
                return BotDecision.Spell(fireball, worstThreat.transform.position);

            List<TowerManager> ownTowers = ctx.TowersOf(ctx.Team);
            if (ownTowers.Count > 0)
            {
                CardType haste = FindSpell(ctx, SpellType.Haste);
                if (haste != CardType.None)
                    return BotDecision.Spell(haste, ownTowers[0].transform.position);
            }
        }

        // ---------- 3) SAVE UP: hold mana for a tower the bot cannot pay for yet ----------
        // Only for a card it can actually reach at full mana, so it never waits on something it could
        // never afford. This is what stops the offence below from spending the bank.
        if (FindTowerPlay(ctx, worstThreat, ctx.MaxMana, out _) != CardType.None)
            return BotDecision.None;

        // ---------- 4) OFFENCE (keep a mana reserve) ----------
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

        // ---------- 5) ECONOMY: near max mana, spend so it doesn't overflow ----------
        // Only reachable when ManaReserve is set above the cap, which skips the offence entirely; the
        // tower play it used to try here is already covered by step 1 at the same mana.
        if (ctx.CurrentMana >= ctx.MaxMana - 0.5f)
        {
            CardType troop = FindCheapestSpawnEnemy(ctx);
            if (troop != CardType.None)
                return BotDecision.SpawnEnemy(troop);
        }

        return BotDecision.None;
    }

    /// <summary>
    /// Chooses how to spend a tower card within <paramref name="budget"/> mana: take new ground while
    /// the bot holds fewer than <see cref="BotSettingsSO.TowerTarget"/> towers, level up what is already
    /// standing once it holds that many. Returns None when the line is both up and fully levelled, and
    /// that is deliberate — it is what stops the bot pouring mana into towers and frees it to attack.
    /// </summary>
    /// <param name="budget">Mana to plan against: what the bot holds now for a play it is about to
    /// make, its cap when asking whether a card is worth waiting for.</param>
    private CardType FindTowerPlay(BotContext ctx, EnemyManager focus, float budget, out Vector2 pos)
    {
        Vector2? near = focus != null ? (Vector2?)(Vector2)focus.transform.position : null;

        if (CountOwnTowers(ctx) < _settings.TowerTarget)
        {
            CardType placement = FindTowerPlacement(ctx, near, budget, out pos);
            if (placement != CardType.None) return placement;
        }

        return FindTowerUpgrade(ctx, near, budget, out pos);
    }

    /// <summary>A tower card in hand within budget, plus the free own slot nearest the threat.</summary>
    private CardType FindTowerPlacement(BotContext ctx, Vector2? near, float budget, out Vector2 pos)
    {
        pos = default;

        AbstractPlaceable freeSlot = ctx.NearestFreePlaceable(near);
        if (freeSlot == null) return CardType.None;

        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is TowerCardDataSO tc && Affordable(tc.Cost, budget))
            {
                pos = freeSlot.PlaceablePoint.position;
                return card;
            }
        }

        return CardType.None;
    }

    /// <summary>
    /// A tower card in hand within budget that matches one of the bot's own upgradeable towers, aimed at
    /// the closest such tower to the threat. None once every matching tower sits at max level.
    /// </summary>
    private CardType FindTowerUpgrade(BotContext ctx, Vector2? near, float budget, out Vector2 pos)
    {
        pos = default;
        if (ctx.OwnPlaceables == null) return CardType.None;

        CardType best = CardType.None;
        float bestDist = float.MaxValue;

        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is not TowerCardDataSO tc || !Affordable(tc.Cost, budget))
                continue;

            foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
            {
                if (pl == null || !pl.IsOccupied() || pl.OccupiedTower == null) continue;
                if (pl.OccupiedTower.Data.TowerType != tc.TowerType) continue;
                if (pl.OccupiedTower.ServerTowerCombat == null || !pl.OccupiedTower.ServerTowerCombat.CanUpgradeTower()) continue;

                // With no threat to rank against every candidate scores 0, so the first match wins.
                Vector2 point = pl.PlaceablePoint.position;
                float dist = near == null ? 0f : (point - near.Value).sqrMagnitude;
                if (dist >= bestDist) continue;

                bestDist = dist;
                best = card;
                pos = point;
            }
        }

        return best;
    }

    // Mirrors the server's spend rule (ServerManaManager.CanAfford), which floors the mana pool.
    private static bool Affordable(int cost, float budget) => cost <= Mathf.FloorToInt(budget);

    /// <summary>How many of the bot's own tower slots currently hold a tower.</summary>
    private static int CountOwnTowers(BotContext ctx)
    {
        if (ctx.OwnPlaceables == null) return 0;

        int count = 0;
        foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
            if (pl != null && pl.IsOccupied()) count++;

        return count;
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
