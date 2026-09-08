using System;

/// <summary>
/// One stat of a card as the player sees it in the info panel: its value at the level they own, paired
/// with what one more level would make it. Produced by
/// <see cref="CardProgressionSettingsSO.GetStatProgress"/> so the panel never has to know how growth
/// compounds — it only prints what it is handed.
/// </summary>
/// <remarks>
/// <see cref="Next"/> equals <see cref="Current"/> at max level and for any stat the growth table leaves
/// out, which is exactly the case the UI must not print an upgrade for: Range and Move Speed sit at 0% in
/// the default table, so an always-on "+0" would appear on nearly every card and read as a bug rather
/// than as a design.
/// </remarks>
public readonly struct CardStatProgress
{
    public readonly CardStatValue Current;
    public readonly CardStatValue Next;

    /// <summary>False at max level: there is no next level to preview.</summary>
    public readonly bool HasNextLevel;

    public CardStatProgress(CardStatValue current, CardStatValue next, bool hasNextLevel)
    {
        Current = current;
        Next = next;
        HasNextLevel = hasNextLevel;
    }

    public CardStatId Id => Current.Id;
    public string Label => Current.Label;
    public string CurrentDisplay => Current.Display;

    /// <summary>The true change, unrounded. <see cref="UpgradeDisplay"/> shows the visible one instead.</summary>
    public float Delta => Next.Value - Current.Value;

    /// <summary>True when the next level moves the number the player can actually see.</summary>
    public bool HasUpgrade => HasNextLevel && DisplayDelta != 0f;

    /// <summary>
    /// The change one level buys, in the stat's own format ("+120", "+1.5%"). Empty when there is nothing
    /// to show. Hit Speed is a cooldown, so its improvement is legitimately negative.
    /// </summary>
    public string UpgradeDisplay
    {
        get
        {
            float delta = DisplayDelta;
            if (!HasNextLevel || delta == 0f) return string.Empty;

            // ToString already writes the minus sign, so only a gain needs one added.
            return delta > 0f ? "+" + delta.ToString(Format) : delta.ToString(Format);
        }
    }

    /// <summary>
    /// The gap between the two values <b>as they are printed</b>, not between the raw floats. Tourniquet's
    /// 0.25s hit speed grows by 0.0048s a level: the raw delta formats as "-0.00" at two decimals, and one
    /// level in three it moves the printed value anyway (0.25 -> 0.24). Rounding both ends the way the
    /// label will round them is what keeps the row self-consistent — the number shown plus the change
    /// shown always equals the number shown next level, and a change too small to print is exactly 0 here.
    /// </summary>
    private float DisplayDelta
    {
        get
        {
            string format = Format;

            // A percent format prints value * 100, so it has to round in those units too.
            double units = format.IndexOf('%') >= 0 ? 100d : 1d;
            double step = Math.Pow(10d, DecimalPlaces(format));

            double current = Math.Round(Current.Value * units * step, MidpointRounding.AwayFromZero);
            double next = Math.Round(Next.Value * units * step, MidpointRounding.AwayFromZero);

            return (float)((next - current) / step / units);
        }
    }

    private string Format => string.IsNullOrEmpty(Current.Format) ? "0.##" : Current.Format;

    /// <summary>How many decimals a custom numeric format prints: the '0' and '#' run after its dot.</summary>
    private static int DecimalPlaces(string format)
    {
        int dot = format.IndexOf('.');
        if (dot < 0) return 0;

        int places = 0;
        for (int i = dot + 1; i < format.Length; i++)
        {
            if (format[i] != '0' && format[i] != '#') break;
            places++;
        }

        return places;
    }

    public override string ToString() => $"{Label} {CurrentDisplay} {UpgradeDisplay}".TrimEnd();
}
