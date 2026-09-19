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

    public override void OnNetworkSpawn()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        BlueHealth.Value = _healthSettings.StartingHealth;
        RedHealth.Value = _healthSettings.StartingHealth;
    }
    
    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseServerPlayerHealthManager>();
        base.OnDestroy();
    }

    public override void DamageBase(float damage, TeamType teamType)
    {
        if (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value != GameState.InMatch) return;

        // The floor is 0 unless someone set one, so a floored base simply never reaches the death check.
        float floor = HealthFloor(teamType);

        switch (teamType)
        {
            case TeamType.Blue:
                BlueHealth.Value = Mathf.Max(BlueHealth.Value - damage, floor);

                if (BlueHealth.Value <= 0)
                {
                    GameLog.Info($"Blue team has been defeated!");
                    TriggerOnTeamDeath(teamType);
                }

                break;
            case TeamType.Red:
                RedHealth.Value = Mathf.Max(RedHealth.Value - damage, floor);

                if (RedHealth.Value <= 0)
                {
                    GameLog.Info($"Red team has been defeated!");
                    TriggerOnTeamDeath(teamType);
                }

                break;
            default:
                GameLog.Warn($"Invalid team type: {teamType}");
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
