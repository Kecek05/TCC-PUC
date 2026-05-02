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

    private HandData _blueHandData;
    private HandData _redHandData;

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

    private void OnAnyCardDeployed(CardDeployedEventArgs args)
        => NotifyCardPlayed(args.TeamDeployed, args.CardDeployed);

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

        for (int i = 0; i < cardHandSettingsSO.HandSize; i++)
        {
            if (!handData.Draw(out CardType drawnCard)) break;
            OnDrawACard?.Invoke(teamType, drawnCard);
            SendOnDrawLocalACardRpc(drawnCard, RpcTarget.Single(_playersDataManager.GetClientIdByTeamType(teamType), RpcTargetUse.Temp));
        }

        PushSyncedState(teamType, handData);
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

        if (!handData.Play(cardType, out CardType drawnCard))
        {
            GameLog.Warn($"[CardHandManager] Played card {cardType} not found in {teamType} hand.");
            return;
        }
        
        GameLog.Info($"[CardHandManager] {teamType} played {cardType}, drew {drawnCard}");
        OnDrawACard?.Invoke(teamType, drawnCard);
        SendOnDrawLocalACardRpc(drawnCard, RpcTarget.Single(_playersDataManager.GetClientIdByTeamType(teamType), RpcTargetUse.Temp));
        PushSyncedState(teamType, handData);
    }

    private void OnMaxManaChanged(TeamType teamType, float newMax)
    {
        if (!IsServer) return;

        HandData handData = GetServerHandData(teamType);
        if (handData == null) return;

        if (handData.Unlock(newMax, _costs))
            PushSyncedState(teamType, handData);
    }

    private void PushSyncedState(TeamType teamType, HandData handData)
    {
        List<CardType> handList = teamType == TeamType.Blue ? BlueHandCards : RedHandCards;

        handList.Clear();
        for (int i = 0; i < handData.HandCards.Count; i++)
            handList.Add(handData.HandCards[i]);

        CardType nextVar = handData.QueuedCards.Count > 0 ? handData.QueuedCards.Peek() : CardType.None;
        
        SendOnLocalNextCardChangedRpc(nextVar, RpcTarget.Single(_playersDataManager.GetClientIdByTeamType(teamType), RpcTargetUse.Temp));
    }

    private HandData GetServerHandData(TeamType teamType)
        => teamType == TeamType.Blue ? _blueHandData : _redHandData;

    private void SetServerHandData(TeamType teamType, HandData data)
    {
        if (teamType == TeamType.Blue)
            _blueHandData = data;
        else if (teamType == TeamType.Red)
            _redHandData = data;
        else
            GameLog.Error($"[CardHandManager] Invalid team: {teamType}");
    }

}
