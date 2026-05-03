using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class TowerManager : MonoBehaviour
{
    [SerializeField, Required, Title("References")]
    private TowerDataSO towerDataSO;
    [SerializeField, Required]
    private NetworkObject networkObject;
    [SerializeField, Required]
    private BaseServerTowerCombat serverTowerCombat;
    [SerializeField, Required]
    private BaseClientTowerCombat clientTowerCombat;
    [SerializeField, Required]
    private EntityTeam entityTeam;

    public TowerDataSO Data => towerDataSO;
    public NetworkObject NetworkObject => networkObject;
    public BaseServerTowerCombat ServerTowerCombat => serverTowerCombat;
    public BaseClientTowerCombat ClientTowerCombat => clientTowerCombat;
    public EntityTeam Team => entityTeam;
}
