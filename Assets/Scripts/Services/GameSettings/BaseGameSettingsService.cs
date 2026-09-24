using System;

/// <summary>
/// Comfort settings the player controls: screen shake and vibration. Game feel reads these before moving the
/// camera or buzzing the phone, so turning one off silences it everywhere at once.
/// </summary>
/// <remarks>
/// Plain C# and registered in <see cref="ServiceLocator"/> by <c>ClientManager</c>, like the save and the
/// reward service, so the Main Menu page that edits these and the match that obeys them share one instance.
/// Kept apart from <see cref="BasePlayerSaveManager"/> on purpose: these belong to the device, not to the
/// player's progress, and must not follow a save that may later move to the cloud onto another phone.
/// </remarks>
public abstract class BaseGameSettingsService
{
    /// <summary>A setting changed. Raised after the new value is stored and applied.</summary>
    public event Action OnSettingsChanged;

    public abstract bool ScreenShakeEnabled { get; }
    public abstract bool VibrationEnabled { get; }

    public abstract void SetScreenShakeEnabled(bool enabled);
    public abstract void SetVibrationEnabled(bool enabled);

    protected void RaiseSettingsChanged() => OnSettingsChanged?.Invoke();
}
