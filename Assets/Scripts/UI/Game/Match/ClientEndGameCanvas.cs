using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class PlayerEndGameCanvasData
{
    [SerializeField] private TextMeshProUGUI playerUsername;
    [SerializeField] private TextMeshProUGUI playerHealth;
    [SerializeField] private TextMeshProUGUI playerWave;
    [SerializeField] private Image waveProgress;

    public void ChangePlayerHealthText(float newHealth)
    {
        playerHealth.text = $"{newHealth}";
    }

    public void ChangePlayerWaveText(int newWave)
    {
        playerWave.text = $"{newWave}";
    }

    public void ChangeWaveProgress(float newProgress)
    {
        waveProgress.fillAmount = newProgress;
    }
}

public class ClientEndGameCanvas : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject rootCanvas;
    [SerializeField] private PlayerEndGameCanvasData localPlayerData;
    [SerializeField] private PlayerEndGameCanvasData enemyPlayerData;
    [SerializeField] private GameObject victoryLabel;
    [SerializeField] private GameObject defeatLabel;
    [SerializeField] private Button okButton;
    [SerializeField] private Button playAgainButton;

    [Title("Reward")]
    [InfoBox("One RewardEntryUI is instantiated into the area per line of the payout — gold is one tile, the " +
             "card another. Optional: with no area or prefab assigned the end screen simply shows no tiles.")]
    [SerializeField] private Transform rewardsArea;
    [SerializeField] private RewardEntryUI rewardEntryPrefab;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private CardDataListSO cardDataListSO;

    private BaseServerEndGameManager _endGameManager;
    private BaseRewardService _rewardService;
    private BaseTeamManager _teamManager;
    private BaseClientManager _clientManager;

    private void Awake()
    {
        rootCanvas.SetActive(false);

        okButton.onClick.AddListener(OnOkButtonClicked);
        playAgainButton.onClick.AddListener(OnPlayAgainButtonClicked);
    }

    private void Start()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _endGameManager = ServiceLocator.Get<BaseServerEndGameManager>();
        _clientManager = ServiceLocator.Get<BaseClientManager>();

        _endGameManager.OnGameEnded += EndGameManager_OnGameEnded;

        // Display only, and deliberately hung off the reward service rather than the end-game Rpc: the
        // service raises after the save is written, so the panel can never show a payout that failed to bank.
        // Absent in debug scenes that have no ClientManager, hence TryGet.
        if (ServiceLocator.TryGet(out _rewardService))
            _rewardService.OnRewardGranted += RewardService_OnRewardGranted;
    }

    private void OnDestroy()
    {
        if (_endGameManager != null) _endGameManager.OnGameEnded -= EndGameManager_OnGameEnded;
        if (_rewardService != null) _rewardService.OnRewardGranted -= RewardService_OnRewardGranted;

        okButton.onClick.RemoveListener(OnOkButtonClicked);
        playAgainButton.onClick.RemoveListener(OnPlayAgainButtonClicked);
    }

    private void EndGameManager_OnGameEnded(EndGameSnapshot endgameSnapshot)
    {
        SetupEndGameUI(endgameSnapshot);

        rootCanvas.SetActive(true);
    }

    /// <summary>
    /// The reward is banked on its own targeted Rpc, which can land before or after the snapshot, so this
    /// only touches the reward widgets and never the win/lose layout.
    /// </summary>
    private void RewardService_OnRewardGranted(Reward reward)
    {
        if (rewardsArea == null || rewardEntryPrefab == null) return;

        // A grant is one whole payout, so rebuild rather than append: a second grant (the debug commands do
        // exactly this) must replace the tiles, not stack a second row on top of them.
        ClearRewardsArea();

        if (reward.Gold > 0)
            Instantiate(rewardEntryPrefab, rewardsArea).SetGold(reward.Gold, coinSprite);

        if (!reward.HasCard) return;

        CardDataSO cardData = cardDataListSO != null ? cardDataListSO.GetCardDataByType(reward.Card) : null;
        Instantiate(rewardEntryPrefab, rewardsArea).SetCard(cardData, reward.Copies);
    }

    private void ClearRewardsArea()
    {
        for (int i = rewardsArea.childCount - 1; i >= 0; i--)
            Destroy(rewardsArea.GetChild(i).gameObject);
    }

    private void SetupEndGameUI(EndGameSnapshot endgameSnapshot)
    {
        SetupLabelsData(endgameSnapshot);

        bool localWon = endgameSnapshot.WinnerTeam == _teamManager.GetLocalTeam();
        victoryLabel.SetActive(localWon);
        defeatLabel.SetActive(!localWon);
    }

    private void SetupLabelsData(EndGameSnapshot endgameSnapshot)
    {
        //TODO: Setup Usernames

        TeamType localTeam = _teamManager.GetLocalTeam();

        if (localTeam == TeamType.None)
        {
            GameLog.Warn("ClientEndGameCanvas: Local team is None. Cannot setup end game UI data.");
            return;
        }

        if (localTeam == TeamType.Blue)
        {
            localPlayerData.ChangePlayerHealthText(endgameSnapshot.BluePlayer.Health);
            localPlayerData.ChangePlayerWaveText(endgameSnapshot.BluePlayer.Wave);
            localPlayerData.ChangeWaveProgress(endgameSnapshot.BluePlayer.WaveProgress);

            enemyPlayerData.ChangePlayerHealthText(endgameSnapshot.RedPlayer.Health);
            enemyPlayerData.ChangePlayerWaveText(endgameSnapshot.RedPlayer.Wave);
            enemyPlayerData.ChangeWaveProgress(endgameSnapshot.RedPlayer.WaveProgress);
        }
        else
        {
            localPlayerData.ChangePlayerHealthText(endgameSnapshot.RedPlayer.Health);
            localPlayerData.ChangePlayerWaveText(endgameSnapshot.RedPlayer.Wave);
            localPlayerData.ChangeWaveProgress(endgameSnapshot.RedPlayer.WaveProgress);

            enemyPlayerData.ChangePlayerHealthText(endgameSnapshot.BluePlayer.Health);
            enemyPlayerData.ChangePlayerWaveText(endgameSnapshot.BluePlayer.Wave);
            enemyPlayerData.ChangeWaveProgress(endgameSnapshot.BluePlayer.WaveProgress);
        }
    }

    private void OnOkButtonClicked() => LeaveMatch();

    // Play Again behaves like OK: tear down the match and return to the menu,
    // where the player re-hosts or re-joins (no matchmaking for a 1-click rematch).
    private void OnPlayAgainButtonClicked() => LeaveMatch();

    private async void LeaveMatch()
    {
        // Guard against double-press while the async teardown is running.
        okButton.interactable = false;
        playAgainButton.interactable = false;

        await _clientManager.LeaveMatchAsync();
    }
}
