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

    private const int Recent = 8;
    // About the footprint of a three-digit number: two that start closer than this draw over each other.
    private const float CrowdWidth = 0.55f;
    private const float CrowdHeight = 0.26f;
    private const float CrowdSeconds = 0.25f;
    private static readonly Vector3[] RecentAt = new Vector3[Recent];
    private static readonly float[] RecentTime = new float[Recent];
    private static int _recentNext;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        for (int i = 0; i < Recent; i++) RecentTime[i] = -10f;
        _recentNext = 0;
    }

    /// <summary>
    /// A number that would land on one shown a moment ago is set above it instead (a line higher each time, leaning
    /// left and right), so a spell through a tight column reads as that many hits and not as one unreadable pile. The
    /// boards stagger them the same way. Allocation-free: a ring of the last few spawns.
    /// </summary>
    private static Vector3 Uncrowd(Vector3 position)
    {
        float now = Time.unscaledTime;
        int crowd = 0;
        for (int i = 0; i < Recent; i++)
        {
            if (now - RecentTime[i] > CrowdSeconds) continue;
            Vector3 apart = RecentAt[i] - position;
            if (Mathf.Abs(apart.x) < CrowdWidth && Mathf.Abs(apart.y) < CrowdHeight) crowd++;
        }

        RecentAt[_recentNext] = position;
        RecentTime[_recentNext] = now;
        _recentNext = (_recentNext + 1) % Recent;

        if (crowd == 0) return position;
        float side = crowd % 2 == 1 ? 1f : -1f;
        return position + new Vector3(side * 0.12f, 0.3f * crowd, 0f);
    }

    private static void Spawn(Vector3 position, string text, float intensity, Gradient color)
    {
        // Each player watches one field at a time; a number on the other one is never seen but would still
        // take a slot of the pooled budget from a number that is.
        if (!CameraFeedback.IsOnScreen(position, 0.05f)) return;
        position = Uncrowd(position);

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
