using UnityEngine;

public enum GameState
{
    None,
    WaitingForPlayers,
    LoadingMatch,
    MatchReady,
    InMatch,
    EndMatch,
    DrawingCards
}

public enum TeamType
{
    None = 0,
    Blue = 1,
    Red = 2
}

/// <summary>
/// A tower's attack color and an enemy's armor color. A tower deals full damage to enemies of the same
/// color and reduced damage (the enemy's off-color resistance) to others. <see cref="None"/> is neutral:
/// a None attacker deals true damage, and a None-armored enemy takes full damage from any color.
/// </summary>
public enum ArmorColor
{
    None = 0,
    Purple,
    Pink,
    Orange,
}

/// <summary>
/// Unique Identifier of a Card
/// </summary>
public enum CardType
{
    None,
    TowerCircle,
    TowerSquare,
    SpellFireball,
    SpawnEnemy1,
    SpellIce,
    SpawnEnemyMiniBoss,
    SpawnEnemyArmy,
    SpellHaste,
    TowerSlam,
    TowerDart,
    SpellRage,
    TowerNeedle,
    TowerStinger,
    TowerPrism,
    SpellRift,
    SpellLance,
    TowerShard,
    TowerBeacon,
    TowerMortar,
    TowerChain,
    SpellFerrugem,
    TowerTorniquete,
    SpawnEnemyShadow,
    SpawnEnemyRam,
    TowerAnel,
    TowerAncora,
    TowerEspelho,
    TowerFonte,
    SpawnEnemyCisma,
    SpawnEnemyMiragem
}

public enum ExistingTypesOfCard
{
    None,
    Tower,
    Spell,
    Enemy
}

public enum SpellType
{
    None,
    Fireball,
    Ice,
    Haste,
    Rage,
    Rift,
    Lance,
    Ferrugem
}

public enum TowerType
{
    None,
    Circle,
    Square,
    Slam,
    Dart,
    Needle,
    Stinger,
    Prism,
    Shard,
    Beacon,
    Mortar,
    Chain,
    Torniquete,
    Anel,
    Ancora,
    Espelho,
    Fonte
}

public enum EnemyType
{
    None,
    Triangle1,
    Triangle2,
    MiniBoss,
    Fodder,
    Triangle1Fast,
    Triangle1Tank,
    Boss,
    PlayerEnemy,
    Shadow,
    Ram,
    Cisma,
    Miragem,
    MiragemDecoy,
}

public enum CardInvalidReason
{
    None,
    NotEnoughMana,
    InvalidTarget,
    WaitingForServer,
    NoTeam,
    Cooldown,
    BlockedByUI,
    EnemyMap,
    NotInHand
}

public enum SpellInvalidReason
{
    None,
    NotEnoughMana,
    InvalidTarget,
    WaitingForServer,
    NoTeam,
    Cooldown,
    BlockedByUI,
    NotSuccess,
    NotInHand
}

public enum TowerReason
{
    None,
    Success,
    LevelUp,
    NotSuccessMaxLevel,
    NotSuccess,
    AlreadyOccupied,
    NotEnoughMana,
    NotInHand
}

public enum AuthState
{
    NotAuthenticated,
    Authenticating,
    Authenticated,
    Error,
    TimeOut,
}

public enum Arena
{
    Arena1,
    Arena2,
    Arena3,
}

public enum GameMode
{
    Default,
}

public enum GameQueue
{
    Ranked,
    UnRanked,
}

public enum CardRarityType
{
    None,
    Common,
    Rare,
    Epic,
    Legendary,
}

/// <summary>
/// Where a reward came from. The grant path is identical for every source — the save is written the same
/// way — so this exists for presentation and logging: it is what a UI branches on to decide whether to show
/// an end-of-match panel, a daily-claim popup or a shop receipt.
/// </summary>
public enum RewardSource
{
    None,
    Match,
    DailyReward,
    Shop,
    Debug,
    Tutorial,
}

