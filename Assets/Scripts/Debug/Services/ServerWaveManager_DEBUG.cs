using Unity.Netcode;

public class ServerWaveManager_DEBUG : BaseServerWaveManager
{
    private NetworkVariable<int> currentWave = new NetworkVariable<int>(1);
    
    private void Awake()
    {
        ServiceLocator.Register<BaseServerWaveManager>(this);
    }
    
    public override void SpawnEnemy(EnemyDataSO enemyData, TeamType targetTeam, bool fromPlayer = false,
        CardLevelScale? cardScale = null, float startProgress = 0f,
        (int generationsLeft, float statMultiplier)? splitState = null)
    {

    }

    public override void SendEnemyFromPlayer(EnemyType enemyType, string senderAuthId,
        CardLevelScale? cardScale = null)
    {

    }

    public override WaypointPath GetPath(TeamType map) {
        return null;
    }

    public override NetworkVariable<int> GetLocalCurrentWave()
    {
        return currentWave;
    }

    public override NetworkVariable<int> GetEnemyCurrentWave()
    {
        return currentWave;
    }

    public override int GetTotalWaves() => 0;
}
