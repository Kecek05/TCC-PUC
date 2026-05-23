using Unity.Netcode;
using UnityEngine;

public class CardTowerDeployer : BaseCardTowerDeployer
{

    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private TowerDataListSO towerDataListSO;
    [SerializeField] private LayersSettingsSO layersSettingsSO;
    
    private BaseTeamManager _teamManager;
    private BaseServerManaManager  _serverManaManager;
    private BasePlayersDataManager _playersDataManager;
    private BaseCardHandManager _cardHandManager;
    private CardDeploymentBus _cardDeploymentBus;

    private void Awake()
    {
        ServiceLocator.Register<BaseCardTowerDeployer>(this);
    }

    public override void OnNetworkSpawn()
    {
        _teamManager  = ServiceLocator.Get<BaseTeamManager>();
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
        ServiceLocator.Unregister<BaseCardTowerDeployer>();
        base.OnDestroy();
    }

    public override void RequestPlaceCardServer(CardType cardType, Vector2 placePosition, RpcParams rpcParams = default)
    {
        SendRequestToServerRpc(cardType, placePosition, rpcParams);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SendRequestToServerRpc(CardType cardType, Vector2 placePosition, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        string authId = _playersDataManager.GetAuthIdByClientId(clientId);
        TeamType team = _teamManager.GetTeam(authId);

        if (team == TeamType.None)
            GameLog.Error($"Client {clientId} (AuthId {authId}) does not have a team.");

        TowerPlaceResult result = TryPlaceTowerInternal(team, clientId, cardType, placePosition);
        PlaceResultRpc(result, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    /// <summary>
    /// Server-internal entry point: same validation + spawn pipeline as the RPC, but takes the
    /// acting team and owner client id explicitly. Both the RPC handler (real player) and bot
    /// AI call this. Returns the result synchronously instead of going through PlaceResultRpc.
    /// </summary>
    public TowerPlaceResult TryPlaceTowerInternal(TeamType team, ulong ownerClientId, CardType cardType, Vector2 placePosition)
    {
        if (team == TeamType.None)
            return Fail(cardType, TowerReason.NotSuccess, placePosition);

        if (!_cardHandManager.TeamHasCardInHand(team, cardType))
        {
            GameLog.Error($"Team {team} tried to play {cardType} but it's not in hand.");
            return Fail(cardType, TowerReason.NotInHand, placePosition);
        }

        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not TowerCardDataSO towerCardData)
            return Fail(cardType, TowerReason.NotSuccess, placePosition);

        var hit = FindClosestValidPlaceable(placePosition, team);

        if (hit.placeable == null)
        {
            GameLog.Warn($"Team {team} tried to place a tower at {placePosition} but no valid placeable was found.");
            return Fail(cardType, TowerReason.NotSuccess, placePosition);
        }

        if (!_serverManaManager.CanAfford(team, towerCardData.Cost))
            return Fail(cardType, TowerReason.NotEnoughMana, placePosition);

        if (hit.placeable.IsOccupied() && hit.placeable.OccupiedTower.Data.TowerType != towerCardData.TowerType)
            return Fail(cardType, TowerReason.AlreadyOccupied, placePosition);

        if (hit.placeable.IsOccupied())
        {
            // Level Up
            TowerManager existingTower = hit.placeable.OccupiedTower.GetComponent<TowerManager>();

            if (!existingTower.ServerTowerCombat.CanUpgradeTower())
                return Fail(cardType, TowerReason.NotSuccessMaxLevel, placePosition);

            if (!_serverManaManager.TrySpendMana(team, towerCardData.Cost))
                return Fail(cardType, TowerReason.NotEnoughMana, placePosition);

            existingTower.ServerTowerCombat.IncrementTowerLevel(1);

            TriggerOnCardDeployed(new CardDeployedEventArgs
            {
                TeamDeployed = team,
                CardDeployed = cardType
            });

            return new TowerPlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.LevelUp,
                Position = hit.placeable.PlaceablePoint.position
            };
        }

        // Spawn server-authoritative
        GameObject newTower = Instantiate(towerCardData.TowerPrefab, hit.placeable.PlaceablePoint.position, Quaternion.identity);
        TowerManager towerManager = newTower.GetComponent<TowerManager>();
        hit.placeable.Occupy(towerManager);

        if (!_serverManaManager.TrySpendMana(team, towerCardData.Cost))
            return Fail(cardType, TowerReason.NotEnoughMana, placePosition);

        if (towerManager.Team != null)
            towerManager.Team.SetTeamType(team);

        towerManager.NetworkObject.SpawnWithOwnership(ownerClientId);

        TriggerOnCardDeployed(new CardDeployedEventArgs
        {
            TeamDeployed = team,
            CardDeployed = cardType
        });

        return new TowerPlaceResult
        {
            CardType = cardType,
            Validation = TowerValidation.Success,
            Position = hit.placeable.PlaceablePoint.position
        };
    }

    private static TowerPlaceResult Fail(CardType cardType, TowerReason reason, Vector2 placePosition) =>
        new TowerPlaceResult
        {
            CardType = cardType,
            Validation = TowerValidation.Invalid(reason),
            Position = placePosition
        };
    
    /// <summary>
    /// Finds the closest valid placeable within a certain radius of the origin point that belongs to the required team.
    /// </summary>
    /// <param name="origin"> Origin Position</param>
    /// <param name="requiredTeam"> Team</param>
    /// <returns> Closest Placeable and the Team of the Placeable</returns>
    private (IPlaceable placeable, TeamIdentifier team) FindClosestValidPlaceable(Vector2 origin, TeamType requiredTeam)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, layersSettingsSO.PlaceableRadius, Vector2.zero, 10f, layersSettingsSO.PlaceableLayer);

        IPlaceable closest = null;
        TeamIdentifier closestTeam = null;
        float closestDist = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            TeamIdentifier team = hit.collider.GetComponentInParent<TeamIdentifier>();
            GameLog.Info($"Found TeamIdentifier {team?.TeamType} at {hit.collider.transform.position} for required team {requiredTeam}");
            if (team == null || team.TeamType != requiredTeam) continue;

            IPlaceable placeable = hit.collider.GetComponentInParent<IPlaceable>();
            if (placeable == null) continue;

            float dist = Vector2.Distance(origin, hit.collider.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = placeable;
                closestTeam = team;
            }
        }

        return (closest, closestTeam);
    }
    
    [Rpc(SendTo.SpecifiedInParams)]
    private void PlaceResultRpc(TowerPlaceResult result, RpcParams rpcParams = default)
    {
        TriggerOnPlaceResult(result);
    }

}

public struct TowerPlaceResult : INetworkSerializable
{
    public CardType CardType;
    public TowerValidation Validation;
    public Vector2 Position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardType);
        serializer.SerializeValue(ref Validation);
        serializer.SerializeValue(ref Position);
    }
}

