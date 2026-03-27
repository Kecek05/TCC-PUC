using Unity.Netcode;
using UnityEngine;

public class ServerManaManager : NetworkBehaviour
{
    public static ServerManaManager Instance { get; private set; }
    
    [SerializeField] private ManaSettingsSO _manaSettings;
    [SerializeField] private float _syncThreshold = 0.1f;
    
    private NetworkVariable<float> _blueMana = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _redMana = new(writePerm: NetworkVariableWritePermission.Server);

    public NetworkVariable<float> BlueMana => _blueMana;
    public NetworkVariable<float> RedMana => _redMana;

    private float _blueLocalMana;
    private float _redLocalMana;

    private void Awake()
    {
        Instance = this;
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
        _blueMana.Value = _manaSettings.StartingMana;
        _redMana.Value = _manaSettings.StartingMana;
    }

    private void Update()
    {
        if (!IsServer) return;
        
        RegenerateMana();
    }
    
    private void RegenerateMana()
    {
        float regen = _manaSettings.RegenPerSecond * Time.deltaTime;

        _blueLocalMana = Mathf.Min(_blueLocalMana + regen, _manaSettings.MaxMana);
        _redLocalMana = Mathf.Min(_redLocalMana + regen, _manaSettings.MaxMana);

        if (Mathf.Abs(_blueLocalMana - _blueMana.Value) >= _syncThreshold)
            _blueMana.Value = _blueLocalMana;

        if (Mathf.Abs(_redLocalMana - _redMana.Value) >= _syncThreshold)
            _redMana.Value = _redLocalMana;
    }

    public float GetMana(TeamType team)
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

    public bool CanAfford(TeamType team, int cost)
    {
        Debug.Log($"Can afford {team} cost {cost} : {Mathf.FloorToInt(GetMana(team)) >= cost}");
        return Mathf.FloorToInt(GetMana(team)) >= cost;
    }

    public bool TrySpendMana(TeamType team, int cost)
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
                _blueMana.Value = _blueLocalMana;
                break;
            case TeamType.Red:
                _redMana.Value = _redLocalMana;
                break;
            default:
                Debug.LogError($"Invalid team: {team}");
                break;
        }
    }
}
