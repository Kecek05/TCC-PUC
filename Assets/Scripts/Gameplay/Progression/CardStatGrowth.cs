using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// How one stat grows per card level, compounding from the value already authored in the gameplay SO:
/// <c>multiplier = (1 + PercentPerLevel) ^ (level - 1)</c>. A card only needs entries for the stats it
/// actually wants to scale; anything absent stays at its authored value.
/// </summary>
[Serializable]
public struct CardStatGrowth
{
    [TableColumnWidth(140, Resizable = false)]
    public CardStatId Stat;

    [PropertyRange(-0.2f, 0.5f)]
    [Tooltip("Compounding fraction added per level. 0.1 = +10% per level. Negative shrinks the stat.")]
    public float PercentPerLevel;

    public CardStatGrowth(CardStatId stat, float percentPerLevel)
    {
        Stat = stat;
        PercentPerLevel = percentPerLevel;
    }
}

/// <summary>
/// One displayable stat of a card at a given level. Produced by <c>CardDataSO.GetStats</c> and consumed by
/// both the inspector preview and (later) the Clash Royale-style current-vs-next stat panel, so the two can
/// never disagree about what a card's numbers are.
/// </summary>
public readonly struct CardStatValue
{
    public readonly CardStatId Id;
    public readonly string Label;
    public readonly float Value;
    public readonly string Format;

    public CardStatValue(CardStatId id, string label, float value, string format = "0.##")
    {
        Id = id;
        Label = label;
        Value = value;
        Format = format;
    }

    public string Display => Value.ToString(string.IsNullOrEmpty(Format) ? "0.##" : Format);

    public override string ToString() => $"{Label} {Display}";
}
