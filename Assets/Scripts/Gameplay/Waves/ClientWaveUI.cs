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
