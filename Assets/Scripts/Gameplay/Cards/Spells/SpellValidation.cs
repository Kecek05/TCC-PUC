using Unity.Netcode;

public enum SpellInvalidReason
{
    None,
    NotEnoughMana,
    InvalidTarget,
    WaitingForServer,
    NoTeam,
    Cooldown,
    BlockedByUI,
    NotSuccess
}

public struct SpellValidation : INetworkSerializable
{
    public bool IsValid;
    public SpellInvalidReason Reason;

    public static SpellValidation Valid => new() { IsValid = true, Reason = SpellInvalidReason.None };

    public static SpellValidation Invalid(SpellInvalidReason reason) =>
        new() { IsValid = false, Reason = reason };

    public static implicit operator bool(SpellValidation v) => v.IsValid;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsValid);
        serializer.SerializeValue(ref Reason);
    }
}