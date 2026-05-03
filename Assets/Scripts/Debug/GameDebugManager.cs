using System;
using UnityEngine;

public class GameDebugManager : MonoBehaviour
{
    [Serializable]
    private class EnemyDebug
    {
        public EnemyManager EnemyManager;
        public bool IsInverted = false;
    }
    
    private PlayersDataManager_SinglePlayerDEBUG playersDataManagerDEBUG;
    [SerializeField] private DebugHand debugHand;
    [SerializeField] private EnemyDebug[] debugEnemiesInMap;
    [SerializeField] WaypointPath waypointPath;
    public void Awake()
    {
        playersDataManagerDEBUG = new PlayersDataManager_SinglePlayerDEBUG();
        playersDataManagerDEBUG.DebugHand = debugHand;
        
        ServiceLocator.Register<BasePlayersDataManager>(playersDataManagerDEBUG);

        foreach (EnemyDebug enemyDebug in debugEnemiesInMap)
        {
            enemyDebug.EnemyManager.Team.SetTeamType(TeamType.Red);
            enemyDebug.EnemyManager.ServerMovement.Initialize(waypointPath, enemyDebug.IsInverted);
        }
    }
}
