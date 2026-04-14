using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ServerPlayerHealthManager : NetworkBehaviour
{
    public static ServerPlayerHealthManager Instance { get; private set; }
    
    [Title("Player Health Settings")]
    [SerializeField] private PlayerHealthSettingsSO _healthSettings;
    
    private NetworkVariable<float> _blueHealth = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _redHealth = new(writePerm: NetworkVariableWritePermission.Server);
    
    public NetworkVariable<float> BlueHealth => _blueHealth;
    public NetworkVariable<float> RedHealth => _redHealth;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Debug.LogError("Multiple instances of ServerPlayerHealthManager detected. This is not allowed.");
            Destroy(this);
        }
    }
    
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
    }

    public void DamageBase(float damage,  TeamType teamType)
    {
        Debug.Log($"Enemy reached the end! Dealing {damage} damage to the {teamType} team.");
        switch (teamType)
        {
            case TeamType.Blue:
                _blueHealth.Value = Mathf.Max(_blueHealth.Value - damage, 0f);
                break;
            case TeamType.Red:
                _redHealth.Value = Mathf.Max(_redHealth.Value - damage, 0f);
                break;
            default:
                Debug.LogWarning($"Invalid team type: {teamType}");
                break;
        }
    }
}
