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

    // Additive sum of every active clear SOURCE (a Torniquete aura, a Ferrugem zone), each a fraction of
    // this enemy's off-color resistance to strip. Exactly ServerEnemyMovement's _slowPercent accumulator:
    // a source adds and removes only its own contribution, so overlapping clears never interfere and one
    // expiring can never wipe another still active. It is a FRACTION rather than the old source counter so
    // the strength can scale with the caster's card level — a level-1 Torniquete strips part of the armor,
    // a maxed one all of it. At 1 the enemy has no off-color resistance at all, which is what every source
    // used to do unconditionally. Server-side only: damage math already lives on the server and replicates
    // through _currentHealth.
    private float _colorResistClearPercent = 0f;

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
        _colorResistClearPercent = 0f;

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
        // anything future) is covered without each one re-implementing the rule. Live color-resist clears
        // (Torniquete aura, Ferrugem zone) strip their combined fraction of the resistance for the duration
        // of the hit, so the policy stays in ArmorResistance and every damage source picks the clear up for
        // free. Clamped here rather than on the accumulator: stacked sources are allowed to over-subscribe,
        // they just cannot push the resistance below zero and start healing the target.
        float resistance = enemyManager.Data.OffColorResistance * (1f - Mathf.Clamp01(_colorResistClearPercent));
        float effective = ArmorResistance.Resolve(damage, enemyManager.Data.ArmorColor, resistance);

        _currentHealth.Value -= effective;

        if (_currentHealth.Value <= 0f)
        {
            _currentHealth.Value = 0f;
            NetworkObject.Despawn();
        }
    }

    /// <summary>
    /// Server-only. Adds one source's contribution to clearing this enemy's off-color resistance, as a
    /// fraction of it to strip (1 = the armor stops mattering entirely). Accumulating per source — not a
    /// single value — is what lets independent clears overlap without clobbering each other on expiry;
    /// each source must pass the <b>identical</b> amount to <see cref="RemoveColorResistClear"/> when it
    /// releases, exactly as the slow and buff accumulators require.
    /// </summary>
    public void AddColorResistClear(float percent)
    {
        if (!IsServer) return;
        _colorResistClearPercent += percent;
    }

    /// <summary>
    /// Server-only. Removes a previously-applied color-resist clear, clamped at 0 so a stray double-remove
    /// can never leave the accumulator negative and start amplifying the enemy's armor.
    /// </summary>
    public void RemoveColorResistClear(float percent)
    {
        if (!IsServer) return;
        _colorResistClearPercent = Mathf.Max(0f, _colorResistClearPercent - percent);
    }
}
