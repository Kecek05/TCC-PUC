using System.Collections.Generic;

/// <summary>
/// Shared registry of all active (spawned) enemies on the server.
/// Enemies self-register via ServerEnemyHealth.OnNetworkSpawn.
/// Consumers (towers, spells) query ActiveEnemies for targeting.
/// </summary>
public static class EnemyRegistry
{
    private static readonly List<EnemyManager> _activeEnemies = new();

    public static IReadOnlyList<EnemyManager> ActiveEnemies => _activeEnemies;

    public static void Register(EnemyManager enemy)
    {
        if (!_activeEnemies.Contains(enemy))
            _activeEnemies.Add(enemy);
    }

    public static void Unregister(EnemyManager enemy)
    {
        _activeEnemies.Remove(enemy);
    }

    public static void Cleanup()
    {
        _activeEnemies.RemoveAll(e => e == null);
    }
}
