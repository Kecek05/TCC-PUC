using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Single scene-level tower range indicator (one instance, not one per tower — mirrors GhostTowerCard).
/// Reuses one range-ring GFX for every tower: tapping a tower moves and rescales the ring to that tower's
/// current range, tapping a different tower retargets it, and re-tapping the selected tower (or empty
/// ground) hides it. Fed world
/// taps by <c>TowerSelectionInput</c>, and resolves the tapped tower from the client-side
/// <see cref="ClientTowerRegistry"/>. Purely cosmetic and client-only.
/// </summary>
public class ClientTowerRangeGFX : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Transform rangeGfx;

    [Title("Selection")]
    [Tooltip("World-space radius around a tower centre that counts as a tap on that tower.")]
    [SerializeField] private float selectRadius = 0.5f;

    private TowerManager _currentTower;
    private bool _levelSubscribed;

    private void Awake()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        UnsubscribeLevel();
    }

    private void Update()
    {
        // The selected tower was destroyed while its ring was up — drop the now-stale ring.
        if (_currentTower == null && rangeGfx != null && rangeGfx.gameObject.activeSelf)
            Hide();
    }

    /// <summary>
    /// Selects the tower whose tap-radius contains <paramref name="worldPoint"/> (nearest centre wins),
    /// or hides the ring when the tap lands on no tower.
    /// </summary>
    public void HandleWorldTap(Vector2 worldPoint)
    {
        TowerManager best = null;
        float bestSqr = selectRadius * selectRadius;

        IReadOnlyList<TowerManager> towers = ClientTowerRegistry.ActiveTowers;
        for (int i = towers.Count - 1; i >= 0; i--)
        {
            TowerManager tower = towers[i];
            if (tower == null) continue;

            float sqr = ((Vector2)tower.transform.position - worldPoint).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = tower;
            }
        }

        // Tapping empty ground — or re-tapping the tower whose ring is already up — hides it (toggle).
        if (best == null || best == _currentTower) Hide();
        else ShowForTower(best);
    }

    /// <summary>Positions and sizes the shared ring over <paramref name="tower"/> and shows it.</summary>
    public void ShowForTower(TowerManager tower)
    {
        if (tower == null)
        {
            Hide();
            return;
        }

        if (tower != _currentTower)
        {
            UnsubscribeLevel();
            _currentTower = tower;
            SubscribeLevel();
        }

        transform.position = tower.transform.position;
        RefreshRange();
        SetVisible(true);
    }

    public void Hide()
    {
        UnsubscribeLevel();
        _currentTower = null;
        SetVisible(false);
    }

    private void RefreshRange()
    {
        if (_currentTower == null) return;

        int level = 1;
        BaseServerTowerCombat combat = _currentTower.ServerTowerCombat;
        if (combat != null) level = Mathf.Clamp(combat.TowerLevel.Value, 1, _currentTower.Data.MaxLevel);

        SetRange(_currentTower.Data.GetRangeByLevel(level));
    }

    // Mirrors GhostTowerCard.SetRange so the selection ring and the placement ghost read identically.
    private void SetRange(float range)
    {
        if (rangeGfx != null) rangeGfx.localScale = Vector3.one * (range * 2f);
    }

    private void SetVisible(bool visible)
    {
        if (rangeGfx != null) rangeGfx.gameObject.SetActive(visible);
    }

    private void SubscribeLevel()
    {
        if (_levelSubscribed || _currentTower == null) return;

        BaseServerTowerCombat combat = _currentTower.ServerTowerCombat;
        if (combat == null) return;

        combat.TowerLevel.OnValueChanged += OnLevelChanged;
        _levelSubscribed = true;
    }

    private void UnsubscribeLevel()
    {
        if (!_levelSubscribed) return;

        if (_currentTower != null && _currentTower.ServerTowerCombat != null)
            _currentTower.ServerTowerCombat.TowerLevel.OnValueChanged -= OnLevelChanged;

        _levelSubscribed = false;
    }

    // Range grows on upgrade: keep the live ring matched to the tower's current level while it's shown.
    private void OnLevelChanged(int previousValue, int newValue)
    {
        if (_currentTower != null) transform.position = _currentTower.transform.position;
        RefreshRange();
    }
}