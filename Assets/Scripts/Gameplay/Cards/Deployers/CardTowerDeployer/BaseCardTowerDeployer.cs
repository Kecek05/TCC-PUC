using System;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseCardTowerDeployer : NetworkBehaviour
{
    public event Action<TowerPlaceResult> OnPlaceResult;

    public abstract void RequestPlaceCardServer(CardType cardType, Vector2 placePosition,
        RpcParams rpcParams = default);

    protected void TriggerOnPlaceResult(TowerPlaceResult result)
    {
        OnPlaceResult?.Invoke(result);
    }
}
