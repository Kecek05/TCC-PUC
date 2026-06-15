using System.Collections.Generic;

/// <summary>
/// Client-side counterpart to <see cref="TowerRegistry"/>: the set of towers visible on this client.
/// Towers self-register from <c>BaseClientTowerCombat</c> on spawn/despawn. Used by
/// <see cref="ClientTowerRangeGFX"/> to resolve which tower a tap landed on (no colliders needed).
/// </summary>
public static class ClientTowerRegistry
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