using Unity.Netcode;
using UnityEngine;

public class ClientEnemyHealth : NetworkBehaviour
{
    [SerializeField] private EnemyHealthBar healthBar;

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

        // Initialize health bar with max health from data
        if (healthBar != null)
        {
            var data = GetComponent<EnemyDataHolder>().EnemyData;
            healthBar.Initialize(transform, data.MaxHealth);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_serverHealth != null)
            _serverHealth.CurrentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        if (healthBar != null)
            healthBar.SetHealth(newValue);

        // TODO: Play hit VFX (Feel/MMFeedbacks) when newValue < previousValue
    }
}
