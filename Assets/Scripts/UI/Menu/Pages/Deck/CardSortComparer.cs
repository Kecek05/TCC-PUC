using System;

/// <summary>
/// Pure ordering rules for the Card Collection grid. Ties always fall back to the card name, so the grid
/// order is deterministic across sessions no matter which key is selected.
/// </summary>
public static class CardSortComparer
{
    /// <summary>Ascending means A-Z, Common-first, cheapest-first, Tower-first. Descending reverses it.</summary>
    public static int Compare(CardDataSO a, CardDataSO b, CardSortKey key, bool ascending)
    {
        if (ReferenceEquals(a, b)) return 0;

        // Missing entries sort last in both directions.
        if (a == null) return 1;
        if (b == null) return -1;

        int result = CompareAscending(a, b, key);
        if (result == 0) result = CompareByName(a, b);

        return ascending ? result : -result;
    }

    public static Comparison<CardDataSO> GetComparison(CardSortKey key, bool ascending)
        => (a, b) => Compare(a, b, key, ascending);

    private static int CompareAscending(CardDataSO a, CardDataSO b, CardSortKey key) => key switch
    {
        CardSortKey.Name => CompareByName(a, b),
        CardSortKey.Rarity => a.Rarity.CompareTo(b.Rarity),
        CardSortKey.Cost => a.Cost.CompareTo(b.Cost),
        CardSortKey.Type => a.ExistingType.CompareTo(b.ExistingType),
        _ => 0
    };

    private static int CompareByName(CardDataSO a, CardDataSO b)
        => string.Compare(a.CardName, b.CardName, StringComparison.OrdinalIgnoreCase);
}
