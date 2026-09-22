using System;
using System.Linq;
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

    private bool _frozen;
    private bool _hasted;

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
    public bool IsPlayingLevelFeedback => HasAnyFeedbackPlaying();

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
        clientTowerCombat.OnBulletFired  -=  FireBulletFeedback;
        clientTowerCombat.OnFrozenChanged -= SetFrozen;
        clientTowerCombat.OnHasteChanged -= SetHasted;
        clientTowerCombat.OnTowerLevelChanged -= UpgradeTower;
    }

    private void UpgradeTower(int newLevel)
    {
        StopAllFeedbacks();
        foreach (TowerFeedback towerFeedback in towerFeedbacks)
        {
            if (towerFeedback.level != newLevel) continue;
            
            towerFeedback.Feedback?.PlayFeedbacks();
            break;
        }
    }

    public void FireBulletFeedback()
    {
        if (HasAnyFeedbackPlaying()) return;

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
        level1Renderer.color = _frozen ? frozenColor : (_hasted ? hastedColor : _authoredColor);
    }

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
