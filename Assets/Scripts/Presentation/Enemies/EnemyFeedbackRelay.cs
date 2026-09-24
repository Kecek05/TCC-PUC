using System;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Tells every client how an enemy left the field — killed, or through the base — so the right effect plays.
/// </summary>
/// <remarks>
/// Needed because a client cannot work it out from replicated state: Netcode does not flush a despawning
/// object's last NetworkVariable changes, so a remote client never sees the killing blow take health to 0 or a
/// leaker's progress reach the end. One byte per enemy, sent while the enemy is still spawned so it arrives
/// ahead of the despawn. It is a bridge and nothing more: gameplay raises the events, this forwards them, and
/// the presenters decide what they look like.
/// </remarks>
public class EnemyFeedbackRelay : NetworkBehaviour
{
    [SerializeField, Required] private EnemyManager enemyManager;

    /// <summary>Client-side (host included): this enemy is about to disappear, and why.</summary>
    public event Action<EnemyExitKind> OnExit;

    private bool _subscribed;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        enemyManager.ServerHealth.OnKilled += HandleKilled;
        enemyManager.ServerMovement.OnReachedBase += HandleReachedBase;
        _subscribed = true;
    }

    public override void OnNetworkDespawn()
    {
        if (!_subscribed) return;

        enemyManager.ServerHealth.OnKilled -= HandleKilled;
        enemyManager.ServerMovement.OnReachedBase -= HandleReachedBase;
        _subscribed = false;
    }

    private void HandleKilled(EnemyManager _) => ExitRpc(EnemyExitKind.Killed);
    private void HandleReachedBase(EnemyManager _) => ExitRpc(EnemyExitKind.ReachedBase);

    [Rpc(SendTo.ClientsAndHost)]
    private void ExitRpc(EnemyExitKind kind) => OnExit?.Invoke(kind);
}
