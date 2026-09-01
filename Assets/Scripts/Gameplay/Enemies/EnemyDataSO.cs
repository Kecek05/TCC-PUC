using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Scriptable Objects/Data/EnemyData")]
public class EnemyDataSO : ScriptableObject
{
    [Title("Enemy Properties")]
    public EnemyType EnemyType;
    public string EnemyName;
    public float MaxHealth = 100f;
    public float MoveSpeed = 2f;
    [Tooltip("Duration of spawn invincibility (frozen + untargetable).")]
    public float SpawnDuration = 1f;
    public float Damage = 10f;

    [Title("Armor")]
    [Tooltip("Armor type. Towers of a different color deal reduced damage; same color deals full damage.")]
    public ArmorColor ArmorColor = ArmorColor.None;
    [PropertyRange(0f, 1f)]
    [Tooltip("Damage reduction applied to hits whose color does not match this armor. 0 = none, 1 = immune to off-color.")]
    public float OffColorResistance = 0.35f;

    [Title("Modifiers")]
    [Tooltip("If true, this enemy ignores every stackable slow (Prism aura, Rift zone, and future slows that route through ServerEnemyMovement.AddSlow). Set for Shadow-style enemies whose whole point is punishing slow-based defenses.")]
    public bool ImmuneToSlow = false;

    [Title("Dash")]
    [Min(0f)]
    [Tooltip("Seconds between dashes. 0 disables dash entirely — the enemy never dashes.")]
    public float DashInterval = 0f;
    [Min(0f)]
    [Tooltip("How long each dash lasts (seconds).")]
    public float DashDuration = 0f;
    [Min(1f)]
    [Tooltip("Speed multiplier applied to the enemy's effective speed while dashing. 1 = no boost, 5 = 5x speed.")]
    public float DashSpeedMultiplier = 1f;

    [Title("Visuals")]
    public Sprite EnemySprite;

    [Title("References")]
    public GameObject EnemyPrefab;
}