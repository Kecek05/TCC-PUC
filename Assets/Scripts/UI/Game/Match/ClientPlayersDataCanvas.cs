using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

//Only Exists in Client
public class ClientPlayersDataCanvas : MonoBehaviour
{
    [Title(("References"))]
    [InfoBox("The order of the arrays must be Local Player -> Enemy Player")]
    [SerializeField] private TextMeshProUGUI[] playersHealth;
    [SerializeField] private TextMeshProUGUI[] playersWaves;
    [SerializeField] private GameObject[] localPlayerContents;
    [SerializeField] private GameObject[] enemyPlayerContents;
    [Space(5f)]
    
    [Title("Tween Settings")]
    [SerializeField] private float sliderTweenDuration = 0.5f;
    [SerializeField] private Ease tweenEase = Ease.OutBack;

    private BaseServerPlayerHealthManager _playerHealthManager;
    private Tween[] _sliderTweens = new Tween[2];
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
        
        WaveManager_OnBlueWaveChanged(0, _waveManager.BlueCurrentWave.Value);
        WaveManager_OnRedWaveChanged(0, _waveManager.RedCurrentWave.Value);
        
        CameraSlide.SideChanged += side =>
        {
            GameLog.Info($"Camera side changed to {side}.");
            switch (side)
            {
                case CameraSide.Local:
                    foreach (GameObject gameObject in localPlayerContents)
                    {
                        gameObject.SetActive(true);
                    }
                    foreach (GameObject gameObject in enemyPlayerContents)
                    {
                        gameObject.SetActive(false);
                    }
                    break;
                case CameraSide.Enemy:
                    foreach (GameObject gameObject in localPlayerContents)
                    {
                        gameObject.SetActive(false);
                    }
                    foreach (GameObject gameObject in enemyPlayerContents)
                    {
                        gameObject.SetActive(true);
                    }
                    break;
            }
        };
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
        playersHealth[isLocal ? 0 : 1].text = $"{newHealth}";
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
        playersWaves[isLocal ? 0 : 1].text = isLocal ?  $"{newWave}/{_waveManager.GetTotalWaves()}" : $"{newWave}";
    }
}
