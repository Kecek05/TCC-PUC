using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Settings every troop shares: the shape of the lane's corners, how troops make room for each other, the dust
/// budget, and the profile a troop walks with when its data names none.
/// </summary>
/// <remarks>
/// Read through <see cref="Instance"/> (Resources), never null, like <see cref="SpellFxSettingsSO"/>. Per-type
/// body language lives on <see cref="TroopMotionProfileSO"/>; what is here cannot sensibly differ per troop
/// (two troops must agree on how hard they push each other apart, and the lane has one shape).
/// </remarks>
[CreateAssetMenu(fileName = "TroopMotion", menuName = "Scriptable Objects/Juice/Troop Motion Settings")]
public class TroopMotionSettingsSO : ScriptableObject
{
    public const string ResourcePath = "Juice/TroopMotion";

    private static TroopMotionSettingsSO _instance;
    private static TroopMotionProfileSO _fallbackProfile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _fallbackProfile = null;
    }

    /// <summary>The asset in Resources, or a default-valued one built in memory so callers never see null.</summary>
    public static TroopMotionSettingsSO Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<TroopMotionSettingsSO>(ResourcePath);
            if (_instance == null)
            {
                _instance = CreateInstance<TroopMotionSettingsSO>();
                _instance.hideFlags = HideFlags.DontSave;
            }
            return _instance;
        }
    }

    [Title("Profiles")]
    [Tooltip("How a troop walks when its EnemyDataSO.MotionProfile is empty.")]
    [SerializeField] private TroopMotionProfileSO defaultProfile;

    [Title("Lane corners")]
    [Tooltip("How far before and after each waypoint the drawn lane starts to curve, in world units. Clamped to " +
             "half of each adjacent segment. Must stay above MaxLateral * 1.42 or an inside troop loops.")]
    [Min(0f)] public float CornerTrim = 0.34f;
    [Tooltip("LUT resolution along the lane, in world units per sample.")]
    [Min(0.005f)] public float CurveSampleSpacing = 0.02f;

    [Title("Steering", "Visual-only: troops make room for each other. Never moves the gameplay position.")]
    [Tooltip("Two troops start pushing apart when their bodies are closer than this share of their summed radii. " +
             "Below 1, bodies are allowed to overlap a little before anything reacts: a crowd reads as a crowd.")]
    [Min(0.1f)] public float SeparationFactor = 0.7f;
    [Tooltip("How fast a full overlap pushes two bodies apart, in world units per second.")]
    [Min(0f)] public float PushSpeed = 1f;
    [Tooltip("How fast a body drifts back onto its own route once it has room again (per second). The ratio to " +
             "PushSpeed sets how much overlap a crowd settles at.")]
    [Min(0f)] public float ReturnRate = 1f;
    [Tooltip("Share of the push spent along the lane. Kept small: an along push makes a body brake and catch up, " +
             "which reads as lag. Sideways is free.")]
    [PropertyRange(0f, 1f)] public float AlongShare = 0.1f;
    [Tooltip("Hard limit on how far steering may put a body ahead of or behind its gameplay position.")]
    [Min(0f)] public float MaxAlong = 0.06f;
    [Tooltip("Opposite-direction troops (a sent troop meeting the wave) look this many seconds ahead, so they " +
             "sidestep before the bodies meet rather than after.")]
    [Min(0f)] public float HeadOnLookAhead = 0.3f;

    [Title("Remote clients")]
    [Tooltip("How quickly a remote client's predicted progress corrects toward the server's (per second).")]
    [Min(0.5f)] public float CorrectionSharpness = 6f;
    [Tooltip("A backward error past this (world units) is drawn as a drag back (an Ancora pull); smaller ones " +
             "just wait for the server, so a troop never visibly steps backwards on jitter.")]
    [Min(0f)] public float BackwardTolerance = 0.12f;
    [Tooltip("An error larger than this (world units) snaps instead of easing: a late join, a teleport.")]
    [Min(0f)] public float SnapDistance = 1.2f;
    [Tooltip("Longest a client extrapolates past the last progress it heard, in seconds.")]
    [Min(0f)] public float MaxExtrapolation = 0.35f;

    [Title("Dust")]
    [Tooltip("Most dust puffs per second across the whole board.")]
    [Min(0f)] public float DustPerSecond = 10f;
    public Color DustColor = new(0.86f, 0.82f, 0.74f, 0.45f);

    [Title("Debug")]
    [Tooltip("Scene-view gizmos: each troop's walked trail and the route its seed lays out ahead of it.")]
    public bool DrawRouteGizmos = true;

    /// <summary>The profile a troop walks with when its data names none. Never null.</summary>
    public TroopMotionProfileSO DefaultProfile
    {
        get
        {
            if (defaultProfile != null) return defaultProfile;
            if (_fallbackProfile == null)
            {
                _fallbackProfile = CreateInstance<TroopMotionProfileSO>();
                _fallbackProfile.hideFlags = HideFlags.DontSave;
            }
            return _fallbackProfile;
        }
    }

    public TroopMotionProfileSO ProfileFor(EnemyDataSO data)
        => data != null && data.MotionProfile != null ? data.MotionProfile : DefaultProfile;
}
