using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "EnemyDataListSO", menuName = "Scriptable Objects/EnemyDataListSO")]
public class EnemyDataListSO : ScriptableObject
{
    public List<EnemyDataSO> EnemyDataList;
    
    public EnemyDataSO GetEnemyDataByType(EnemyType enemyType)
    {
        return EnemyDataList.Find(enemyData => enemyData.EnemyType == enemyType);
    }
}

public enum EnemyType
{
    None,
    Triangle1,
    Triangle2
}