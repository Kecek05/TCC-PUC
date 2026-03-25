using Unity.Netcode;
using UnityEngine;

public class ClientEnemyHealth : NetworkBehaviour
{
    private ServerEnemyHealth _serverHealth;

    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        _serverHealth = GetComponent<ServerEnemyHealth>();
        _serverHealth.CurrentHealth.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (_serverHealth != null)
            _serverHealth.CurrentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        // TODO: Update health bar UI
        // TODO: Play hit VFX (Feel/MMFeedbacks)
    }
}
