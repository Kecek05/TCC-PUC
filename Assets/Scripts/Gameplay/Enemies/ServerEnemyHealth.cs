using Unity.Netcode;
using UnityEngine;

public class ServerEnemyHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private EnemyManager enemyManager;
    
    private NetworkVariable<float> _currentHealth = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    private float _maxHealth;

    public NetworkVariable<float> CurrentHealth => _currentHealth;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _maxHealth = enemyManager.Data.MaxHealth;
        _currentHealth.Value = _maxHealth;

        ServerTowerCombat.RegisterEnemy(enemyManager);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            ServerTowerCombat.UnregisterEnemy(enemyManager);
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;

        _currentHealth.Value -= damage;

        if (_currentHealth.Value <= 0f)
        {
            _currentHealth.Value = 0f;
            NetworkObject.Despawn();
        }
    }
}
