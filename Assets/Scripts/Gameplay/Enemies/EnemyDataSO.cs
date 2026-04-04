using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Scriptable Objects/EnemyData")]
public class EnemyDataSO : ScriptableObject
{
    [Title("Enemy Properties")]
    [SerializeField] private EnemyType enemyType;
    [SerializeField] private string enemyName;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float moveSpeed = 2f;

    [Title("Visuals")]
    [SerializeField] private Sprite enemySprite;

    [Title("References")]
    [SerializeField] private GameObject enemyPrefab;

    public EnemyType EnemyType => enemyType;
    public string EnemyName => enemyName;
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public Sprite EnemySprite => enemySprite;
    public GameObject EnemyPrefab => enemyPrefab;
}