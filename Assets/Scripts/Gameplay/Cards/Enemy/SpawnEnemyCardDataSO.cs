using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnEnemyCardData", menuName = "Scriptable Objects/Cards/SpawnEnemyCardData")]
public class SpawnEnemyCardDataSO : CardDataSO
{
    [Title("Spawn Data")]
    public EnemyType EnemyType;

    [Min(1)]
    [Tooltip("How many enemies this card summons.")]
    public int SpawnCount = 1;

    [Min(0f)]
    [Tooltip("Seconds of delay between each spawned enemy, so they string out in a row down the lane. Customizable per card.")]
    public float DelayBetweenSpawns = 0.3f;
}
