#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// Ramps <see cref="Time.timeScale"/> to a target over capture time. The only simulated effect the trailer
    /// is allowed (the final blow's slow motion): the game has no slow motion of its own. Under the Recorder's
    /// constant frame rate a slowed game still produces 60 smooth frames per video second.
    /// </summary>
    [Serializable]
    public class TimeScaleRamp : ICaptureAction
    {
        public float Target = 0.25f;
        public float Seconds = 0.4f;

        public TimeScaleRamp() { }
        public TimeScaleRamp(float target, float seconds)
        {
            Target = target;
            Seconds = seconds;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            float from = Time.timeScale;
            float t = 0f;
            while (t < Seconds)
            {
                t += CaptureTime.Delta;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, Seconds));
                Time.timeScale = Mathf.Lerp(from, Target, 1f - (1f - k) * (1f - k));
                yield return null;
            }

            Time.timeScale = Target;
        }

        public string Describe() => $"TimeScale -> {Target:0.00} over {Seconds:0.00}s";
    }

    /// <summary>
    /// Pushes the camera in on a board target (zoom and re-centre), optionally following it as it moves.
    /// Moves the camera rig, not the camera, because shakes own the camera's local offset. Zoom punches are
    /// held off while the director owns the camera's size. <see cref="CameraReset"/> hands it back.
    /// </summary>
    [Serializable]
    public class CameraFocus : ICaptureAction
    {
        public BoardTarget Target = new();
        [Tooltip("Orthographic size as a share of the field's normal size: 0.6 = a 40% push-in.")]
        public float Zoom = 0.6f;
        public float Seconds = 0.8f;
        [Tooltip("Keep re-centring on the target for this long after arriving (0 = stop on arrival).")]
        public float FollowSeconds;
        [Tooltip("Stop following as soon as the target no longer resolves (the enemy died), whatever FollowSeconds says.")]
        public bool UntilGone;

        public CameraFocus() { }
        public CameraFocus(BoardTarget target, float zoom, float seconds, float followSeconds = 0f)
        {
            Target = target;
            Zoom = zoom;
            Seconds = seconds;
            FollowSeconds = followSeconds;
        }

        public IEnumerator Run(CaptureContext ctx)
        {
            Camera cam = ctx.MainCamera;
            Transform rig = CameraRig.Of(ctx);
            if (cam == null || rig == null) yield break;

            CameraRig.Remember(cam, rig);
            if (ServiceLocator.TryGet(out CameraFeedbackGate gate)) gate.SetZoomPunchesSuppressed(true);
            rig.DOKill();

            Vector3 startPos = rig.position;
            float startSize = cam.orthographicSize;
            float endSize = CameraRig.HomeSize * Zoom;
            Vector2 aim = Target.Resolve(ctx) ?? (Vector2)startPos;

            float t = 0f;
            while (t < Seconds)
            {
                t += CaptureTime.Delta;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Seconds));
                aim = Target.Resolve(ctx) ?? aim;
                rig.position = new Vector3(Mathf.Lerp(startPos.x, aim.x, k), Mathf.Lerp(startPos.y, aim.y, k), startPos.z);
                cam.orthographicSize = Mathf.Lerp(startSize, endSize, k);
                yield return null;
            }

            float follow = 0f;
            while (follow < FollowSeconds)
            {
                follow += CaptureTime.Delta;
                Vector2? now = Target.Resolve(ctx);
                if (UntilGone && !now.HasValue) yield break;
                aim = now ?? aim;
                Vector3 p = rig.position;
                float s = 1f - Mathf.Exp(-CaptureTime.Delta * 6f);
                rig.position = new Vector3(Mathf.Lerp(p.x, aim.x, s), Mathf.Lerp(p.y, aim.y, s), p.z);
                yield return null;
            }
        }

        public string Describe() => $"CameraFocus {Target} zoom {Zoom:0.00} in {Seconds:0.00}s";
    }

    /// <summary>Hands the camera back: the field's own size and position, and zoom punches on again.</summary>
    [Serializable]
    public class CameraReset : ICaptureAction
    {
        public float Seconds = 0.5f;

        public IEnumerator Run(CaptureContext ctx)
        {
            Camera cam = ctx.MainCamera;
            Transform rig = CameraRig.Of(ctx);
            if (cam == null || rig == null || !CameraRig.HasHome) yield break;

            Vector3 startPos = rig.position;
            float startSize = cam.orthographicSize;
            float t = 0f;
            while (t < Seconds)
            {
                t += CaptureTime.Delta;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.0001f, Seconds)));
                rig.position = Vector3.Lerp(startPos, CameraRig.HomePosition, k);
                cam.orthographicSize = Mathf.Lerp(startSize, CameraRig.HomeSize, k);
                yield return null;
            }

            rig.position = CameraRig.HomePosition;
            cam.orthographicSize = CameraRig.HomeSize;
            CameraRig.Forget();
            if (ServiceLocator.TryGet(out CameraFeedbackGate gate)) gate.SetZoomPunchesSuppressed(false);
        }

        public string Describe() => $"CameraReset {Seconds:0.00}s";
    }

    /// <summary>Where the camera was before the director took it.</summary>
    public static class CameraRig
    {
        public static bool HasHome { get; private set; }
        public static Vector3 HomePosition { get; private set; }
        public static float HomeSize { get; private set; }

        public static Transform Of(CaptureContext ctx)
        {
            CameraSlide slide = ctx.Slide;
            return slide != null ? Reflect.Get<Transform>(slide, "Rig") : ctx.MainCamera != null ? ctx.MainCamera.transform : null;
        }

        public static void Remember(Camera cam, Transform rig)
        {
            if (HasHome) return;
            HasHome = true;
            HomePosition = rig.position;
            HomeSize = cam.orthographicSize;
        }

        public static void Forget() => HasHome = false;
    }
}
#endif
