using System;
using Unity.Netcode;
using UnityEngine;


public class CardSpellDeployer : NetworkBehaviour
{
    public static CardSpellDeployer Instance { get; private set; }

    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SpellDataListSO spellDataListSO;
    [SerializeField] private LayersSettingsSO layersSettingsSO;

    public event Action<SpellSpawnResult> OnSpellResult;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Debug.LogError("Multiple instances of CardSpellDeployer detected. This is not allowed.");
            Destroy(this);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSpellCardServerRpc(CardType cardType, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        TeamType team = TeamManager.Instance.GetTeam(clientId);
        
        if (team == TeamType.None)
        {
            Debug.LogError($"Client {clientId} does not have a team.");
            PlaceResultRpc(new SpellSpawnResult
            {
                CardType = cardType,
                Validation = SpellValidation.Invalid(SpellInvalidReason.NoTeam),
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }
        
        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not SpellCardDataSO  spellCardData)
        {
            PlaceResultRpc(new SpellSpawnResult
            {
                CardType = cardType,
                Validation = SpellValidation.Invalid(SpellInvalidReason.NotSuccess),
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }

        if (!ServerManaManager.Instance.TrySpendMana(team, spellCardData.Cost))
        {
            PlaceResultRpc(new SpellSpawnResult
            {
                CardType = cardType,
                Validation = SpellValidation.Invalid(SpellInvalidReason.NotEnoughMana),
            }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            return;
        }

        // ServerWaveManager.Instance.SendEnemyFromPlayer(spellCardData.EnemyType, clientId);
        
        PlaceResultRpc(new SpellSpawnResult
        {
            CardType = cardType,
            Validation = SpellValidation.Valid,
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }
    
    [Rpc(SendTo.SpecifiedInParams)]
    private void PlaceResultRpc(SpellSpawnResult result, RpcParams rpcParams = default)
    {
        OnSpellResult?.Invoke(result);
    }

}

public struct SpellSpawnResult : INetworkSerializable
{
    public CardType CardType;
    public SpellValidation Validation;
    public Vector2 Position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardType);
        serializer.SerializeValue(ref Validation);
        serializer.SerializeValue(ref Position);
    }
}