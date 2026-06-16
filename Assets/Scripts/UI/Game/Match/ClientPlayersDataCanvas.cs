using System.Collections;
using DG.Tweening;
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
    [SerializeField] private GameObject[] localPlayerContents;
    [SerializeField] private GameObject[] enemyPlayerContents;
    [SerializeField] private Image[] waveFillImage;
    [Space(5f)]
    
    [Title("Tween Settings")]
    [SerializeField] private float sliderTweenDuration = 0.5f;
    [SerializeField] private Ease tweenEase = Ease.OutBack;

    private BaseServerPlayerHealthManager _playerHealthManager;
    private Tween[] _waveFillTweens = new Tween[2];
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
        
        _waveManager.BlueCurrentWaveProgressNormalized.OnValueChanged += WaveManager_OnBlueWaveProgressChanged;
        _waveManager.RedCurrentWaveProgressNormalized.OnValueChanged += WaveManager_OnRedWaveProgressChanged;
        
        WaveManager_OnBlueWaveChanged(0, _waveManager.BlueCurrentWave.Value);
        WaveManager_OnRedWaveChanged(0, _waveManager.RedCurrentWave.Value);
        WaveManager_OnBlueWaveProgressChanged(0, _waveManager.BlueCurrentWaveProgressNormalized.Value);
        WaveManager_OnRedWaveProgressChanged(0, _waveManager.RedCurrentWaveProgressNormalized.Value);
        
        // Named method (not a lambda) so OnDestroy can unsubscribe. SideChanged is a
        // static event; an un-removed subscription survives this scene and fires on the
        // next match's CameraSlide, hitting this canvas's already-destroyed GameObjects.
        CameraSlide.SideChanged += HandleCameraSideChanged;
    }

    private void HandleCameraSideChanged(CameraSide side)
    {
        GameLog.Info($"Camera side changed to {side}.");
        switch (side)
        {
            case CameraSide.Local:
                foreach (GameObject content in localPlayerContents)
                    content.SetActive(true);
                foreach (GameObject content in enemyPlayerContents)
                    content.SetActive(false);
                break;
            case CameraSide.Enemy:
                foreach (GameObject content in localPlayerContents)
                    content.SetActive(false);
                foreach (GameObject content in enemyPlayerContents)
                    content.SetActive(true);
                break;
        }
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
            _waveManager.BlueCurrentWaveProgressNormalized.OnValueChanged -= WaveManager_OnBlueWaveProgressChanged;
            _waveManager.RedCurrentWaveProgressNormalized.OnValueChanged -= WaveManager_OnRedWaveProgressChanged;
        }

        CameraSlide.SideChanged -= HandleCameraSideChanged;
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
        int index = isLocal ? 0 : 1;
        _waveFillTweens[index]?.Kill();
        _waveFillTweens[index] = DOTween.To(
            () => waveFillImage[index].fillAmount, 
            x => waveFillImage[index].fillAmount = x, 
            newProgress, sliderTweenDuration)
            .SetEase(tweenEase);
    }
}
