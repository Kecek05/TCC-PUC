using Unity.Netcode;

/// <summary>
/// What one player takes home from a finished match. Rolled on the server and delivered to that player
/// alone, so it is <see cref="INetworkSerializable"/> rather than part of the broadcast
/// <see cref="EndGameSnapshot"/> — a reward is per-player, the snapshot is shared.
/// </summary>
public struct MatchReward : INetworkSerializable
{
    public bool Won;

    public int Gold;

    /// <summary><see cref="CardType.None"/> when no card was awarded (the loser).</summary>
    public CardType Card;

    public int Copies;

    public bool HasCard => Card != CardType.None && Copies > 0;

    public static MatchReward GoldOnly(bool won, int gold) =>
        new() { Won = won, Gold = gold, Card = CardType.None, Copies = 0 };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Won);
        serializer.SerializeValue(ref Gold);
        serializer.SerializeValue(ref Card);
        serializer.SerializeValue(ref Copies);
    }

    public override string ToString() =>
        HasCard ? $"{(Won ? "win" : "loss")}: {Gold} gold, {Copies}x {Card}"
                : $"{(Won ? "win" : "loss")}: {Gold} gold";
}
