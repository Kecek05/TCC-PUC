using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveData", menuName = "Scriptable Objects/WaveData")]
public class WaveDataSO : ScriptableObject
{
    [Title("Wave Sequence")]
    [SerializeField] private List<WaveEntry> waves = new();

    [Title("Timing")]
    [Tooltip("Seconds before the first wave starts")]
    [SerializeField] private float initialDelay = 5f;

    [Tooltip("Seconds between waves (after all enemies in a wave have spawned)")]
    [SerializeField] private float delayBetweenWaves = 10f;

    public IReadOnlyList<WaveEntry> Waves => waves;
    public float InitialDelay => initialDelay;
    public float DelayBetweenWaves => delayBetweenWaves;
    
    public List<GameObject> GetAllEnemyPrefabs()
    {
        List<GameObject> prefabs = new();
        foreach (var wave in waves)
        {
            if (!prefabs.Contains(wave.enemyData.EnemyPrefab))
                prefabs.Add(wave.enemyData.EnemyPrefab);
        }
        return prefabs;
    }
}

[Serializable]
public struct WaveEntry
{
    [Tooltip("Which enemy to spawn (references EnemyDataSO for prefab + stats)")]
    public EnemyDataSO enemyData;

    [Tooltip("How many enemies in this wave")]
    public int count;

    [Tooltip("Seconds between each enemy spawn within this wave")]
    public float spawnInterval;
}
