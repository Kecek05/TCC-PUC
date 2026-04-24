using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative coordinator. Holds the per-team <see cref="HandData"/> server-side,
/// listens to <see cref="IMaxManaProvider"/> to unlock cards, and pushes the visible slice
/// (hand + next card) onto the synced NetworkList / NetworkVariable for clients.
/// Clients read the synced state and subscribe to <see cref="BaseCardHandManager.OnHandChanged"/>.
/// </summary>
public class CardHandManager : BaseCardHandManager
{
    [Title("Config")]
    [SerializeField, MinValue(1)] private int _handSize = 4;
    [SerializeField] private CardDataListSO _cardDataListSO;

    private HandData _blueHandData;
    private HandData _redHandData;

    private ICardCostProvider _costs;
    private IMaxManaProvider _maxManaProvider;

    protected override void Awake()
    {
        base.Awake();
        ServiceLocator.Register<BaseCardHandManager>(this);
        _costs = _cardDataListSO;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
            StartCoroutine(WaitForManaProvider());
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsServer && _maxManaProvider != null)
            _maxManaProvider.OnMaxManaChanged -= OnMaxManaChanged;
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseCardHandManager>();
        base.OnDestroy();
    }

    private IEnumerator WaitForManaProvider()
    {
        yield return new WaitUntil(() => ServiceLocator.Get<BaseServerManaManager>() != null);

        _maxManaProvider = ServiceLocator.Get<BaseServerManaManager>();
        _maxManaProvider.OnMaxManaChanged += OnMaxManaChanged;
    }

    public override void SetHandForPlayer(TeamType teamType, List<CardType> cardsInDeck)
    {
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
        HandData handData = HandData.Distribute(cardsInDeck, _handSize, maxMana, _costs);

        SetServerHandData(teamType, handData);
        PushSyncedState(teamType, handData);
    }

    public override void NotifyCardPlayed(TeamType teamType, CardType cardType)
    {
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
        NetworkList<CardTypeEntry> handList = teamType == TeamType.Blue ? BlueHandCards : RedHandCards;
        NetworkVariable<CardType> nextVar = teamType == TeamType.Blue ? BlueNextCard : RedNextCard;

        handList.Clear();
        for (int i = 0; i < handData.HandCards.Count; i++)
            handList.Add(handData.HandCards[i]);

        nextVar.Value = handData.QueuedCards.Count > 0 ? handData.QueuedCards.Peek() : CardType.None;
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
