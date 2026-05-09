using System;
using System.Collections;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class WaveWarningCanvas : MonoBehaviour
{
    private const string WAVE_TOKEN = "{WAVE}";

    [Title("References")]
    [SerializeField] private MMF_Player waveWarningPlayer;
    [SerializeField] private GameObject waveWarningRoot;
    [SerializeField] private TextMeshProUGUI  waveWarningText;

    private BaseServerWaveManager _serverWaveManager;

    private string _waveWarningTemplate;

    private TeamType _localTeamConnection;

    private void Awake()
    {
        _waveWarningTemplate = waveWarningText.text;
        waveWarningRoot.SetActive(false);
    }

    private void Start()
    {
        _serverWaveManager = ServiceLocator.Get<BaseServerWaveManager>();

        StartCoroutine(SubscribeToCorrectEvent());
    }

    private IEnumerator SubscribeToCorrectEvent()
    {
        yield return new WaitUntil(() => ServiceLocator.Get<BaseTeamManager>() != null);
        BaseTeamManager teamManager = ServiceLocator.Get<BaseTeamManager>();
        
        yield return new WaitUntil(() => teamManager.HasLocalTeamBeenAssigned());

        _localTeamConnection = teamManager.GetLocalTeam();

        switch (_localTeamConnection)
        {
            case TeamType.Red:
                _serverWaveManager.RedCurrentWave.OnValueChanged += OnWaveChanged;
                break;
            case TeamType.Blue:
                _serverWaveManager.BlueCurrentWave.OnValueChanged += OnWaveChanged;
                break;
        }
    }

    private void OnDestroy()
    {
        if (_serverWaveManager == null) return;
        
        switch (_localTeamConnection)
        {
            case TeamType.Red:
                _serverWaveManager.RedCurrentWave.OnValueChanged -= OnWaveChanged;
                break;
            case TeamType.Blue:
                _serverWaveManager.BlueCurrentWave.OnValueChanged -= OnWaveChanged;
                break;
        }
    }

    private void OnWaveChanged(int previousValue, int newValue)
    {
        ShowWaveWarning(newValue);
    }
    
    private void ShowWaveWarning(int waveToShow)
    {
        waveWarningText.text = _waveWarningTemplate.Replace(WAVE_TOKEN, waveToShow.ToString());
        waveWarningRoot.SetActive(true);
        waveWarningPlayer.PlayFeedbacks();
    }
}
