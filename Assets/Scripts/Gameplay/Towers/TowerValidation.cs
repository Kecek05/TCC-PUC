

using Unity.Netcode;

public enum TowerReason
{
    None,
    Success,
    LevelUp,
    NotSuccessMaxLevel,
    NotSuccess,
    AlreadyOccupied,
    NotEnoughMana,
}

public struct TowerValidation : INetworkSerializable 
{
    public bool IsValid;
    public TowerReason Reason;
    
    public static TowerValidation Success = new TowerValidation() { IsValid = true, Reason = TowerReason.Success };
    
    public static TowerValidation LevelUp = new TowerValidation() { IsValid = true, Reason = TowerReason.LevelUp };

    public static TowerValidation Invalid(TowerReason reason) =>
        new() { IsValid = false, Reason = reason };

    public static implicit operator bool(TowerValidation v) => v.IsValid;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsValid);
        serializer.SerializeValue(ref Reason);
    }
}