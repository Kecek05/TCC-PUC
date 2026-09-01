using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ServerCardHandManager : BaseCardHandManager, IOnDrawACard, IOnLocalDrawnACard, IOnLocalNextCardChanged
{
    [Title("Config")]
    [SerializeField] CardHandSettingsSO cardHandSettingsSO;
    [SerializeField] private CardDataListSO cardDataListSO;

    public event Action<TeamType, CardType> OnDrawACard;
    public event Action<CardType> OnLocalDrawACard;
    public event Action<CardType> OnLocalNextCardChanged;

    private ICardCostProvider _costs;
    private IMaxManaProvider _maxManaProvider;
    private CardDeploymentBus _deploymentBus;
    private BasePlayersDataManager  _playersDataManager;

    public void Awake()
    {
        ServiceLocator.Register<BaseCardHandManager>(this);
        ServiceLocator.Register<IOnDrawACard>(this);
        ServiceLocator.Register<IOnLocalDrawnACard>(this);
        ServiceLocator.Register<IOnLocalNextCardChanged>(this);
        _costs = cardDataListSO;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
            StartCoroutine(WaitForReady());
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (!IsServer) return;

        if (_maxManaProvider != null)
            _maxManaProvider.OnMaxManaChanged -= OnMaxManaChanged;

        if (_deploymentBus != null)
            _deploymentBus.OnAnyCardDeployed -= OnAnyCardDeployed;
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseCardHandManager>();
        ServiceLocator.Unregister<IOnDrawACard>();
        ServiceLocator.Unregister<IOnLocalDrawnACard>();
        ServiceLocator.Unregister<IOnLocalNextCardChanged>();
        base.OnDestroy();
    }

    private IEnumerator WaitForReady()
    {
        yield return new WaitUntil(
            () => ServiceLocator.Get<BaseServerManaManager>() != null
               && ServiceLocator.Get<CardDeploymentBus>() != null);

        _playersDataManager = ServiceLocator.Get<BasePlayersDataManager>();
        
        _maxManaProvider = ServiceLocator.Get<BaseServerManaManager>();
        _maxManaProvider.OnMaxManaChanged += OnMaxManaChanged;

        _deploymentBus = ServiceLocator.Get<CardDeploymentBus>();
        _deploymentBus.OnAnyCardDeployed += OnAnyCardDeployed;
    }

    private void OnAnyCardDeployed(CardDeployedEventArgs args) => NotifyCardPlayed(args.TeamDeployed, args.CardDeployed);

    public override void SetDeckForPlayer(TeamType teamType, List<CardType> cardsInDeck)
    {
        GameLog.Info($"[CardHandManager] SetHandForPlayer: Setting hand for {teamType} with deck of {cardsInDeck.Count} cards.");
        if (!IsServer)
        {
            GameLog.Error("[CardHandManager] SetHandForPlayer must be called on the server.");
            return;
        }

        if (cardsInDeck == null || cardsInDeck.Count == 0)
        {
            GameLog.Error($"[CardHandManager] SetHandForPlayer called for {teamType} with empty deck.");
            return;
        }

        float maxMana = _maxManaProvider != null ? _maxManaProvider.GetMaxMana(teamType) : float.MaxValue;
        HandData handData = HandData.Distribute(cardsInDeck, maxMana, _costs);

        SetServerHandData(teamType, handData);

        RefillHand(teamType, handData);
        PushSyncedState(teamType);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SendOnDrawLocalACardRpc(CardType drawnCard, RpcParams rpcParams = default)
    {
        OnLocalDrawACard?.Invoke(drawnCard);
    }
    
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void SendOnLocalNextCardChangedRpc(CardType nextCard, RpcParams rpcParams = default)
    {
        OnLocalNextCardChanged?.Invoke(nextCard);
    }

    public override void NotifyCardPlayed(TeamType teamType, CardType cardType)
    {
        GameLog.Info($"[CardHandManager] NotifyCardPlayed: {teamType} played {cardType}");
        if (!IsServer)
        {
            GameLog.Error("[CardHandManager] NotifyCardPlayed must be called on the server.");
            return;
        }

        HandData handData = GetServerHandData(teamType);
        if (handData == null)
        {
            GameLog.Error($"[CardHandManager] NotifyCardPlayed for {teamType} before hand was set.");
            return;
        }

        if (!handData.Play(cardType))
        {
            GameLog.Warn($"[CardHandManager] Played card {cardType} not found in {teamType} hand.");
            return;
        }

        RefillHand(teamType, handData);
        PushSyncedState(teamType);
    }

    private void OnMaxManaChanged(TeamType teamType, float newMax)
    {
        if (!IsServer) return;

        HandData handData = GetServerHandData(teamType);
        if (handData == null) return;

        // Unlocking is what usually makes room to grow, but refill either way: a hand left short because
        // the drawable pool was smaller than the hand at deal time should not have to wait for the next
        // unlock to catch up.
        handData.Unlock(newMax, _costs);

        RefillHand(teamType, handData);
        PushSyncedState(teamType);
    }

    /// <summary>
    /// Tops a hand up to <see cref="CardHandSettingsSO.HandSize"/>, or to whatever the drawable queue can
    /// supply when that is fewer. This is the single rule for how many cards a hand holds, so a hand that
    /// is short — the deck had fewer cards under the mana cap than there are slots, or an earlier draw
    /// found the queue empty — grows on its own the moment cards become drawable again.
    /// </summary>
    private void RefillHand(TeamType teamType, HandData handData)
    {
        while (handData.CardsTypeInHand.Count < cardHandSettingsSO.HandSize)
        {
            if (!handData.Draw(out CardType drawnCard)) break;

            OnDrawACard?.Invoke(teamType, drawnCard);
            SendDrawnCardToClient(teamType, drawnCard);
        }
    }

    private void PushSyncedState(TeamType teamType)
    {
        // Try-variant: a bot team legitimately has no client mapped, and the loud GetClientIdByTeamType
        // would paint an error line for every card the bot plays.
        if (!_playersDataManager.TryGetClientIdByTeamType(teamType, out ulong clientId)) return;
        if (!IsRealClient(clientId)) return;

        HandData handData = GetServerHandData(teamType);

        CardType nextVar = handData.QueuedCardsType.Count > 0 ? handData.QueuedCardsType.Peek() : CardType.None;

        SendOnLocalNextCardChangedRpc(nextVar, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    // A bot team has no network client, so TryGetClientIdByTeamType returns false for it. The
    // draw / next-card sync is purely client-side UI, so skip it silently when there is no real client
    // to receive it (the server-side HandData is unaffected, so the bot's hand still deals and advances).
    private void SendDrawnCardToClient(TeamType teamType, CardType drawnCard)
    {
        if (!_playersDataManager.TryGetClientIdByTeamType(teamType, out ulong clientId)) return;
        if (!IsRealClient(clientId)) return;

        SendOnDrawLocalACardRpc(drawnCard, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    private bool IsRealClient(ulong clientId)
    {
        return clientId != ulong.MaxValue && NetworkManager != null && NetworkManager.ConnectedClients.ContainsKey(clientId);
    }

    private HandData GetServerHandData(TeamType teamType) => teamType == TeamType.Blue ? BlueHandData : RedHandData;

    private void SetServerHandData(TeamType teamType, HandData data)
    {
        if (teamType == TeamType.Blue)
            BlueHandData = data;
        else if (teamType == TeamType.Red)
            RedHandData = data;
        else
            GameLog.Error($"[CardHandManager] Invalid team: {teamType}");
    }
    

}
