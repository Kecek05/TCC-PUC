using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Scriptable Objects/EnemyData")]
public class EnemyDataSO : ScriptableObject
{
    [Title("Enemy Properties")]
    public EnemyType EnemyType;
    public string EnemyName;
    public float MaxHealth = 100f;
    public float MoveSpeed = 2f;
    [Tooltip("Duration of spawn invincibility (frozen + untargetable).")]
    public float SpawnDuration = 1f;

    [Title("Visuals")]
    public Sprite EnemySprite;

    [Title("References")]
    public GameObject EnemyPrefab;
}