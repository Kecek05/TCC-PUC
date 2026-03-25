using System;
using Unity.Netcode;
using UnityEngine;

public class CardDeployer : NetworkBehaviour
{
    public static CardDeployer Instance { get; private set; }

    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private float castRadius = 0.5f;
    [SerializeField] private LayerMask placeableLayerMask;

    public event Action<PlaceResult> OnPlaceResult;
    
    private void Awake()
    {
        Instance = this;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPlaceCardServerRpc(int cardId, Vector2 placePosition, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        TeamType team = TeamManager.Instance.GetTeam(clientId);
        CardDataSO cardData = cardDataListSO.GetCardDataById(cardId);
        if (cardData == null) return;
        
        var hit = FindClosestValidPlaceable(placePosition, team);
        if (hit.placeable == null || hit.placeable.IsOccupied())
        {
            PlaceResultRpc(new PlaceResult
            {
                CardId = cardId,
                Success = false,
                Position = placePosition
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));

            return;
        };

        // Spawn server-authoritative
        hit.placeable.Place();
        var newTower = Instantiate(cardData.CardPrefab, hit.placeable.PlaceablePoint.position, Quaternion.identity);
        newTower.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        
        PlaceResultRpc(new PlaceResult
        {
            CardId = cardId,
            Success = true,
            Position = hit.placeable.PlaceablePoint.position
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }
    
    private (IPlaceable placeable, TeamIdentifier team) FindClosestValidPlaceable(Vector2 origin, TeamType requiredTeam)
    {
        var hits = Physics2D.CircleCastAll(origin, castRadius, Vector2.zero, 10f, placeableLayerMask);

        IPlaceable closest = null;
        TeamIdentifier closestTeam = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var team = hit.collider.GetComponentInParent<TeamIdentifier>();
            if (team == null || team.TeamType != requiredTeam) continue;

            var placeable = hit.collider.GetComponentInParent<IPlaceable>();
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
    public int CardId;
    public bool Success;
    public Vector2 Position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardId);
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref Position);
    }
}

