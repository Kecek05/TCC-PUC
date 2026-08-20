using System;
using Unity.Netcode;
using UnityEngine;

public class ServerEnemyHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private EnemyManager enemyManager;
    
    
    private NetworkVariable<float> _currentHealth = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Replicated because it is scaled by the summoner's card level: the client health bar normalises
    /// against this, and reading the shared EnemyDataSO instead would draw the wrong fill.
    /// </summary>
    private NetworkVariable<float> _maxHealth = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    public NetworkVariable<float> CurrentHealth => _currentHealth;
    public NetworkVariable<float> MaxHealth => _maxHealth;
    public static event Action<EnemyManager> OnDeath;
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _maxHealth.Value = enemyManager.Data.MaxHealth * enemyManager.CardScale.Health;
        _currentHealth.Value = _maxHealth.Value;

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

    public void TakeDamage(DamageInfo damage)
    {
        if (!IsServer) return;
        if (enemyManager.ServerMovement.Invincible.Value) return;

        // The enemy owns its armor, so resistance is resolved here — every damage source (towers, spells,
        // anything future) is covered without each one re-implementing the rule.
        float effective = ArmorResistance.Resolve(damage, enemyManager.Data.ArmorColor, enemyManager.Data.OffColorResistance);

        _currentHealth.Value -= effective;

        if (_currentHealth.Value <= 0f)
        {
            _currentHealth.Value = 0f;
            NetworkObject.Despawn();
        }
    }
}
