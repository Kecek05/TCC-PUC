using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "EnemyDataListSO", menuName = "Scriptable Objects/EnemyDataListSO")]
public class EnemyDataListSO : ScriptableObject
{
    [SerializeField] private List<EnemyDataSO> enemyDataList;
    
    public EnemyDataSO GetEnemyDataByType(EnemyType enemyType)
    {
        return enemyDataList.Find(enemyData => enemyData.EnemyType == enemyType);
    }
}

public enum EnemyType
{
    None,
    Triangle1,
    Triangle2
}