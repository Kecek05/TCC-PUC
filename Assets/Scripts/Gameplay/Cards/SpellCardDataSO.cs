using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellCardDataSO", menuName = "Scriptable Objects/Cards/SpellCardDataSO")]
public class SpellCardDataSO : CardDataSO
{
    [Title("Tower Data")]
    public TowerType TowerType;
    [Tooltip("Sprite that will be used in the GhostTowerCard")] 
    public Sprite TowerGhostSprite;
    public GameObject TowerPrefab;
}
