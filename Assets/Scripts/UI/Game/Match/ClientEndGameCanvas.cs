using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[Serializable]
public class PlayerEndGameCanvasData
{
    [SerializeField] private TextMeshProUGUI playerUsername;
    [SerializeField] private TextMeshProUGUI playerHealth;
    [SerializeField] private TextMeshProUGUI playerWave;

    public void ChangePlayerHealthText(float newHealth)
    {
        playerHealth.text = $"Health: {newHealth}";
    }

    public void ChangePlayerWaveText(int newWave)
    {
        playerWave.text = $"Wave: {newWave}";
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

    private BaseServerEndGameManager _endGameManager;
    private BaseTeamManager _teamManager;

    private void Awake()
    {
        rootCanvas.SetActive(false);
    }

    private void Start()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _endGameManager = ServiceLocator.Get<BaseServerEndGameManager>();
        
        _endGameManager.OnGameEnded += EndGameManager_OnGameEnded;
    }

    private void OnDestroy()
    {
        if (_endGameManager != null)
            _endGameManager.OnGameEnded -= EndGameManager_OnGameEnded;
    }
    
    private void EndGameManager_OnGameEnded(EndGameSnapshot endgameSnapshot)
    {
        SetupEndGameUI(endgameSnapshot);

        rootCanvas.SetActive(true);
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
            
            enemyPlayerData.ChangePlayerHealthText(endgameSnapshot.RedPlayer.Health);
            enemyPlayerData.ChangePlayerWaveText(endgameSnapshot.RedPlayer.Wave);
        }
        else
        {
            localPlayerData.ChangePlayerHealthText(endgameSnapshot.RedPlayer.Health);
            localPlayerData.ChangePlayerWaveText(endgameSnapshot.RedPlayer.Wave);
            
            enemyPlayerData.ChangePlayerHealthText(endgameSnapshot.BluePlayer.Health);
            enemyPlayerData.ChangePlayerWaveText(endgameSnapshot.BluePlayer.Wave);
        }
    }
}
