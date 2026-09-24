using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Tuning for how enemies react to being hit, killed and leaking. One asset shared by every enemy prefab.
/// </summary>
[CreateAssetMenu(fileName = "HitFeedbackSettings", menuName = "Scriptable Objects/Presentation/Hit Feedback Settings")]
public class HitFeedbackSettingsSO : ScriptableObject
{
    [Title("Hit Flash")]
    [Tooltip("The body's tint for an instant on each hit. Coloured rather than white on purpose: the art is " +
             "authored white and tinted by its armour, so a white flash would not show on unarmoured enemies.")]
    public Color FlashColor = new(1f, 0.42f, 0.42f, 1f);
    [Min(0f)] public float FlashDuration = 0.09f;

    [Title("Heavy Hit")]
    [PropertyRange(0f, 1f)]
    [Tooltip("A hit worth at least this fraction of the enemy's max health counts as heavy.")]
    public float HeavyHitFraction = 0.2f;
    [Min(0f)]
    [Tooltip("How long the enemy's own visual holds still on a heavy hit. Per enemy, never Time.timeScale: " +
             "on the host that would slow the simulation for both players.")]
    public float HitStopSeconds = 0.06f;

    [Title("Damage Numbers")]
    public bool ShowDamageNumbers = true;
    public Vector3 NumberOffset = new(0f, 0.35f, 0f);
    public Gradient NumberColor;
    public Gradient HeavyNumberColor;
    [Min(0f)] public float HeavyNumberIntensity = 1.5f;

    [Title("Effects")]
    public PooledVfxSO SpawnPuff;
    public PooledVfxSO DeathBurst;
    public PooledVfxSO LeakBurst;
}
