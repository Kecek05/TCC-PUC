using System;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Tells every client that a Fonte just paid out, and how much, so the payout can be seen where it happened.
/// A client cannot tell a grant from ordinary regen by watching the mana value, hence one small Rpc per payout
/// (every few seconds per Fonte). Forwarding only — <see cref="TowerFeedbackPresenter"/> decides the look.
/// </summary>
public class ManaGrantFeedbackRelay : NetworkBehaviour
{
    [SerializeField, Required] private ServerFonteTowerCombat fonte;

    /// <summary>Client-side (host included): mana was granted, and how much.</summary>
    public event Action<float> OnManaGranted;

    private bool _subscribed;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        fonte.OnManaGranted += HandleManaGranted;
        _subscribed = true;
    }

    public override void OnNetworkDespawn()
    {
        if (!_subscribed) return;

        fonte.OnManaGranted -= HandleManaGranted;
        _subscribed = false;
    }

    private void HandleManaGranted(float amount) => ManaGrantedRpc(amount);

    [Rpc(SendTo.ClientsAndHost)]
    private void ManaGrantedRpc(float amount) => OnManaGranted?.Invoke(amount);
}
