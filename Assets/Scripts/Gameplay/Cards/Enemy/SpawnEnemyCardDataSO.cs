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

    [Title("Decoys")]
    [Tooltip("Optional second troop sent alongside the real one, shuffled into the same column so the " +
             "defender cannot tell which body is which. None disables it. This is what makes a Miragem: " +
             "one real unit and a couple of look-alikes that evaporate to the first hit they take.")]
    public EnemyType DecoyEnemyType = EnemyType.None;

    [Min(0)]
    [Tooltip("How many decoys accompany the real troops.")]
    public int DecoyCount = 0;

    /// <summary>
    /// The full column this card sends, real troops and decoys shuffled together. Built here rather than in
    /// the deployer so the card owns what it summons, and shuffled because a fixed order would let the
    /// defender learn which position is the real one and read straight through the bluff.
    /// </summary>
    public List<EnemyType> BuildSpawnOrder(System.Random rng)
    {
        List<EnemyType> order = new();

        int real = Mathf.Max(1, SpawnCount);
        for (int i = 0; i < real; i++) order.Add(EnemyType);

        if (DecoyEnemyType != EnemyType.None)
            for (int i = 0; i < Mathf.Max(0, DecoyCount); i++) order.Add(DecoyEnemyType);

        // Nothing to hide when there are no decoys - keep the authored order so an army still strings out
        // exactly the way it was written.
        if (DecoyEnemyType == EnemyType.None || DecoyCount <= 0) return order;

        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        return order;
    }

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
