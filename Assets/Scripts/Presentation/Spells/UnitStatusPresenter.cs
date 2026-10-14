using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dresses enemies in whatever they are under (Spell Juice, "Worn, not painted"): wings for a speed buff, echoes for
/// a slow, a cracked shield for broken armour, a shackle for a hold. One look per EFFECT, whoever caused it: a Rift
/// and a Prism both slow, a Ferrugem and a Torniquete both break armour, and an enemy reads the same under either.
/// </summary>
/// <remarks>
/// Driven by what the server replicates about each enemy (<see cref="ServerEnemyMovement.SlowedPercent"/>,
/// <see cref="ServerEnemyMovement.HastedPercent"/>, <see cref="ServerEnemyMovement.Held"/>,
/// <see cref="ServerEnemyHealth.ArmorBrokenPercent"/>), not inferred from the zones a client happens to be drawing.
/// The first version inferred, which cost nothing on the wire but could only ever know about spells: a tower's aura
/// re-scans on its own tick and an Ancora picks a single target, and neither can be guessed from the outside. The
/// replicated values are whole percents written only when they change, so the price is a byte per change.
/// <para/>
/// Enemies announce themselves (<see cref="Track"/> from their spawn hook, <see cref="Untrack"/> on despawn), so
/// nothing is searched for. Polled at 10 Hz rather than subscribed to: four delegates per spawn would be garbage on
/// every enemy of every wave, and a tenth of a second is under the time a status takes to fade in anyway. The views
/// animate by hand in LateUpdate, no tween per unit.
/// </remarks>
public class UnitStatusPresenter : MonoBehaviour
{
    private const float ScanInterval = 0.1f;

    private static UnitStatusPresenter _instance;

    private readonly List<EnemyManager> _tracked = new();
    private readonly Dictionary<EnemyManager, UnitStatusView> _views = new();
    private readonly Stack<UnitStatusView> _free = new();
    private float _nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    /// <summary>An enemy came to life on this client: its statuses are watched from now on.</summary>
    public static void Track(EnemyManager enemy)
    {
        if (enemy == null || !Application.isPlaying) return;
        if (_instance == null)
        {
            GameObject go = new GameObject("[Juice] UnitStatus");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            _instance = go.AddComponent<UnitStatusPresenter>();
        }

        if (!_instance._tracked.Contains(enemy)) _instance._tracked.Add(enemy);
    }

    /// <summary>The enemy left (killed, leaked, pooled): whatever it wore goes at once, with no farewell on an empty spot.</summary>
    public static void Untrack(EnemyManager enemy)
    {
        if (_instance == null || enemy == null) return;
        _instance._tracked.Remove(enemy);
        _instance.Release(enemy, clear: true);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now < _nextScan) return;
        _nextScan = now + ScanInterval;

        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = _tracked[i];
            if (!IsAlive(enemy))
            {
                _tracked.RemoveAt(i);
                Release(enemy, clear: true);
                continue;
            }

            Evaluate(enemy);
        }
    }

    private void Evaluate(EnemyManager enemy)
    {
        ServerEnemyMovement movement = enemy.ServerMovement;
        ServerEnemyHealth health = enemy.ServerHealth;
        EnemyDataSO data = enemy.Data;

        float hasted = movement.HastedPercent.Value / 100f;
        float slowed = movement.SlowedPercent.Value / 100f;
        bool held = movement.Held.Value;
        // Only an armoured enemy wears the broken shield: on the others there is no armour to break.
        float broken = data != null && data.ArmorColor != ArmorColor.None ? health.ArmorBrokenPercent.Value / 100f : 0f;

        bool any = hasted > 0f || slowed > 0f || broken > 0f || held;
        if (!_views.TryGetValue(enemy, out UnitStatusView view))
        {
            if (!any) return;
            view = _free.Count > 0 ? _free.Pop() : CreateView();
            view.Bind(enemy);
            _views[enemy] = view;
        }

        view.SetStatuses(hasted, slowed, broken, held);
        if (!any && view.IsIdle) Release(enemy, clear: false);
    }

    private void Release(EnemyManager enemy, bool clear)
    {
        // A destroyed enemy still finds its view: Unity's null check is not the dictionary's key comparison.
        if (ReferenceEquals(enemy, null) || !_views.TryGetValue(enemy, out UnitStatusView view)) return;
        _views.Remove(enemy);
        if (view == null) return;
        if (clear) view.Clear();
        view.Unbind();
        _free.Push(view);
    }

    private static bool IsAlive(EnemyManager enemy) =>
        enemy != null && enemy.isActiveAndEnabled && enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned;

    private UnitStatusView CreateView()
    {
        GameObject go = new GameObject("UnitStatus");
        go.transform.SetParent(transform, false);
        return go.AddComponent<UnitStatusView>();
    }
}
