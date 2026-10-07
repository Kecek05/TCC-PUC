using Lofelt.NiceVibrations;
using UnityEngine;

/// <summary>What a buzz means. Each role maps to a Nice Vibrations preset through <see cref="JuiceTokensSO.Haptics"/>.</summary>
public enum HapticRole
{
    Select = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3,
    Success = 4,
    Warning = 5,
    Failure = 6,
}

/// <summary>
/// Buzzes the phone by role. Callers say what the buzz means; the service picks the preset, enforces the
/// budget-haptic gap, and respects the Vibration setting.
/// </summary>
public abstract class BaseHapticsService
{
    public abstract void Play(HapticRole role);
}

/// <summary>
/// Nice Vibrations presets with a minimum gap between two buzzes, so a burst of events never buzzes
/// continuously. Plain C#, like the settings it reads.
/// </summary>
/// <remarks>
/// The Vibration setting is also pushed into <c>HapticController.hapticsEnabled</c> by
/// <c>GameSettingsService</c>, which is what keeps Feel's own <c>MMF_NVPreset</c> feedbacks quiet; checking it
/// here as well skips the budget bookkeeping for a buzz that would not happen anyway.
/// </remarks>
public class HapticsService : BaseHapticsService
{
    private readonly BaseGameSettingsService _settings;
    private float _lastBuzz = -10f;

    public HapticsService(BaseGameSettingsService settings)
    {
        _settings = settings;
    }

    public override void Play(HapticRole role)
    {
        if (_settings != null && !_settings.VibrationEnabled) return;

        JuiceTokensSO tokens = JuiceTokensSO.Instance;
        float now = Time.unscaledTime;
        if (now - _lastBuzz < tokens.Budgets.HapticGap) return;

        HapticPatterns.PresetType preset = tokens.PresetFor(role);
        if (preset == HapticPatterns.PresetType.None) return;

        _lastBuzz = now;
        HapticPatterns.PlayPreset(preset);
    }
}
