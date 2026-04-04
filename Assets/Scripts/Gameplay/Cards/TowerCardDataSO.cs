using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "TowerCardData", menuName = "Scriptable Objects/Cards/TowerCardData")]
public class TowerCardDataSO : CardDataSO
{
    [Title("Tower Data")]
    [SerializeField] private TowerType towerType;
    [SerializeField] private GameObject towerPrefab;

    public TowerType TowerType => towerType;
    
    public GameObject TowerPrefab => towerPrefab;
}