using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.Serialization;

public abstract class BaseServerWaveManager : NetworkBehaviour
{
    public event Action<TeamType> OnTeamDefeatLastWave;

    /// <summary>Per lane, the first wave it may not start. Absent means the lane runs every wave.</summary>
    private readonly Dictionary<TeamType, int> _holdBeforeWave = new();

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

    /// <summary>
    /// Server-only. Holds <paramref name="lane"/> before wave <paramref name="waveNumber"/>: it runs every
    /// wave up to that one and then waits, never starting it. 0 lifts the hold.
    /// </summary>
    /// <remarks>
    /// A lane that never starts its last wave can never clear it, and clearing it is how a lane wins the
    /// race (<see cref="OnTeamDefeatLastWave"/>). So holding the opponent's last wave hands the race to the
    /// other side while leaving the rest of a normal match intact — how the tutorial's first match cannot
    /// be lost to the bot outpacing a new player, without the bot's lane ever looking empty before then.
    /// </remarks>
    public void HoldLaneBeforeWave(TeamType lane, int waveNumber)
    {
        if (waveNumber > 0) _holdBeforeWave[lane] = waveNumber;
        else _holdBeforeWave.Remove(lane);
    }

    /// <summary>Whether <paramref name="lane"/> must wait before starting <paramref name="waveNumber"/>.</summary>
    protected bool IsLaneHeld(TeamType lane, int waveNumber) =>
        _holdBeforeWave.TryGetValue(lane, out int heldAt) && waveNumber >= heldAt;

    protected void TriggerOnTeamDefeatLastWave(TeamType teamType) => OnTeamDefeatLastWave?.Invoke(teamType);
    protected void TriggerOnNewWave(TeamType teamType, int waveNumber) => OnNewWave?.Invoke(teamType, waveNumber);
}
