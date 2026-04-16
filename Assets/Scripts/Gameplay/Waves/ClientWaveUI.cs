using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client-only: reads wave state NetworkVariables and updates UI text.
/// Shows the local player's current wave number and time until next wave.
/// </summary>
public class ClientWaveUI : NetworkBehaviour
{
    [SerializeField] private TMP_Text waveNumberText;

    private bool _initialized;
    private TeamType _localTeam;

    private BaseTeamManager _teamManager;
    private BaseServerWaveManager _waveManager;

    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        StartCoroutine(WaitForInitialization());
    }

    private IEnumerator WaitForInitialization()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _waveManager = ServiceLocator.Get<BaseServerWaveManager>();

        yield return new WaitUntil(() =>
            _teamManager != null &&
            _teamManager.HasLocalTeamBeenAssigned() &&
            _waveManager != null);

        _localTeam = _teamManager.GetLocalTeam();
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        var waveManager = _waveManager;
        if (waveManager == null) return;

        int wave;

        if (_localTeam == TeamType.Blue)
        {
            wave = waveManager.BlueCurrentWave.Value;
        }
        else
        {
            wave = waveManager.RedCurrentWave.Value;
        }

        if (waveNumberText != null)
            waveNumberText.text = $"Wave {wave}";
    }
}
