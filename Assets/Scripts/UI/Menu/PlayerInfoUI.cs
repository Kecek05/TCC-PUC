using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerInfoUI : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private TextMeshProUGUI currentLevelLabel;
    [SerializeField] private TextMeshProUGUI currentXpLabel;
    [SerializeField] private TextMeshProUGUI currentMoneyLabel;
    [SerializeField] private TextMeshProUGUI currentGemsLabel;
    [SerializeField] private TextMeshProUGUI playerNameLabel;
    [SerializeField] private TextMeshProUGUI currentTrophiesLabel;
    
    private BaseClientManager _clientManager;

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        UpdatePlayerInfo();
    }

    private void UpdatePlayerInfo()
    {
        //PLACE HOLDERS
        UpdateCurrentGemsLabel(Random.Range(0, 1000));
        UpdateCurrentLevelLabel(Random.Range(0, 100));
        UpdateCurrentMoneyLabel(Random.Range(0, 100));
        UpdateCurrentXpLabel(Random.Range(0, 100), 100);
        UpdateCurrentTrophiesLabel(Random.Range(0, 10000));
        UpdatePlayerNameLabel(_clientManager.UserData.PlayerName);
    }

    private void UpdateCurrentLevelLabel(int level)
    {
        currentLevelLabel.text = level.ToString();
    }

    private void UpdateCurrentXpLabel(int xp, int maxXp)
    {
        currentXpLabel.text = $"{xp}/{maxXp}";
    }

    private void UpdateCurrentMoneyLabel(int money)
    {
        currentMoneyLabel.text = $"{money}";
    }

    private void UpdateCurrentGemsLabel(int gems)
    {
        currentGemsLabel.text = $"{gems}";
    }
    
    private void UpdatePlayerNameLabel(string playerName)
    {
        playerNameLabel.text = playerName;
    }

    private void UpdateCurrentTrophiesLabel(int trophies)
    {
        currentTrophiesLabel.text = $"{trophies}";
    }
}
