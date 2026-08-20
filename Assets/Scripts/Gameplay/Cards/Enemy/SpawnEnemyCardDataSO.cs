using System;
using System.Collections.Generic;
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

    [Title("Progression Display")]
    [Required]
    [Tooltip("Only used to look up this troop's stats for the level preview and the card info UI. " +
             "The spawn path itself resolves the enemy by EnemyType, so these can never disagree.")]
    public EnemyDataListSO EnemyDataList;

    public override IReadOnlyList<CardStatValue> GetStats(CardLevelScale scale)
    {
        EnemyDataSO data = ResolveEnemyData();
        if (data == null) return Array.Empty<CardStatValue>();

        return new[]
        {
            new CardStatValue(CardStatId.Health, "Health", data.MaxHealth * scale.Health, "0"),
            new CardStatValue(CardStatId.Damage, "Damage", data.Damage * scale.Damage, "0.#"),
            new CardStatValue(CardStatId.MoveSpeed, "Speed", data.MoveSpeed * scale.MoveSpeed, "0.##"),
        };
    }

    public EnemyDataSO ResolveEnemyData() =>
        EnemyDataList != null ? EnemyDataList.GetEnemyDataByType(EnemyType) : null;
}
