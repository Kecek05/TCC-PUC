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
    [SerializeField] private TMP_Text waveTimerText;

    private bool _initialized;
    private TeamType _localTeam;

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
        yield return new WaitUntil(() =>
            TeamManager.Instance != null &&
            TeamManager.Instance.HasLocalTeamBeenAssigned() &&
            ServerWaveManager.Instance != null);

        _localTeam = TeamManager.Instance.GetLocalTeam();
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        var waveManager = ServerWaveManager.Instance;
        if (waveManager == null) return;

        int wave;
        float timer;

        if (_localTeam == TeamType.Blue)
        {
            wave = waveManager.BlueCurrentWave.Value;
            timer = waveManager.BlueWaveTimer.Value;
        }
        else
        {
            wave = waveManager.RedCurrentWave.Value;
            timer = waveManager.RedWaveTimer.Value;
        }

        if (waveNumberText != null)
            waveNumberText.text = $"Wave {wave}";

        if (waveTimerText != null)
        {
            if (timer > 0f)
                waveTimerText.text = $"Next wave: {timer:F0}s";
            else
                waveTimerText.text = "";
        }
    }
}
