using UnityEngine;

public class ServerManaManager : BaseServerManaManager
{
    [SerializeField] private ManaSettingsSO _manaSettings;
    [SerializeField] private float _syncThreshold = 0.1f;

    private BaseGameFlowManager _gameFlowManager;

    private float _blueLocalMana;
    private float _redLocalMana;

    private void Awake()
    {
        ServiceLocator.Register<BaseServerManaManager>(this);
    }

    private void Start()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _blueLocalMana = _manaSettings.StartingMana;
        _redLocalMana = _manaSettings.StartingMana;
        BlueMana.Value = _manaSettings.StartingMana;
        RedMana.Value = _manaSettings.StartingMana;
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

        _blueLocalMana = Mathf.Min(_blueLocalMana + regen, _manaSettings.MaxMana);
        _redLocalMana = Mathf.Min(_redLocalMana + regen, _manaSettings.MaxMana);

        if (Mathf.Abs(_blueLocalMana - BlueMana.Value) >= _syncThreshold)
            BlueMana.Value = _blueLocalMana;

        if (Mathf.Abs(_redLocalMana - RedMana.Value) >= _syncThreshold)
            RedMana.Value = _redLocalMana;
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
