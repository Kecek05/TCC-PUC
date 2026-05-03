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
                towerFeedback.Feedback.StopFeedbacks();
        }
        if (shootFeedback != null && shootFeedback.IsPlaying)
            shootFeedback.StopFeedbacks();
    }
}
