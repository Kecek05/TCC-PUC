using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Default competent bot. Each tick it works its own lane first — build or level a tower, then a spell on
/// the lane when it is under real pressure (Fireball or Lance for damage, Rift, Ferrugem or Haste to help the
/// towers) — and only spends what is left on the offence: troops, with Rage on them and Ice on the towers in
/// their way while they march. A tower or a troop it wants but cannot afford yet makes it hold the mana
/// rather than leak it into something cheap. Pure decision logic — all mutation happens in the BotController
/// via the shared deployer cores.
/// </summary>
/// <remarks>
/// It plays every card in the set: towers and troops by family, so a new one needs nothing here, and the
/// seven spells by name, because where a spell goes is part of what it is. Every card in a deck must also
/// have a play at every point of the match, because a card with none sits in the hand for good: the hand
/// only cycles through what is played, so a dead card is a lost slot and two of them halve the hand.
/// </remarks>
public class HeuristicBotBrain : IBotBrain
{
    /// <summary>What a tower is for, which decides the slot it wants.</summary>
    private enum TowerRole
    {
        /// <summary>Works on the enemies in its range: every damage tower, plus Prism, Tourniquet and Ancora.</summary>
        Lane,
        /// <summary>Works on the ally towers in its range (Anel), so it belongs inside the line.</summary>
        TowerAura,
        /// <summary>Works from anywhere (Fonte's mana, Espelho's sends): a lane slot would be wasted on it.</summary>
        Economy,
    }

    private readonly BotSettingsSO _settings;

    // Base HP only decreases, so the highest value seen approximates the starting max. Used to tell how
    // badly the bot is losing without needing the health-settings SO.
    private float _maxOwnHealthSeen = -1f;

    // Set each decision: whether the lane is in real danger — an enemy past the middle of the lane, or the
    // base badly hurt. Only then is saving up wrong; otherwise the bot banks mana for a tower its line does
    // not have yet, or a big troop, instead of spending on the cheap card in its hand. Deliberately not
    // "under pressure" (which also counts three enemies anywhere on the lane): from the middle waves on there
    // are always three, so gating on it meant the bot never saved for anything costing more than it held,
    // and a Fonte or a MiniBoss sat in its hand for the rest of the match.
    private bool _inDanger;

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
        _inDanger = worstProgress >= 0.5f || lowHealth;
        bool underPressure = threats.Count >= 3 || _inDanger;
        Vector2? focus = worstThreat != null ? (Vector2?)(Vector2)worstThreat.transform.position : null;

        // ---------- 1) THE LANE: build and level the defence, threat or no threat ----------
        // Not gated on being attacked. Towers are the bot's whole defence and cost 4-6 while a troop
        // costs 2, so leaving them below the offence meant the bot spent every 2 mana it had on the
        // cheapest troop, never banked a tower card, and left its lane bare for the whole match.
        CardType tower = FindTowerPlay(ctx, focus, ctx.CurrentMana, out Vector2 towerPos);
        if (tower != CardType.None)
            return BotDecision.Tower(tower, towerPos);

        // ---------- 2) DEFENCE: spells, once the lane is under real pressure ----------
        if (underPressure && threats.Count > 0)
        {
            // Damage first, on the enemy closest to the base: a blast, or a lance down the lane from it.
            CardType fireball = FindSpell(ctx, SpellType.Fireball);
            if (fireball != CardType.None)
                return BotDecision.Spell(fireball, worstThreat.transform.position);

            CardType lance = FindSpell(ctx, SpellType.Lance);
            if (lance != CardType.None)
                return BotDecision.Spell(lance, worstThreat.transform.position);

            // The rest only buys the towers time or bite, so it needs towers to be worth a card.
            List<TowerManager> ownTowers = ctx.TowersOf(ctx.Team);
            if (ownTowers.Count > 0)
            {
                CardType rift = FindSpell(ctx, SpellType.Rift);
                if (rift != CardType.None)
                    return BotDecision.Spell(rift, worstThreat.transform.position);

                // Ferrugem strips armour: nothing to strip, nothing to cast it for.
                if (AnyArmoured(threats))
                {
                    CardType ferrugem = FindSpell(ctx, SpellType.Ferrugem);
                    if (ferrugem != CardType.None)
                        return BotDecision.Spell(ferrugem, worstThreat.transform.position);
                }

                // Haste works on every tower inside its radius, so it goes where the most of them stand.
                CardType haste = FindSpell(ctx, SpellType.Haste, out SpellCardDataSO hasteData);
                if (haste != CardType.None)
                    return BotDecision.Spell(haste, DensestPoint(PositionsOf(ownTowers), RangeOf(hasteData), null));
            }
        }

