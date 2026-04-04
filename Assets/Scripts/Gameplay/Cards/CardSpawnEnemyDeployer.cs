using System;
using Unity.Netcode;
using UnityEngine;

public class CardSpawnEnemyDeployer : NetworkBehaviour
{
    public static CardSpawnEnemyDeployer Instance { get; private set; }
    
    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private LayerMask placeableLayerMask;
    
    public event Action<SpawnEnemyResult> OnSpawnResult;
    
    private void Awake()
    {
        Instance = this;
    }
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSpawnEnemyCardServerRpc(CardType cardType, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        TeamType team = TeamManager.Instance.GetTeam(clientId);
        
        if (team == TeamType.None)
        {
            Debug.LogError($"Client {clientId} does not have a team.");
            SpawnResultRpc(new SpawnEnemyResult
            {
                CardType = cardType,
                Validation = CardValidation.Invalid(CardInvalidReason.NoTeam),
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }
        
        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not SpawnEnemyCardDataSO spawnCardData)
        {
            SpawnResultRpc(new SpawnEnemyResult
            {
                CardType = cardType,
                Validation = CardValidation.Invalid(CardInvalidReason.None),
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }

        if (!ServerManaManager.Instance.TrySpendMana(team, spawnCardData.Cost))
        {
            SpawnResultRpc(new SpawnEnemyResult
            {
                CardType = cardType,
                Validation = CardValidation.Invalid(CardInvalidReason.NotEnoughMana),
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }

        ServerWaveManager.Instance.SendEnemyFromPlayer(spawnCardData.EnemyType, clientId);
        
        SpawnResultRpc(new SpawnEnemyResult
        {
            CardType = cardType,
            Validation = CardValidation.Valid,
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }
    
    [Rpc(SendTo.SpecifiedInParams)]
    private void SpawnResultRpc(SpawnEnemyResult result, RpcParams rpcParams = default)
    {
        OnSpawnResult?.Invoke(result);
    }
}
public struct SpawnEnemyResult : INetworkSerializable
{
    public CardType CardType;
    public CardValidation Validation;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardType);
        serializer.SerializeValue(ref Validation);
    }
}
