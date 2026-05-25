using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only AI controller. Owns the perceive → decide → act tick loop for one bot.
/// Call InitializeAsBot once (after the bot has been registered in PlayersDataManager and
/// assigned a team in TeamManager) to start the loop.
/// </summary>
public class BotController : MonoBehaviour
{
    [Title("References")]
    [SerializeField, Required] private BotProfileSO profile;
    [SerializeField, Required] private CardDataListSO cardDataListSO;
    [SerializeField, Required] private TowerDataListSO towerDataListSO;

    [Title("Debug")]
    [SerializeField] private bool verbose;

    [ShowInInspector, ReadOnly] public TeamType Team { get; private set; }
    [ShowInInspector, ReadOnly] public string AuthId { get; private set; }
    [ShowInInspector, ReadOnly] public ulong ClientId { get; private set; }
    [ShowInInspector, ReadOnly] public bool IsInitialized { get; private set; }

    [Title("Last Decision")]
    [ShowInInspector, ReadOnly] private CardType _lastCard;
    [ShowInInspector, ReadOnly] private float _lastScore;
    [ShowInInspector, ReadOnly] private Vector2 _lastPosition;

    private BotWorldView _world;
    private BotDecisionMaker _decision;
    private BotActionExecutor _executor;
    private BotContext _ctx;
    private System.Random _rng;
    private Coroutine _tickCoroutine;

    public void InitializeAsBot(TeamType team, string authId, ulong clientId, List<CardType> deck)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            GameLog.Error("[BotController] InitializeAsBot must be called on the server.");
            return;
        }
        if (IsInitialized)
        {
            GameLog.Warn("[BotController] Already initialized — ignoring.");
            return;
        }

        Team = team;
        AuthId = authId;
        ClientId = clientId;

        BaseServerManaManager mana = ServiceLocator.Get<BaseServerManaManager>();
        BaseCardHandManager hand = ServiceLocator.Get<BaseCardHandManager>();
        BaseServerPlayerHealthManager health = ServiceLocator.Get<BaseServerPlayerHealthManager>();
        BaseServerWaveManager waves = ServiceLocator.Get<BaseServerWaveManager>();
        BaseGameFlowManager gameFlow = ServiceLocator.Get<BaseGameFlowManager>();
        BaseCardTowerDeployer towerDep = ServiceLocator.Get<BaseCardTowerDeployer>();
        BaseCardSpellDeployer spellDep = ServiceLocator.Get<BaseCardSpellDeployer>();
        BaseCardSpawnEnemyDeployer spawnDep = ServiceLocator.Get<BaseCardSpawnEnemyDeployer>();

        _rng = new System.Random();
        _world = new BotWorldView(team, mana, hand, health, waves, gameFlow, cardDataListSO, towerDataListSO);
        _executor = new BotActionExecutor(towerDep, spellDep, spawnDep);
        _decision = new BotDecisionMaker(BotScorerRegistry.CreateDefault());
        _ctx = new BotContext(_world, profile, _rng);

        hand.SetDeckForPlayer(team, deck);

        IsInitialized = true;
        _tickCoroutine = StartCoroutine(TickLoop());

        GameLog.Info($"[BotController] Initialized as {team} (authId={authId}, clientId={clientId}, deck={deck.Count} cards)");
    }

    private IEnumerator TickLoop()
    {
        yield return new WaitUntil(() => _world.IsInMatch());

        while (IsInitialized)
        {
            yield return new WaitForSeconds(profile.DecisionTickRate);
            if (!IsInitialized) yield break;
            if (!_world.IsInMatch()) continue;

            float mana = _world.SelfMana();
            if (mana < CheapestAffordableCost()) continue;

            if (!_decision.TryPickAction(_ctx, out ScoredCandidate winner, out CardDataSO winnerData))
            {
                if (verbose) GameLog.Info($"[Bot {Team}] no action this tick (mana={mana:F1})");
                continue;
            }

            if (profile.ReactionDelaySeconds > 0f)
                yield return new WaitForSeconds(profile.ReactionDelaySeconds);

            if (!IsInitialized) yield break;
            if (winnerData == null) continue;
            if (_world.SelfMana() - winnerData.Cost < profile.ManaSpendFloor) continue;

            _lastCard = winnerData.CardType;
            _lastScore = winner.Score;
            _lastPosition = winner.Position;

            _executor.Execute(Team, AuthId, winnerData, winner.Position);
        }
    }

    private float CheapestAffordableCost()
    {
        IReadOnlyList<CardType> hand = _world.Hand();
        if (hand.Count == 0) return float.MaxValue;

        float cheapest = float.MaxValue;
        for (int i = 0; i < hand.Count; i++)
        {
            CardDataSO data = _world.LookupCard(hand[i]);
            if (data != null && data.Cost < cheapest) cheapest = data.Cost;
        }
        return cheapest;
    }

    private void OnDestroy()
    {
        IsInitialized = false;
        if (_tickCoroutine != null)
        {
            StopCoroutine(_tickCoroutine);
            _tickCoroutine = null;
        }
    }
}
