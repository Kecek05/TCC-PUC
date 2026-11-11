#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// The capture's own clock. While the Recorder holds a constant frame rate it sets
    /// <see cref="Time.captureDeltaTime"/>, and every frame is exactly one video frame long whatever the Editor
    /// manages in wall time; outside a recording it is plain unscaled time. Never scaled: a slow-motion ramp
    /// slows the game, not the director's waits.
    /// </summary>
    public static class CaptureTime
    {
        public static float Delta => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
    }
}
#endif
