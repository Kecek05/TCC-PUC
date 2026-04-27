using System;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseCardTowerDeployer : NetworkBehaviour, ICardDeployer
{
    public event Action<CardDeployedEventArgs> OnCardDeployed;
    public event Action<TowerPlaceResult> OnPlaceResult;

    public abstract void RequestPlaceCardServer(CardType cardType, Vector2 placePosition,
        RpcParams rpcParams = default);

    protected void TriggerOnCardDeployed(CardDeployedEventArgs args) => OnCardDeployed?.Invoke(args);

    protected void TriggerOnPlaceResult(TowerPlaceResult result) => OnPlaceResult?.Invoke(result);
}
