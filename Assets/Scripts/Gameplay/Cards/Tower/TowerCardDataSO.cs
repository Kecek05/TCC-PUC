using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "TowerCardData", menuName = "Scriptable Objects/Cards/TowerCardData")]
public class TowerCardDataSO : CardDataSO
{
    [Title("Tower Data")]
    public TowerType TowerType;
    [Tooltip("Sprite that will be used in the GhostTowerCard")] 
    public Sprite TowerGhostSprite;
    public GameObject TowerPrefab;
    
}