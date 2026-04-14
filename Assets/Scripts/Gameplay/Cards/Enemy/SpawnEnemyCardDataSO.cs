using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnEnemyCardData", menuName = "Scriptable Objects/Cards/SpawnEnemyCardData")]
public class SpawnEnemyCardDataSO : CardDataSO
{
    [Title("Spawn Data")]
    public EnemyType EnemyType;
}