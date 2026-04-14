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
    
    private void Start()
    {
        UpgradeTower(1);
    }

    public void UpgradeTower(int newLevel)
    {
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
        shootFeedback?.PlayFeedbacks();
    }
}
