using System;
using Unity.Netcode;

public abstract class BaseCardSpawnEnemyDeployer : NetworkBehaviour, ICardDeployer
{
    public event Action<CardDeployedEventArgs> OnCardDeployed;
    public event Action<SpawnEnemyResult> OnSpawnResult;

    public abstract void RequestSpawnEnemyCardServer(CardType cardType, RpcParams rpcParams = default);

    protected void TriggerOnCardDeployed(CardDeployedEventArgs args) => OnCardDeployed?.Invoke(args);

    protected void TriggerOnSpawnResult(SpawnEnemyResult spawnEnemyResult) => OnSpawnResult?.Invoke(spawnEnemyResult);
}
