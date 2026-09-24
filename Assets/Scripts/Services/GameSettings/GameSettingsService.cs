using Lofelt.NiceVibrations;
using UnityEngine;

/// <summary>
/// Stores the comfort settings in <see cref="PlayerPrefs"/>. Both default to on.
/// </summary>
/// <remarks>
/// Vibration is also pushed straight into Nice Vibrations' global switch, so every haptic in the game —
/// Feel's haptic feedbacks and any direct <c>HapticPatterns</c> call alike — obeys it without having to
/// check the setting itself. Screen shake has no such global switch; the camera's shakers read it
/// through <c>CameraFeedbackGate</c>.
/// </remarks>
public class GameSettingsService : BaseGameSettingsService
{
    private const string ScreenShakeKey = "settings.screenShake";
    private const string VibrationKey = "settings.vibration";

    private bool _screenShake;
    private bool _vibration;

    public override bool ScreenShakeEnabled => _screenShake;
    public override bool VibrationEnabled => _vibration;

    public GameSettingsService()
    {
        _screenShake = PlayerPrefs.GetInt(ScreenShakeKey, 1) == 1;
        _vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1;

        ApplyVibration();
    }

    public override void SetScreenShakeEnabled(bool enabled)
    {
        if (_screenShake == enabled) return;

        _screenShake = enabled;
        PlayerPrefs.SetInt(ScreenShakeKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        RaiseSettingsChanged();
    }

    public override void SetVibrationEnabled(bool enabled)
    {
        if (_vibration == enabled) return;

        _vibration = enabled;
        PlayerPrefs.SetInt(VibrationKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        ApplyVibration();
        RaiseSettingsChanged();
    }

    // Only written when it differs: the setter stops any playing haptic, which is pointless work at boot.
    private void ApplyVibration()
    {
        if (HapticController.hapticsEnabled != _vibration)
            HapticController.hapticsEnabled = _vibration;
    }
}
