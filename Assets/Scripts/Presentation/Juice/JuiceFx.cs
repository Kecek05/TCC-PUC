using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// The motion vocabulary of the Juice &amp; Feedback System as DOTween calls, every number read from
/// <see cref="JuiceTokensSO"/>. Each helper returns its tween so callers can chain, await or kill it.
/// </summary>
/// <remarks>
/// UI helpers default to unscaled time: the tutorial freezes <see cref="Time.timeScale"/> while it talks and a
/// button must still answer. Helpers that scale assume the target rests at the scale they are given (1 unless
/// stated), and kill the target's own tweens first, so two answers to two quick taps never fight.
/// Reduce motion halves punches and drops overshoots.
/// </remarks>
public static class JuiceFx
{
    private static JuiceTokensSO T => JuiceTokensSO.Instance;

    private static readonly Dictionary<int, Tween> Punches = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Punches.Clear();

    private static float Motion(float amount) => Juice.ReduceMotion ? amount * 0.5f : amount;

    /// <summary>Pointer down: to scale-press over dur-press on ease-press.</summary>
    public static Tween Press(Transform target, float rest = 1f, bool unscaled = true)
    {
        target.DOKill();
        return target.DOScale(rest * T.Scales.Press, T.Durations.Press).SetEase(T.Eases.Press)
            .SetUpdate(unscaled).SetLink(target.gameObject);
    }

    /// <summary>Pointer up: through scale-pop-peak back to rest over dur-release.</summary>
    public static Tween Release(Transform target, float rest = 1f, bool unscaled = true)
    {
        target.DOKill();
        float d = T.Durations.Release;
        float peak = Juice.ReduceMotion ? 1f : T.Scales.PopPeak;
        // Targeted, so the next Press (which kills the transform's tweens) also ends this release: a quick
        // re-press must not be fought by the last release still writing the scale.
        return DOTween.Sequence().SetUpdate(unscaled).SetLink(target.gameObject).SetTarget(target)
            .Append(target.DOScale(rest * peak, d * 0.45f).SetEase(T.Eases.Out))
            .Append(target.DOScale(rest, d * 0.55f).SetEase(T.Eases.Press));
    }

    /// <summary>Appear from <paramref name="from"/> through scale-pop-peak to rest (Enter Pop).</summary>
    public static Tween PopIn(Transform target, float from = 0f, float rest = 1f, float duration = -1f, bool unscaled = true)
    {
        target.DOKill();
        float d = duration > 0f ? duration : T.Durations.Pop;
        float peak = Juice.ReduceMotion ? 1f : T.Scales.PopPeak;
        target.localScale = Vector3.one * (rest * from);
        return DOTween.Sequence().SetUpdate(unscaled).SetLink(target.gameObject).SetTarget(target)
            .Append(target.DOScale(rest * peak, d * 0.6f).SetEase(T.Eases.Out))
            .Append(target.DOScale(rest, d * 0.4f).SetEase(T.Eases.Press));
    }

    /// <summary>
    /// DOPunchScale at a token strength (punch-soft, punch-hard). A punch still running on the same target is
    /// completed first, so rapid punches never leave the rest scale drifting.
    /// </summary>
    public static Tween Punch(Transform target, float strength, float duration = -1f, bool unscaled = true)
    {
        int key = target.GetInstanceID();
        if (Punches.TryGetValue(key, out Tween running) && running.IsActive()) running.Complete();

        Tween punch = target.DOPunchScale(Vector3.one * Motion(strength), duration > 0f ? duration : T.Durations.Pop, 6, 0.6f)
            .SetUpdate(unscaled).SetLink(target.gameObject);
        punch.OnKill(() => { if (Punches.TryGetValue(key, out Tween t) && t == punch) Punches.Remove(key); });
        Punches[key] = punch;
        return punch;
    }

    /// <summary>
    /// A landing wobble: a small size bump and a tremble, both settling back to rest, after <paramref name="delay"/>
    /// (NavTabs' arrival). Pass 0 for either to leave that property alone, for a target whose scale or rotation
    /// something else owns. Targeted, and cut short it puts back only what it moved: a tap killing the target's
    /// tweens never leaves a button tilted.
    /// </summary>
    public static Tween Jiggle(Transform target, float scale, float degrees, float delay = 0f, float duration = -1f,
        bool unscaled = true)
    {
        if (target == null || (scale <= 0f && degrees == 0f)) return null;

        float d = duration > 0f ? duration : T.Durations.Pop + 0.1f;
        Vector3 restScale = target.localScale;
        Quaternion restRotation = target.localRotation;

        Sequence s = DOTween.Sequence().SetUpdate(unscaled).SetLink(target.gameObject).SetTarget(target).SetDelay(delay);
        if (scale > 0f) s.Join(target.DOPunchScale(restScale * Motion(scale), d, 6, 0.55f));
        if (degrees != 0f) s.Join(target.DOPunchRotation(new Vector3(0f, 0f, Motion(degrees)), d * 1.15f, 7, 0.5f));

        s.OnKill(() =>
        {
            if (target == null) return;
            if (scale > 0f) target.localScale = restScale;
            if (degrees != 0f) target.localRotation = restRotation;
        });
        return s;
    }

