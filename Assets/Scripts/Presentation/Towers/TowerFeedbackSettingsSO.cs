using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Tuning for tower placement, upgrade, recoil and payout feedback. One asset shared by every tower prefab.
/// </summary>
[CreateAssetMenu(fileName = "TowerFeedbackSettings", menuName = "Scriptable Objects/Presentation/Tower Feedback Settings")]
public class TowerFeedbackSettingsSO : ScriptableObject
{
    [Title("Effects")]
    [Tooltip("Dust ring where a tower lands.")]
    public PooledVfxSO PlacedDust;
    [Tooltip("Sparkle when a tower levels up.")]
    public PooledVfxSO UpgradeSparkle;

    [Title("Recoil")]
    [Tooltip("Velocity kicked into the art's position spring, away from the target, on every shot.")]
    [Min(0f)] public float RecoilKick = 2.5f;

    [Title("Mana Payout")]
    [Tooltip("Where a Fonte's \"+N\" rises from, relative to the tower.")]
    public Vector3 ManaPopupOffset = new(0f, 0.45f, 0f);
    public Gradient ManaPopupColor;
    [Min(0f)] public float ManaPopupIntensity = 1.2f;
}
