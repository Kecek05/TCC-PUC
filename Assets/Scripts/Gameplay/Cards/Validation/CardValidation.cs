using Unity.Netcode;

public enum CardInvalidReason
{
    None,
    NotEnoughMana,
    InvalidTarget,
    WaitingForServer,
    NoTeam,
    Cooldown
}

public struct CardValidation : INetworkSerializable
{
    public bool IsValid;
    public CardInvalidReason Reason;

    public static CardValidation Valid => new() { IsValid = true, Reason = CardInvalidReason.None };

    public static CardValidation Invalid(CardInvalidReason reason) =>
        new() { IsValid = false, Reason = reason };

    public static implicit operator bool(CardValidation v) => v.IsValid;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsValid);
        serializer.SerializeValue(ref Reason);
    }
}