using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// Floating numbers over the board — damage on hits, "+N" on a mana payout — through Feel's pooled
/// <see cref="MMFloatingTextSpawner"/>. Hits are the most frequent event in a match, so nothing here may
/// allocate per call: the channel is cached and number strings are built once and reused.
/// </summary>
public static class FloatingNumbers
{
    private const int CachedCount = 1000;

    private static readonly MMChannelData Channel = new(MMChannelModes.Int, 0, null);
    private static readonly string[] Plain = new string[CachedCount];
    private static readonly string[] Signed = new string[CachedCount];

    /// <summary>Damage rounds to a whole number and never shows as 0: a hit that landed should read as one.</summary>
    public static void Damage(Vector3 position, float amount, float intensity, Gradient color)
    {
        Spawn(position, Text(Plain, Mathf.Max(1, Mathf.RoundToInt(amount)), false), intensity, color);
    }

    /// <summary>
    /// A gain, shown as "+N" — with one decimal when it is not a whole amount (a Fonte's payout scales with card
    /// level, and "+1" for 1.1 would under-report it). Gains are occasional, so formatting those is fine.
    /// </summary>
    public static void Gain(Vector3 position, float amount, float intensity, Gradient color)
    {
        float rounded = Mathf.Round(amount * 10f) / 10f;
        int whole = Mathf.RoundToInt(rounded);
        string text = Mathf.Approximately(rounded, whole)
            ? Text(Signed, Mathf.Max(1, whole), true)
            : "+" + rounded.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        Spawn(position, text, intensity, color);
    }

    /// <summary>
    /// A word over the board ("INCOMING!", "SENT x3"): the same pooled rise as a number, for the few beats
    /// that are worth saying out loud. Rare, so building the string at the call site is fine.
    /// </summary>
    public static void Say(Vector3 position, string text, float intensity, Color color)
    {
        Spawn(position, text, intensity, Solid(color));
    }

    /// <summary>A gradient that is just one colour, for callers that think in colours.</summary>
    public static Gradient Solid(Color color)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    private static void Spawn(Vector3 position, string text, float intensity, Gradient color)
    {
        // Each player watches one field at a time; a number on the other one is never seen but would still
        // take a slot of the pooled budget from a number that is.
        if (!CameraFeedback.IsOnScreen(position, 0.05f)) return;

        MMFloatingTextSpawnEvent.Trigger(Channel, position, text, Vector3.up, intensity,
            forceColor: color != null, animateColorGradient: color);
    }

    // Built on first use rather than up front, so a match only pays for the numbers it actually shows.
    private static string Text(string[] cache, int value, bool signed)
    {
        if (value >= CachedCount) return signed ? "+" + value : value.ToString();
        return cache[value] ??= signed ? "+" + value : value.ToString();
    }
}
