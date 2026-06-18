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
    public float OffColorResistance = 0.5f;

    [Title("Visuals")]
    public Sprite EnemySprite;

    [Title("References")]
    public GameObject EnemyPrefab;
}