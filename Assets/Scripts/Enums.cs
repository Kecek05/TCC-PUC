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
    Red,
    Blue,
    Green,
    Yellow
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
    TowerDart
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
    Haste
}

public enum TowerType
{
    None,
    Circle,
    Square,
    Slam,
    Dart
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