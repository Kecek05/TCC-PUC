using System;
using Unity.Netcode;

public abstract class BaseCardSpawnEnemyDeployer : NetworkBehaviour, ICardDeployer
{
    public event Action<CardDeployedEventArgs> OnCardDeployed;
    public event Action<SpawnEnemyResult> OnSpawnResult;

    public abstract void RequestSpawnEnemyCardServer(CardType cardType, RpcParams rpcParams = default);

    /// <summary>
    /// Server-internal entry point bypassing the RPC. Used by AI bots that don't have a
    /// real ClientId / RPC channel. Caller passes the acting team and AuthId explicitly
    /// (the AuthId is forwarded to ServerWaveManager.SendEnemyFromPlayer).
    /// </summary>
    public abstract SpawnEnemyResult TrySpawnEnemyInternal(TeamType team, string authId, CardType cardType);

    protected void TriggerOnCardDeployed(CardDeployedEventArgs args) => OnCardDeployed?.Invoke(args);

    protected void TriggerOnSpawnResult(SpawnEnemyResult spawnEnemyResult) => OnSpawnResult?.Invoke(spawnEnemyResult);
}
