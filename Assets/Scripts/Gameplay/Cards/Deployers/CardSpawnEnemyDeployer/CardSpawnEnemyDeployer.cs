using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class CardSpawnEnemyDeployer : BaseCardSpawnEnemyDeployer
{

    [SerializeField] private CardDataListSO cardDataListSO;
    
    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;
    private BaseServerWaveManager _serverWaveManager;
    private BasePlayersDataManager _playersDataManager;
    private BaseCardHandManager _cardHandManager;
    private CardDeploymentBus _cardDeploymentBus;

    public void Awake()
    {
        ServiceLocator.Register<BaseCardSpawnEnemyDeployer>(this);
    }

    public override void OnNetworkSpawn()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();
        _serverWaveManager = ServiceLocator.Get<BaseServerWaveManager>();

        if (IsServer)
        {
            _playersDataManager = ServiceLocator.Get<BasePlayersDataManager>();
            _cardHandManager = ServiceLocator.Get<BaseCardHandManager>();
            _cardDeploymentBus = ServiceLocator.Get<CardDeploymentBus>();
            _cardDeploymentBus.Register(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && _cardDeploymentBus != null)
            _cardDeploymentBus.Unregister(this);
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseCardSpawnEnemyDeployer>();
        base.OnDestroy();
    }
    
    public override void RequestSpawnEnemyCardServer(CardType cardType, RpcParams rpcParams = default)
    {
        SendRequestToServerRpc(cardType, rpcParams);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SendRequestToServerRpc(CardType cardType, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string authId = _playersDataManager.GetAuthIdByClientId(clientId);
        TeamType team = _teamManager.GetTeam(authId);

        CardValidation result = TryDeploySpawnEnemy(team, authId, cardType);
        if (result.IsValid)
            SpawnResultRpc(new SpawnEnemyResult
            {
                CardType = cardType,
                Validation = CardValidation.Valid,
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        else
            SendFailure(clientId, cardType, result.Reason);
    }

    /// <summary>
    /// Server-side "play this spawn-enemy card" core, callable directly (no RPC/clientId) so a bot can
    /// drive it. Runs the same hand/mana/spawn/hand-advance path a human's RPC uses. Caller feedback
    /// (SpawnResultRpc) is the wrapper's job; this returns the validation instead.
    /// </summary>
    public override CardValidation TryDeploySpawnEnemy(TeamType team, string authId, CardType cardType)
    {
        if (team == TeamType.None)
        {
            GameLog.Error($"AuthId {authId} does not have a team.");
            return CardValidation.Invalid(CardInvalidReason.NoTeam);
        }

        if (!_cardHandManager.TeamHasCardInHand(team, cardType))
        {
            GameLog.Error($"Team {team} tried to play {cardType} but it's not in hand.");
            return CardValidation.Invalid(CardInvalidReason.NotInHand);
        }

        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not SpawnEnemyCardDataSO spawnCardData)
            return CardValidation.Invalid(CardInvalidReason.None);

        if (!_serverManaManager.TrySpendMana(team, spawnCardData.Cost))
            return CardValidation.Invalid(CardInvalidReason.NotEnoughMana);

        // Resolved once per cast: an army's troops all share the level the card had when it was played.
        CardLevelScale cardScale = MatchCardLevels.ScaleFor(team, cardType);

        int spawnCount = Mathf.Max(1, spawnCardData.SpawnCount);
        if (spawnCount <= 1)
            _serverWaveManager.SendEnemyFromPlayer(spawnCardData.EnemyType, authId, cardScale);
        else
            StartCoroutine(SendEnemyArmy(spawnCardData.EnemyType, authId, spawnCount,
                spawnCardData.DelayBetweenSpawns, cardScale));

        TriggerOnCardDeployed(new CardDeployedEventArgs
        {
            TeamDeployed = team,
            CardDeployed = cardType
        });

        return CardValidation.Valid;
    }

    // Spawns several enemies of the same type, staggered by the card's own delay so they string out
    // in a row down the opponent's lane (mana was already spent once for the whole batch).
    private IEnumerator SendEnemyArmy(EnemyType enemyType, string authId, int count, float delayBetweenSpawns,
        CardLevelScale cardScale)
    {
        for (int i = 0; i < count; i++)
        {
            _serverWaveManager.SendEnemyFromPlayer(enemyType, authId, cardScale);
            if (i < count - 1 && delayBetweenSpawns > 0f)
                yield return new WaitForSeconds(delayBetweenSpawns);
        }
    }

    private void SendFailure(ulong clientId, CardType cardType, CardInvalidReason reason)
    {
        SpawnResultRpc(new SpawnEnemyResult
        {
            CardType = cardType,
            Validation = CardValidation.Invalid(reason),
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }
    
    [Rpc(SendTo.SpecifiedInParams)]
    private void SpawnResultRpc(SpawnEnemyResult result, RpcParams rpcParams = default)
    {
        TriggerOnSpawnResult(result);
    }
}

public struct SpawnEnemyResult : INetworkSerializable
{
    public CardType CardType;
    public CardValidation Validation;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardType);
        serializer.SerializeValue(ref Validation);
    }
}
