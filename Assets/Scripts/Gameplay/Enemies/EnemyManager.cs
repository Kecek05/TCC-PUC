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
    [SerializeField, Required]
    private EntityTeam entityTeam;
    [SerializeField, Required]
    private EnemyPathAssignment enemyPathAssignment;
    
    public EnemyDataSO Data => enemyData;
    public NetworkObject NetworkObject => networkObject;
    public ServerEnemyMovement ServerMovement => serverEnemyMovement;
    public ServerEnemyHealth ServerHealth => serverEnemyHealth;
    public EntityTeam Team => entityTeam;
    public EnemyPathAssignment PathAssignment => enemyPathAssignment;

    /// <summary>
    /// Multipliers from the summoning player's persistent card level (level 1 for the AI wave horde).
    /// Enemies are pooled and share one EnemyDataSO asset, so scaling has to live per instance here rather
    /// than on the data.
    /// </summary>
    public CardLevelScale CardScale { get; private set; } = CardLevelScale.One;

    /// <summary>
    /// Server-only, and must be called before <c>NetworkObject.Spawn</c>: OnNetworkSpawn is the only
    /// re-initialisation hook a recycled instance gets, and that is where the stats are read.
    /// </summary>
    public void SetCardLevelScale(CardLevelScale scale) => CardScale = scale;
}
