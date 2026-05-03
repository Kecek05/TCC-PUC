using UnityEngine;

public class GameDebugManager : MonoBehaviour
{
    private PlayersDataManager_SinglePlayerDEBUG playersDataManagerDEBUG;
    [SerializeField] private DebugHand debugHand;
    [SerializeField] private EnemyManager[] debugEnemiesInMap;
    [SerializeField] WaypointPath waypointPath;
    public void Awake()
    {
        playersDataManagerDEBUG = new PlayersDataManager_SinglePlayerDEBUG();
        playersDataManagerDEBUG.DebugHand = debugHand;
        
        ServiceLocator.Register<BasePlayersDataManager>(playersDataManagerDEBUG);

        foreach (EnemyManager enemyManager in debugEnemiesInMap)
        {
            enemyManager.Team.SetTeamType(TeamType.Red);
            enemyManager.ServerMovement.Initialize(waypointPath);
        }
    }
}
