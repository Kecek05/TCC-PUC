using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dresses enemies in the statuses spells leave on them (Spell Juice, "Worn, not painted"): wings for Rage, echoes for
/// Rift, a cracked shield for Ferrugem. Nothing about these crosses the network: each client applies the server's
/// own rule to the zones it is already drawing (<see cref="SpellZoneRegistry"/>).
/// </summary>
/// <remarks>
/// The rules mirror the executors exactly, since an inferred status that disagrees with the game is worse than none:
/// <list type="bullet">
/// <item>Rage (RageExecutor): troops attacking the field it landed on (team neither the caster's nor None), while
/// inside and for LingerDuration after leaving or after the zone ends.</item>
/// <item>Rift (RiftExecutor): enemies walking at the caster, not ImmuneToSlow; once touched, until the zone ends,
/// even after they walk out.</item>
/// <item>Ferrugem (FerrugemExecutor): the same touch-until-the-end rule, drawn only on armoured enemies: on the
/// others there is no armour to break and nothing changes.</item>
/// </list>
/// Created on demand by the first zone and bound to the scene. Scans at 10 Hz and only while a status zone exists;
/// the views animate by hand in LateUpdate, no tween per unit.
/// </remarks>
public class SpellStatusPresenter : MonoBehaviour
{
    private const float ScanInterval = 0.1f;

    private static SpellStatusPresenter _instance;

    private readonly Dictionary<EnemyManager, UnitStatusView> _views = new();
    private readonly Stack<UnitStatusView> _free = new();
    private readonly List<EnemyManager> _released = new();
    private EnemyManager[] _enemies = Array.Empty<EnemyManager>();
    private float _nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    public static void EnsureRunning()
    {
        if (_instance != null || !Application.isPlaying) return;

        GameObject go = new GameObject("[Juice] SpellStatus");
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        _instance = go.AddComponent<SpellStatusPresenter>();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        float now = Time.time;
        if (now < _nextScan) return;
        _nextScan = now + ScanInterval;

        IReadOnlyList<SpellZone> zones = SpellZoneRegistry.Zones;
        bool anyStatusZone = false;
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            SpellZone zone = zones[i];
            if (zone.IsSpent(now)) { SpellZoneRegistry.Remove(zone); continue; }
            if (IsStatusSpell(zone.Spell)) anyStatusZone = true;
        }

        if (!anyStatusZone && _views.Count == 0) return;

        foreach (UnitStatusView view in _views.Values) view.Evaluated = false;

        // A joining client has no server registry, so enemies are found the way the spell aim finds them.
        _enemies = anyStatusZone ? FindObjectsByType<EnemyManager>(FindObjectsSortMode.None) : Array.Empty<EnemyManager>();

        foreach (EnemyManager enemy in _enemies)
        {
            if (!IsAlive(enemy)) continue;
            Evaluate(enemy, zones, now);
        }

        // Views whose enemy left (killed, leaked, recycled) go at once; the rest that nothing covered this time
        // take their statuses off, and are handed back once those have finished leaving.
        _released.Clear();
        foreach (KeyValuePair<EnemyManager, UnitStatusView> pair in _views)
        {
            if (!IsAlive(pair.Key))
            {
                pair.Value.Clear();
                _released.Add(pair.Key);
                continue;
            }

            if (!pair.Value.Evaluated) pair.Value.SetStatuses(null, null, null);
            if (pair.Value.IsIdle) _released.Add(pair.Key);
        }

        foreach (EnemyManager enemy in _released)
        {
            if (!_views.TryGetValue(enemy, out UnitStatusView view)) continue;
            _views.Remove(enemy);
            view.Unbind();
            _free.Push(view);
        }
    }

    private void Evaluate(EnemyManager enemy, IReadOnlyList<SpellZone> zones, float now)
    {
        Vector3 position = enemy.transform.position;
        TeamType team = enemy.Team != null ? enemy.Team.GetTeamType() : TeamType.None;
        ulong id = enemy.NetworkObject.NetworkObjectId;
        EnemyDataSO data = enemy.Data;

        SpellZone rage = null, rift = null, rust = null;

        for (int i = 0; i < zones.Count; i++)
        {
            SpellZone zone = zones[i];
            switch (zone.Spell)
            {
                case SpellType.Rage:
                    if (team == TeamType.None || team == zone.CasterTeam) break;
                    if (zone.IsAlive(now) && zone.Covers(position)) zone.Units[id] = now + zone.Linger;
                    if (zone.Units.TryGetValue(id, out float until) && now < until) rage ??= zone;
                    break;

                case SpellType.Rift:
                    if (team != zone.CasterTeam || data == null || data.ImmuneToSlow) break;
                    if (zone.IsAlive(now) && zone.Covers(position)) zone.Units[id] = 1f;
                    if (zone.IsAlive(now) && zone.Units.ContainsKey(id)) rift ??= zone;
                    break;

                case SpellType.Ferrugem:
                    if (team != zone.CasterTeam || data == null || data.ArmorColor == ArmorColor.None) break;
                    if (zone.IsAlive(now) && zone.Covers(position)) zone.Units[id] = 1f;
                    if (zone.IsAlive(now) && zone.Units.ContainsKey(id)) rust ??= zone;
                    break;
            }
        }

        bool any = rage != null || rift != null || rust != null;
        if (!_views.TryGetValue(enemy, out UnitStatusView view))
        {
            if (!any) return;
            view = _free.Count > 0 ? _free.Pop() : CreateView();
            view.Bind(enemy);
            _views[enemy] = view;
        }

        view.SetStatuses(rage, rift, rust);
    }

    private static bool IsAlive(EnemyManager enemy) =>
        enemy != null && enemy.isActiveAndEnabled && enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned;

    private UnitStatusView CreateView()
    {
        GameObject go = new GameObject("UnitStatus");
        go.transform.SetParent(transform, false);
        return go.AddComponent<UnitStatusView>();
    }

    private static bool IsStatusSpell(SpellType spell) =>
        spell == SpellType.Rage || spell == SpellType.Rift || spell == SpellType.Ferrugem;
}
