using System;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseCardSpellDeployer : NetworkBehaviour
{
    public event Action<SpellSpawnResult> OnSpellResult;

    public abstract void RequestSpellCardServer(CardType cardType, Vector2 serverPosition, RpcParams rpcParams = default);

    protected void TriggerOnSpellResult(SpellSpawnResult result)
    {
        OnSpellResult?.Invoke(result);
    }
}
