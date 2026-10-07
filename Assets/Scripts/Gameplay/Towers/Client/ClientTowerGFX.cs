using System;
using System.Linq;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public struct TowerFeedback
{
    public int level;
    public MMF_Player Feedback;
}

public class ClientTowerGFX : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private BaseClientTowerCombat clientTowerCombat;
    [SerializeField] private TowerFeedback[] towerFeedbacks;
    [SerializeField] private MMF_Player shootFeedback;

    [Title("Status tints (placeholder)")]
    [SerializeField] private SpriteRenderer level1Renderer;
    [SerializeField] private Color frozenColor = Color.blue;
    [SerializeField] private Color hastedColor = Color.yellow;

    [Title("Spawn")]
    [Tooltip("Drop the tower onto its spot and squash it on landing (Juice: dur-fall, then land-x/land-y settling " +
             "on ease-settle) instead of the authored level-1 scale-in. Readable from its first frame, ~0.6s.")]
    [SerializeField] private bool dropOnSpawn = true;
    [Tooltip("How far above its spot, in world units, the tower starts falling.")]
    [SerializeField, Min(0f)] private float dropHeight = 1.4f;

    private bool _frozen;
    private bool _hasted;
    private Sequence _drop;

    /// <summary>The spawn drop touched down: the moment for dust, the camera kick and the thud.</summary>
    public event Action OnSpawnLanded;

    /// <summary>Whether this tower arrives by the drop (and so announces its landing) rather than the authored scale-in.</summary>
    public bool DropsOnSpawn => dropOnSpawn;

    /// <summary>The tower's own colour (its card tint), whatever status is tinting it right now.</summary>
    public Color Tint => _authoredColor;

    /// <summary>The level-1 art: what the tower's size and sorting are read from.</summary>
    public SpriteRenderer ArtRenderer => level1Renderer;

    /// <summary>
    /// What "no status" looks like: the tint the prefab was authored with, read off the renderer rather
    /// than declared beside it. This used to be a serialized normalColor defaulting to white, which made
    /// the tower's colour two facts instead of one — and the two drifted the moment the towers were given
    /// their own art, so the first freeze or haste repainted a coloured tower and it never came back.
    /// Mortar was the worst of it: authored pink from when it borrowed Square's sprite, so an orange
    /// tower turned pink rather than merely white.
    /// </summary>
    private Color _authoredColor = Color.white;

    /// <summary>True while a spawn or upgrade animation is still playing out.</summary>
    public bool IsPlayingLevelFeedback => HasAnyFeedbackPlaying() || (_drop != null && _drop.IsActive());

    // Subscribe in Awake, not Start: BaseClientTowerCombat replays the initial tower level (and frozen/haste
    // state) inside OnNetworkSpawn, which runs AFTER Awake but BEFORE Start. Subscribing in Start would miss
    // that initial replay, so the level-1 spawn fade-in never fires.
    private void Awake()
    {
        // Before any subscription: BaseClientTowerCombat replays the initial frozen/haste state in
        // OnNetworkSpawn, which runs after Awake, so the authored tint has to be captured by then.
        if (level1Renderer != null) _authoredColor = level1Renderer.color;

        clientTowerCombat.OnBulletFired  +=  FireBulletFeedback;
        clientTowerCombat.OnFrozenChanged += SetFrozen;
        clientTowerCombat.OnHasteChanged += SetHasted;
        clientTowerCombat.OnTowerLevelChanged += UpgradeTower;
    }

    private void OnDestroy()
    {
        _drop?.Kill();
        clientTowerCombat.OnBulletFired  -=  FireBulletFeedback;
        clientTowerCombat.OnFrozenChanged -= SetFrozen;
        clientTowerCombat.OnHasteChanged -= SetHasted;
        clientTowerCombat.OnTowerLevelChanged -= UpgradeTower;
    }

    private void UpgradeTower(int newLevel)
    {
        StopAllFeedbacks();
        _drop?.Kill();

        if (newLevel == 1 && dropOnSpawn)
        {
            PlayDrop();
            return;
        }

        foreach (TowerFeedback towerFeedback in towerFeedbacks)
        {
            if (towerFeedback.level != newLevel) continue;
            
            towerFeedback.Feedback?.PlayFeedbacks();
            break;
        }
    }

    /// <summary>
    /// TowerPlacement's arrival: the art falls the last stretch onto its spot, stretched (dur-fall, ease-fall),
    /// lands squashed (land-x by land-y) and settles on ease-settle. Scaled time, like the authored animation it
    /// replaces, so the tutorial's settle still measures it through <see cref="IsPlayingLevelFeedback"/>.
    /// </summary>
    private void PlayDrop()
    {
        JuiceTokensSO t = Juice.Tokens;
        Transform art = transform;
        Vector3 rest = Vector3.zero;
        Vector3 squash = Juice.ReduceMotion ? Vector3.one : new Vector3(t.Scales.LandX, t.Scales.LandY, 1f);

        if (level1Renderer != null) level1Renderer.transform.localScale = Vector3.one;

        // Set before the sequence starts, so its tweens read these as their starting point.
        art.localPosition = rest + Vector3.up * dropHeight;
        art.localScale = new Vector3(0.86f, 1.16f, 1f);

        _drop = DOTween.Sequence().SetLink(gameObject).SetTarget(this)
            .Append(art.DOLocalMove(rest, t.Durations.Fall).SetEase(t.Eases.Fall))
            .Join(art.DOScale(new Vector3(0.9f, 1.12f, 1f), t.Durations.Fall).SetEase(t.Eases.Fall))
            .AppendCallback(() => OnSpawnLanded?.Invoke())
            .Append(DOVirtual.Float(0f, 1f, t.Durations.Settle, k => art.localScale = Vector3.LerpUnclamped(squash, Vector3.one, k))
                .SetEase(t.Eases.Settle, t.Eases.SettleAmplitude, t.Eases.SettlePeriod))
            .OnKill(() =>
            {
                if (art == null) return;
                art.localPosition = rest;
                art.localScale = Vector3.one;
                RecaptureFeedbackRests();
            });
    }

    /// <summary>
    /// Feel's players record their target's pose as its rest when they initialize, in their Start, and that runs
    /// after OnNetworkSpawn has already put the art in the drop's stretched start pose. Left with it, the shot's
    /// squash spring (and the level-ups' additive scale) settle back to that pose, and the tower stays squeezed
    /// narrow and tall for the rest of the match. So they record it again once the art is really at rest.
    /// </summary>
    private void RecaptureFeedbackRests()
    {
        if (shootFeedback != null && !shootFeedback.IsPlaying) shootFeedback.Initialization();

        foreach (TowerFeedback towerFeedback in towerFeedbacks)
            if (towerFeedback.Feedback != null && !towerFeedback.Feedback.IsPlaying)
                towerFeedback.Feedback.Initialization();
    }

    public void FireBulletFeedback()
    {
        if (HasAnyFeedbackPlaying() || (_drop != null && _drop.IsActive())) return;

        shootFeedback?.PlayFeedbacks();
    }

    /// <summary>
    /// Placeholder status visual: tints the Level 1 tower GFX blue while frozen, yellow while hasted
    /// (freeze takes priority when both apply), and back to the tower's own armour colour otherwise.
    /// </summary>
    private void SetFrozen(bool frozen)
    {
        _frozen = frozen;
        RefreshStatusTint();
    }

    private void SetHasted(bool hasted)
    {
        _hasted = hasted;
        RefreshStatusTint();
    }

    private void RefreshStatusTint()
    {
        if (level1Renderer == null) return;

        Color target = _frozen ? frozenColor : (_hasted ? hastedColor : _authoredColor);
        level1Renderer.DOKill();

        // A status arriving is something happening to the tower; it fades in over dur-pop rather than snapping.
        // The first replay (OnNetworkSpawn, before the tower is even visible) is set outright.
        if (!isActiveAndEnabled || !_tintPrimed)
        {
            _tintPrimed = true;
            level1Renderer.color = target;
            return;
        }

        level1Renderer.DOColor(target, Juice.Tokens.Durations.Pop).SetEase(Juice.Tokens.Eases.Out).SetLink(gameObject);
    }

    private bool _tintPrimed;

    private bool HasAnyFeedbackPlaying()
    {
        return towerFeedbacks.Any(towerFeedback => towerFeedback.Feedback != null && towerFeedback.Feedback.IsPlaying);
    }

    private void StopAllFeedbacks()
    {
        foreach (TowerFeedback towerFeedback in towerFeedbacks)
        {
            if (towerFeedback.Feedback != null && towerFeedback.Feedback.IsPlaying)
            {
                towerFeedback.Feedback.StopFeedbacks();
                towerFeedback.Feedback.RestoreInitialValues();
            }
        }

        if (shootFeedback == null || !shootFeedback.IsPlaying) return;
        
        shootFeedback.StopFeedbacks();
        shootFeedback.RestoreInitialValues();
    }
}
