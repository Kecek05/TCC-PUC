using Unity.Netcode;
using UnityEngine;

public class ServerManaManager : NetworkBehaviour
{
    public static ServerManaManager Instance { get; private set; }
    
    [SerializeField] private ManaSettingsSO _manaSettings;
    [SerializeField] private float _syncThreshold = 0.1f;
    
    private NetworkVariable<float> redMana = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> blueMana = new(writePerm: NetworkVariableWritePermission.Server);

    private float localRedMana = 0f;
    private float localBlueMana = 0f;

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

        redMana.Value = _manaSettings.StartingMana;
        blueMana.Value = _manaSettings.StartingMana;
        localRedMana = _manaSettings.StartingMana;
        localBlueMana = _manaSettings.StartingMana;
    }

    private void Update()
    {
        if (!IsServer) return;
        
        RegenerateMana();
    }
    
    private void RegenerateMana()
    {
        localBlueMana += _manaSettings.RegenPerSecond * Time.deltaTime;
        localRedMana += _manaSettings.RegenPerSecond * Time.deltaTime;
        
        localBlueMana = Mathf.Min(localBlueMana, _manaSettings.MaxMana);
        localRedMana = Mathf.Min(localRedMana, _manaSettings.MaxMana);
        
        if (localBlueMana - redMana.Value >= _syncThreshold)
        {
            redMana.Value = localRedMana;
            Debug.Log("Sync red mana to: " + redMana.Value);
        }

        if (localBlueMana - blueMana.Value >= _syncThreshold)
        {
            blueMana.Value = localBlueMana;
            Debug.Log("Sync blue mana to: " + blueMana.Value);
        }
    }

    public float GetMana(TeamType teamType)
    {
        return teamType == TeamType.Blue ? blueMana.Value : redMana.Value;
    }

    public bool CanAfford(TeamType teamType, int cost)
    {
        return Mathf.FloorToInt(GetMana(teamType)) >= cost;
    }

    public bool TrySpendMana(TeamType teamType, int cost)
    {
        if (!CanAfford(teamType, cost)) return false;
        
        if (teamType == TeamType.Blue)
        {
            localBlueMana -= cost;
            blueMana.Value = localBlueMana; // Force sync immediately on spend
            Debug.Log($"Blue team spent {cost} mana. Remaining: {localBlueMana}");
        }
        else
        {
            localRedMana -= cost;
            redMana.Value = localRedMana; // Force sync immediately on spend
            Debug.Log($"Red team spent {cost} mana. Remaining: {localRedMana}");
        }
        return true;
    }
}
