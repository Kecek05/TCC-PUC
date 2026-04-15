using System;
using Sirenix.OdinInspector;
using Unity.Android.Gradle.Manifest;
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
    public event Action<TeamType> OnPlayerDeath;
    
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
        
        _blueHealth.Value = _healthSettings.StartingHealth;
        _redHealth.Value = _healthSettings.StartingHealth;
    }

    public void DamageBase(float damage,  TeamType teamType)
    {
        if (GameFlowManager.Instance.CurrentGameState.Value != GameState.InMatch) return;
        
        Debug.Log($"Enemy reached the end! Dealing {damage} damage to the {teamType} team.");
        switch (teamType)
        {
            case TeamType.Blue:
                _blueHealth.Value = Mathf.Max(_blueHealth.Value - damage, 0f);

                if (_blueHealth.Value <= 0)
                {
                    Debug.Log($"Blue team has been defeated!");
                    OnPlayerDeath?.Invoke(teamType);
                }
                
                break;
            case TeamType.Red:
                _redHealth.Value = Mathf.Max(_redHealth.Value - damage, 0f);

                if (_redHealth.Value <= 0)
                {
                    Debug.Log($"Red team has been defeated!");
                    OnPlayerDeath?.Invoke(teamType);
                }
                
                break;
            default:
                Debug.LogWarning($"Invalid team type: {teamType}");
                break;
        }
    }
}
