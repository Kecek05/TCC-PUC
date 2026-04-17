using Unity.Netcode;

public abstract class BaseServerWaveManager : NetworkBehaviour
{
    public NetworkVariable<int> BlueCurrentWave = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RedCurrentWave = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> BlueCurrentWaveProgress =  new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedCurrentWaveProgress =  new(writePerm: NetworkVariableWritePermission.Server);
    public abstract void SpawnEnemy(EnemyDataSO enemyData, TeamType targetTeam, bool fromPlayer = false);
    public abstract void SendEnemyFromPlayer(EnemyType enemyType, ulong clientId);
    public abstract WaypointPath GetPath(TeamType map);
}
