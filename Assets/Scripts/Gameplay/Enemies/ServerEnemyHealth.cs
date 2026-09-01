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

    // Independent sources (a Torniquete aura, a Ferrugem zone, future ones) each contribute one increment
    // and remove exactly one on expiry. While > 0, the enemy is treated as having no off-color resistance;
    // the counter mirrors ServerEnemyMovement's slow / speed-buff accumulators, so overlapping clears never
    // interfere and one source expiring can never wipe another that is still active. Server-side only —
    // damage math already lives on the server and replicates through _currentHealth.
    private int _colorResistCleared = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        _maxHealth.Value = enemyManager.Data.MaxHealth
                           * enemyManager.CardScale.Health
                           * enemyManager.SplitStatMultiplier;
        _currentHealth.Value = _maxHealth.Value;
        // Pooled instances re-enter OnNetworkSpawn on reuse; clears from a previous life must not carry over.
        _colorResistCleared = 0;

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
        // anything future) is covered without each one re-implementing the rule. A live color-resist clear
        // (Torniquete aura, Ferrugem zone) drops the resistance to 0 for the duration of the hit, so the
        // policy stays in ArmorResistance and every damage source picks up the clear for free.
        float resistance = _colorResistCleared > 0 ? 0f : enemyManager.Data.OffColorResistance;
        float effective = ArmorResistance.Resolve(damage, enemyManager.Data.ArmorColor, resistance);

        _currentHealth.Value -= effective;

        if (_currentHealth.Value <= 0f)
        {
            _currentHealth.Value = 0f;
            NetworkObject.Despawn();
        }
    }

    /// <summary>
    /// Server-only. Marks one source (an aura, a zone) as clearing this enemy's off-color resistance.
    /// The counter — not a bool — is what lets independent clears overlap without clobbering each other
    /// on expiry; each source must call <see cref="RemoveColorResistClear"/> exactly once when it releases.
    /// </summary>
    public void AddColorResistClear()
    {
        if (!IsServer) return;
        _colorResistCleared++;
    }

    /// <summary>
    /// Server-only. Removes a previously-applied color-resist clear, clamped at 0 so a stray double-remove
    /// can never leave the counter negative and permanently expose the armor.
    /// </summary>
    public void RemoveColorResistClear()
    {
        if (!IsServer) return;
        if (_colorResistCleared > 0) _colorResistCleared--;
    }
}
