using Unity.Netcode;
using UnityEngine;

public class ServerManaManager : BaseServerManaManager
{
    [SerializeField] private ManaSettingsSO _manaSettings;
    [SerializeField] private float _syncThreshold = 0.1f;

    private BaseGameFlowManager _gameFlowManager;
    private BaseServerWaveManager _waveManager;

    private float _blueLocalMana;
    private float _redLocalMana;

    private void Awake()
    {
        ServiceLocator.Register<BaseServerManaManager>(this);
    }

    public override void OnNetworkSpawn()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        _waveManager = ServiceLocator.Get<BaseServerWaveManager>();
        
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _blueLocalMana = _manaSettings.StartingMana;
        _redLocalMana = _manaSettings.StartingMana;
        BlueMana.Value = _manaSettings.StartingMana;
        RedMana.Value = _manaSettings.StartingMana;

        BlueMaxMana.Value = _manaSettings.StartingMaxMana;
        RedMaxMana.Value = _manaSettings.StartingMaxMana;

        if (_waveManager == null)
            _waveManager = ServiceLocator.Get<BaseServerWaveManager>();

        _waveManager.OnNewWave += WaveManager_OnNewWave;
    }

    public override void OnNetworkDespawn()
    {
        if (_waveManager != null)
            _waveManager.OnNewWave -= WaveManager_OnNewWave;
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseServerManaManager>();
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsServer) return;

        if (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value != GameState.InMatch) return;

        RegenerateMana();
    }

    private void RegenerateMana()
    {
        float regen = _manaSettings.RegenPerSecond * Time.deltaTime;

        _blueLocalMana = Mathf.Min(_blueLocalMana + regen, BlueMaxMana.Value);
        _redLocalMana = Mathf.Min(_redLocalMana + regen, RedMaxMana.Value);

        if (Mathf.Abs(_blueLocalMana - BlueMana.Value) >= _syncThreshold)
            BlueMana.Value = _blueLocalMana;

        if (Mathf.Abs(_redLocalMana - RedMana.Value) >= _syncThreshold)
            RedMana.Value = _redLocalMana;
    }

    private void WaveManager_OnNewWave(TeamType team, int waveNumber)
    {
        if (!_manaSettings.TryGetMaxManaForWave(waveNumber, out float newMax))
            return;

        NetworkVariable<float> maxVar = GetMaxManaNetworkVariable(team);
        if (Mathf.Approximately(maxVar.Value, newMax))
            return;

        maxVar.Value = newMax;

        ClampLocalManaToMax(team, newMax);
    }

    private void ClampLocalManaToMax(TeamType team, float newMax)
    {
        if (team == TeamType.Blue && _blueLocalMana > newMax)
        {
            _blueLocalMana = newMax;
            BlueMana.Value = _blueLocalMana;
        }
        else if (team == TeamType.Red && _redLocalMana > newMax)
        {
            _redLocalMana = newMax;
            RedMana.Value = _redLocalMana;
        }
    }

    public override float GetMana(TeamType team)
    {
        switch (team)
        {
            case TeamType.Blue:
                return _blueLocalMana;
            case TeamType.Red:
                return _redLocalMana;
            default:
                Debug.LogError($"Invalid team: {team}");
                return 0f;
        }
    }

    public override float GetMaxMana(TeamType team)
    {
        switch (team)
        {
            case TeamType.Blue:
                return BlueMaxMana.Value;
            case TeamType.Red:
                return RedMaxMana.Value;
            default:
                Debug.LogError($"Invalid team: {team}");
                return 0f;
        }
    }

    public override NetworkVariable<float> GetMaxManaNetworkVariable(TeamType team)
    {
        switch (team)
        {
            case TeamType.Blue:
                return BlueMaxMana;
            case TeamType.Red:
                return RedMaxMana;
            default:
                Debug.LogError($"Invalid team: {team}");
                return null;
        }
    }

    public override bool CanAfford(TeamType team, int cost)
    {
        return Mathf.FloorToInt(GetMana(team)) >= cost;
    }

    public override bool TrySpendMana(TeamType team, int cost)
    {
        if (!CanAfford(team, cost)) return false;

        if (team == TeamType.Blue)
            _blueLocalMana -= cost;
        else if  (team == TeamType.Red)
            _redLocalMana -= cost;
        else
            return false;

        SyncMana(team);
        return true;
    }

    private void SyncMana(TeamType team)
    {
        switch (team)
        {
            case TeamType.Blue:
                BlueMana.Value = _blueLocalMana;
                break;
            case TeamType.Red:
                RedMana.Value = _redLocalMana;
                break;
            default:
                Debug.LogError($"Invalid team: {team}");
                break;
        }
    }
}
