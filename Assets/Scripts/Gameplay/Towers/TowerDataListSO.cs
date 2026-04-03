using System.Collections.Generic;
using UnityEngine;

// [CreateAssetMenu(fileName = "TowerDataListSO", menuName = "Scriptable Objects/TowerDataListSO")]
public class TowerDataListSO : ScriptableObject
{
    [SerializeField] private List<TowerDataSO> towerDataList;
    
    public TowerDataSO GetTowerDataByType(TowerType towerType)
    {
        return towerDataList.Find(towerData => towerData.TowerType == towerType);
    }
}

public enum TowerType
{
    None,
    Circle,
    Square
}