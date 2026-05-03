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
    }
    
    protected virtual void OnTowerLevelChanged(int previousValue, int newValue)
    {
        clientTowerGFX.UpgradeTower(newValue);
    }
}
