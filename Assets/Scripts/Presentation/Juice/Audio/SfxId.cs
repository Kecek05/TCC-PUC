/// <summary>
/// Every sound cue in the game, one per entry in the Juice &amp; Feedback System's cue list. A presenter asks for
/// a cue by id and <see cref="BaseSfxService"/> decides the clip, bus, pitch and whether it may play at all.
/// </summary>
/// <remarks>
/// <b>Append only.</b> <see cref="SfxLibrarySO"/> and every <c>MMF_JuiceSfx</c> feedback serialize this enum as
/// an int, so inserting a member mid-enum silently re-maps every sound already wired (the same rule as
/// <c>CardType</c>).
/// </remarks>
public enum SfxId
{
    None = 0,
    Tick = 1,
    Press = 2,
    Tap = 3,
    Confirm = 4,
    Deny = 5,
    Pick = 6,
    Play = 7,
    Whoosh = 8,
    Pop = 9,
    Thud = 10,
    Place = 11,
    Coin = 12,
    Rise = 13,
    Sparkle = 14,
    Charge = 15,
    Upgrade = 16,
    LevelUp = 17,
    Impact = 18,
    Boom = 19,
    Hit = 20,
    Heavy = 21,
    Clink = 22,
    Death = 23,
    Leak = 24,
    BaseHit = 25,
    Heartbeat = 26,
    Horn = 27,
    Drum = 28,
    Fanfare = 29,
    Defeat = 30,
    Mana = 31,
    Full = 32,
    Spawn = 33,
    Flip = 34,
    Stamp = 35,
    Found = 36,
}

/// <summary>The mix group a cue plays on; each has its own volume token.</summary>
public enum SfxBus
{
    Ui = 0,
    Game = 1,
    Impact = 2,
    Music = 3,
}
