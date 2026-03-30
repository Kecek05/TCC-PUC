public enum CardInvalidReason
{
    None,
    NotEnoughMana,
    InvalidTarget,
    WaitingForServer,
    Cooldown
}

public struct CardValidation
{
    public bool IsValid;
    public CardInvalidReason Reason;

    public static CardValidation Valid => new() { IsValid = true, Reason = CardInvalidReason.None };

    public static CardValidation Invalid(CardInvalidReason reason) =>
        new() { IsValid = false, Reason = reason };

    public static implicit operator bool(CardValidation v) => v.IsValid;
}