using System;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseCardTowerDeployer : NetworkBehaviour, ICardDeployer
{
    public event Action<CardDeployedEventArgs> OnCardDeployed;
    public event Action<TowerPlaceResult> OnPlaceResult;

    public abstract void RequestPlaceCardServer(CardType cardType, Vector2 placePosition,
        RpcParams rpcParams = default);

    /// <summary>Server-side deploy core (no RPC/clientId) so a bot can place/upgrade this tower directly.</summary>
    public abstract TowerValidation TryDeployTower(TeamType team, CardType cardType, Vector2 placePosition,
        ulong ownerClientId, out Vector2 resolvedPosition);

    public void TriggerOnCardDeployed(CardDeployedEventArgs args) => OnCardDeployed?.Invoke(args);

    protected void TriggerOnPlaceResult(TowerPlaceResult result) => OnPlaceResult?.Invoke(result);
}
