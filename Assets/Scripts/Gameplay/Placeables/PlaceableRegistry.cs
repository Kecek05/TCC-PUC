using System.Collections.Generic;

/// <summary>
/// Shared registry of placement spots, keyed by team.
/// Placeables self-register via AbstractPlaceable.Awake using their parent TeamIdentifier.
/// Consumers (bot AI) query GetByTeam / GetFreeByTeam to pick placement candidates.
/// </summary>
public static class PlaceableRegistry
{
    private static readonly Dictionary<TeamType, List<IPlaceable>> _placeablesByTeam = new();

    public static void Register(IPlaceable placeable, TeamType team)
    {
        if (placeable == null) return;

        if (!_placeablesByTeam.TryGetValue(team, out List<IPlaceable> list))
        {
            list = new List<IPlaceable>();
            _placeablesByTeam[team] = list;
        }

        if (!list.Contains(placeable))
            list.Add(placeable);
    }

    public static void Unregister(IPlaceable placeable, TeamType team)
    {
        if (_placeablesByTeam.TryGetValue(team, out List<IPlaceable> list))
            list.Remove(placeable);
    }

    public static IReadOnlyList<IPlaceable> GetByTeam(TeamType team)
    {
        return _placeablesByTeam.TryGetValue(team, out List<IPlaceable> list)
            ? list
            : System.Array.Empty<IPlaceable>();
    }

    public static IEnumerable<IPlaceable> GetFreeByTeam(TeamType team)
    {
        IReadOnlyList<IPlaceable> list = GetByTeam(team);
        for (int i = 0; i < list.Count; i++)
        {
            IPlaceable p = list[i];
            if (p != null && !p.Occupied)
                yield return p;
        }
    }
}
