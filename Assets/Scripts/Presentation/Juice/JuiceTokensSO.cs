using System;
using DG.Tweening;
using Lofelt.NiceVibrations;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Every number the game's feedback is tuned with: the tokens of the Juice &amp; Feedback System design system,
/// mirrored one to one from its tokens.json. Presenters and services read these instead of typing 0.3f into a
/// tween, so retiming the game is an edit to this asset (or a re-import of the design system), never a code
/// change.
/// </summary>
/// <remarks>
/// Lives in Resources so anything can reach it without a serialized reference: a pooled enemy prefab, a static
/// helper, a scene launched without the boot flow. A missing asset degrades to the defaults written below,
/// which are the design system's values, so the game never loses its feel to a broken reference.
/// Durations are seconds; distances are canvas units at the 1080x1920 reference; shakes are a share of the
/// screen's height.
/// </remarks>
[CreateAssetMenu(fileName = "JuiceTokens", menuName = "Scriptable Objects/Presentation/Juice Tokens")]
public class JuiceTokensSO : ScriptableObject
{
    public const string ResourcePath = "Juice/JuiceTokens";

    private static JuiceTokensSO _instance;

    /// <summary>The project's tokens, loaded once. Never null.</summary>
    public static JuiceTokensSO Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<JuiceTokensSO>(ResourcePath);
            if (_instance == null) _instance = CreateInstance<JuiceTokensSO>();
            return _instance;
        }
    }

    [Serializable]
    public class DurationTokens
    {
        [Tooltip("dur-press: pointer down on any button or card.")] public float Press = 0.08f;
        [Tooltip("dur-select: card select lift, drag scale-up.")] public float Select = 0.12f;
        [Tooltip("dur-release: pointer up, back to rest with overshoot.")] public float Release = 0.18f;
        [Tooltip("dur-flash: hit flash.")] public float Flash = 0.09f;
        [Tooltip("dur-hitstop: heavy-hit freeze of one enemy's visual. Never Time.timeScale.")] public float HitStop = 0.06f;
        [Tooltip("dur-fall: the last stretch of a drop onto the board.")] public float Fall = 0.16f;
        [Tooltip("dur-punch: commit punch when a card is played.")] public float Punch = 0.22f;
        [Tooltip("dur-pop: appearing or snapping.")] public float Pop = 0.3f;
        [Tooltip("dur-settle: a landing's squash settling back to rest.")] public float Settle = 0.45f;
        [Tooltip("dur-deny: refusal shake.")] public float Deny = 0.32f;
        [Tooltip("dur-panel: modal open.")] public float Panel = 0.35f;
        [Tooltip("dur-travel: a card or coin travelling, a camera slide.")] public float Travel = 0.4f;
        [Tooltip("dur-count: a number rolling to its new value.")] public float Count = 0.6f;
        [Tooltip("dur-spawn: the old tower scale-in.")] public float Spawn = 0.7f;
        [Tooltip("dur-levelup: a new tower level popping in.")] public float LevelUp = 0.9f;
        [Tooltip("dur-celebrate: a card upgrade, end to end.")] public float Celebrate = 1.4f;
        [Tooltip("dur-toast: a refusal toast on screen.")] public float Toast = 1.6f;
        [Tooltip("dur-banner: wave banner rise.")] public float Banner = 2f;
        [Tooltip("dur-shake: short camera shake.")] public float Shake = 0.12f;
        [Tooltip("dur-shake-long: base hit, heavy spell landing.")] public float ShakeLong = 0.3f;
        [Tooltip("stagger: between items that move as a list.")] public float Stagger = 0.04f;
        [Tooltip("stagger-reveal: between reward tiles.")] public float StaggerReveal = 0.16f;
    }

    [Serializable]
    public class EaseTokens
    {
        [Tooltip("ease-out: anything arriving.")] public Ease Out = Ease.OutCubic;
        [Tooltip("ease-in: anything leaving.")] public Ease In = Ease.InCubic;
        [Tooltip("ease-in-out: travel with both ends on screen.")] public Ease InOut = Ease.InOutCubic;
        [Tooltip("ease-pop: appear, select, release.")] public Ease Pop = Ease.OutBack;
        [Tooltip("ease-press: press down, colour fades.")] public Ease Press = Ease.OutQuad;
        [Tooltip("ease-snap: a card returning to the hand.")] public Ease Snap = Ease.OutExpo;
        [Tooltip("ease-anticipate: the wind-up before a launch.")] public Ease Anticipate = Ease.InBack;
        [Tooltip("ease-fall: things dropping onto the board.")] public Ease Fall = Ease.InQuad;
        [Tooltip("ease-settle: a landing settling. Use it on the size of a squash.")] public Ease Settle = Ease.OutElastic;
        [Tooltip("ease-settle amplitude.")] public float SettleAmplitude = 1f;
        [Tooltip("ease-settle period.")] public float SettlePeriod = 0.4f;
        [Tooltip("ease-breathe: idle loops.")] public Ease Breathe = Ease.InOutSine;
    }

    [Serializable]
    public class ScaleTokens
    {
        [Tooltip("scale-press")] public float Press = 0.92f;
        [Tooltip("scale-select")] public float Select = 1.08f;
        [Tooltip("scale-drag")] public float Drag = 1.12f;
        [Tooltip("scale-nav")] public float Nav = 1.15f;
        [Tooltip("scale-panel-from")] public float PanelFrom = 0.8f;
        [Tooltip("scale-pop-peak")] public float PopPeak = 1.06f;
        [Tooltip("scale-commit")] public float Commit = 1.22f;
        [Tooltip("punch-soft: DOPunchScale strength for counters and icons.")] public float PunchSoft = 0.12f;
        [Tooltip("punch-hard: level-ups, rewards, a refusing counter.")] public float PunchHard = 0.35f;
        [Tooltip("hit-squash")] public float HitSquash = 1.3f;
        [Tooltip("hit-rebound")] public float HitRebound = 0.88f;
        [Tooltip("land-x: width at the instant of landing.")] public float LandX = 1.25f;
        [Tooltip("land-y: height at the instant of landing.")] public float LandY = 0.8f;
        [Tooltip("breathe: peak of an idle breath.")] public float Breathe = 1.04f;
    }

    [Serializable]
    public class DistanceTokens
    {
        [Tooltip("lift-card (canvas units)")] public float LiftCard = 22f;
        [Tooltip("shake-card (canvas units)")] public float ShakeCard = 18f;
        [Tooltip("shake-bar (canvas units)")] public float ShakeBar = 14f;
        [Tooltip("rise-banner (canvas units)")] public float RiseBanner = 40f;
        [Tooltip("rise-toast (canvas units)")] public float RiseToast = 24f;
        [Tooltip("tilt-drag (degrees)")] public float TiltDrag = 12f;
        [Tooltip("tilt-deny (degrees)")] public float TiltDeny = 6f;
    }

    [Serializable]
    public class ShakeTokens
    {
        [Tooltip("shake-micro: share of screen height.")] public float Micro = 0.0042f;
        [Tooltip("shake-small: share of screen height.")] public float Small = 0.008f;
        [Tooltip("shake-medium: share of screen height.")] public float Medium = 0.015f;
        [Tooltip("shake-large: share of screen height.")] public float Large = 0.025f;
        [Tooltip("zoom-kick: share of the orthographic size.")] public float ZoomKick = 0.05f;
        [Tooltip("shake-freq (Hz)")] public float Frequency = 35f;
    }

    [Serializable]
    public class HapticTokens
    {
        public HapticPatterns.PresetType Select = HapticPatterns.PresetType.Selection;
        public HapticPatterns.PresetType Light = HapticPatterns.PresetType.LightImpact;
        public HapticPatterns.PresetType Medium = HapticPatterns.PresetType.MediumImpact;
        public HapticPatterns.PresetType Heavy = HapticPatterns.PresetType.HeavyImpact;
        public HapticPatterns.PresetType Success = HapticPatterns.PresetType.Success;
        public HapticPatterns.PresetType Warning = HapticPatterns.PresetType.Warning;
        public HapticPatterns.PresetType Failure = HapticPatterns.PresetType.Failure;
    }

    [Serializable]
    public class AudioTokens
    {
        [Range(0f, 1f), Tooltip("vol-ui")] public float VolumeUi = 0.5f;
        [Range(0f, 1f), Tooltip("vol-game")] public float VolumeGame = 0.8f;
        [Range(0f, 1f), Tooltip("vol-impact")] public float VolumeImpact = 1f;
        [Range(0f, 1f), Tooltip("vol-music")] public float VolumeMusic = 0.35f;
        [Tooltip("duck-music (dB)")] public float DuckMusicDb = -6f;
        [Tooltip("pitch-jitter: random pitch either way, as a share.")] public float PitchJitter = 0.06f;
        [Tooltip("pitch-step: one semitone, for ladders.")] public float PitchStep = 1.0595f;
        [Tooltip("sfx-cooldown (seconds)")] public float Cooldown = 0.05f;
    }

    [Serializable]
    public class OpacityTokens
    {
        [Range(0f, 1f), Tooltip("disabled")] public float Disabled = 0.5f;
        [Range(0f, 1f), Tooltip("flash-peak")] public float FlashPeak = 0.55f;
        [Range(0f, 1f), Tooltip("ghost-blocked")] public float GhostBlocked = 0.3f;
    }

    [Serializable]
    public class BudgetTokens
    {
        [Tooltip("budget-shake: camera shakes per second.")] public int ShakesPerSecond = 2;
        [Tooltip("budget-flash: large flashes per second.")] public int FlashesPerSecond = 3;
        [Tooltip("budget-numbers: floating numbers alive at once.")] public int Numbers = 16;
        [Tooltip("budget-voices: sounds playing at once.")] public int Voices = 12;
        [Tooltip("budget-haptic: minimum gap between two haptics (seconds).")] public float HapticGap = 0.1f;
    }

    [Serializable]
    public class ColorTokens
    {
        public Color Ink = Hex("#f5f5f5");
        public Color InkMuted = Hex("#b3b3b3");
        public Color Outline = Hex("#000000");
        public Color Sheet = Hex("#14171ff2");
        public Color Scrim = Hex("#000000b8");
        public Color Deny = Hex("#ff4747");
        public Color CardFlash = Hex("#ff3838");
        public Color HitFlash = Hex("#ff6b6b");
        public Color Damage = Hex("#ffffff");
        public Color DamageHeavy = Hex("#ffd140");
        public Color DamageResist = Hex("#b3b3b3");
        public Color Gain = Hex("#8aff86");
        public Color Mana = Hex("#a0cfff");
        public Color ManaGain = Hex("#8cd9ff");
        public Color ManaShort = Hex("#293a4a");
        public Color Attention = Hex("#ffeb73");
        public Color PlaceOk = Hex("#ffffff");
        public Color Ghost = Hex("#c0c0c06e");
        public Color Range = Hex("#dfdfdf3d");
        public Color RangeNext = Hex("#66ff664d");
        public Color Sparkle = Hex("#ffdb59");
        public Color Dust = Hex("#ebebf2");
        public Color Blast = Hex("#f1934e");
        public Color Ice = Hex("#00c0ff");
        public Color Hasted = Hex("#ffeb04");
        public Color Gold = Hex("#efec82");
        public Color HpYou = Hex("#97f2dd");
        public Color HpFoe = Hex("#ff7273");
        public Color NameYou = Hex("#ffc986");
        public Color NameFoe = Hex("#ff868c");
        public Color Cta = Hex("#f5f55b");
        public Color Upgrade = Hex("#59d564");
        public Color Panel = Hex("#cacaca");
        public Color Stat = Hex("#8c8c8c");
        public Color Tile = Hex("#959595");
        public Color Nav = Hex("#2c273f");
        public Color ButtonFace = Hex("#d5d5d5");
        public Color RarityLegendary = Hex("#fdd26c");
        public Color ZoneHaste = Hex("#ffd94d66");
        public Color ZoneRage = Hex("#4cffe166");
    }

    [FoldoutGroup("Durations"), HideLabel, InlineProperty] public DurationTokens Durations = new();
    [FoldoutGroup("Eases"), HideLabel, InlineProperty] public EaseTokens Eases = new();
    [FoldoutGroup("Scales"), HideLabel, InlineProperty] public ScaleTokens Scales = new();
    [FoldoutGroup("Distances"), HideLabel, InlineProperty] public DistanceTokens Distances = new();
    [FoldoutGroup("Shake"), HideLabel, InlineProperty] public ShakeTokens Shake = new();
    [FoldoutGroup("Haptics"), HideLabel, InlineProperty] public HapticTokens Haptics = new();
    [FoldoutGroup("Audio"), HideLabel, InlineProperty] public AudioTokens Audio = new();
    [FoldoutGroup("Opacity"), HideLabel, InlineProperty] public OpacityTokens Opacity = new();
    [FoldoutGroup("Budgets"), HideLabel, InlineProperty] public BudgetTokens Budgets = new();
    [FoldoutGroup("Colors"), HideLabel, InlineProperty] public ColorTokens Colors = new();

