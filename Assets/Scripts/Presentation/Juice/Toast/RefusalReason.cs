/// <summary>
/// Why the game said no. Every refusal the player can meet is one of these, so the toast's words live in one
/// table (<see cref="RefusalCopySO"/>) and never drift between the screens that refuse the same thing.
/// </summary>
/// <remarks>
/// <b>Append only:</b> <see cref="RefusalCopySO"/> serializes this enum as an int. The mappings from the game's
/// own reason enums (<c>CardInvalidReason</c>, <c>TowerReason</c>, <c>CardUpgradeInvalidReason</c>) live in
/// <see cref="RefusalReasons"/>.
/// </remarks>
public enum RefusalReason
{
    None = 0,
    Generic = 1,
    NotEnoughMana = 2,
    InvalidTarget = 3,
    WaitingForServer = 4,
    EnemyFieldOnly = 5,
    OwnFieldOnly = 6,
    NotInHand = 7,
    PathBlocked = 8,
    SlotTaken = 9,
    DifferentTower = 10,
    TowerMaxLevel = 11,
    DeckFull = 12,
    CardNotOwned = 13,
    CardAlreadyInDeck = 14,
    NotEnoughGold = 15,
    NotEnoughCopies = 16,
    CardMaxLevel = 17,
    DeckNotFull = 18,
    MatchmakingFailed = 19,
    JoinFailed = 20,
    Cooldown = 21,
    NoTeam = 22,
    ConnectionLost = 23,
}

/// <summary>Translations from the game's own refusal enums to <see cref="RefusalReason"/>.</summary>
public static class RefusalReasons
{
    public static RefusalReason From(CardInvalidReason reason) => reason switch
    {
        CardInvalidReason.None => RefusalReason.None,
        CardInvalidReason.NotEnoughMana => RefusalReason.NotEnoughMana,
        CardInvalidReason.InvalidTarget => RefusalReason.InvalidTarget,
        CardInvalidReason.WaitingForServer => RefusalReason.WaitingForServer,
        CardInvalidReason.NoTeam => RefusalReason.NoTeam,
        CardInvalidReason.Cooldown => RefusalReason.Cooldown,
        // Dropping back on the hand is a cancel, not a refusal: nothing to explain.
        CardInvalidReason.BlockedByUI => RefusalReason.None,
        CardInvalidReason.EnemyMap => RefusalReason.OwnFieldOnly,
        CardInvalidReason.NotInHand => RefusalReason.NotInHand,
        CardInvalidReason.DifferentTower => RefusalReason.DifferentTower,
        CardInvalidReason.TowerMaxLevel => RefusalReason.TowerMaxLevel,
        CardInvalidReason.NoPlaceable => RefusalReason.PathBlocked,
        CardInvalidReason.LocalMap => RefusalReason.EnemyFieldOnly,
        _ => RefusalReason.Generic
    };

    public static RefusalReason From(TowerReason reason) => reason switch
    {
        TowerReason.None or TowerReason.Success or TowerReason.LevelUp => RefusalReason.None,
        TowerReason.NotSuccessMaxLevel => RefusalReason.TowerMaxLevel,
        TowerReason.AlreadyOccupied => RefusalReason.SlotTaken,
        TowerReason.NotEnoughMana => RefusalReason.NotEnoughMana,
        TowerReason.NotInHand => RefusalReason.NotInHand,
        TowerReason.NotSuccess => RefusalReason.InvalidTarget,
        _ => RefusalReason.Generic
    };

    public static RefusalReason From(EquipRefusal reason) => reason switch
    {
        EquipRefusal.None => RefusalReason.None,
        EquipRefusal.NotOwned => RefusalReason.CardNotOwned,
        EquipRefusal.DeckFull => RefusalReason.DeckFull,
        EquipRefusal.AlreadyInDeck => RefusalReason.CardAlreadyInDeck,
        _ => RefusalReason.Generic
    };

    public static RefusalReason From(CardUpgradeInvalidReason reason) => reason switch
    {
        CardUpgradeInvalidReason.None => RefusalReason.None,
        CardUpgradeInvalidReason.NotOwned => RefusalReason.CardNotOwned,
        CardUpgradeInvalidReason.MaxLevel => RefusalReason.CardMaxLevel,
        CardUpgradeInvalidReason.NotEnoughCopies => RefusalReason.NotEnoughCopies,
        CardUpgradeInvalidReason.NotEnoughGold => RefusalReason.NotEnoughGold,
        _ => RefusalReason.Generic
    };
}
