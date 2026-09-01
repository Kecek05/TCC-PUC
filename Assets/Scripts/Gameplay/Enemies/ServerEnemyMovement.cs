using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only: advances PathProgress along the assigned WaypointPath each tick.
/// Clients read PathProgress via NetworkVariable to determine visual position.
/// </summary>
public class ServerEnemyMovement : NetworkBehaviour
{
    [SerializeField] private EnemyManager enemyManager;
    
    private NetworkVariable<float> _pathProgress = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _currentSpeed = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _reversed = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> _invincible = new(writePerm: NetworkVariableWritePermission.Server);

    private BaseGameFlowManager _gameFlowManager;
    
    public NetworkVariable<float> PathProgress => _pathProgress;
    public NetworkVariable<float> CurrentSpeed => _currentSpeed;
    public NetworkVariable<bool> Reversed => _reversed;
    public NetworkVariable<bool> Invincible => _invincible;

    public bool IsTargetable => !_invincible.Value;

    /// <summary>
    /// Un-throttled progress along the path, 0..1. <see cref="PathProgress"/> only syncs past
    /// <see cref="SyncThreshold"/>, so anything server-side that needs the exact spot an enemy is standing
    /// on - a Cisma split placing its children where the parent died - has to read this instead.
    /// </summary>
    public float Progress => _localProgress;

    /// <summary>True while at least one source (an Ancora tower) is holding this enemy in place.</summary>
    public bool IsHeld => _holdCount > 0;

    /// <summary>
    /// The lane this enemy walks. Read by lane-shaped effects (e.g. the Lance strip) that need to tell
    /// "same path" apart from "same progress on a different path".
    /// </summary>
    public WaypointPath Path => _path;

    // Only sync PathProgress when the change exceeds this threshold.
    private const float SyncThreshold = 0.005f;

    private WaypointPath _path;
    private float _baseSpeed;
    private float _localProgress;
    private bool _reachedEnd;
    private bool _reversedLocal;
    private float _invincibilityTimer;

    // Current speed is composed from independent sources so they never clobber each other:
    //   effective = base * slowMultiplier * (1 - slowPercent) * (1 + speedBuffPercent)
    // _slowMultiplier is a single multiplicative slow (1 = none) owned by whoever sets the base speed;
    // _slowPercent is the additive sum of every active slow SOURCE (a Prism aura, a Rift zone) and
    // _speedBuffPercent the additive sum of every speed buff (stacked Rage zones). Both accumulators
    // mirror the tower attack-speed one: a source adds and removes only its own contribution, so a zone
    // expiring can never wipe an aura that is still holding the enemy.
    private float _slowMultiplier = 1f;
    private float _slowPercent = 0f;
    private float _speedBuffPercent = 0f;

    // A HOLD is deliberately not a slow. Slows are capped at MaxSlowPercent so control can never fully
    // replace damage, which is exactly the rule an Ancora is meant to break for one enemy at a time. It is
    // a counter rather than a bool for the same reason every other modifier here is: two Ancoras gripping
    // the same enemy must each release only their own grip.
    private int _holdCount = 0;

    // Where this enemy enters the lane. 0 for everything that spawns at the mouth of the path; a split
    // child inherits its parent's progress so the pieces appear where the parent actually died.
    private float _startProgress = 0f;

    // Stacked slows can never fully stop an enemy: at the cap it still crawls, so control has to be paired
    // with damage rather than replace it.
    private const float MaxSlowPercent = 0.85f;

    // Dash state (per-enemy, driven by EnemyDataSO.Dash* fields). _dashCooldown counts down between
    // dashes; when it reaches 0 the enemy enters a dash of DashDuration seconds during which _dashActive
    // is true and RecalculateSpeed multiplies the effective speed by DashSpeedMultiplier. The multiplier
    // composes with slows: a 50%-slowed dasher accelerates FROM its slowed speed, not from base.
    private float _dashCooldown;
    private float _dashRemaining;
    private bool _dashActive;

    /// <summary>
    /// Called by the spawner (e.g. ServerWaveManager) after instantiating to assign the path.
    /// Must be called on the server before or right after NetworkObject.Spawn().
    /// </summary>
    /// <param name="startProgress">
    /// Normalised point on the path to enter at. Defaults to the start of the lane; a Cisma split passes
    /// the dying parent's <see cref="Progress"/> so its children carry on from there.
    /// </param>
    public void Initialize(WaypointPath path, bool reversed = false, float startProgress = 0f)
    {
        _path = path;
        _reversedLocal = reversed;
        _startProgress = Mathf.Clamp01(startProgress);
    }

