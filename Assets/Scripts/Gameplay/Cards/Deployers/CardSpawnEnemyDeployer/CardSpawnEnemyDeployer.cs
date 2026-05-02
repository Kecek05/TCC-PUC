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
            ServiceLocator.Get<CardDeploymentBus>()?.Register(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            ServiceLocator.Get<CardDeploymentBus>()?.Unregister(this);
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
        {
            GameLog.Error($"Client {clientId} (AuthId {authId}) does not have a team.");
            SendFailure(clientId, cardType, CardInvalidReason.NoTeam);
            return;
        }

        if (!_cardHandManager.TeamHasCardInHand(team, cardType))
        {
            GameLog.Error($"Client {clientId} (Team {team}) tried to play {cardType} but it's not in hand.");
            SendFailure(clientId, cardType, CardInvalidReason.NotInHand);
            return;
        }

        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not SpawnEnemyCardDataSO spawnCardData)
        {
            SendFailure(clientId, cardType, CardInvalidReason.None);
            return;
        }

        if (!_serverManaManager.TrySpendMana(team, spawnCardData.Cost))
        {
            SendFailure(clientId, cardType, CardInvalidReason.NotEnoughMana);
            return;
        }

        _serverWaveManager.SendEnemyFromPlayer(spawnCardData.EnemyType, authId);

        SpawnResultRpc(new SpawnEnemyResult
        {
            CardType = cardType,
            Validation = CardValidation.Valid,
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));

        TriggerOnCardDeployed(new CardDeployedEventArgs
        {
            TeamDeployed = team,
            CardDeployed = cardType
        });
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
