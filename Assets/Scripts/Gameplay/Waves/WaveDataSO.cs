using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveData", menuName = "Scriptable Objects/Data/WaveData")]
public class WaveDataSO : ScriptableObject
{
    [Title("Timing")]
    [InfoBox("Counts, spawn intervals and inter-wave delays are min/max ranges. At match start the SERVER rolls " +
             "each range ONCE and both players receive the identical resolved sequence, so the match is always " +
             "fair. Set Min == Max anywhere you want a fixed value.")]
    [Tooltip("Seconds before the first wave starts")]
    [Unit(Units.Second)]
    [MinValue(0)]
    public float InitialDelay = 5f;

    [Tooltip("Seconds between waves (after all enemies in a wave are dead). Rolled per wave-transition at " +
             "runtime within this range; both players always get the same roll.")]
    [ValidateInput("@$value.max >= $value.min", "Delay: Max must be ≥ Min.")]
    public FloatRange DelayBetweenWaves = new(10f, 10f);

    [Title("Wave Sequence")]
    [ListDrawerSettings(DraggableItems = false, ShowIndexLabels = true, ShowPaging = false)]
    [ValidateInput("@$value != null && $value.Count > 0", "Add at least one wave.")]
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

    /// <summary>
    /// Rolls every authored min/max range once into a single concrete plan. Called only on the server; the
    /// returned plan is shared by both teams, so every player faces the exact same enemies, counts, spawn
    /// intervals and inter-wave delays. Pass a seeded <see cref="System.Random"/> for reproducible runs.
    /// </summary>
    public List<ResolvedWave> ResolveWaves(System.Random rng)
    {
        List<ResolvedWave> resolved = new(Waves.Count);

        for (int waveIndex = 0; waveIndex < Waves.Count; waveIndex++)
        {
            WaveEntry entry = Waves[waveIndex];

            ResolvedWaveEnemy[] enemies = new ResolvedWaveEnemy[entry.waveEnemies.Length];
            for (int e = 0; e < entry.waveEnemies.Length; e++)
            {
                WaveEnemy waveEnemy = entry.waveEnemies[e];
                enemies[e] = new ResolvedWaveEnemy(
                    waveEnemy.enemyData,
                    waveEnemy.count.Resolve(rng),
                    waveEnemy.spawnInterval.Resolve(rng));
            }

            // The first wave is preceded by InitialDelay, not by a between-waves delay.
            float delayBeforeWave = waveIndex == 0 ? 0f : DelayBetweenWaves.Resolve(rng);

            resolved.Add(new ResolvedWave(enemies, delayBeforeWave));
        }

        return resolved;
    }
}

[Serializable]
public struct WaveEnemy
{
    [Tooltip("Which enemy to spawn")]
    [Required]
    public EnemyDataSO enemyData;

    [Tooltip("How many enemies to spawn (inclusive min/max). Rolled once on the server and shared by both players.")]
    [ValidateInput("@$value.min >= 1 && $value.max >= $value.min", "Enemy count: Min must be ≥ 1 and Max ≥ Min.")]
    public IntRange count;

    [Tooltip("Seconds between each spawn of THIS enemy within the wave (inclusive min/max). Rolled once on the " +
             "server and shared by both players.")]
    [ValidateInput("@$value.max >= $value.min", "Spawn interval: Max must be ≥ Min.")]
    public FloatRange spawnInterval;
}

[Serializable]
public struct WaveEntry
{
    [ValidateInput("@$value != null && $value.Length > 0", "A wave must contain at least one enemy entry.")]
    public WaveEnemy[] waveEnemies;
}

/// <summary>
/// Inclusive integer range that resolves to a single value at runtime. Values are floored at 0; usage sites add
/// any stricter rule (e.g. enemy count requires Min ≥ 1).
/// </summary>
[Serializable]
[InlineProperty]
public struct IntRange
{
    [HorizontalGroup, LabelText("Min"), LabelWidth(30), MinValue(0)] public int min;
    [HorizontalGroup, LabelText("Max"), LabelWidth(30), MinValue(0)] public int max;

    public IntRange(int min, int max)
    {
        this.min = min;
        this.max = max;
    }

    /// <summary>Random value in [min, max] (both ends inclusive). Defensive against an inverted range.</summary>
    public int Resolve(System.Random rng)
    {
        int lo = Mathf.Min(min, max);
        int hi = Mathf.Max(min, max);
        return rng.Next(lo, hi + 1);
    }
}

/// <summary>Inclusive float range that resolves to a single value at runtime. Values are floored at 0.</summary>
[Serializable]
[InlineProperty]
public struct FloatRange
{
    [HorizontalGroup, LabelText("Min"), LabelWidth(30), MinValue(0)] public float min;
    [HorizontalGroup, LabelText("Max"), LabelWidth(30), MinValue(0)] public float max;

    public FloatRange(float min, float max)
    {
        this.min = min;
        this.max = max;
    }

    /// <summary>Random value in [min, max]. Defensive against an inverted range.</summary>
    public float Resolve(System.Random rng)
    {
        float lo = Mathf.Min(min, max);
        float hi = Mathf.Max(min, max);
        return Mathf.Lerp(lo, hi, (float)rng.NextDouble());
    }
}

/// <summary>
/// A single enemy line in a wave after its count and spawn-interval ranges have been rolled. Runtime-only
/// (server), never serialized.
/// </summary>
public readonly struct ResolvedWaveEnemy
{
    public readonly EnemyDataSO EnemyData;
    public readonly int Count;
    public readonly float SpawnInterval;

    public ResolvedWaveEnemy(EnemyDataSO enemyData, int count, float spawnInterval)
    {
        EnemyData = enemyData;
        Count = count;
        SpawnInterval = spawnInterval;
    }
}

/// <summary>
/// A wave after all its ranges (per-enemy counts and spawn intervals, plus the pre-wave delay) have been rolled.
/// Runtime-only (server), shared by both teams so the two maps spawn identical waves.
/// </summary>
public class ResolvedWave
{
    public readonly ResolvedWaveEnemy[] Enemies;
    public readonly float DelayBeforeWave;

    public ResolvedWave(ResolvedWaveEnemy[] enemies, float delayBeforeWave)
    {
        Enemies = enemies;
        DelayBeforeWave = delayBeforeWave;
    }

    public int GetTotalEnemiesCount()
    {
        int total = 0;
        foreach (ResolvedWaveEnemy enemy in Enemies)
            total += enemy.Count;
        return total;
    }
}
