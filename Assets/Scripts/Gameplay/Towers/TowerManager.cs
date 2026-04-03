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
    private ServerTowerCombat serverTowerCombat;
    [SerializeField, Required]
    private ClientTowerCombat clientTowerCombat;
    [SerializeField, Required]
    private EntityTeam entityTeam;

    public TowerDataSO Data => towerDataSO;
    public NetworkObject NetworkObject => networkObject;
    public ServerTowerCombat ServerCombat => serverTowerCombat;
    public ClientTowerCombat ClientCombat => clientTowerCombat;
    public EntityTeam Team => entityTeam;
}
