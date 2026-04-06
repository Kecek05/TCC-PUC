using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "TowerDataListSO", menuName = "Scriptable Objects/TowerDataListSO")]
public class TowerDataListSO : ScriptableObject
{
    public List<TowerDataSO> TowerDataList;
    
    public TowerDataSO GetTowerDataByType(TowerType towerType)
    {
        return TowerDataList.Find(towerData => towerData.TowerType == towerType);
    }
}

public enum TowerType
{
    None,
    Circle,
    Square
}