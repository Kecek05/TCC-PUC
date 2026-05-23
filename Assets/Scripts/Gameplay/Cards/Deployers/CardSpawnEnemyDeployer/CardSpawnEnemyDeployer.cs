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

        if (team == TeamType.None)
            GameLog.Error($"Client {clientId} (AuthId {authId}) does not have a team.");

        SpawnEnemyResult result = TrySpawnEnemyInternal(team, authId, cardType);
        SpawnResultRpc(result, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    /// <summary>
    /// Server-internal entry point: same validation + dispatch pipeline as the RPC, but takes
    /// the acting team and authId explicitly. Both the RPC handler (real player) and bot AI
    /// call this. Returns the result synchronously instead of going through SpawnResultRpc.
    /// </summary>
    public SpawnEnemyResult TrySpawnEnemyInternal(TeamType team, string authId, CardType cardType)
    {
        if (team == TeamType.None)
            return Fail(cardType, CardInvalidReason.NoTeam);

        if (!_cardHandManager.TeamHasCardInHand(team, cardType))
        {
            GameLog.Error($"Team {team} tried to play {cardType} but it's not in hand.");
            return Fail(cardType, CardInvalidReason.NotInHand);
        }

        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not SpawnEnemyCardDataSO spawnCardData)
            return Fail(cardType, CardInvalidReason.None);

        if (!_serverManaManager.TrySpendMana(team, spawnCardData.Cost))
            return Fail(cardType, CardInvalidReason.NotEnoughMana);

        _serverWaveManager.SendEnemyFromPlayer(spawnCardData.EnemyType, authId);

        TriggerOnCardDeployed(new CardDeployedEventArgs
        {
            TeamDeployed = team,
            CardDeployed = cardType
        });

        return new SpawnEnemyResult
        {
            CardType = cardType,
            Validation = CardValidation.Valid,
        };
    }

    private static SpawnEnemyResult Fail(CardType cardType, CardInvalidReason reason) =>
        new SpawnEnemyResult
        {
            CardType = cardType,
            Validation = CardValidation.Invalid(reason),
        };
    
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
