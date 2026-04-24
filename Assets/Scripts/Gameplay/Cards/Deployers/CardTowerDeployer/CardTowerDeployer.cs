using Unity.Netcode;
using UnityEngine;

public class CardTowerDeployer : BaseCardTowerDeployer
{

    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private TowerDataListSO towerDataListSO;
    [SerializeField] private LayersSettingsSO layersSettingsSO;
    
    private BaseTeamManager _teamManager;
    private BaseServerManaManager  _serverManaManager;
    
    private void Awake()
    {
        ServiceLocator.Register<BaseCardTowerDeployer>(this);
    }

    public override void OnNetworkSpawn()
    {
        _teamManager  = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();
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
        TeamType team = _teamManager.GetTeam(clientId);
        
        if (team == TeamType.None)
        {
            GameLog.Error($"Client {clientId} does not have a team.");
            SendFailure(clientId, cardType, TowerReason.NotSuccess, placePosition);
            return;
        }
        
        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not TowerCardDataSO towerCardData)
        {
            SendFailure(clientId, cardType, TowerReason.NotSuccess, placePosition);
            return;
        }

        var hit = FindClosestValidPlaceable(placePosition, team);

        if (hit.placeable == null)
        {
            SendFailure(clientId, cardType, TowerReason.NotSuccess, placePosition);
            return;
        }
        
        if (!_serverManaManager.CanAfford(team, towerCardData.Cost))
        {
            SendFailure(clientId, cardType, TowerReason.NotEnoughMana, placePosition);
            return;
        }
        
        if (hit.placeable.IsOccupied() && hit.placeable.OccupiedTower.Data.TowerType != towerCardData.TowerType)
        {
            SendFailure(clientId, cardType, TowerReason.AlreadyOccupied, placePosition);
            return;
        }
        
        if (hit.placeable.IsOccupied())
        {
            //Level Up
            TowerManager towerManager = hit.placeable.OccupiedTower.GetComponent<TowerManager>();

            if (!towerManager.ServerCombat.CanUpgradeTower())
            {
                SendFailure(clientId, cardType, TowerReason.NotSuccessMaxLevel, placePosition);
                return;
            }

            if (!_serverManaManager.TrySpendMana(team, towerCardData.Cost))
            {
                SendFailure(clientId, cardType, TowerReason.NotEnoughMana, placePosition);
                return;
            }

            towerManager.ServerCombat.IncrementTowerLevel(1);
            
            PlaceResultRpc(new TowerPlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.LevelUp,
                Position = hit.placeable.PlaceablePoint.position
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
        else
        {
            // Spawn server-authoritative
            GameObject newTower = Instantiate(towerCardData.TowerPrefab, hit.placeable.PlaceablePoint.position, Quaternion.identity);
            TowerManager towerManager = newTower.GetComponent<TowerManager>();
            hit.placeable.Occupy(towerManager);
            
            if (!_serverManaManager.TrySpendMana(team, towerCardData.Cost))
            {
                SendFailure(clientId, cardType, TowerReason.NotEnoughMana, placePosition);
                return;
            }

            if (towerManager.Team != null)
                towerManager.Team.SetTeamType(team);

            towerManager.NetworkObject.SpawnWithOwnership(clientId);
        
            PlaceResultRpc(new TowerPlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.Success,
                Position = hit.placeable.PlaceablePoint.position
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }
    
    private void SendFailure(ulong clientId, CardType cardType, TowerReason reason, Vector2 placePosition)
    {
        PlaceResultRpc(new TowerPlaceResult
        {
            CardType = cardType,
            Validation = TowerValidation.Invalid(reason),
            Position = placePosition
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }
    
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

