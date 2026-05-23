using Unity.Netcode;
using UnityEngine;

public class CardSpellDeployer : BaseCardSpellDeployer
{
    
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SpellDataListSO spellDataListSO;
    
    private BaseMapTranslator _mapTranslator;
    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;
    private BasePlayersDataManager _playersDataManager;
    private BaseCardHandManager _cardHandManager;
    private CardDeploymentBus _cardDeploymentBus;

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

        SpellSpawnResult result = TrySpellInternal(team, cardType, serverPosition);
        PlaceResultRpc(result, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    /// <summary>
    /// Server-internal entry point: same validation + execution pipeline as the RPC, but takes
    /// the acting team explicitly. Both the RPC handler (real player) and bot AI call this.
    /// Returns the result synchronously instead of going through PlaceResultRpc.
    /// </summary>
    public SpellSpawnResult TrySpellInternal(TeamType team, CardType cardType, Vector2 serverPosition)
    {
        if (team == TeamType.None)
            return Fail(cardType, SpellInvalidReason.NoTeam);

        if (!_cardHandManager.TeamHasCardInHand(team, cardType))
        {
            GameLog.Error($"Team {team} tried to play {cardType} but it's not in hand.");
            return Fail(cardType, SpellInvalidReason.NotInHand);
        }

        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not SpellCardDataSO spellCardData)
            return Fail(cardType, SpellInvalidReason.NotSuccess);

        SpellDataSO spellData = spellDataListSO.GetSpellDataByType(spellCardData.SpellType);
        ISpellExecutor executor = SpellExecutorFactory.GetExecutor(spellCardData.SpellType);

        if (spellData == null || executor == null)
            return Fail(cardType, SpellInvalidReason.NotSuccess);

        if (!_serverManaManager.TrySpendMana(team, spellCardData.Cost))
            return Fail(cardType, SpellInvalidReason.NotEnoughMana);

        executor.Execute(new SpellExecutionContext
        {
            ServerPosition = serverPosition,
            CasterTeam = team,
            SpellData = spellData,
            CoroutineRunner = this,
        });

        SpawnSpellVisualRpc(spellCardData.SpellType, serverPosition, team);

        TriggerOnCardDeployed(new CardDeployedEventArgs
        {
            TeamDeployed = team,
            CardDeployed = cardType
        });

        return new SpellSpawnResult
        {
            CardType = cardType,
            Validation = SpellValidation.Valid,
            Position = serverPosition,
        };
    }

    private static SpellSpawnResult Fail(CardType cardType, SpellInvalidReason reason) =>
        new SpellSpawnResult
        {
            CardType = cardType,
            Validation = SpellValidation.Invalid(reason),
        };

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
