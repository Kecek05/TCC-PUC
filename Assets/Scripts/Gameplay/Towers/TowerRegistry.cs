using System.Collections.Generic;

/// <summary>
/// Shared registry of all active (spawned) towers on the server.
/// Towers self-register via BaseServerTowerCombat.OnNetworkSpawn (server only).
/// Consumers (bot AI, analytics) query ActiveTowers / GetTowersByTeam for decisions.
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

    public static IEnumerable<TowerManager> GetTowersByTeam(TeamType team)
    {
        for (int i = 0; i < _activeTowers.Count; i++)
        {
            TowerManager t = _activeTowers[i];
            if (t == null) continue;
            if (t.Team != null && t.Team.GetTeamType() == team)
                yield return t;
        }
    }
}
