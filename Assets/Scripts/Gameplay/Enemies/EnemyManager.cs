using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [SerializeField, Required, Title("References")]
    private EnemyDataSO enemyData;
    [SerializeField, Required]
    private NetworkObject networkObject;
    [SerializeField, Required]
    private ServerEnemyMovement serverEnemyMovement;
    [SerializeField, Required]
    private ServerEnemyHealth serverEnemyHealth;
    
    public EnemyDataSO Data => enemyData;
    public NetworkObject NetworkObject => networkObject;
    public ServerEnemyMovement ServerMovement => serverEnemyMovement;
    public ServerEnemyHealth ServerHealth => serverEnemyHealth;
}
