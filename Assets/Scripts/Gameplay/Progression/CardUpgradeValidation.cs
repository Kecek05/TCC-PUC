/// <summary>
/// Result of asking whether a card can be upgraded. Carries the typed reason so the UI can say
/// <i>which</i> requirement is missing instead of just greying a button — the same shape as
/// <see cref="CardValidation"/> and <see cref="TowerValidation"/>, minus the network serialization
/// (upgrades never leave the client).
/// </summary>
public readonly struct CardUpgradeValidation
{
    public readonly bool IsValid;
    public readonly CardUpgradeInvalidReason Reason;

    /// <summary>Copies needed for the next level, 0 when there is no next level.</summary>
    public readonly int CopiesRequired;

    /// <summary>Gold needed for the next level, 0 when there is no next level.</summary>
    public readonly int GoldCost;

    private CardUpgradeValidation(bool isValid, CardUpgradeInvalidReason reason, int copiesRequired, int goldCost)
    {
        IsValid = isValid;
        Reason = reason;
        CopiesRequired = copiesRequired;
        GoldCost = goldCost;
    }

    public static CardUpgradeValidation Valid(int copiesRequired, int goldCost) =>
        new(true, CardUpgradeInvalidReason.None, copiesRequired, goldCost);

    public static CardUpgradeValidation Invalid(CardUpgradeInvalidReason reason, int copiesRequired = 0, int goldCost = 0) =>
        new(false, reason, copiesRequired, goldCost);

    public static implicit operator bool(CardUpgradeValidation v) => v.IsValid;

    /// <summary>
    /// The refusal to show the player when this is what stopped an upgrade. It lives on the validation rather
    /// than at each call site because both doors into an upgrade, the deck page popup and the card info panel,
    /// have to explain a refusal the same way; the words themselves are the toast's (<c>RefusalCopySO</c>).
    /// </summary>
    public RefusalReason Refusal => RefusalReasons.From(Reason);

    /// <summary>What the player is short of, for the toast's hint.</summary>
    public string RefusalDetail => Reason switch
    {
        CardUpgradeInvalidReason.NotEnoughGold => $"Need {GoldCost} gold",
        CardUpgradeInvalidReason.NotEnoughCopies => $"Need {CopiesRequired} copies",
        _ => null
    };
}
