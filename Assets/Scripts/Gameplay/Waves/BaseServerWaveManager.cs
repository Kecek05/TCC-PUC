using System;
using Unity.Netcode;
using UnityEngine.Serialization;

public abstract class BaseServerWaveManager : NetworkBehaviour
{
    public event Action<TeamType> OnTeamDefeatLastWave;

    /// <summary>
    /// Fired on the server when a team advances to a new wave. Payload: (team, newWaveNumber).
    /// </summary>
    public event Action<TeamType, int> OnNewWave;

    public NetworkVariable<int> BlueCurrentWave = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RedCurrentWave = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> BlueCurrentWaveProgressNormalized =  new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedCurrentWaveProgressNormalized =  new(writePerm: NetworkVariableWritePermission.Server);
    /// <param name="cardScale">
    /// Multipliers from the summoning player's card level. Null means "nobody summoned this" - the AI wave
    /// horde - which falls back to the wave level on CardProgressionSettings. Nullable rather than a
    /// default of CardLevelScale.One because a struct cannot be a compile-time default.
    /// </param>
    /// <param name="startProgress">
    /// Where on the lane the enemy enters, 0..1. Everything spawns at the mouth of the path except a split
    /// child, which inherits the spot its parent died on.
    /// </param>
    /// <param name="splitState">
    /// Generations of splitting left and the compounding stat fraction for this body. Null means "spawned
    /// normally" - the data's own generation count at full stats.
    /// </param>
    public abstract void SpawnEnemy(EnemyDataSO enemyData, TeamType targetTeam, bool fromPlayer = false,
        CardLevelScale? cardScale = null, float startProgress = 0f,
        (int generationsLeft, float statMultiplier)? splitState = null);

    public abstract void SendEnemyFromPlayer(EnemyType enemyType, string senderAuthId,
        CardLevelScale? cardScale = null);
    public abstract WaypointPath GetPath(TeamType map);
    public abstract NetworkVariable<int> GetLocalCurrentWave();
    public abstract NetworkVariable<int> GetEnemyCurrentWave();
    public abstract int GetTotalWaves();
    protected void TriggerOnTeamDefeatLastWave(TeamType teamType) => OnTeamDefeatLastWave?.Invoke(teamType);
    protected void TriggerOnNewWave(TeamType teamType, int waveNumber) => OnNewWave?.Invoke(teamType, waveNumber);
}