    private void Start()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }
        
        _baseSpeed = enemyManager.Data.MoveSpeed * enemyManager.CardScale.MoveSpeed;
        _slowMultiplier = 1f;
        _slowPercent = 0f;
        _speedBuffPercent = 0f;
        // Pooled instances re-enter OnNetworkSpawn on reuse; a dash mid-flight from a previous life would
        // otherwise carry into the next spawn. Start the interval fresh so the first dash lands one full
        // interval after spawn rather than the instant the enemy appears.
        _dashCooldown = enemyManager.Data.DashInterval;
        _dashRemaining = 0f;
        _dashActive = false;
        RecalculateSpeed();
        _reversed.Value = _reversedLocal;
        // Pooled instances re-enter OnNetworkSpawn on reuse; a grip from a previous life must not carry over.
        _holdCount = 0;
        _localProgress = _startProgress;
        _pathProgress.Value = _startProgress;

        _invincibilityTimer = enemyManager.Data.SpawnDuration;
        _invincible.Value = _invincibilityTimer > 0f;
    }

    private void Update()
    {
        if (!IsServer || _path == null || _reachedEnd) return;
        
        if (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value != GameState.InMatch) return;

        if (_invincible.Value)
        {
            _invincibilityTimer -= Time.deltaTime;
            if (_invincibilityTimer <= 0f)
                _invincible.Value = false;
            return;
        }

        TickDash();

        // Held: the enemy stays exactly where it is. Dash and the speed accumulators keep ticking so the
        // grip only costs it the distance it would have walked, never its buffs.
        if (_holdCount > 0) return;

        float totalLength = _path.TotalLength;
        if (totalLength <= 0f) return;

        // Advance local progress every frame
        _localProgress += (_currentSpeed.Value * Time.deltaTime) / totalLength;

        // Only push to NetworkVariable when change exceeds threshold (saves bandwidth)
        if (_localProgress - _pathProgress.Value >= SyncThreshold)
            _pathProgress.Value = _localProgress;

        if (_localProgress >= 1f)
        {
            _localProgress = 1f;
            _pathProgress.Value = 1f;
            _reachedEnd = true;
            OnReachedEnd();
        }

        // Update server-side transform for tower targeting distance checks
        float sampleT = _reversed.Value ? 1f - _localProgress : _localProgress;
        transform.position = _path.SamplePosition(sampleT);
    }

    private void OnReachedEnd()
    {
        // TODO: Apply damage to the player's base, then despawn
        ServiceLocator.Get<BaseServerPlayerHealthManager>()
            .DamageBase(
                enemyManager.Data.Damage * enemyManager.CardScale.Damage * enemyManager.SplitStatMultiplier,
                enemyManager.Team.GetTeamType());
        NetworkObject.Despawn();
    }

    /// <summary>
    /// Recomputes the replicated speed from its independent contributions. Server-only.
    /// </summary>
    private void RecalculateSpeed()
    {
        if (!IsServer) return;
        float dashFactor = _dashActive ? Mathf.Max(1f, enemyManager.Data.DashSpeedMultiplier) : 1f;
        _currentSpeed.Value = _baseSpeed
                              * _slowMultiplier
                              * (1f - Mathf.Min(_slowPercent, MaxSlowPercent))
                              * (1f + _speedBuffPercent)
                              * dashFactor;
    }

    /// <summary>
    /// Data-driven dash: if DashInterval > 0, the enemy alternates between waiting DashInterval seconds
    /// and dashing for DashDuration seconds. The dash surfaces through RecalculateSpeed as a multiplicative
    /// speed boost, so it composes with slows and buffs rather than replacing them.
    /// </summary>
    private void TickDash()
    {
        if (enemyManager.Data.DashInterval <= 0f) return;

        if (_dashActive)
        {
            _dashRemaining -= Time.deltaTime;
            if (_dashRemaining <= 0f)
            {
                _dashActive = false;
                _dashCooldown = enemyManager.Data.DashInterval;
                RecalculateSpeed();
            }
            return;
        }

        _dashCooldown -= Time.deltaTime;
        if (_dashCooldown <= 0f)
        {
            _dashActive = true;
            _dashRemaining = enemyManager.Data.DashDuration;
            RecalculateSpeed();
        }
    }

    /// <summary>
    /// Apply a multiplicative speed modifier (e.g. a slow effect from a tower).
    /// Pass 1.0 to restore normal speed. Composes with any active speed buffs.
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        if (!IsServer) return;
        _slowMultiplier = multiplier;
        RecalculateSpeed();
    }

    /// <summary>
    /// Server-only. Adds <paramref name="percent"/> (a fraction, 0.2 = +20%) to this enemy's stacked
    /// move-speed bonus. Independent buff sources (e.g. overlapping Rage zones) stack additively. Kept
    /// separate from the base/slow speed so one source can be removed without disturbing the others.
    /// </summary>
    public void AddSpeedBuff(float percent)
    {
        if (!IsServer) return;
        _speedBuffPercent += percent;
        RecalculateSpeed();
    }

    /// <summary>
    /// Server-only. Removes a previously-applied move-speed contribution, clamped at 0 so a stray
    /// double-remove can never invert the speed.
    /// </summary>
    public void RemoveSpeedBuff(float percent)
    {
        if (!IsServer) return;
        _speedBuffPercent = Mathf.Max(0f, _speedBuffPercent - percent);
        RecalculateSpeed();
    }

    /// <summary>
    /// Server-only. Grips the enemy in place until the matching <see cref="RemoveHold"/>. Unlike a slow this
    /// is absolute - a held enemy makes no progress at all - so it is metered by the holder (an Ancora takes
    /// one enemy at a time for a fixed duration) rather than by a cap here.
    /// </summary>
    public void AddHold()
    {
        if (!IsServer) return;
        _holdCount++;
    }

    /// <summary>
    /// Server-only. Releases one grip, clamped at 0 so a stray double-release can never leave the enemy
    /// permanently un-holdable.
    /// </summary>
    public void RemoveHold()
    {
        if (!IsServer) return;
        if (_holdCount > 0) _holdCount--;
    }

    /// <summary>
    /// Server-only. Drags the enemy <paramref name="worldDistance"/> back down its own lane. Pulling toward
    /// the tower is not expressible here - enemies are pinned to a path, not free agents - so an Ancora's
    /// pull is backwards ALONG that path, which produces the same effect the card is after: the wave
    /// bunches up behind the one that got yanked.
    /// </summary>
    /// <remarks>
    /// Writes <see cref="PathProgress"/> directly instead of leaving it to the throttle in Update, which
    /// only ever syncs an INCREASE - a pull that just lowered _localProgress would never reach the clients.
    /// </remarks>
    public void PullBack(float worldDistance)
    {
        if (!IsServer) return;
        if (worldDistance <= 0f || _path == null) return;

        float totalLength = _path.TotalLength;
        if (totalLength <= 0f) return;

        _localProgress = Mathf.Max(0f, _localProgress - worldDistance / totalLength);
        _pathProgress.Value = _localProgress;

        float sampleT = _reversed.Value ? 1f - _localProgress : _localProgress;
        transform.position = _path.SamplePosition(sampleT);
    }

    /// <summary>
    /// Server-only. Adds <paramref name="percent"/> (a fraction, 0.4 = -40% speed) to this enemy's stacked
    /// slow. Independent sources - a Prism aura and a Rift zone, or two overlapping Rifts - accumulate, and
    /// each removes only what it added. The total is capped by <see cref="MaxSlowPercent"/>.
    /// Enemies flagged <see cref="EnemyDataSO.ImmuneToSlow"/> silently ignore the call so callers do not
    /// need special-cases and RemoveSlow stays a symmetric no-op.
    /// </summary>
    public void AddSlow(float percent)
    {
        if (!IsServer) return;
        if (enemyManager.Data.ImmuneToSlow) return;
        _slowPercent += percent;
        RecalculateSpeed();
    }

    /// <summary>
    /// Server-only. Removes a previously-applied slow contribution, clamped at 0 so a stray double-remove
    /// can never make the enemy faster than its base speed. No-op on slow-immune enemies, mirroring
    /// <see cref="AddSlow"/> so the source's paired Add/Remove stays balanced without branching per enemy.
    /// </summary>
    public void RemoveSlow(float percent)
    {
        if (!IsServer) return;
        if (enemyManager.Data.ImmuneToSlow) return;
        _slowPercent = Mathf.Max(0f, _slowPercent - percent);
        RecalculateSpeed();
    }
}
