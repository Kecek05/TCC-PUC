using System;
using Unity.Netcode;

public abstract class BaseCardSpawnEnemyDeployer : NetworkBehaviour, ICardDeployer
{
    public event Action<CardDeployedEventArgs> OnCardDeployed;
    public event Action<SpawnEnemyResult> OnSpawnResult;

    public abstract void RequestSpawnEnemyCardServer(CardType cardType, RpcParams rpcParams = default);

    /// <summary>Server-side deploy core (no RPC/clientId) so a bot can play this card type directly.</summary>
    public abstract CardValidation TryDeploySpawnEnemy(TeamType team, string authId, CardType cardType);

    public void TriggerOnCardDeployed(CardDeployedEventArgs args) => OnCardDeployed?.Invoke(args);

    protected void TriggerOnSpawnResult(SpawnEnemyResult spawnEnemyResult) => OnSpawnResult?.Invoke(spawnEnemyResult);
}
