using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveData", menuName = "Scriptable Objects/Data/WaveData")]
public class WaveDataSO : ScriptableObject
{
    [Title("Timing")]
    [Tooltip("Seconds before the first wave starts")]
    [Unit(Units.Second)] 
    public float InitialDelay = 5f;

    [Tooltip("Seconds between waves (after all enemies in a wave are dead)")]
    [Unit(Units.Second)] 
    public float DelayBetweenWaves = 10f;
    
    [Title("Wave Sequence")] [ListDrawerSettings(DraggableItems = false, ShowIndexLabels = true, ShowPaging = false)]
    public List<WaveEntry> Waves = new();
    
    public List<GameObject> GetAllEnemyPrefabs()
    {
        List<GameObject> prefabs = new();
        foreach (WaveEntry wave in Waves)
        {
            foreach (WaveEnemy waveEnemy in wave.waveEnemies)
            {
                if (!prefabs.Contains(waveEnemy.enemyData.EnemyPrefab))
                    prefabs.Add(waveEnemy.enemyData.EnemyPrefab);
            }
        }
        return prefabs;
    }
}

[Serializable]
public struct WaveEnemy
{
    [Tooltip("Which enemy to spawn")]
    public EnemyDataSO enemyData;

    [Tooltip("How many enemies")]
    public int count;
}

[Serializable]
public struct WaveEntry
{
    public WaveEnemy[] waveEnemies;

    [Tooltip("Seconds between each enemy spawn within this wave"), Unit(Units.Second)]
    public float spawnInterval;

    public int GetTotalEnemiesCount()
    {
        int enemiesCount = 0;
        foreach (WaveEnemy waveEnemy in waveEnemies)
        {
            enemiesCount += waveEnemy.count;
        }
        return enemiesCount;
    }
}
