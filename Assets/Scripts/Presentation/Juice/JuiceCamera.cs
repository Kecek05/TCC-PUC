using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
/// Camera feedback in tokens: shakes as a share of the screen's height, zoom kicks as a share of the
/// orthographic size. Both go through Feel's camera events, so <see cref="CameraFeedbackGate"/> and the Screen
/// shake setting still govern them; the budget-shake cap and Reduce motion are applied here.
/// </summary>
public static class JuiceCamera
{
    private static readonly float[] RecentShakes = new float[8];
    private static readonly float[] RecentFlashes = new float[8];
    private static int _nextShake;
    private static int _nextFlash;

    private static readonly MMChannelData Channel = new(MMChannelModes.Int, 0, null);
    private static readonly AnimationCurve Bell = new(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        for (int i = 0; i < RecentShakes.Length; i++) RecentShakes[i] = RecentFlashes[i] = -10f;
        _nextShake = _nextFlash = 0;
    }

    /// <summary>
    /// Shakes by a shake token (a share of screen height) for a duration token. Returns false when Reduce
    /// motion or the budget held it back: past budget-shake per second a request merges into the running shake.
    /// </summary>
    public static bool Shake(float share, float duration)
    {
        if (Juice.ReduceMotion || share <= 0f) return false;
        if (!Take(RecentShakes, ref _nextShake, JuiceTokensSO.Instance.Budgets.ShakesPerSecond)) return false;

        CameraFeedback.Shake(duration, JuiceTokensSO.ShakeUnits(share, Camera.main), JuiceTokensSO.Instance.Shake.Frequency);
        return true;
    }

    /// <summary>Your own tower placed: the orthographic size dips by zoom-kick and comes back.</summary>
    public static void ZoomKick(float duration = 0.28f)
    {
        if (Juice.ReduceMotion) return;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float delta = -JuiceTokensSO.Instance.Shake.ZoomKick * cam.orthographicSize;
        MMCameraOrthographicSizeShakeEvent.Trigger(Bell, duration, 0f, delta, true, 1f, Channel,
            true, true, true, TimescaleModes.Unscaled);
    }

    /// <summary>
    /// Whether a large flash may play now (budget-flash per second, photosensitivity guideline; one a second
    /// under Reduce motion). Hit flashes on single enemies are not large flashes and never ask.
    /// </summary>
    public static bool TakeFlash()
    {
        int max = Juice.ReduceMotion ? 1 : JuiceTokensSO.Instance.Budgets.FlashesPerSecond;
        return Take(RecentFlashes, ref _nextFlash, max);
    }

    private static bool Take(float[] recent, ref int next, int perSecond)
    {
        float now = Time.unscaledTime;
        int inWindow = 0;
        for (int i = 0; i < recent.Length; i++)
            if (now - recent[i] < 1f) inWindow++;

        if (inWindow >= Mathf.Max(1, perSecond)) return false;

        recent[next] = now;
        next = (next + 1) % recent.Length;
        return true;
    }
}
