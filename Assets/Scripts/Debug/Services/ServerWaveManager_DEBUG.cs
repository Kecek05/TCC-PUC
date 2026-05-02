using Unity.Netcode;

public class ServerWaveManager_DEBUG : BaseServerWaveManager
{
    private NetworkVariable<int> currentWave = new NetworkVariable<int>(1);
    
    private void Awake()
    {
        ServiceLocator.Register<BaseServerWaveManager>(this);
    }
    
    public override void SpawnEnemy(EnemyDataSO enemyData, TeamType targetTeam, bool fromPlayer = false)
    {

    }

    public override void SendEnemyFromPlayer(EnemyType enemyType, string senderAuthId)
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
}
