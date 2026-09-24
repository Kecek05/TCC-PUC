using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// Camera feedback for effects raised from code rather than from an authored MMF_Player — an explosion on
/// some tower, a hit on some enemy. Goes through the same Feel event the camera's shaker listens to, so the
/// screen-shake setting (<see cref="CameraFeedbackGate"/>) governs these too.
/// </summary>
public static class CameraFeedback
{
    // Cached: the shaker listens on channel 0, and a new MMChannelData per shake would be garbage per hit.
    private static readonly MMChannelData Channel = new(MMChannelModes.Int, 0, null);

    /// <summary>Whether a world point is inside the view. Each player sees one field at a time, and shaking
    /// the screen for an explosion on the other one reads as a glitch, not as an impact.</summary>
    public static bool IsOnScreen(Vector3 worldPosition, float margin = 0f)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 viewport = cam.WorldToViewportPoint(worldPosition);
        return viewport.x >= -margin && viewport.x <= 1f + margin &&
               viewport.y >= -margin && viewport.y <= 1f + margin;
    }

    /// <summary>A short position shake. Unscaled, so it reads the same inside the end cutscene's slow motion.</summary>
    public static void Shake(float duration, float amplitude, float frequency)
    {
        MMCameraShakeEvent.Trigger(duration, amplitude, frequency, 0f, 0f, 0f, false, Channel, true);
    }
}
