using System;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseClientTowerCombat : NetworkBehaviour
{
    public event Action OnBulletFired;
    public event Action<int> OnTowerLevelChanged;
    public event Action<bool> OnFrozenChanged;
    public event Action<bool> OnHasteChanged;
    
    [Title("References")]
    [SerializeField] protected BaseServerTowerCombat serverTowerCombat;

    // Runtime cache (not serialized); protected so concrete combats (e.g. ClientSlamTowerCombat) reuse it
    // instead of shadowing it with a same-named field, which Unity rejects as a duplicate serialized name.
    protected TowerManager _towerManager;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        serverTowerCombat.TowerLevel.OnValueChanged += OnTowerLevelValueChanged;
        OnTowerLevelValueChanged(0, serverTowerCombat.TowerLevel.Value);

        serverTowerCombat.IsFrozen.OnValueChanged += OnIsFrozenChanged;
        OnIsFrozenChanged(false, serverTowerCombat.IsFrozen.Value);

        serverTowerCombat.IsHasted.OnValueChanged += OnHastedChanged;
        OnHastedChanged(false, serverTowerCombat.IsHasted.Value);

        // Client-visible towers self-register so the shared range indicator can resolve taps.
        _towerManager = GetComponent<TowerManager>();
        ClientTowerRegistry.Register(_towerManager);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        ClientTowerRegistry.Unregister(_towerManager);

        if (serverTowerCombat == null) return;

        serverTowerCombat.TowerLevel.OnValueChanged -= OnTowerLevelValueChanged;
        serverTowerCombat.IsFrozen.OnValueChanged -= OnIsFrozenChanged;
        serverTowerCombat.IsHasted.OnValueChanged -= OnHastedChanged;
    }
    
    protected void TriggerOnBulletFired() => OnBulletFired?.Invoke();

    protected virtual void OnTowerLevelValueChanged(int previousValue, int newValue)
    {
        OnTowerLevelChanged?.Invoke(newValue);
    }

    protected virtual void OnIsFrozenChanged(bool previousValue, bool newValue)
    {
        OnFrozenChanged?.Invoke(newValue);
    }

    protected virtual void OnHastedChanged(bool previousValue, bool newValue)
    {
        OnHasteChanged?.Invoke(newValue);
    }
}
