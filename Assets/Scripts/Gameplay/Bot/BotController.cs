using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Seats a client-less "virtual" Blue player (a bot) when the host waits too long alone, then drives it
/// through the same server-authoritative deploy path a human uses (via the deployer cores). Lives in
/// GameScene; inert on non-server peers.
/// </summary>
public class BotController : BaseBotController
{
    private const string BotAuthId = "BOT_PLAYER";

    [SerializeField, Required] private BotSettingsSO botSettings;

    [Tooltip("Same CardDataListSO the deployers use — the brain reads card costs/types from it.")]
    [SerializeField, Required] private CardDataListSO cardDataListSO;

    private bool _isSeated;
    private TeamType _botTeam = TeamType.None;
    private IBotBrain _brain;

    private BaseCardTowerDeployer _towerDeployer;
    private BaseCardSpellDeployer _spellDeployer;
    private BaseCardSpawnEnemyDeployer _spawnDeployer;

    public override bool IsSeated => _isSeated;
    public override bool BotFallbackEnabled => botSettings != null && botSettings.EnableBotFallback;
    public override float FillTimeoutSeconds => botSettings != null ? botSettings.FillTimeoutSeconds : 30f;

    private void Awake()
    {
        ServiceLocator.Register<BaseBotController>(this);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            enabled = false;
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseBotController>();
        base.OnDestroy();
    }

    public override void SeatBot()
    {
        if (!IsServer || _isSeated) return;

        List<CardType> deck = botSettings != null && botSettings.DeckList != null
            ? botSettings.DeckList.GetRandomDeck()
            : null;

        if (deck == null || deck.Count == 0)
        {
            GameLog.Error("[BotController] Cannot seat bot: no deck configured.");
            return;
        }

        _isSeated = true;

        var playersData = ServiceLocator.Get<BasePlayersDataManager>();
        var teamManager = ServiceLocator.Get<BaseTeamManager>();
        var mapTranslator = ServiceLocator.Get<BaseMapTranslator>();

        PlayerData botData = new PlayerData
        {
            ClientId = ulong.MaxValue, // sentinel: the bot has no network client
            UserData = new UserData
            {
                PlayerName = botSettings.BotName,
                PlayerAuthId = BotAuthId,
                UserTrophies = 0,
                DeckCards = deck,
            }
        };

        playersData.RegisterBot(BotAuthId, botData);
        teamManager.AssignTeamForAuthId(BotAuthId);     // host holds Red -> the bot lands on Blue
        _botTeam = teamManager.GetTeam(BotAuthId);
        mapTranslator.MarkPlayerInitialized(_botTeam);  // satisfy the LoadingMatch -> MatchReady gate

        GameLog.Info($"[BotController] Bot '{botSettings.BotName}' seated as {_botTeam}.");

        // The match commits (admission gate + lobby close) when the FSM leaves WaitingForPlayers, which
        // happens next tick once this Blue assignment flips BothTeamsAssigned() true.
        StartCoroutine(DecisionLoop());
    }

    private IEnumerator DecisionLoop()
    {
        var gameFlow = ServiceLocator.Get<BaseGameFlowManager>();
        yield return new WaitUntil(() => gameFlow.CurrentGameState.Value == GameState.InMatch);

        _towerDeployer = ServiceLocator.Get<BaseCardTowerDeployer>();
        _spellDeployer = ServiceLocator.Get<BaseCardSpellDeployer>();
        _spawnDeployer = ServiceLocator.Get<BaseCardSpawnEnemyDeployer>();

        BotContext ctx = BuildContext();
        _brain ??= new HeuristicBotBrain(botSettings);

        while (gameFlow.CurrentGameState.Value == GameState.InMatch)
        {
            float jitter = Random.Range(-botSettings.DecisionJitter, botSettings.DecisionJitter);
            yield return new WaitForSeconds(Mathf.Max(0.1f, botSettings.DecisionInterval + jitter));

            if (gameFlow.CurrentGameState.Value != GameState.InMatch) break;

            ExecuteDecision(_brain.Decide(ctx));
        }
    }

    private BotContext BuildContext()
    {
        return new BotContext
        {
            Team = _botTeam,
            EnemyTeam = _botTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue,
            Mana = ServiceLocator.Get<BaseServerManaManager>(),
            Hand = ServiceLocator.Get<BaseCardHandManager>(),
            Health = ServiceLocator.Get<BaseServerPlayerHealthManager>(),
            Cards = cardDataListSO,
            OwnPlaceables = CacheOwnPlaceables(_botTeam),
        };
    }

    // The bot's own defensive tower slots (its lane). Static scene objects, so cache once at match start.
    private List<AbstractPlaceable> CacheOwnPlaceables(TeamType team)
    {
        var result = new List<AbstractPlaceable>();
        AbstractPlaceable[] all = FindObjectsByType<AbstractPlaceable>(FindObjectsSortMode.None);
        foreach (AbstractPlaceable p in all)
        {
            TeamIdentifier id = p.GetComponentInParent<TeamIdentifier>();
            if (id != null && id.TeamType == team)
                result.Add(p);
        }
        GameLog.Info($"[BotController] Cached {result.Count} placeable slots for team {team}.");
        return result;
    }

    private void ExecuteDecision(BotDecision decision)
    {
        switch (decision.Kind)
        {
            case BotActionKind.Tower:
                _towerDeployer.TryDeployTower(_botTeam, decision.Card, decision.ServerPosition,
                    NetworkManager.ServerClientId, out _);
                break;
            case BotActionKind.Spell:
                _spellDeployer.TryDeploySpell(_botTeam, decision.Card, decision.ServerPosition);
                break;
            case BotActionKind.SpawnEnemy:
                _spawnDeployer.TryDeploySpawnEnemy(_botTeam, BotAuthId, decision.Card);
                break;
        }
    }
}