/// <summary>
/// How the Card Collection grid on the deck page is ordered. Paired with a bool for the direction
/// (ascending = A-Z / Common-first / cheapest-first / Tower-first).
/// </summary>
public enum CardSortKey
{
    Name,
    Rarity,
    Cost,
    Type,
}

/// <summary>
/// A stat that can scale with a card's persistent level. Kept deliberately small: one entry per stat that
/// actually exists on a TowerDataSO / EnemyDataSO / SpellDataSO today.
/// </summary>
public enum CardStatId
{
    Damage,
    Health,
    Range,
    /// <summary>Higher is better: it divides a tower's shoot cooldown.</summary>
    AttackSpeed,
    MoveSpeed,
    Duration,
    /// <summary>A buff spell's bonus fraction (Haste attack speed, Rage move speed).</summary>
    EffectBonus,
}

/// <summary>Why a card upgrade was refused, so the UI can say which requirement is missing.</summary>
public enum CardUpgradeInvalidReason
{
    None,
    NotOwned,
    MaxLevel,
    NotEnoughCopies,
    NotEnoughGold,
}

/// <summary>
/// Which half of the first-time experience the player is in. Persisted only as "finished or not"
/// (<c>PlayerSaveData.TutorialCompleted</c>); the split between the two halves lives in memory, because a
/// player who quits mid-tutorial should start it again rather than resume into a menu step with no match
/// behind it.
/// </summary>
public enum TutorialPhase
{
    None,

    /// <summary>The scripted match in TutorialScene.</summary>
    Match,

    /// <summary>The Main Menu half: equip the reward card, upgrade it, start a real match.</summary>
    Menu,
}

/// <summary>
/// One beat of the tutorial. The id is what a step's logic is keyed on and what
/// <c>TutorialCopySO</c> looks its text up by, so the copy can be rewritten without touching code.
/// Appended to only — the copy table stores these by name.
/// </summary>
public enum TutorialStepId
{
    None,

    // --- Match ---
    Welcome,
    Mana,
    Hand,
    PlaceTower,
    LevelUpTower,
    SwapToEnemyMap,
    SendTroop,
    CastSpell,
    SwapBackHome,
    MatchOutro,

    // --- Menu ---
    MenuWelcome,
    OpenDeckPage,
    RemoveCard,
    EquipRewardCard,
    OpenCardDetails,
    UpgradeCard,
    OpenBattlePage,
    PressBattle,

    // --- Appended, not slotted into the sections above ---
    // TutorialCopySO serializes these as ints, so inserting one mid-enum would re-key every line after it
    // onto the wrong step. Where a step actually runs is the director's list, never this enum.
    CloseCardDetails,   // Menu: after UpgradeCard
    TroopDirection,     // Match: after SendTroop
    CastDefensiveSpell, // Match: after SwapBackHome
    SpellKinds,         // Match: before CastSpell

    // Free-play tips, after the scripted steps. Not steps — nothing waits on them — but they are lines the
    // tutorial says, so they live in the same copy table.
    TipDefend,
    TipBuildTower,

    // Taught when the match first shows the thing it is about, during free play.
    ArmorLesson,
}

/// <summary>How much of the game the tutorial lets the player's finger reach.</summary>
public enum TutorialInputMode
{
    /// <summary>The tutorial holds nothing back: normal play.</summary>
    Free,

    /// <summary>Nothing reaches the game. Only the overlay's own buttons (Continue, Skip) still answer.</summary>
    Blocked,

    /// <summary>Only the highlighted hole reaches the game — the one thing the step is asking for.</summary>
    TargetOnly,
}

/// <summary>How the tutorial overlay animates its pointing hand at a highlighted target.</summary>
public enum TutorialHintKind
{
    /// <summary>Ring only, no hand. For "look at this" steps.</summary>
    None,

    Tap,

    /// <summary>Hand travels from the highlight to the step's drag destination and repeats.</summary>
    Drag,

    /// <summary>Vertical swipe on the spot — the table-swap gesture.</summary>
    SwipeUp,

    SwipeDown,
}
