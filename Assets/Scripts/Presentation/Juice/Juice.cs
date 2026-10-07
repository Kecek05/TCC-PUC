using UnityEngine;

/// <summary>
/// The one door to the game's feedback channels: sound, haptics, refusal toasts, the tokens and the comfort
/// settings. Presenters call these instead of reaching for services, so every call tolerates a missing
/// service: a scene launched straight from the editor still plays, and nothing here can throw at a caller.
/// </summary>
public static class Juice
{
    /// <summary>The project's feedback tokens. Never null.</summary>
    public static JuiceTokensSO Tokens => JuiceTokensSO.Instance;

    /// <summary>Plays a sound cue. <paramref name="pitchStep"/> climbs a semitone ladder.</summary>
    public static void Sfx(SfxId id, int pitchStep = 0, float volume = 1f)
    {
        if (id != SfxId.None && TryService(out BaseSfxService sfx)) sfx.Play(id, pitchStep, volume);
    }

    /// <summary>Buzzes the phone for a role, within the haptic budget and the Vibration setting.</summary>
    public static void Haptic(HapticRole role)
    {
        if (TryService(out BaseHapticsService haptics)) haptics.Play(role);
    }

    /// <summary>The refusal toast for a typed reason, with the deny sound and a Warning buzz.</summary>
    public static void Refuse(RefusalReason reason, string detail = null, bool feedback = true)
    {
        if (reason != RefusalReason.None && TryService(out BaseToastService toast)) toast.Show(reason, detail, feedback);
    }

    /// <summary>The refusal toast with free text, for a no that has no typed reason.</summary>
    public static void Toast(string title, string hint = null, bool feedback = true)
    {
        if (TryService(out BaseToastService toast)) toast.ShowMessage(title, hint, feedback);
    }

    /// <summary>The Reduce motion setting: no shakes or idle loops, punches halved.</summary>
    public static bool ReduceMotion => ServiceLocator.TryGet(out BaseGameSettingsService settings) && settings.ReduceMotion;

    private static bool TryService<T>(out T service) where T : class
    {
        if (ServiceLocator.TryGet(out service)) return true;
        if (!Application.isPlaying) return false;

        JuiceServices.EnsureInstalled();
        return ServiceLocator.TryGet(out service);
    }
}

/// <summary>
/// Creates the feedback services and registers them. <c>ClientManager</c> installs them at boot, right after
/// the settings they read, so they outlive every scene like the save and the reward service do; a scene
/// launched without the boot flow gets them on first use instead.
/// </summary>
public static class JuiceServices
{
    private static bool _installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _installed = false;

    public static void Install(BaseGameSettingsService settings)
    {
        if (_installed) return;
        _installed = true;

        SfxLibrarySO library = Resources.Load<SfxLibrarySO>(SfxLibrarySO.ResourcePath);
        RefusalCopySO copy = Resources.Load<RefusalCopySO>(RefusalCopySO.ResourcePath);

        ServiceLocator.Register<BaseSfxService>(SfxService.Create(library, settings));
        ServiceLocator.Register<BaseHapticsService>(new HapticsService(settings));
        ServiceLocator.Register<BaseToastService>(ToastService.Create(copy));
    }

    /// <summary>For scenes launched without the boot flow: installs against whatever settings are registered.</summary>
    public static void EnsureInstalled()
    {
        if (_installed) return;
        ServiceLocator.TryGet(out BaseGameSettingsService settings);
        Install(settings);
    }

    public static void Uninstall()
    {
        ServiceLocator.Unregister<BaseSfxService>();
        ServiceLocator.Unregister<BaseHapticsService>();
        ServiceLocator.Unregister<BaseToastService>();
        _installed = false;
    }
}
