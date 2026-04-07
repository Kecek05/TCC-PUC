using System;
using Unity.Netcode;
using UnityEngine;

public class CardTowerDeployer : NetworkBehaviour
{
    public static CardTowerDeployer Instance { get; private set; }

    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private TowerDataListSO towerDataListSO;
    [SerializeField] private LayersSettingsSO layersSettingsSO;

    public event Action<PlaceResult> OnPlaceResult;
    
    private void Awake()
    {
        Instance = this;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPlaceCardServerRpc(CardType cardType, Vector2 placePosition, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        TeamType team = TeamManager.Instance.GetTeam(clientId);
        
        if (team == TeamType.None)
        {
            Debug.LogError($"Client {clientId} does not have a team.");
            PlaceResultRpc(new PlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.Invalid(TowerReason.NotSuccess),
                Position = placePosition
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }
        
        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not TowerCardDataSO towerCardData)
        {
            PlaceResultRpc(new PlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.Invalid(TowerReason.NotSuccess),
                Position = placePosition
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }

        var hit = FindClosestValidPlaceable(placePosition, team);

        if (hit.placeable == null)
        {
            PlaceResultRpc(new PlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.Invalid(TowerReason.NotSuccess),
                Position = placePosition
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }
        
        if (!ServerManaManager.Instance.CanAfford(team, towerCardData.Cost))
        {
            PlaceResultRpc(new PlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.Invalid(TowerReason.NotEnoughMana),
                Position = placePosition
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }
        
        if (hit.placeable.IsOccupied() && hit.placeable.OccupiedTower.Data.TowerType != towerCardData.TowerType)
        {
            PlaceResultRpc(new PlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.Invalid(TowerReason.AlreadyOccupied),
                Position = placePosition
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }
        
        if (hit.placeable.IsOccupied())
        {
            //Level Up
            TowerManager towerManager = hit.placeable.OccupiedTower.GetComponent<TowerManager>();

            if (!towerManager.ServerCombat.CanUpgradeTower())
            {
                PlaceResultRpc(new PlaceResult
                {
                    CardType = cardType,
                    Validation = TowerValidation.Invalid(TowerReason.NotSuccessMaxLevel),
                    Position = placePosition
                }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            if (!ServerManaManager.Instance.TrySpendMana(team, towerCardData.Cost))
            {
                PlaceResultRpc(new PlaceResult
                {
                    CardType = cardType,
                    Validation = TowerValidation.Invalid(TowerReason.NotEnoughMana),
                    Position = placePosition
                }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }
            
            towerManager.ServerCombat.UpgradeTower(1);
            
            PlaceResultRpc(new PlaceResult
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
            
            if (!ServerManaManager.Instance.TrySpendMana(team, towerCardData.Cost))
            {
                PlaceResultRpc(new PlaceResult
                {
                    CardType = cardType,
                    Validation = TowerValidation.Invalid(TowerReason.NotEnoughMana),
                    Position = placePosition
                }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            if (towerManager.Team != null)
                towerManager.Team.SetTeamType(team);

            towerManager.NetworkObject.SpawnWithOwnership(clientId);
        
            PlaceResultRpc(new PlaceResult
            {
                CardType = cardType,
                Validation = TowerValidation.Success,
                Position = hit.placeable.PlaceablePoint.position
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
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
    private void PlaceResultRpc(PlaceResult result, RpcParams rpcParams = default)
    {
        OnPlaceResult?.Invoke(result);
    }

}

public struct PlaceResult : INetworkSerializable
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

