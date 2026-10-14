using System;
using Unity.Netcode;
using UnityEngine;

public class ServerEnemyHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private EnemyManager enemyManager;

    /// <summary>
    /// Whether the last hit was cut down by this enemy's armor (an off-color attack, no armor break on it). The one
    /// fact a client cannot work out from the health it sees: it draws that hit small and grey instead of white.
    /// <para/>
    /// Declared BEFORE <see cref="_currentHealth"/> on purpose. NGO writes and reads a behaviour's dirty variables
    /// in declaration order inside one delta, so a client has this value in hand by the time the health change
    /// that it describes raises its event. Two hits inside one network tick arrive as a single drop wearing the
    /// later hit's flag; that is a number's colour, never its amount.
    /// </summary>
    private NetworkVariable<bool> _lastHitResisted = new(
        writePerm: NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// How much of this enemy's off-color armor is stripped right now, in whole percent (0 = whole, 100 = gone), for
    /// the clients to draw the broken shield. The damage maths stays on <see cref="_colorResistClearPercent"/>.
    /// </summary>
    private NetworkVariable<byte> _armorBrokenPercent = new(
        writePerm: NetworkVariableWritePermission.Server
    );

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
    public NetworkVariable<bool> LastHitResisted => _lastHitResisted;
    public NetworkVariable<byte> ArmorBrokenPercent => _armorBrokenPercent;
    public static event Action<EnemyManager> OnDeath;

    /// <summary>
    /// Server-only. This enemy was killed — raised just before its despawn, so it is still spawned for anything
    /// that has to act on it (an Rpc telling clients how it died). Unlike <see cref="OnDeath"/>, which fires for
    /// every despawn, this fires only for a kill.
    /// </summary>
    public event Action<EnemyManager> OnKilled;

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
        // Before the health, like every write of it: a pooled body must not start its next life "resisted".
        _lastHitResisted.Value = false;
        _currentHealth.Value = _maxHealth.Value;
        // Pooled instances re-enter OnNetworkSpawn on reuse; clears from a previous life must not carry over.
        _colorResistClearPercent = 0f;
        PublishArmorBreak();

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

        // Grey only while the armor is whole. Once a Torniquete or a Ferrugem is stripping it the hit reads white
        // again, even if a part of the resistance is left: the change of colour is the proof that the break worked.
        // Written before the health so both travel in one delta and this one is read first (see the field).
        _lastHitResisted.Value = effective < damage.Amount - 0.001f && _colorResistClearPercent <= 0f;
        _currentHealth.Value -= effective;

        if (_currentHealth.Value <= 0f)
        {
            _currentHealth.Value = 0f;
            OnKilled?.Invoke(enemyManager);
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
        PublishArmorBreak();
    }

    /// <summary>
    /// Server-only. Removes a previously-applied color-resist clear, clamped at 0 so a stray double-remove
    /// can never leave the accumulator negative and start amplifying the enemy's armor.
    /// </summary>
    public void RemoveColorResistClear(float percent)
    {
        if (!IsServer) return;
        _colorResistClearPercent = Mathf.Max(0f, _colorResistClearPercent - percent);
        PublishArmorBreak();
    }

    /// <summary>Whole percent, written only on a change (see ServerEnemyMovement.PublishStatuses).</summary>
    private void PublishArmorBreak()
    {
        byte broken = (byte)Mathf.RoundToInt(Mathf.Clamp01(_colorResistClearPercent) * 100f);
        if (_armorBrokenPercent.Value != broken) _armorBrokenPercent.Value = broken;
    }
}
