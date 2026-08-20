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
    [InfoBox("Optional. Unassigned fields are skipped, so the end screen keeps working before the reward " +
             "art exists.")]
    [SerializeField] private GameObject rewardRoot;
    [SerializeField] private TextMeshProUGUI rewardGoldLabel;
    [SerializeField] private GameObject rewardCardRoot;
    [SerializeField] private Image rewardCardIcon;
    [SerializeField] private TextMeshProUGUI rewardCardLabel;
    [SerializeField] private CardDataListSO cardDataListSO;

    private BaseServerEndGameManager _endGameManager;
    private BaseTeamManager _teamManager;
    private BaseClientManager _clientManager;

    private void Awake()
    {
        rootCanvas.SetActive(false);
        if (rewardRoot != null) rewardRoot.SetActive(false);

        okButton.onClick.AddListener(OnOkButtonClicked);
        playAgainButton.onClick.AddListener(OnPlayAgainButtonClicked);
    }

    private void Start()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _endGameManager = ServiceLocator.Get<BaseServerEndGameManager>();
        _clientManager = ServiceLocator.Get<BaseClientManager>();

        _endGameManager.OnGameEnded += EndGameManager_OnGameEnded;

        // Display only. ClientRewardHandler is what actually banks the reward into the save.
        _endGameManager.OnRewardGranted += EndGameManager_OnRewardGranted;
    }

    private void OnDestroy()
    {
        if (_endGameManager != null)
        {
            _endGameManager.OnGameEnded -= EndGameManager_OnGameEnded;
            _endGameManager.OnRewardGranted -= EndGameManager_OnRewardGranted;
        }

        okButton.onClick.RemoveListener(OnOkButtonClicked);
        playAgainButton.onClick.RemoveListener(OnPlayAgainButtonClicked);
    }

    private void EndGameManager_OnGameEnded(EndGameSnapshot endgameSnapshot)
    {
        SetupEndGameUI(endgameSnapshot);

        rootCanvas.SetActive(true);
    }

    /// <summary>
    /// The reward arrives on its own targeted Rpc, which can land before or after the snapshot, so this
    /// only touches the reward widgets and never the win/lose layout.
    /// </summary>
    private void EndGameManager_OnRewardGranted(MatchReward reward)
    {
        if (rewardRoot != null) rewardRoot.SetActive(true);
        if (rewardGoldLabel != null) rewardGoldLabel.text = $"+{reward.Gold}";

        if (rewardCardRoot != null) rewardCardRoot.SetActive(reward.HasCard);
        if (!reward.HasCard) return;

        CardDataSO cardData = cardDataListSO != null ? cardDataListSO.GetCardDataByType(reward.Card) : null;

        if (rewardCardIcon != null && cardData != null)
        {
            rewardCardIcon.sprite = cardData.CardImage;
            rewardCardIcon.color = cardData.CardColor;
        }

        if (rewardCardLabel != null)
            rewardCardLabel.text = cardData != null ? $"+{reward.Copies} {cardData.CardName}" : $"+{reward.Copies}";
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
