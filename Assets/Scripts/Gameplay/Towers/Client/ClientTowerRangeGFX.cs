using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Single scene-level tower range indicator (one instance, not one per tower — mirrors GhostTowerCard).
/// Reuses one range-ring GFX for every tower: tapping a tower scales the ring in (DOTween) over that
/// tower's current range, tapping a different tower retargets it, and re-tapping the selected tower (or
/// empty ground) hides it instantly. Fed world taps by <c>TowerSelectionInput</c>, and resolves the
/// tapped tower from the client-side <see cref="ClientTowerRegistry"/>. Purely cosmetic and client-only.
/// </summary>
public class ClientTowerRangeGFX : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Transform rangeGfx;

    [Title("Selection")]
    [Tooltip("World-space radius around a tower centre that counts as a tap on that tower.")]
    [SerializeField] private float selectRadius = 0.5f;

    [Title("Animation")]
    [Tooltip("Seconds for the range ring to scale in when a tower is selected.")]
    [SerializeField] private float showDuration = 0.25f;
    [Tooltip("Easing for the scale-in. Hiding is instant (no tween).")]
    [SerializeField] private Ease showEase = Ease.OutBack;

    private TowerManager _currentTower;
    private bool _levelSubscribed;
    private Tween _scaleTween;

    private static ClientTowerRangeGFX _instance;

    private void Awake()
    {
        _instance = this;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        KillTween();
        UnsubscribeLevel();
    }

    /// <summary>
    /// Hides the range readout from anywhere — e.g. when the player grabs a card or interacts with UI,
    /// so the selection ring doesn't linger over the field during an unrelated action.
    /// </summary>
    public static void HideSelection()
    {
        if (_instance != null) _instance.Hide();
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

    /// <summary>Positions the shared ring over <paramref name="tower"/> and scales it in.</summary>
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
        SetVisible(true);
        AnimateRange(fromZero: true);
    }

    public void Hide()
    {
        // Hiding is instant — no tween, per design; just stop any in-flight scale-in.
        KillTween();
        UnsubscribeLevel();
        _currentTower = null;
        SetVisible(false);
    }

    // DOTween scale-in to the tower's diameter (2 x range). fromZero pops it in from nothing on select;
    // a live upgrade grows from the current size instead. Mirrors GhostTowerCard's localScale = range*2.
    private void AnimateRange(bool fromZero)
    {
        if (rangeGfx == null) return;

        KillTween();
        if (fromZero) rangeGfx.localScale = Vector3.zero;
        _scaleTween = rangeGfx.DOScale(CurrentRange() * 2f, showDuration).SetEase(showEase);
    }

    private float CurrentRange()
    {
        if (_currentTower == null) return 0f;

        int level = 1;
        BaseServerTowerCombat combat = _currentTower.ServerTowerCombat;
        if (combat != null) level = Mathf.Clamp(combat.TowerLevel.Value, 1, _currentTower.Data.MaxLevel);

        return _currentTower.Data.GetRangeByLevel(level);
    }

    private void KillTween()
    {
        if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Kill();
        _scaleTween = null;
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

    // Range grows on upgrade: tween the live ring to the new level's range while it's shown.
    private void OnLevelChanged(int previousValue, int newValue)
    {
        if (_currentTower != null) transform.position = _currentTower.transform.position;
        AnimateRange(fromZero: false);
    }
}
