using System;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Server-authoritative card-hand state. The server owns the full <see cref="HandData"/>
/// (deck, queue, locked) privately; clients only observe the synced fields below:
/// the current hand cards and the next card to be drawn.
/// </summary>
public abstract class BaseCardHandManager : NetworkBehaviour
{
    /// <summary>Current hand cards for the blue team. Server-write, everyone-read.</summary>
    public NetworkList<CardTypeEntry> BlueHandCards;
    /// <summary>Current hand cards for the red team. Server-write, everyone-read.</summary>
    public NetworkList<CardTypeEntry> RedHandCards;

    /// <summary>
    /// Head of the blue team's draw queue (the card that will replace the next played
    /// card). <see cref="CardType.None"/> when no card is queued.
    /// </summary>
    public NetworkVariable<CardType> BlueNextCard = new(writePerm: NetworkVariableWritePermission.Server);
    /// <summary>
    /// Head of the red team's draw queue. <see cref="CardType.None"/> when no card is queued.
    /// </summary>
    public NetworkVariable<CardType> RedNextCard = new(writePerm: NetworkVariableWritePermission.Server);

    /// <summary>
    /// Fired on every peer (server and clients) when a team's hand cards or next card
    /// changes. UI subscribes here for a single refresh trigger.
    /// </summary>
    public event Action<TeamType> OnHandChanged;

    /// <summary>Server-only. Distributes an initial hand for the given team from the provided deck.</summary>
    public abstract void SetHandForPlayer(TeamType teamType, List<CardType> cardsInDeck);

    /// <summary>Server-only. Called when a player plays a card — draws a replacement into the hand.</summary>
    public abstract void NotifyCardPlayed(TeamType teamType, CardType cardType);

    /// <summary>
    /// Returns true if the given card is currently in the team's visible hand.
    /// Reads the synced NetworkList — safe to call from server (authoritative check)
    /// and from clients (display logic).
    /// </summary>
    public bool TeamHasCardInHand(TeamType teamType, CardType cardType)
    {
        NetworkList<CardTypeEntry> handList = teamType == TeamType.Blue ? BlueHandCards : RedHandCards;
        for (int i = 0; i < handList.Count; i++)
        {
            if (handList[i].Value == cardType) return true;
        }
        return false;
    }

    protected virtual void Awake()
    {
        BlueHandCards = new NetworkList<CardTypeEntry>();
        RedHandCards = new NetworkList<CardTypeEntry>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        BlueHandCards.OnListChanged += OnBlueHandListChanged;
        RedHandCards.OnListChanged += OnRedHandListChanged;
        BlueNextCard.OnValueChanged += OnBlueNextChanged;
        RedNextCard.OnValueChanged += OnRedNextChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        BlueHandCards.OnListChanged -= OnBlueHandListChanged;
        RedHandCards.OnListChanged -= OnRedHandListChanged;
        BlueNextCard.OnValueChanged -= OnBlueNextChanged;
        RedNextCard.OnValueChanged -= OnRedNextChanged;
    }

    private void OnBlueHandListChanged(NetworkListEvent<CardTypeEntry> _) => OnHandChanged?.Invoke(TeamType.Blue);
    private void OnRedHandListChanged(NetworkListEvent<CardTypeEntry> _) => OnHandChanged?.Invoke(TeamType.Red);
    private void OnBlueNextChanged(CardType _, CardType __) => OnHandChanged?.Invoke(TeamType.Blue);
    private void OnRedNextChanged(CardType _, CardType __) => OnHandChanged?.Invoke(TeamType.Red);
}
