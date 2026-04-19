using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ServerPlayerHealthManager : BaseServerPlayerHealthManager
{
    [Title("Player Health Settings")]
    [SerializeField] private PlayerHealthSettingsSO _healthSettings;

    private BaseGameFlowManager _gameFlowManager;
    private BaseTeamManager _teamManager;

    private void Awake()
    {
        ServiceLocator.Register<BaseServerPlayerHealthManager>(this);
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseServerPlayerHealthManager>();
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        
        BlueHealth.Value = _healthSettings.StartingHealth;
        RedHealth.Value = _healthSettings.StartingHealth;
    }

    public override void DamageBase(float damage, TeamType teamType)
    {
        if (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value != GameState.InMatch) return;

        switch (teamType)
        {
            case TeamType.Blue:
                BlueHealth.Value = Mathf.Max(BlueHealth.Value - damage, 0f);

                if (BlueHealth.Value <= 0)
                {
                    Debug.Log($"Blue team has been defeated!");
                    TriggerOnTeamDeath(teamType);
                }

                break;
            case TeamType.Red:
                RedHealth.Value = Mathf.Max(RedHealth.Value - damage, 0f);

                if (RedHealth.Value <= 0)
                {
                    Debug.Log($"Red team has been defeated!");
                    TriggerOnTeamDeath(teamType);
                }

                break;
            default:
                Debug.LogWarning($"Invalid team type: {teamType}");
                break;
        }
    }

    public override NetworkVariable<float> GetLocalHealth()
    {
        return _teamManager.GetLocalTeam() == TeamType.Blue ? BlueHealth : RedHealth;
    }

    public override NetworkVariable<float> GetEnemyHealth()
    {
        return _teamManager.GetLocalTeam() == TeamType.Blue ? RedHealth : BlueHealth;
    }
}
