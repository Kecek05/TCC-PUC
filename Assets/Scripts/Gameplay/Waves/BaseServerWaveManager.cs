using System;
using Unity.Netcode;

public abstract class BaseServerWaveManager : NetworkBehaviour
{
    public event Action<TeamType> OnTeamDefeatLastWave;

    /// <summary>
    /// Fired on the server when a team advances to a new wave. Payload: (team, newWaveNumber).
    /// </summary>
    public event Action<TeamType, int> OnNewWave;

    public NetworkVariable<int> BlueCurrentWave = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RedCurrentWave = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> BlueCurrentWaveProgress =  new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedCurrentWaveProgress =  new(writePerm: NetworkVariableWritePermission.Server);
    public abstract void SpawnEnemy(EnemyDataSO enemyData, TeamType targetTeam, bool fromPlayer = false);
    public abstract void SendEnemyFromPlayer(EnemyType enemyType, ulong clientId);
    public abstract WaypointPath GetPath(TeamType map);
    public abstract NetworkVariable<int> GetLocalCurrentWave();
    public abstract NetworkVariable<int> GetEnemyCurrentWave();
    protected void TriggerOnTeamDefeatLastWave(TeamType teamType) => OnTeamDefeatLastWave?.Invoke(teamType);
    protected void TriggerOnNewWave(TeamType teamType, int waveNumber) => OnNewWave?.Invoke(teamType, waveNumber);
}
