using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only: manages PvPvE wave spawning.
/// - PvE: automated waves attack each player's own base on independent timers
/// - PvP: players can send extra enemies to the opponent's map via RPC
/// Spawns enemies as NetworkObjects and initializes their path + components.
/// </summary>
public class ServerWaveManager : NetworkBehaviour
{
    public static ServerWaveManager Instance { get; private set; }

    [Title("Wave Configuration")]
    [SerializeField] private WaveDataSO waveData;

    [Title("Paths (one per map)")]
    [SerializeField] private WaypointPath blueMapPath;
    [SerializeField] private WaypointPath redMapPath;

    [Title("Synced State")]
    private NetworkVariable<int> _blueCurrentWave = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _redCurrentWave = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _blueWaveTimer = new(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<float> _redWaveTimer = new(writePerm: NetworkVariableWritePermission.Server);

    public NetworkVariable<int> BlueCurrentWave => _blueCurrentWave;
    public NetworkVariable<int> RedCurrentWave => _redCurrentWave;
    public NetworkVariable<float> BlueWaveTimer => _blueWaveTimer;
    public NetworkVariable<float> RedWaveTimer => _redWaveTimer;

    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        foreach (GameObject enemy in waveData.GetAllEnemyPrefabs())
        {
            EnemyNetworkPool.Instance.RegisterPrefab(enemy);
        }
        
        StartCoroutine(RunWaves(TeamType.Blue));
        StartCoroutine(RunWaves(TeamType.Red));
    }

    private IEnumerator RunWaves(TeamType map)
    {
        // Initial delay
        float timer = waveData.InitialDelay;
        while (timer > 0f)
        {
            SetWaveTimer(map, timer);
            timer -= Time.deltaTime;
            yield return null;
        }
        SetWaveTimer(map, 0f);

        for (int w = 0; w < waveData.Waves.Count; w++)
        {
            SetCurrentWave(map, w + 1);
            var wave = waveData.Waves[w];

            // Spawn all enemies in this wave

            for (int i = 0; i < wave.count; i++)
            {
                SpawnEnemy(wave.enemyData, map);
                if (i < wave.count - 1)
                    yield return new WaitForSeconds(wave.spawnInterval);
            }

            // Delay between waves
            if (w < waveData.Waves.Count - 1)
            {
                timer = waveData.DelayBetweenWaves;
                while (timer > 0f)
                {
                    SetWaveTimer(map, timer);
                    timer -= Time.deltaTime;
                    yield return null;
                }
                SetWaveTimer(map, 0f);
            }
        }
    }

    /// <summary>
    /// Spawns an enemy on the specified map's path.
    /// Used by both PvE waves and PvP send-enemy mechanic.
    /// </summary>
    public void SpawnEnemy(EnemyDataSO enemyData, TeamType targetTeam)
    {
        if (!IsServer) return;

        WaypointPath path = GetPath(targetTeam);
        if (path == null || path.WaypointCount < 2) return;

        Vector3 spawnPos = path.SamplePosition(0f);
        GameObject enemyObj = Instantiate(enemyData.EnemyPrefab, spawnPos, Quaternion.identity);

        EnemyManager enemyManager = enemyObj.GetComponent<EnemyManager>();

        enemyManager.ServerMovement.Initialize(path);
        
        enemyManager.PathAssignment.SetTargetMap(targetTeam);
        
        enemyManager.Team.SetTeamType(targetTeam);
        
        enemyManager.NetworkObject.Spawn();

    }

    /// <summary>
    /// PvP: player requests to send an enemy to their opponent's map.
    /// Server validates and spawns on the opponent's map.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSendEnemyServerRpc(int enemyId, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        TeamType senderTeam = TeamManager.Instance.GetTeam(clientId);

        // Send enemy to the OPPONENT's map
        TeamType targetMap = senderTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;

        // TODO: Validate cost/cooldown for sending enemies
        EnemyDataSO enemyData = FindEnemyDataById(enemyId);
        if (enemyData == null) return;

        SpawnEnemy(enemyData, targetMap);
    }

    public WaypointPath GetPath(TeamType map)
    {
        return map == TeamType.Blue ? blueMapPath : redMapPath;
    }

    private void SetCurrentWave(TeamType map, int wave)
    {
        if (map == TeamType.Blue)
            _blueCurrentWave.Value = wave;
        else
            _redCurrentWave.Value = wave;
    }

    private void SetWaveTimer(TeamType map, float time)
    {
        if (map == TeamType.Blue)
            _blueWaveTimer.Value = time;
        else
            _redWaveTimer.Value = time;
    }

    private EnemyDataSO FindEnemyDataById(int id)
    {
        // Search through wave data for matching enemy
        foreach (var wave in waveData.Waves)
        {
            if (wave.enemyData != null && wave.enemyData.EnemyId == id)
                return wave.enemyData;
        }
        return null;
    }
}
