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
    [SerializeField] private bool isLocalPlayer = false;
    
    public TextMeshProUGUI PlayerUsername => playerUsername;
    public TextMeshProUGUI PlayerHealth => playerHealth;
    public TextMeshProUGUI PlayerWave => playerWave;
    public bool IsLocalPlayer => isLocalPlayer;
}

public class ClientEndGameCanvas : NetworkBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject rootCanvas;
    [SerializeField] private PlayerEndGameCanvasData[] playersData;
    [SerializeField] private GameObject victoryLabel;
    [SerializeField] private GameObject defeatLabel;

    private BaseServerEndGameManager _endGameManager;
    private BaseTeamManager _teamManager;
    private BaseServerPlayerHealthManager _playerHealthManager;
    private BaseServerWaveManager _waveManager;

    private void Awake()
    {
        rootCanvas.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsClient)
        {
            enabled = false;
            return;
        }

        _playerHealthManager = ServiceLocator.Get<BaseServerPlayerHealthManager>();
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _endGameManager = ServiceLocator.Get<BaseServerEndGameManager>();
        _waveManager = ServiceLocator.Get<BaseServerWaveManager>();
        _endGameManager.WinnerTeam.OnValueChanged += ServerEndGameManager_OnWinnerTeamValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient && _endGameManager != null)
            _endGameManager.WinnerTeam.OnValueChanged -= ServerEndGameManager_OnWinnerTeamValueChanged;
    }

    private void ServerEndGameManager_OnWinnerTeamValueChanged(TeamType previousValue, TeamType winnerTeam)
    {
        if (winnerTeam == TeamType.None) return;
        
        SetupEndGameUI(winnerTeam);

        rootCanvas.SetActive(true);
    }

    private void SetupEndGameUI(TeamType winnerTeam)
    {
        //TODO: Setup Usernames
        
        
        //Setup Healths
        SetupHealths();
        
        //Setup Waves
        SetupWaves();
        
        victoryLabel.SetActive(false);
        defeatLabel.SetActive(false);
        
        if (winnerTeam == _teamManager.GetLocalTeam())
            victoryLabel.SetActive(true);
        else
            defeatLabel.SetActive(true);
    }

    private void SetupHealths()
    {
        foreach (PlayerEndGameCanvasData playerData in playersData)
        {
            if (playerData.IsLocalPlayer)
            {
                _playerHealthManager.GetLocalHealth().OnValueChanged += (previousValue, currentValue) =>
                {
                    playerData.PlayerHealth.text = currentValue.ToString();
                };
                playerData.PlayerHealth.text = _playerHealthManager.GetLocalHealth().Value.ToString();
            }
            else
            {
                _playerHealthManager.GetEnemyHealth().OnValueChanged += (previousValue, currentValue) =>
                {
                    playerData.PlayerHealth.text = currentValue.ToString();
                };
                playerData.PlayerHealth.text = _playerHealthManager.GetEnemyHealth().Value.ToString();
            }
        }
    }

    private void SetupWaves()
    {
        foreach (PlayerEndGameCanvasData playerData in playersData)
        {
            if (playerData.IsLocalPlayer)
            {
                _waveManager.GetLocalCurrentWave().OnValueChanged += (previousValue, currentValue) =>
                {
                    playerData.PlayerWave.text = currentValue.ToString();
                };
                playerData.PlayerWave.text = _waveManager.GetLocalCurrentWave().Value.ToString();
            }
            else
            {
                _waveManager.GetEnemyCurrentWave().OnValueChanged += (previousValue, currentValue) =>
                {
                    playerData.PlayerWave.text = currentValue.ToString();
                };
                playerData.PlayerWave.text = _waveManager.GetEnemyCurrentWave().Value.ToString();
            }
        }
    }
}
