using System;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class PlayerEndGameCanvasData
{
    [SerializeField] private TextMeshProUGUI playerUsername;
    [SerializeField] private TextMeshProUGUI playerHealth;
    [SerializeField] private TextMeshProUGUI playerWave;
    [SerializeField] private bool isLocalPlayer;
    
    public TextMeshProUGUI PlayerUsername => playerUsername;
    public TextMeshProUGUI PlayerHealth => playerHealth;
    public TextMeshProUGUI PlayerWave => playerWave;
    public bool IsLocalPlayer => isLocalPlayer;
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
        
        victoryLabel.SetActive(false);
        defeatLabel.SetActive(false);
        
        if (endgameSnapshot.WinnerTeam == _teamManager.GetLocalTeam())
            victoryLabel.SetActive(true);
        else
            defeatLabel.SetActive(true);
    }

    private void SetupLabelsData(EndGameSnapshot endgameSnapshot)
    {
        //TODO: Setup Usernames
        
        TeamType localTeam = _teamManager.GetLocalTeam();

        if (localTeam == TeamType.None)
        {
            Debug.LogWarning("ClientEndGameCanvas: Local team is None. Cannot setup end game UI data.");
            return;
        }
        
        if (localTeam == TeamType.Blue)
        {
            localPlayerData.PlayerHealth.text = endgameSnapshot.BluePlayer.Health.ToString();
            localPlayerData.PlayerWave.text = endgameSnapshot.BluePlayer.Wave.ToString();
            enemyPlayerData.PlayerHealth.text = endgameSnapshot.RedPlayer.Health.ToString();
            enemyPlayerData.PlayerWave.text = endgameSnapshot.RedPlayer.Wave.ToString();
        }
        else
        {
            localPlayerData.PlayerHealth.text = endgameSnapshot.RedPlayer.Health.ToString();
            localPlayerData.PlayerWave.text = endgameSnapshot.RedPlayer.Wave.ToString();
            enemyPlayerData.PlayerHealth.text = endgameSnapshot.BluePlayer.Health.ToString();
            enemyPlayerData.PlayerWave.text = endgameSnapshot.BluePlayer.Wave.ToString();
        }
    }
}
