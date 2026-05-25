using System;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseCardTowerDeployer : NetworkBehaviour, ICardDeployer
{
    public event Action<CardDeployedEventArgs> OnCardDeployed;
    public event Action<TowerPlaceResult> OnPlaceResult;

    public abstract void RequestPlaceCardServer(CardType cardType, Vector2 placePosition,
        RpcParams rpcParams = default);

    /// <summary>
    /// Server-internal entry point bypassing the RPC. Used by AI bots that don't have a
    /// real ClientId / RPC channel. Caller passes the acting team and the NetworkObject
    /// owner clientId explicitly (real players pass their own clientId; bots typically
    /// pass NetworkManager.ServerClientId).
    /// </summary>
    public abstract TowerPlaceResult TryPlaceTowerInternal(TeamType team, ulong ownerClientId, CardType cardType, Vector2 placePosition);

    protected void TriggerOnCardDeployed(CardDeployedEventArgs args) => OnCardDeployed?.Invoke(args);

    protected void TriggerOnPlaceResult(TowerPlaceResult result) => OnPlaceResult?.Invoke(result);
}
