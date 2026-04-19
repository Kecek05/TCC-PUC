using UnityEngine;

public enum GameState
{
    None,
    WaitingForPlayers,
    LoadingMatch,
    MatchReady,
    InMatch,
    EndMatch
}

public enum TeamType
{
    None = 0,
    Blue = 1,
    Red = 2
}

public enum CardType
{
    None,
    TowerCircle,
    TowerSquare,
    SpellFireball,
    SpawnEnemy1
}

public enum SpellType
{
    None,
    Fireball,
    Freeze
}

public enum TowerType
{
    None,
    Circle,
    Square
}

public enum EnemyType
{
    None,
    Triangle1,
    Triangle2
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
    EnemyMap
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
    NotSuccess
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
}

public enum AuthState
{
    NotAuthenticated,
    Authenticating,
    Authenticated,
    Error,
    TimeOut,
}