using Unity.Netcode;
using UnityEngine;

public class CardSpawnEnemyDeployer : BaseCardSpawnEnemyDeployer
{

    [SerializeField] private CardDataListSO cardDataListSO;
    
    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;
    private BaseServerWaveManager _serverWaveManager;
    
    public void Awake()
    {
        ServiceLocator.Register<BaseCardSpawnEnemyDeployer>(this);
    }

    public override void OnNetworkSpawn()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();
        _serverWaveManager = ServiceLocator.Get<BaseServerWaveManager>();
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
        TeamType team = _teamManager.GetTeam(clientId);
        
        if (team == TeamType.None)
        {
            GameLog.Error($"Client {clientId} does not have a team.");
            SendFailure(clientId, cardType, CardInvalidReason.NoTeam);
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

        _serverWaveManager.SendEnemyFromPlayer(spawnCardData.EnemyType, clientId);
        
        SpawnResultRpc(new SpawnEnemyResult
        {
            CardType = cardType,
            Validation = CardValidation.Valid,
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
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
