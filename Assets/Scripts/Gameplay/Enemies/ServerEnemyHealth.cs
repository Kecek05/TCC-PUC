using System;
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
    public static event Action<EnemyManager> OnDeath;
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _maxHealth = enemyManager.Data.MaxHealth;
        _currentHealth.Value = _maxHealth;

        EnemyRegistry.Register(enemyManager);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        EnemyRegistry.Unregister(enemyManager);

        // OnNetworkDespawn fires both for real removals (killed / reached the base)
        // AND when NGO destroys every NetworkObject during a NetworkManager shutdown
        // (host left / match teardown). In the shutdown case we must NOT raise the
        // gameplay "death" reaction: ServerWaveManager would re-run win-condition
        // logic (double SetWinner) and write NetworkVariables mid-shutdown — the
        // exact condition NGO warns about (NetworkVariableBase: ShutdownInProgress).
        if (NetworkManager != null && NetworkManager.ShutdownInProgress) return;

        OnDeath?.Invoke(enemyManager);
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;
        if (enemyManager.ServerMovement.Invincible.Value) return;

        _currentHealth.Value -= damage;

        if (_currentHealth.Value <= 0f)
        {
            _currentHealth.Value = 0f;
            NetworkObject.Despawn();
        }
    }
}
