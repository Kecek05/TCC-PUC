using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//Only Exists in Client
public class ClientPlayersDataCanvas : MonoBehaviour
{
    [Title(("References"))]
    [InfoBox("The order of the arrays must be Local Player -> Enemy Player")]
    [SerializeField] private TextMeshProUGUI[] playersHealth;
    [SerializeField] private TextMeshProUGUI[] playersWaves;
    [SerializeField] private Slider[] playersWaveSliders;

    private BaseServerPlayerHealthManager _playerHealthManager;
    private BaseTeamManager  _teamManager;
    private BaseServerWaveManager  _waveManager;
    
    private IEnumerator Start()
    {
        _playerHealthManager = ServiceLocator.Get<BaseServerPlayerHealthManager>();
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _waveManager = ServiceLocator.Get<BaseServerWaveManager>();
        
        yield return new WaitUntil(() => 
            _teamManager.HasLocalTeamBeenAssigned()
        );
        
        _playerHealthManager.BlueHealth.OnValueChanged += PlayerHealthManager_OnBlueHealthChanged;
        _playerHealthManager.RedHealth.OnValueChanged += PlayerHealthManager_OnRedHealthChanged;
        
        PlayerHealthManager_OnBlueHealthChanged(0, _playerHealthManager.BlueHealth.Value);
        PlayerHealthManager_OnRedHealthChanged(0, _playerHealthManager.RedHealth.Value);
        
        _waveManager.BlueCurrentWave.OnValueChanged += WaveManager_OnBlueWaveChanged;
        _waveManager.RedCurrentWave.OnValueChanged += WaveManager_OnRedWaveChanged;
        _waveManager.BlueCurrentWaveProgress.OnValueChanged += WaveManager_OnBlueWaveProgressChanged;
        _waveManager.RedCurrentWaveProgress.OnValueChanged += WaveManager_OnRedWaveProgressChanged;
        
        WaveManager_OnBlueWaveChanged(0, _waveManager.BlueCurrentWave.Value);
        WaveManager_OnRedWaveChanged(0, _waveManager.RedCurrentWave.Value);
        WaveManager_OnBlueWaveProgressChanged(0, _waveManager.BlueCurrentWaveProgress.Value);
        WaveManager_OnRedWaveProgressChanged(0, _waveManager.RedCurrentWaveProgress.Value);
    }

    private void OnDestroy()
    {
        if (_playerHealthManager != null)
        {
            _playerHealthManager.BlueHealth.OnValueChanged -= PlayerHealthManager_OnBlueHealthChanged;
            _playerHealthManager.RedHealth.OnValueChanged -= PlayerHealthManager_OnRedHealthChanged;
        }

        if (_waveManager != null)
        {
            _waveManager.BlueCurrentWave.OnValueChanged -= WaveManager_OnBlueWaveChanged;
            _waveManager.RedCurrentWave.OnValueChanged -= WaveManager_OnRedWaveChanged;
            _waveManager.BlueCurrentWaveProgress.OnValueChanged -= WaveManager_OnBlueWaveProgressChanged;
            _waveManager.RedCurrentWaveProgress.OnValueChanged -= WaveManager_OnRedWaveProgressChanged;
        }
    }

    private void PlayerHealthManager_OnBlueHealthChanged(float previousValue, float newValue)
    {
        UpdateHealth(newValue, _teamManager.GetLocalTeam() == TeamType.Blue);
    }
    
    private void PlayerHealthManager_OnRedHealthChanged(float previousValue, float newValue)
    {
        UpdateHealth(newValue, _teamManager.GetLocalTeam() == TeamType.Red);
    }

    private void UpdateHealth(float newHealth, bool isLocal)
    {
        playersHealth[isLocal ? 0 : 1].text = $"Health: {newHealth}";
    }

    private void WaveManager_OnBlueWaveChanged(int previousValue, int newValue)
    {
        ChangeWaveCount(newValue, _teamManager.GetLocalTeam() == TeamType.Blue);
    }
    
    private void WaveManager_OnRedWaveChanged(int previousValue, int newValue)
    {
        ChangeWaveCount(newValue, _teamManager.GetLocalTeam() == TeamType.Red);
    }

    private void ChangeWaveCount(int newWave, bool isLocal)
    {
        playersWaves[isLocal ? 0 : 1].text = $"Wave: {newWave}";
    }
    
    private void WaveManager_OnBlueWaveProgressChanged(float previousValue, float newValue)
    {
        ChangeWaveProgress(newValue, _teamManager.GetLocalTeam() == TeamType.Blue);
    }
    
    private void WaveManager_OnRedWaveProgressChanged(float previousValue, float newValue)
    {
        ChangeWaveProgress(newValue, _teamManager.GetLocalTeam() == TeamType.Red);
    }
    
    private void ChangeWaveProgress(float newProgress, bool isLocal)
    {
        Debug.Log($"Updating wave progress for {(isLocal ? "local" : "enemy")} player to {newProgress}");
        playersWaveSliders[isLocal ? 0 : 1].value = newProgress;
    }
}
