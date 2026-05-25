using System.Collections.Generic;

/// <summary>
/// Read-only perception facade. Scorers and the decision-maker consume only this — they
/// never touch ServiceLocator. Wraps all managers and registries the bot needs to see.
/// All positions are server-space.
/// </summary>
public class BotWorldView
{
    public TeamType SelfTeam { get; }
    public TeamType EnemyTeam { get; }

    private readonly BaseServerManaManager _mana;
    private readonly BaseCardHandManager _hand;
    private readonly BaseServerPlayerHealthManager _health;
    private readonly BaseServerWaveManager _waves;
    private readonly BaseGameFlowManager _gameFlow;
    private readonly CardDataListSO _cardDataList;
    private readonly TowerDataListSO _towerDataList;

    public BotWorldView(
        TeamType selfTeam,
        BaseServerManaManager mana,
        BaseCardHandManager hand,
        BaseServerPlayerHealthManager health,
        BaseServerWaveManager waves,
        BaseGameFlowManager gameFlow,
        CardDataListSO cardDataList,
        TowerDataListSO towerDataList)
    {
        SelfTeam = selfTeam;
        EnemyTeam = selfTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;
        _mana = mana;
        _hand = hand;
        _health = health;
        _waves = waves;
        _gameFlow = gameFlow;
        _cardDataList = cardDataList;
        _towerDataList = towerDataList;
    }

    public bool IsInMatch() => _gameFlow.CurrentGameState.Value == GameState.InMatch;

    public float SelfMana() => _mana.GetMana(SelfTeam);
    public float SelfMaxMana() => _mana.GetMaxMana(SelfTeam);
    public float EnemyMana() => _mana.GetMana(EnemyTeam);
    public float EnemyMaxMana() => _mana.GetMaxMana(EnemyTeam);

    public IReadOnlyList<CardType> Hand() =>
        SelfTeam == TeamType.Blue ? _hand.BlueHandCards : _hand.RedHandCards;

    public CardDataSO LookupCard(CardType card) => _cardDataList.GetCardDataByType(card);
    public TowerDataSO LookupTower(TowerType type) => _towerDataList.GetTowerDataByType(type);

    public float SelfBaseHp() =>
        SelfTeam == TeamType.Blue ? _health.BlueHealth.Value : _health.RedHealth.Value;

    public float EnemyBaseHp() =>
        SelfTeam == TeamType.Blue ? _health.RedHealth.Value : _health.BlueHealth.Value;

    public WaypointPath SelfPath() => _waves.GetPath(SelfTeam);
    public WaypointPath EnemyPath() => _waves.GetPath(EnemyTeam);

    /// <summary>Enemies walking on the bot's own map (attacking the bot).</summary>
    public IEnumerable<EnemyManager> EnemiesOnSelfMap() => EnemiesOf(SelfTeam);

    /// <summary>Enemies walking on the opponent's map (attacking the opponent).</summary>
    public IEnumerable<EnemyManager> EnemiesOnEnemyMap() => EnemiesOf(EnemyTeam);

    private static IEnumerable<EnemyManager> EnemiesOf(TeamType team)
    {
        var list = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < list.Count; i++)
        {
            EnemyManager e = list[i];
            if (e == null || e.Team == null) continue;
            if (e.Team.GetTeamType() == team) yield return e;
        }
    }

    public IEnumerable<TowerManager> SelfTowers() => TowerRegistry.GetTowersByTeam(SelfTeam);
    public IEnumerable<TowerManager> EnemyTowers() => TowerRegistry.GetTowersByTeam(EnemyTeam);

    public IEnumerable<IPlaceable> FreeSelfPlaceables() => PlaceableRegistry.GetFreeByTeam(SelfTeam);
    public IReadOnlyList<IPlaceable> AllSelfPlaceables() => PlaceableRegistry.GetByTeam(SelfTeam);
}
