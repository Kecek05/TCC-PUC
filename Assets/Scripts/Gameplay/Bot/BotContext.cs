using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Read-only view of the live server state a <see cref="IBotBrain"/> reasons over, plus small query
/// helpers. Built once by the BotController and reused each decision tick (registries are queried live).
/// All positions are server-space (the bot has no client view, so no MapTranslator round-trip is needed).
/// </summary>
public class BotContext
{
    public TeamType Team;
    public TeamType EnemyTeam;

    public BaseServerManaManager Mana;
    public BaseCardHandManager Hand;
    public BaseServerPlayerHealthManager Health;
    public CardDataListSO Cards;
    public IReadOnlyList<AbstractPlaceable> OwnPlaceables;

    public float CurrentMana => Mana.GetMana(Team);
    public float MaxMana => Mana.GetMaxMana(Team);
    public bool CanAfford(int cost) => Mana.CanAfford(Team, cost);

    public float OwnBaseHealth => GetHealth(Team);
    public float EnemyBaseHealth => GetHealth(EnemyTeam);

    /// <summary>Cards currently in the bot's hand (server-side source of truth).</summary>
    public IReadOnlyList<CardType> HandCards =>
        Team == TeamType.Blue ? Hand.BlueHandData?.CardsTypeInHand : Hand.RedHandData?.CardsTypeInHand;

    private float GetHealth(TeamType team)
    {
        if (Health == null) return 0f;
        return team == TeamType.Blue ? Health.BlueHealth.Value : Health.RedHealth.Value;
    }

    /// <summary>All active enemies currently walking (and attacking) the given team's lane.</summary>
    public List<EnemyManager> EnemiesOnLane(TeamType laneTeam)
    {
        var result = new List<EnemyManager>();
        IReadOnlyList<EnemyManager> all = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < all.Count; i++)
        {
            EnemyManager e = all[i];
            if (e != null && e.Team != null && e.Team.GetTeamType() == laneTeam)
                result.Add(e);
        }
        return result;
    }

    /// <summary>All active towers owned by the given team.</summary>
    public List<TowerManager> TowersOf(TeamType team)
    {
        var result = new List<TowerManager>();
        IReadOnlyList<TowerManager> all = TowerRegistry.ActiveTowers;
        for (int i = 0; i < all.Count; i++)
        {
            TowerManager t = all[i];
            if (t != null && t.Team != null && t.Team.GetTeamType() == team)
                result.Add(t);
        }
        return result;
    }

    /// <summary>Nearest unoccupied own placeable to <paramref name="near"/> (any free one if near is null).</summary>
    public AbstractPlaceable NearestFreePlaceable(Vector2? near)
    {
        if (OwnPlaceables == null) return null;

        AbstractPlaceable best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < OwnPlaceables.Count; i++)
        {
            AbstractPlaceable p = OwnPlaceables[i];
            if (p == null || p.IsOccupied()) continue;
            if (near == null) return p;

            float d = ((Vector2)p.PlaceablePoint.position - near.Value).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }
}
