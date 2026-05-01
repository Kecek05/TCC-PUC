using Unity.Netcode;
using UnityEngine;

public class CardSpellDeployer : BaseCardSpellDeployer
{
    
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SpellDataListSO spellDataListSO;
    
    private BaseMapTranslator _mapTranslator;
    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;
    private PlayersDataManager _playersDataManager;

    private void Awake()
    {
        ServiceLocator.Register<BaseCardSpellDeployer>(this);
    }

    public override void OnNetworkSpawn()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _mapTranslator = ServiceLocator.Get<BaseMapTranslator>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();

        if (IsServer)
        {
            _playersDataManager = ServiceLocator.Get<PlayersDataManager>();
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
        ServiceLocator.Unregister<BaseCardSpellDeployer>();
        base.OnDestroy();
    }

    public override void RequestSpellCardServer(CardType cardType, Vector2 serverPosition, RpcParams rpcParams = default)
    {
        SendRequestToServerRpc(cardType, serverPosition, rpcParams);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SendRequestToServerRpc(CardType cardType, Vector2 serverPosition, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string authId = _playersDataManager.GetAuthIdByClientId(clientId);
        TeamType team = _teamManager.GetTeam(authId);

        if (team == TeamType.None)
        {
            SendFailure(clientId, cardType, SpellInvalidReason.NoTeam);
            return;
        }

        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not SpellCardDataSO spellCardData)
        {
            SendFailure(clientId, cardType, SpellInvalidReason.NotSuccess);
            return;
        }

        SpellDataSO spellData = spellDataListSO.GetSpellDataByType(spellCardData.SpellType);
        ISpellExecutor executor = SpellExecutorFactory.GetExecutor(spellCardData.SpellType);

        if (spellData == null || executor == null)
        {
            SendFailure(clientId, cardType, SpellInvalidReason.NotSuccess);
            return;
        }

        if (!_serverManaManager.TrySpendMana(team, spellCardData.Cost))
        {
            SendFailure(clientId, cardType, SpellInvalidReason.NotEnoughMana);
            return;
        }

        executor.Execute(new SpellExecutionContext
        {
            ServerPosition = serverPosition,
            CasterTeam = team,
            SpellData = spellData,
            CoroutineRunner = this,
        });

        SpawnSpellVisualRpc(spellCardData.SpellType, serverPosition, team);

        PlaceResultRpc(new SpellSpawnResult
        {
            CardType = cardType,
            Validation = SpellValidation.Valid,
            Position = serverPosition,
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));

        TriggerOnCardDeployed(new CardDeployedEventArgs
        {
            TeamDeployed = team,
            CardDeployed = cardType
        });
    }

    private void SendFailure(ulong clientId, CardType cardType, SpellInvalidReason reason)
    {
        PlaceResultRpc(new SpellSpawnResult
        {
            CardType = cardType,
            Validation = SpellValidation.Invalid(reason),
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PlaceResultRpc(SpellSpawnResult result, RpcParams rpcParams = default)
    {
        TriggerOnSpellResult(result);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SpawnSpellVisualRpc(SpellType spellType, Vector2 serverPosition, TeamType casterTeam)
    {
        SpellDataSO spellData = spellDataListSO.GetSpellDataByType(spellType);
        if (spellData == null || spellData.VisualPrefab == null) return;

        Vector3 localPos = _mapTranslator.ServerToLocal(serverPosition, casterTeam);

        GameObject visual = Instantiate(spellData.VisualPrefab, localPos, Quaternion.identity);

        if (visual.TryGetComponent(out CosmeticSpellProjectile projectile))
        {
            projectile.Initialize(localPos, spellData.TravelTime, spellData.Range);
        }
    }
}

public struct SpellSpawnResult : INetworkSerializable
{
    public CardType CardType;
    public SpellValidation Validation;
    public Vector2 Position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardType);
        serializer.SerializeValue(ref Validation);
        serializer.SerializeValue(ref Position);
    }
}
