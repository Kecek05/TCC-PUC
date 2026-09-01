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
    /// Splits this instance still has left in it. Seeded from <see cref="EnemyDataSO.SplitGenerations"/> at
    /// spawn and decremented for each child, so the lineage terminates without needing a separate data
    /// asset and prefab per generation.
    /// </summary>
    public int SplitGenerationsLeft { get; private set; }

    /// <summary>
    /// Compounding stat fraction inherited from being a split child (1 for anything spawned normally).
    /// Kept apart from <see cref="CardScale"/> because the two mean different things - card level is the
    /// summoning player's investment, this is how far down the split chain this particular body sits - and
    /// folding them together would make a level-5 Cisma's grandchildren indistinguishable from a level-1's.
    /// </summary>
    public float SplitStatMultiplier { get; private set; } = 1f;

    /// <summary>
    /// Server-only, and must be called before <c>NetworkObject.Spawn</c>: OnNetworkSpawn is the only
    /// re-initialisation hook a recycled instance gets, and that is where the stats are read.
    /// </summary>
    public void SetCardLevelScale(CardLevelScale scale) => CardScale = scale;

    /// <summary>
    /// Server-only, and must be called before <c>NetworkObject.Spawn</c> for the same reason as
    /// <see cref="SetCardLevelScale"/>. Pooled instances are reused, so this is always written explicitly -
    /// a fresh spawn passes the data's own generation count and a multiplier of 1.
    /// </summary>
    public void SetSplitState(int generationsLeft, float statMultiplier)
    {
        SplitGenerationsLeft = Mathf.Max(0, generationsLeft);
        SplitStatMultiplier = Mathf.Max(0.01f, statMultiplier);
    }
}
