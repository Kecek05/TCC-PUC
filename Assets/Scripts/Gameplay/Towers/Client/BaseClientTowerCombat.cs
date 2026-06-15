using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseClientTowerCombat : NetworkBehaviour
{
    [Title("References")]
    [SerializeField] protected ClientTowerGFX clientTowerGFX;
    [SerializeField] protected BaseServerTowerCombat serverTowerCombat;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        serverTowerCombat.TowerLevel.OnValueChanged += OnTowerLevelChanged;
        OnTowerLevelChanged(0, serverTowerCombat.TowerLevel.Value);

        serverTowerCombat.IsFrozen.OnValueChanged += OnFrozenChanged;
        OnFrozenChanged(false, serverTowerCombat.IsFrozen.Value);

        serverTowerCombat.IsHasted.OnValueChanged += OnHastedChanged;
        OnHastedChanged(false, serverTowerCombat.IsHasted.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (serverTowerCombat == null) return;

        serverTowerCombat.TowerLevel.OnValueChanged -= OnTowerLevelChanged;
        serverTowerCombat.IsFrozen.OnValueChanged -= OnFrozenChanged;
        serverTowerCombat.IsHasted.OnValueChanged -= OnHastedChanged;
    }

    protected virtual void OnTowerLevelChanged(int previousValue, int newValue)
    {
        clientTowerGFX.UpgradeTower(newValue);
    }

    protected virtual void OnFrozenChanged(bool previousValue, bool newValue)
    {
        clientTowerGFX.SetFrozen(newValue);
    }

    protected virtual void OnHastedChanged(bool previousValue, bool newValue)
    {
        clientTowerGFX.SetHasted(newValue);
    }
}
