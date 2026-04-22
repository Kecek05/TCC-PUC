using System;
using Unity.Netcode;

public abstract class BaseCardSpawnEnemyDeployer : NetworkBehaviour
{
    public event Action<SpawnEnemyResult> OnSpawnResult;

    public abstract void RequestSpawnEnemyCardServer(CardType cardType, RpcParams rpcParams = default);

    protected void TriggerOnSpawnResult(SpawnEnemyResult spawnEnemyResult)
    {
        OnSpawnResult?.Invoke(spawnEnemyResult);
    }
}
