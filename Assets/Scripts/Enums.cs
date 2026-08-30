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
    TowerChain
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
    Lance
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
    Chain
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
