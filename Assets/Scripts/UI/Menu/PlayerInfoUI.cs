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
    private BasePlayerSaveManager _playerSaveManager;

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();

        // Gold is real now and changes while the menu is open (upgrades, match rewards), so this panel
        // has to react rather than read once. It lives on the persistent header, visible from every page.
        if (ServiceLocator.TryGet(out _playerSaveManager))
            _playerSaveManager.OnGoldChanged += HandleGoldChanged;

        UpdatePlayerInfo();
    }

    private void OnDestroy()
    {
        if (_playerSaveManager != null) _playerSaveManager.OnGoldChanged -= HandleGoldChanged;
    }

    private void HandleGoldChanged() => UpdateCurrentMoneyLabel(_playerSaveManager.Gold);

    private void UpdatePlayerInfo()
    {
        UpdateCurrentMoneyLabel(_playerSaveManager != null ? _playerSaveManager.Gold : 0);
        UpdatePlayerNameLabel(_clientManager.UserData.PlayerName);

        //PLACE HOLDERS - account level, xp, gems and trophies are still stubs.
        UpdateCurrentGemsLabel(Random.Range(0, 1000));
        UpdateCurrentLevelLabel(Random.Range(0, 100));
        UpdateCurrentXpLabel(Random.Range(0, 100), 100);
        UpdateCurrentTrophiesLabel(Random.Range(0, 10000));
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
