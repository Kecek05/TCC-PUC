using UnityEngine;

public class EnemyDataHolder : MonoBehaviour
{
    [SerializeField] private EnemyDataSO enemyData;

    public EnemyDataSO EnemyData => enemyData;
}