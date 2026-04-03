using Unity.Netcode;
using UnityEngine;

public class TowerManager : MonoBehaviour
{
    [SerializeField] private TowerDataHolder towerDataHolder;
    [SerializeField] private NetworkObject networkObject;
    [SerializeField] private ServerTowerCombat serverTowerCombat;
    [SerializeField] private ClientTowerCombat clientTowerCombat;
    [SerializeField] private EntityTeam entityTeam;
    
    public TowerDataHolder TowerDataHolder => towerDataHolder;
    public NetworkObject NetworkObject => networkObject;
    public ServerTowerCombat ServerTowerCombat => serverTowerCombat;
    public ClientTowerCombat ClientTowerCombat => clientTowerCombat;
    public EntityTeam EntityTeam => entityTeam;
}
