using Lofelt.NiceVibrations;
using UnityEngine;

/// <summary>
/// Stores the comfort settings in <see cref="PlayerPrefs"/>. Everything defaults to on except Reduce motion.
/// </summary>
/// <remarks>
/// Vibration is also pushed straight into Nice Vibrations' global switch, so every haptic in the game —
/// Feel's haptic feedbacks and any direct <c>HapticPatterns</c> call alike — obeys it without having to
/// check the setting itself. Screen shake has no such global switch; the camera's shakers read it
/// through <c>CameraFeedbackGate</c>. Sound, music and reduce motion are read by the Juice services
/// (<c>SfxService</c>, <c>JuiceCamera</c>, <c>JuiceFx</c>).
/// </remarks>
public class GameSettingsService : BaseGameSettingsService
{
    private const string ScreenShakeKey = "settings.screenShake";
    private const string VibrationKey = "settings.vibration";
    private const string SoundKey = "settings.sound";
    private const string MusicKey = "settings.music";
    private const string ReduceMotionKey = "settings.reduceMotion";

    private bool _screenShake;
    private bool _vibration;
    private bool _sound;
    private bool _music;
    private bool _reduceMotion;

    public override bool ScreenShakeEnabled => _screenShake;
    public override bool VibrationEnabled => _vibration;
    public override bool SoundEnabled => _sound;
    public override bool MusicEnabled => _music;
    public override bool ReduceMotion => _reduceMotion;

    public GameSettingsService()
    {
        _screenShake = PlayerPrefs.GetInt(ScreenShakeKey, 1) == 1;
        _vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        _sound = PlayerPrefs.GetInt(SoundKey, 1) == 1;
        _music = PlayerPrefs.GetInt(MusicKey, 1) == 1;
        _reduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;

        ApplyVibration();
    }

    public override void SetScreenShakeEnabled(bool enabled)
    {
        if (Store(ref _screenShake, enabled, ScreenShakeKey)) RaiseSettingsChanged();
    }

    public override void SetVibrationEnabled(bool enabled)
    {
        if (!Store(ref _vibration, enabled, VibrationKey)) return;

        ApplyVibration();
        RaiseSettingsChanged();
    }

    public override void SetSoundEnabled(bool enabled)
    {
        if (Store(ref _sound, enabled, SoundKey)) RaiseSettingsChanged();
    }

    public override void SetMusicEnabled(bool enabled)
    {
        if (Store(ref _music, enabled, MusicKey)) RaiseSettingsChanged();
    }

    public override void SetReduceMotion(bool enabled)
    {
        if (Store(ref _reduceMotion, enabled, ReduceMotionKey)) RaiseSettingsChanged();
    }

    // Writes and persists a changed value; false when it already had it.
    private static bool Store(ref bool field, bool value, string key)
    {
        if (field == value) return false;

        field = value;
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
        return true;
    }

    // Only written when it differs: the setter stops any playing haptic, which is pointless work at boot.
    private void ApplyVibration()
    {
        if (HapticController.hapticsEnabled != _vibration)
            HapticController.hapticsEnabled = _vibration;
    }
}