#if UNITY_EDITOR
    [Title("Design system")]
    [Tooltip("The design system's tokens.json, re-imported by the button below.")]
    public TextAsset TokensJson;
#endif

    /// <summary>A shake token (share of screen height) in world units for an orthographic camera.</summary>
    public static float ShakeUnits(float share, Camera camera)
    {
        float size = camera != null && camera.orthographic ? camera.orthographicSize : 6f;
        return share * 2f * size;
    }

    /// <summary>The Nice Vibrations preset this asset assigns to a role.</summary>
    public HapticPatterns.PresetType PresetFor(HapticRole role) => role switch
    {
        HapticRole.Select => Haptics.Select,
        HapticRole.Light => Haptics.Light,
        HapticRole.Medium => Haptics.Medium,
        HapticRole.Heavy => Haptics.Heavy,
        HapticRole.Success => Haptics.Success,
        HapticRole.Warning => Haptics.Warning,
        HapticRole.Failure => Haptics.Failure,
        _ => HapticPatterns.PresetType.None
    };

    /// <summary>"#rrggbb" or "#rrggbbaa" to a colour. Plain C# on purpose: it runs in field initializers, which
    /// Unity executes during deserialization, where its own native APIs may not be called.</summary>
    public static Color Hex(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Color.magenta;
        string h = hex[0] == '#' ? hex.Substring(1) : hex;
        if (h.Length != 6 && h.Length != 8) return Color.magenta;
        try
        {
            byte r = Convert.ToByte(h.Substring(0, 2), 16);
            byte g = Convert.ToByte(h.Substring(2, 2), 16);
            byte b = Convert.ToByte(h.Substring(4, 2), 16);
            byte a = h.Length == 8 ? Convert.ToByte(h.Substring(6, 2), 16) : (byte)255;
            return new Color32(r, g, b, a);
        }
        catch (FormatException)
        {
            return Color.magenta;
        }
    }
}
