using System;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseCardSpellDeployer : NetworkBehaviour, ICardDeployer
{
    public event Action<CardDeployedEventArgs> OnCardDeployed;
    public event Action<SpellSpawnResult> OnSpellResult;

    public abstract void RequestSpellCardServer(CardType cardType, Vector2 serverPosition, RpcParams rpcParams = default);

    protected void TriggerOnCardDeployed(CardDeployedEventArgs args) => OnCardDeployed?.Invoke(args);

    protected void TriggerOnSpellResult(SpellSpawnResult result) => OnSpellResult?.Invoke(result);
}
