using System;

/// <summary>
/// Bundle of dependencies passed to BaseCardScorer.Score so scorers don't reach into ServiceLocator.
/// </summary>
public class BotContext
{
    public BotWorldView World { get; }
    public BotProfileSO Profile { get; }
    private readonly Random _rng;

    public BotContext(BotWorldView world, BotProfileSO profile, Random rng)
    {
        World = world;
        Profile = profile;
        _rng = rng;
    }

    /// <summary>Returns a random float in [min, max).</summary>
    public float NextFloat(float min, float max) => (float)(_rng.NextDouble() * (max - min) + min);
}
