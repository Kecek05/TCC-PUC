using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One payout to one player: gold, and optionally copies of a card. Deliberately source-agnostic — the end
/// of a match, a daily claim and a shop purchase all hand this same struct to
/// <see cref="BaseRewardService.Grant"/>, and only <see cref="Source"/> tells them apart.
/// </summary>
/// <remarks>
/// <see cref="INetworkSerializable"/> because the match payout is rolled on the server and delivered to one
/// player alone, rather than riding the broadcast <c>EndGameSnapshot</c> that everybody sees. Menu-side
/// sources never leave the client, so they pay nothing for that.
/// </remarks>
public struct Reward : INetworkSerializable
{
    /// <summary>What handed this over. Presentation branches on it; the banking never does.</summary>
    public RewardSource Source;

    public int Gold;

    /// <summary><see cref="CardType.None"/> when no card was awarded (a match loser, or a gold-only shop item).</summary>
    public CardType Card;

    public int Copies;

    public bool HasCard => Card != CardType.None && Copies > 0;

    /// <summary>Nothing to bank. Granting one is a no-op rather than a pointless save write.</summary>
    public bool IsEmpty => Gold == 0 && !HasCard;

    public static Reward GoldOnly(RewardSource source, int gold) =>
        new() { Source = source, Gold = gold, Card = CardType.None, Copies = 0 };

    public static Reward WithCard(RewardSource source, CardType card, int copies, int gold = 0) =>
        new() { Source = source, Gold = gold, Card = card, Copies = Mathf.Max(1, copies) };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Source);
        serializer.SerializeValue(ref Gold);
        serializer.SerializeValue(ref Card);
        serializer.SerializeValue(ref Copies);
    }

    public override string ToString() =>
        HasCard ? $"{Source}: {Gold} gold, {Copies}x {Card}"
                : $"{Source}: {Gold} gold";
}