        // ---------- 3) SAVE UP: hold mana for a tower the bot cannot pay for yet ----------
        // Only for a card it can actually reach at full mana, so it never waits on something it could
        // never afford. This is what stops the offence below from spending the bank.
        if (FindTowerPlay(ctx, focus, ctx.MaxMana, out _) != CardType.None)
            return BotDecision.None;

        // ---------- 4) OFFENCE (keep a mana reserve) ----------
        if (ctx.CurrentMana >= _settings.ManaReserve)
        {
            // Back a push already marching: Rage and Ice only pay off on troops of the bot's own, so this is
            // where they are cast, while the troops are still on the lane to use them.
            List<EnemyManager> push = ctx.OwnTroopsOnLane(ctx.EnemyTeam);
            if (push.Count > 0)
            {
                CardType rage = FindSpell(ctx, SpellType.Rage, out SpellCardDataSO rageData);
                if (rage != CardType.None)
                    return BotDecision.Spell(rage, DensestPoint(PositionsOf(push), RangeOf(rageData), null));

                List<TowerManager> enemyTowers = ctx.TowersOf(ctx.EnemyTeam);
                if (enemyTowers.Count > 0)
                {
                    CardType ice = FindSpell(ctx, SpellType.Ice, out SpellCardDataSO iceData);
                    if (ice != CardType.None)
                        return BotDecision.Spell(ice, DensestPoint(PositionsOf(enemyTowers), RangeOf(iceData),
                            (Vector2)PushLeader(push).transform.position));
                }
            }

            CardType troop = FindTroopPlay(ctx, out bool bankForTroop);
            if (troop != CardType.None)
                return BotDecision.SpawnEnemy(troop);

            // A bigger troop is coming: what is left below would spend the bank.
            if (bankForTroop)
                return BotDecision.None;
        }

        // ---------- 5) SPARE MANA ----------
        // A tower card with nothing left to level only leaves the hand by being placed, so the first mana
        // nothing else wants goes there. Only here, after the offence: placed any earlier, every such card
        // became a new tower plus two levels on it, all ahead of the offence, and the bot turtled — in one
        // test it made twelve tower plays to one troop in two minutes while Ice sat in its hand.
        CardType stuck = FindStuckTowerPlacement(ctx, focus, out Vector2 stuckPos);
        if (stuck != CardType.None)
            return BotDecision.Tower(stuck, stuckPos);

        // Near max mana, regen is about to go to waste: spend it on anything that still does something.
        if (ctx.CurrentMana >= ctx.MaxMana - 0.5f)
        {
            CardType troop = FindCheapestSpawnEnemy(ctx);
            if (troop != CardType.None)
                return BotDecision.SpawnEnemy(troop);

            // With no push of its own, Rage still hurts the opponent: everything on their lane is walking
            // at them, their own waves included.
            List<EnemyManager> enemyLane = ctx.EnemiesOnLane(ctx.EnemyTeam);
            if (enemyLane.Count > 0)
            {
                CardType rage = FindSpell(ctx, SpellType.Rage, out SpellCardDataSO rageData);
                if (rage != CardType.None)
                    return BotDecision.Spell(rage, DensestPoint(PositionsOf(enemyLane), RangeOf(rageData), null));
            }

            List<TowerManager> enemyTowers = ctx.TowersOf(ctx.EnemyTeam);
            if (enemyTowers.Count > 0)
            {
                CardType ice = FindSpell(ctx, SpellType.Ice, out SpellCardDataSO iceData);
                if (ice != CardType.None)
                    return BotDecision.Spell(ice, DensestPoint(PositionsOf(enemyTowers), RangeOf(iceData), null));
            }
        }

