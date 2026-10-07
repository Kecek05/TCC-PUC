using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// Plays a Juice sound cue from an authored Feel player, through the same <see cref="BaseSfxService"/> code uses:
/// one sound path, so cooldowns, pitch jitter, the voice budget and the Sound setting hold everywhere.
/// </summary>
[AddComponentMenu("")]
[FeedbackHelp("Plays a Juice sound cue through the shared sound service (cooldowns, pitch jitter, voice budget, Sound setting).")]
[Serializable]
[FeedbackPath("Audio/Juice Sfx")]
public class MMF_JuiceSfx : MMF_Feedback
{
    public static bool FeedbackTypeAuthorized = true;

    public override float FeedbackDuration => 0f;

#if UNITY_EDITOR
    public override Color FeedbackColor => MMFeedbacksInspectorColors.SoundsColor;
    public override string RequiredTargetText => Cue.ToString();
#endif

    [MMFInspectorGroup("Juice Sfx", true, 14)]
    [Tooltip("The cue to play.")]
    public SfxId Cue = SfxId.Tap;

    [Tooltip("Semitones up from the cue's pitch.")]
    public int PitchStep;

    [Tooltip("Multiplies the cue's volume.")]
    [Range(0f, 1f)]
    public float Volume = 1f;

    protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1f)
    {
        if (!Active || !FeedbackTypeAuthorized) return;
        Juice.Sfx(Cue, PitchStep, Volume * Mathf.Clamp01(feedbacksIntensity));
    }
}