    /// <summary>A refusal on UI: a decaying sideways shake of <paramref name="distance"/> canvas units with a small wobble (tilt-deny).</summary>
    public static Tween Deny(RectTransform target, float distance, bool unscaled = true)
    {
        // Targeted, so this DOComplete finishes a shake still running from a quick second tap instead of
        // starting a new one from its mid-shake pose (which it would then end on, for good).
        target.DOComplete();
        Vector2 restPosition = target.anchoredPosition;
        Quaternion restRotation = target.localRotation;

        float d = T.Durations.Deny;
        return DOTween.Sequence().SetUpdate(unscaled).SetLink(target.gameObject).SetTarget(target)
            .Join(target.DOShakeAnchorPos(d, new Vector2(Motion(distance), 0f), 16, 0f, false, true, ShakeRandomnessMode.Harmonic))
            .Join(target.DOShakeRotation(d, new Vector3(0f, 0f, Motion(T.Distances.TiltDeny)), 16, 0f, true, ShakeRandomnessMode.Harmonic))
            .OnKill(() =>
            {
                if (target == null) return;
                target.anchoredPosition = restPosition;
                target.localRotation = restRotation;
            });
    }

    /// <summary>A refusal in the world: a decaying sideways shake of <paramref name="distance"/> world units.</summary>
    public static Tween Deny(Transform target, float distance, bool unscaled = true)
    {
        target.DOComplete();
        Vector3 rest = target.localPosition;
        return target.DOShakePosition(T.Durations.Deny, new Vector3(Motion(distance), 0f, 0f), 16, 0f, false, true, ShakeRandomnessMode.Harmonic)
            .SetUpdate(unscaled).SetLink(target.gameObject)
            .OnKill(() => { if (target != null) target.localPosition = rest; });
    }

    /// <summary>
    /// The last stretch of a drop onto the board: from <paramref name="height"/> above, stretched, on ease-fall
    /// over dur-fall. Chain <see cref="Land"/> after it.
    /// </summary>
    public static Tween Fall(Transform target, Vector3 restLocalPosition, Vector3 restScale, float height, bool unscaled = false)
    {
        target.DOKill();
        target.localPosition = restLocalPosition + Vector3.up * height;
        target.localScale = Vector3.Scale(restScale, new Vector3(0.86f, 1.16f, 1f));
        float d = T.Durations.Fall;
        return DOTween.Sequence().SetUpdate(unscaled).SetLink(target.gameObject)
            .Join(target.DOLocalMove(restLocalPosition, d).SetEase(T.Eases.Fall))
            .Join(target.DOScale(Vector3.Scale(restScale, new Vector3(0.9f, 1.12f, 1f)), d).SetEase(T.Eases.Fall));
    }

    /// <summary>A landing: squash to land-x by land-y, then settle to rest on ease-settle over dur-settle.</summary>
    public static Tween Land(Transform target, Vector3 restScale, bool unscaled = false)
    {
        target.DOKill();
        target.localScale = Juice.ReduceMotion ? restScale : Vector3.Scale(restScale, new Vector3(T.Scales.LandX, T.Scales.LandY, 1f));
        return target.DOScale(restScale, T.Durations.Settle)
            .SetEase(T.Eases.Settle, T.Eases.SettleAmplitude, T.Eases.SettlePeriod)
            .SetUpdate(unscaled).SetLink(target.gameObject);
    }

    /// <summary>An idle breath to the breathe token and back, forever. Null under Reduce motion.</summary>
    public static Tween Breathe(Transform target, float rest = 1f)
    {
        if (Juice.ReduceMotion) return null;
        return target.DOScale(rest * T.Scales.Breathe, 0.8f).SetEase(T.Eases.Breathe)
            .SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(target.gameObject);
    }

    /// <summary>
    /// Rolls a label from one whole number to another over dur-count on ease-out, calling
    /// <paramref name="onTick"/> each time the shown value changes (ladders climb on it).
    /// </summary>
    public static Tween Count(TMP_Text label, int from, int to, Func<int, string> format = null, Action<int> onTick = null,
        float duration = -1f, bool unscaled = true)
    {
        format ??= v => v.ToString();
        label.DOKill();
        int shown = from;
        label.text = format(from);
        return DOVirtual.Int(from, to, duration > 0f ? duration : T.Durations.Count, v =>
            {
                if (v == shown) return;
                shown = v;
                label.text = format(v);
                onTick?.Invoke(v);
            })
            .SetEase(T.Eases.Out).SetUpdate(unscaled).SetLink(label.gameObject).SetTarget(label);
    }
}