        return BotDecision.None;
    }

    // ------------------------------------------------------------------ towers

    /// <summary>
    /// Chooses how to spend a tower card within <paramref name="budget"/> mana. While the bot holds fewer
    /// than <see cref="BotSettingsSO.TowerTarget"/> towers it takes new ground, repeats included: that is the
    /// core line. Past it, a tower TYPE the line does not have yet still goes up, once, and everything else
    /// goes into levelling what stands.
    /// </summary>
    /// <remarks>
    /// Without the "new type past the core" rule a deck's expensive towers never reached the board at all:
    /// the cap only pays for Mortar and Espelho from wave 6 and for Chain and Fonte from wave 7, by which
    /// time three cheap towers had long filled the core, so every copy was a dead card for the whole match.
    /// Once that line is up and fully levelled there is no tower play left, which is what frees the mana
    /// for the offence.
    /// </remarks>
    /// <param name="budget">Mana to plan against: what the bot holds now for a play it is about to
    /// make, its cap when asking whether a card is worth waiting for.</param>
    private CardType FindTowerPlay(BotContext ctx, Vector2? near, float budget, out Vector2 pos)
    {
        bool coreUp = CountOwnTowers(ctx) >= _settings.TowerTarget;

        CardType placement = FindTowerPlacement(ctx, near, budget, newTypesOnly: coreUp, out pos, out bool bankForNew);
        if (placement != CardType.None) return placement;

        // A tower the line lacks is within reach of the mana cap: neither repeat nor level, bank for it.
        // Asked again at the cap (the save-up step), the same card comes back as the play to wait for.
        if (bankForNew) return CardType.None;

        return FindTowerUpgrade(ctx, near, budget, out pos);
    }

    /// <summary>
    /// A tower card in hand within budget, and the free own slot its role wants. Two rules pick the card.
    /// A support tower (Prism, Tourniquet, Anel, Ancora, Espelho, Fonte) is only placed once a damage tower is
    /// standing, since on its own it defends nothing. And a tower the line does not have yet beats one it
    /// already has: the cheapest tower comes back to the hand first, so taking the first affordable card built
    /// a line of three Darts and never showed the rest of the deck. With nothing new to place, a repeat still
    /// goes up while the core is incomplete: a line is worth more than variety.
    /// </summary>
    /// <param name="newTypesOnly">Past the core line: only a type the line lacks is placed, and an economy
    /// tower (Fonte, Espelho) waits out a lane in danger, since it does nothing for a lane being overrun.</param>
    /// <param name="bankForNew">True when nothing is returned because a tower of a new type sits in the hand
    /// just out of reach: affordable at the mana cap but not at <paramref name="budget"/>. The hand only deals
    /// what the cap can pay for, so in the first waves the cheapest tower returns after every play, and a bot
    /// that spends the moment it can afford anything never holds enough for the others. Never while the lane
    /// is in danger, and never for the first tower.</param>
    private CardType FindTowerPlacement(BotContext ctx, Vector2? near, float budget, bool newTypesOnly,
        out Vector2 pos, out bool bankForNew)
    {
        pos = default;
        bankForNew = false;

        if (ctx.NearestFreePlaceable(null) == null) return CardType.None;

        bool hasDamageTower = HasDamageTower(ctx);
        HashSet<TowerType> standing = StandingTowerTypes(ctx);

        TowerCardDataSO chosen = null;
        TowerCardDataSO repeat = null;
        TowerCardDataSO repeatSupport = null;
        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is not TowerCardDataSO tc) continue;

            TowerDataSO data = tc.ResolveTowerData();
            bool damage = DealsDamage(data);
            if (!damage && !hasDamageTower) continue;

            bool isNew = !standing.Contains(tc.TowerType);
            if (newTypesOnly && !isNew) continue;
            if (newTypesOnly && _inDanger && RoleOf(data) == TowerRole.Economy) continue;

            if (isNew && !Affordable(tc.Cost, budget))
            {
                // New to the line, out of reach for now: worth waiting for if the cap can ever pay for it.
                if (hasDamageTower && !_inDanger && Affordable(tc.Cost, ctx.MaxMana)) bankForNew = true;
                continue;
            }

            if (!Affordable(tc.Cost, budget)) continue;

            if (isNew) { chosen = tc; break; }

            if (damage) repeat ??= tc;
            else repeatSupport ??= tc;
        }

        // A new tower is coming: hold the repeat back and let the caller bank for it.
        if (chosen == null && bankForNew) return CardType.None;

        chosen ??= repeat ?? repeatSupport;
        if (chosen == null) return CardType.None;

        AbstractPlaceable slot = ChooseSlot(ctx, chosen.ResolveTowerData(), near);
        if (slot == null) return CardType.None;

        pos = slot.PlaceablePoint.position;
        return chosen.CardType;
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
                if (!IsUpgradeable(pl, tc.TowerType)) continue;

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

    /// <summary>
    /// The cheapest stuck tower card the bot can pay for now (see <see cref="IsStuck"/>), placed as a new
    /// tower: the only play such a card has left.
    /// </summary>
    private CardType FindStuckTowerPlacement(BotContext ctx, Vector2? near, out Vector2 pos)
    {
        pos = default;

        TowerCardDataSO best = null;
        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is not TowerCardDataSO tc) continue;
            if (!IsStuck(ctx, tc) || !ctx.CanAfford(tc.Cost)) continue;
            if (best == null || tc.Cost < best.Cost) best = tc;
        }
        if (best == null) return CardType.None;

        AbstractPlaceable slot = ChooseSlot(ctx, best.ResolveTowerData(), near);
        if (slot == null) return CardType.None;

        pos = slot.PlaceablePoint.position;
        return best.CardType;
    }

    /// <summary>
    /// A tower card with nothing left to do: the core line is up (so a repeat is not wanted), its type is
    /// already on the field (so it is not new), and every tower of that type is at the top tier (so it cannot
    /// level anything). The hand never discards, so it stays there until it is placed.
    /// </summary>
    private bool IsStuck(BotContext ctx, TowerCardDataSO tc)
    {
        if (ctx.OwnPlaceables == null || CountOwnTowers(ctx) < _settings.TowerTarget) return false;

        bool standing = false;
        foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
        {
            if (pl == null || !pl.IsOccupied() || pl.OccupiedTower == null || pl.OccupiedTower.Data == null) continue;
            if (pl.OccupiedTower.Data.TowerType != tc.TowerType) continue;

            standing = true;
            if (IsUpgradeable(pl, tc.TowerType)) return false;
        }
        return standing;
    }

    private static bool IsUpgradeable(AbstractPlaceable pl, TowerType type) =>
        pl != null && pl.IsOccupied() && pl.OccupiedTower != null && pl.OccupiedTower.Data != null &&
        pl.OccupiedTower.Data.TowerType == type &&
        pl.OccupiedTower.ServerTowerCombat != null && pl.OccupiedTower.ServerTowerCombat.CanUpgradeTower();

    private static TowerRole RoleOf(TowerDataSO data) => data switch
    {
        AuraTowerDataSO => TowerRole.TowerAura,
        ManaTowerDataSO or MirrorTowerDataSO => TowerRole.Economy,
        _ => TowerRole.Lane,
    };

    /// <summary>
    /// The free own slot a tower of this role wants. A lane tower goes nearest the threat; an Anel where its
    /// ring covers the most towers it can buff; a Fonte or Espelho as far from the threat (or from the line,
    /// on a quiet lane) as it can, so it never takes a slot a damage tower could shoot from.
    /// </summary>
    private static AbstractPlaceable ChooseSlot(BotContext ctx, TowerDataSO data, Vector2? near)
    {
        switch (RoleOf(data))
        {
            case TowerRole.TowerAura:
                return SlotCoveringMostTowers(ctx, data.GetRangeByLevel(1), near)
                       ?? ctx.NearestFreePlaceable(near ?? CentroidOfOwnTowers(ctx));

            case TowerRole.Economy:
                return FarthestFreePlaceable(ctx, near ?? CentroidOfOwnTowers(ctx))
                       ?? ctx.NearestFreePlaceable(near);

            default:
                return ctx.NearestFreePlaceable(near);
        }
    }

    /// <summary>
    /// The free slot whose ring would hold the most of the bot's towers, ties to the one nearest the threat.
    /// A ring never buffs another ring, so those are not counted. Null when no slot covers any tower.
    /// </summary>
    private static AbstractPlaceable SlotCoveringMostTowers(BotContext ctx, float radius, Vector2? near)
    {
        if (ctx.OwnPlaceables == null) return null;

        float radiusSq = radius * radius;
        AbstractPlaceable best = null;
        int bestCount = 0;
        float bestDist = float.MaxValue;

        foreach (AbstractPlaceable slot in ctx.OwnPlaceables)
        {
            if (slot == null || slot.IsOccupied()) continue;
            Vector2 point = slot.PlaceablePoint.position;

            int count = 0;
            foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
            {
                if (pl == null || !pl.IsOccupied() || pl.OccupiedTower == null) continue;
                if (pl.OccupiedTower.Data is AuraTowerDataSO) continue;
                if (((Vector2)pl.PlaceablePoint.position - point).sqrMagnitude <= radiusSq) count++;
            }
            if (count == 0) continue;

            float dist = near == null ? 0f : (point - near.Value).sqrMagnitude;
            if (count > bestCount || (count == bestCount && dist < bestDist))
            {
                best = slot;
                bestCount = count;
                bestDist = dist;
            }
        }
        return best;
    }

    private static AbstractPlaceable FarthestFreePlaceable(BotContext ctx, Vector2? from)
    {
        if (ctx.OwnPlaceables == null || from == null) return null;

        AbstractPlaceable best = null;
        float bestDist = -1f;
        foreach (AbstractPlaceable slot in ctx.OwnPlaceables)
        {
            if (slot == null || slot.IsOccupied()) continue;
            float d = ((Vector2)slot.PlaceablePoint.position - from.Value).sqrMagnitude;
            if (d > bestDist) { bestDist = d; best = slot; }
        }
        return best;
    }

    private static Vector2? CentroidOfOwnTowers(BotContext ctx)
    {
        if (ctx.OwnPlaceables == null) return null;

        Vector2 sum = Vector2.zero;
        int count = 0;
        foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
        {
            if (pl == null || !pl.IsOccupied()) continue;
            sum += (Vector2)pl.PlaceablePoint.position;
            count++;
        }
        return count > 0 ? sum / count : null;
    }

    /// <summary>The tower types the bot already has on its field.</summary>
    private static HashSet<TowerType> StandingTowerTypes(BotContext ctx)
    {
        HashSet<TowerType> types = new();
        if (ctx.OwnPlaceables == null) return types;

        foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
            if (pl != null && pl.IsOccupied() && pl.OccupiedTower != null && pl.OccupiedTower.Data != null)
                types.Add(pl.OccupiedTower.Data.TowerType);

        return types;
    }

    /// <summary>Whether a tower hits anything itself. Unknown data counts as yes, so a tower is never held back by a missing reference.</summary>
    private static bool DealsDamage(TowerDataSO data) => data == null || data.GetDamageByLevel(1) > 0f;

    private static bool HasDamageTower(BotContext ctx)
    {
        if (ctx.OwnPlaceables == null) return false;

        foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
            if (pl != null && pl.IsOccupied() && pl.OccupiedTower != null && DealsDamage(pl.OccupiedTower.Data))
                return true;

        return false;
    }

    /// <summary>How many of the bot's own tower slots currently hold a tower.</summary>
    private static int CountOwnTowers(BotContext ctx)
    {
        if (ctx.OwnPlaceables == null) return 0;

        int count = 0;
        foreach (AbstractPlaceable pl in ctx.OwnPlaceables)
            if (pl != null && pl.IsOccupied()) count++;

        return count;
    }

    // ------------------------------------------------------------------ troops

    /// <summary>
    /// The troop to send. The plan is the biggest troop in hand the mana cap can pay for: sent when the bot
    /// can afford it, banked for while the lane is safe, and swapped for the cheapest one affordable now when
    /// the lane is in danger and there is no time to save.
    /// </summary>
    /// <remarks>
    /// Sending the cheapest troop first meant a 6-cost troop only went out when no 2-cost one was in hand
    /// and nothing else spent the mana on the way to 6, and the 10-cost MiniBoss never at all. Banking for
    /// the big one does not starve the small ones: once the big card is played it cycles away, and the next
    /// plan is whatever troop is biggest in the hand then.
    /// </remarks>
    private CardType FindTroopPlay(BotContext ctx, out bool bankForTroop)
    {
        bankForTroop = false;

        SpawnEnemyCardDataSO biggest = null;
        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is not SpawnEnemyCardDataSO se) continue;
            if (!Affordable(se.Cost, ctx.MaxMana)) continue;
            if (biggest == null || se.Cost > biggest.Cost) biggest = se;
        }
        if (biggest == null) return CardType.None;

        if (ctx.CanAfford(biggest.Cost)) return biggest.CardType;

        if (!_inDanger)
        {
            bankForTroop = true;
            return CardType.None;
        }

        return FindCheapestSpawnEnemy(ctx);
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

    /// <summary>The troop of a push furthest down its path: the one the towers ahead will meet first.</summary>
    private static EnemyManager PushLeader(List<EnemyManager> push)
    {
        EnemyManager leader = push[0];
        float best = -1f;
        foreach (EnemyManager e in push)
        {
            float p = e.ServerMovement != null ? e.ServerMovement.PathProgress.Value : 0f;
            if (p > best) { best = p; leader = e; }
        }
        return leader;
    }

    private static bool AnyArmoured(List<EnemyManager> enemies)
    {
        foreach (EnemyManager enemy in enemies)
            if (enemy != null && enemy.Data != null && enemy.Data.ArmorColor != ArmorColor.None)
                return true;

        return false;
    }

    // ------------------------------------------------------------------ spells

    private CardType FindSpell(BotContext ctx, SpellType type) => FindSpell(ctx, type, out _);

    private CardType FindSpell(BotContext ctx, SpellType type, out SpellCardDataSO data)
    {
        foreach (CardType card in ctx.HandCards)
        {
            if (ctx.Cards.GetCardDataByType(card) is SpellCardDataSO sc && sc.SpellType == type && ctx.CanAfford(sc.Cost))
            {
                data = sc;
                return card;
            }
        }
        data = null;
        return CardType.None;
    }

    private static float RangeOf(SpellCardDataSO spell) =>
        spell != null && spell.SpellData != null ? spell.SpellData.Range : 1f;

    /// <summary>
    /// Of <paramref name="points"/>, the one with the most others within <paramref name="radius"/>: where a
    /// zone spell centred on it covers the most. Ties go to the point nearest <paramref name="tieBreak"/>, or
    /// to the first one.
    /// </summary>
    private static Vector2 DensestPoint(List<Vector2> points, float radius, Vector2? tieBreak)
    {
        float radiusSq = radius * radius;
        Vector2 best = points[0];
        int bestCount = -1;
        float bestDist = float.MaxValue;

        foreach (Vector2 p in points)
        {
            int count = 0;
            foreach (Vector2 q in points)
                if ((q - p).sqrMagnitude <= radiusSq) count++;

            float dist = tieBreak == null ? 0f : (p - tieBreak.Value).sqrMagnitude;
            if (count > bestCount || (count == bestCount && dist < bestDist))
            {
                best = p;
                bestCount = count;
                bestDist = dist;
            }
        }
        return best;
    }

    private static List<Vector2> PositionsOf(List<TowerManager> towers)
    {
        List<Vector2> points = new(towers.Count);
        foreach (TowerManager t in towers) points.Add(t.transform.position);
        return points;
    }

    private static List<Vector2> PositionsOf(List<EnemyManager> enemies)
    {
        List<Vector2> points = new(enemies.Count);
        foreach (EnemyManager e in enemies) points.Add(e.transform.position);
        return points;
    }

    // Mirrors the server's spend rule (ServerManaManager.CanAfford), which floors the mana pool.
    private static bool Affordable(int cost, float budget) => cost <= Mathf.FloorToInt(budget);
}
