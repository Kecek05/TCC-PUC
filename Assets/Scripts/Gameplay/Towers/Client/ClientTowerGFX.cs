using System;
using MoreMountains.Feedbacks;
using UnityEngine;

[Serializable]
public struct TowerFeedback
{
    public int level;
    public MMF_Player Feedback;
}

public class ClientTowerGFX : MonoBehaviour
{
    [SerializeField] private TowerFeedback[] towerFeedbacks;
    [SerializeField] private MMF_Player shootFeedback;

    [Header("Status tints (placeholder)")]
    [SerializeField] private SpriteRenderer level1Renderer;
    [SerializeField] private Color frozenColor = Color.blue;
    [SerializeField] private Color hastedColor = Color.yellow;
    [SerializeField] private Color normalColor = Color.white;

    private bool _frozen;
    private bool _hasted;

    public void UpgradeTower(int newLevel)
    {
        StopAllFeedbacks();
        foreach (TowerFeedback towerFeedback in towerFeedbacks)
        {
            if (towerFeedback.level == newLevel)
            {
                towerFeedback.Feedback?.PlayFeedbacks();
                break;
            }
        }
    }

    public void FireBulletFeedback()
    {
        if (HasAnyFeedbackPlaying()) return;

        shootFeedback?.PlayFeedbacks();
    }

    /// <summary>
    /// Placeholder status visual: tints the Level 1 tower GFX blue while frozen, yellow while hasted
    /// (freeze takes priority when both apply), white otherwise.
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        _frozen = frozen;
        RefreshStatusTint();
    }

    public void SetHasted(bool hasted)
    {
        _hasted = hasted;
        RefreshStatusTint();
    }

    private void RefreshStatusTint()
    {
        if (level1Renderer == null) return;
        level1Renderer.color = _frozen ? frozenColor : (_hasted ? hastedColor : normalColor);
    }

    private bool HasAnyFeedbackPlaying()
    {
        foreach (TowerFeedback towerFeedback in towerFeedbacks)
        {
            if (towerFeedback.Feedback != null && towerFeedback.Feedback.IsPlaying)
                return true;
        }
        return false;
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
        if (shootFeedback != null && shootFeedback.IsPlaying)
        {
            shootFeedback.StopFeedbacks();
            shootFeedback.RestoreInitialValues();
        }
    }
}
