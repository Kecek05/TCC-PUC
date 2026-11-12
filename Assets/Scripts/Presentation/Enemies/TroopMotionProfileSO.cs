using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// How one family of troops walks: the route it takes inside the lane and the body language on top of it.
/// Purely presentation: nothing here reaches the server, a range check or the wire.
/// </summary>
/// <remarks>
/// The default profile hangs off <see cref="TroopMotionSettingsSO.DefaultProfile"/>; a troop type that should
/// walk differently points <see cref="EnemyDataSO.MotionProfile"/> at its own asset. Anything that must read as
/// the same unit at a glance (Miragem and its decoy) has to share one profile, for the same reason they share a
/// MoveSpeed: a decoy that walks differently is a bluff the defender can read.
/// </remarks>
[CreateAssetMenu(fileName = "TroopMotionProfile", menuName = "Scriptable Objects/Juice/Troop Motion Profile")]
public class TroopMotionProfileSO : ScriptableObject
{
    [Title("Route", "Where in the lane this troop walks. Distances in world units; the lane is one 0.75 cell wide.")]
    [Tooltip("Hard limit on how far the body may sit from the lane centreline, all terms together (offset, sway, " +
             "steering). 0.15 keeps a standard 0.37 body fully inside the lane.")]
    [Min(0f)] public float MaxLateral = 0.15f;
    [Tooltip("Each troop picks a fixed lateral offset within +/- this, from its seed.")]
    [Min(0f)] public float OffsetRange = 0.09f;
    [Tooltip("Amplitude of the slow Perlin sway around that offset.")]
    [Min(0f)] public float SwayAmplitude = 0.06f;
    [Tooltip("Sway cycles per world unit walked. Distance-based, so a troop held in place does not drift.")]
    [Min(0f)] public float SwayFrequency = 0.55f;

    [Title("Gait", "A step cycle driven by distance walked: slowed troops step slower, raged ones faster.")]
    [Tooltip("World units per step at normal speed. Each troop varies it by StrideJitter.")]
    [Min(0.02f)] public float StrideLength = 0.32f;
    [PropertyRange(0f, 0.5f)] public float StrideJitter = 0.12f;
    [Tooltip("Zero-mean surge along the lane within each step: the cosmetic speed variance, as a share of the " +
             "troop's speed (0.06 = +/-6% over a step). Never changes when the troop arrives, only how it gets " +
             "there. Keep it small: much past 0.1 the walk reads as stutter, not as a gait.")]
    [PropertyRange(0f, 0.3f)] public float SurgeShare = 0.06f;
    [Tooltip("Uniform scale added at the top of each step: the hop, read top-down as rising toward the camera.")]
    [Min(0f)] public float HopScale = 0.07f;
    [Tooltip("Stretch along the travel direction mid-step (squash on landing is the mirror).")]
    [Min(0f)] public float StepStretch = 0.10f;
    [Tooltip("Side-to-side tilt per step, in degrees.")]
    [Min(0f)] public float WaddleAngle = 7f;
    [Tooltip("Extra stretch per unit of speed above the troop's base speed: dashes and Rage read as a lunge.")]
    [Min(0f)] public float SpeedStretch = 0.12f;

    [Title("Turning")]
    [Tooltip("How fast the body swings round to a new heading (Hz). Lower reads heavier.")]
    [Min(0.5f)] public float TurnFrequency = 4f;
    [Tooltip("Spring damping. Below 1 the body overshoots a turn slightly, which reads as weight.")]
    [PropertyRange(0.2f, 1.2f)] public float TurnDamping = 0.6f;
    [Tooltip("Degrees of lean into a turn per degree-per-second of turn rate.")]
    [Min(0f)] public float LeanPerTurnRate = 0.045f;
    [Min(0f)] public float MaxLean = 14f;

    [Title("Spawn and idle")]
    [Min(0.05f)] public float SpawnPopDuration = 0.35f;
    [Tooltip("easeOutBack overshoot of the pop. 0 = none, 1.7 = the classic back ease.")]
    [Min(0f)] public float SpawnPopOvershoot = 1.9f;
    [Tooltip("Breathing scale while standing still (spawn-in, held, frozen match).")]
    [Min(0f)] public float IdleBreath = 0.03f;
    [Min(0f)] public float IdleBreathRate = 0.9f;

    [Title("Dust")]
    [Tooltip("Chance a landing kicks up a puff. The global budget in TroopMotionSettingsSO caps the total.")]
    [PropertyRange(0f, 1f)] public float DustChance = 0.25f;
    [Min(0f)] public float DustSize = 0.16f;
}
