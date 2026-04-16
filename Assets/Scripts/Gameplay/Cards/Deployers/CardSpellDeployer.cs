using System;
using Unity.Netcode;
using UnityEngine;

public class CardSpellDeployer : NetworkBehaviour
{
    public static CardSpellDeployer Instance { get; private set; }

    [SerializeField] private CardDataListSO cardDataListSO;
    [SerializeField] private SpellDataListSO spellDataListSO;

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
    public void RequestSpellCardServerRpc(CardType cardType, Vector2 serverPosition, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        TeamType team = ServiceLocator.Get<BaseTeamManager>().GetTeam(clientId);

        if (team == TeamType.None)
        {
            SendFailure(clientId, cardType, SpellInvalidReason.NoTeam);
            return;
        }

        CardDataSO cardData = cardDataListSO.GetCardDataByType(cardType);
        if (cardData is not SpellCardDataSO spellCardData)
        {
            SendFailure(clientId, cardType, SpellInvalidReason.NotSuccess);
            return;
        }

        SpellDataSO spellData = spellDataListSO.GetSpellDataByType(spellCardData.SpellType);
        ISpellExecutor executor = SpellExecutorFactory.GetExecutor(spellCardData.SpellType);

        if (spellData == null || executor == null)
        {
            SendFailure(clientId, cardType, SpellInvalidReason.NotSuccess);
            return;
        }

        if (!ServiceLocator.Get<BaseServerManaManager>().TrySpendMana(team, spellCardData.Cost))
        {
            SendFailure(clientId, cardType, SpellInvalidReason.NotEnoughMana);
            return;
        }

        executor.Execute(new SpellExecutionContext
        {
            ServerPosition = serverPosition,
            CasterTeam = team,
            SpellData = spellData,
            CoroutineRunner = this,
        });

        SpawnSpellVisualRpc(spellCardData.SpellType, serverPosition, team);
        
        PlaceResultRpc(new SpellSpawnResult
        {
            CardType = cardType,
            Validation = SpellValidation.Valid,
            Position = serverPosition,
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    private void SendFailure(ulong clientId, CardType cardType, SpellInvalidReason reason)
    {
        PlaceResultRpc(new SpellSpawnResult
        {
            CardType = cardType,
            Validation = SpellValidation.Invalid(reason),
        }, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PlaceResultRpc(SpellSpawnResult result, RpcParams rpcParams = default)
    {
        OnSpellResult?.Invoke(result);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SpawnSpellVisualRpc(SpellType spellType, Vector2 serverPosition, TeamType casterTeam)
    {
        SpellDataSO spellData = spellDataListSO.GetSpellDataByType(spellType);
        if (spellData == null || spellData.VisualPrefab == null) return;

        Vector3 localPos = ServiceLocator.Get<BaseMapTranslator>().ServerToLocal(serverPosition, casterTeam);

        GameObject visual = Instantiate(spellData.VisualPrefab, localPos, Quaternion.identity);

        if (visual.TryGetComponent(out CosmeticSpellProjectile projectile))
        {
            projectile.Initialize(localPos, spellData.TravelTime, spellData.Range);
        }
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
