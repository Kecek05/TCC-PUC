using System.Collections.Generic;

/// <summary>
/// Shared registry of all active (spawned) towers on the server.
/// Towers self-register via BaseServerTowerCombat.OnNetworkSpawn / OnNetworkDespawn.
/// Consumers (spells) query ActiveTowers for area effects (e.g. the freeze spell).
/// </summary>
public static class TowerRegistry
{
    private static readonly List<TowerManager> _activeTowers = new();

    public static IReadOnlyList<TowerManager> ActiveTowers => _activeTowers;

    public static void Register(TowerManager tower)
    {
        if (tower != null && !_activeTowers.Contains(tower))
            _activeTowers.Add(tower);
    }

    public static void Unregister(TowerManager tower)
    {
        _activeTowers.Remove(tower);
    }

    public static void Cleanup()
    {
        _activeTowers.RemoveAll(t => t == null);
    }
}
