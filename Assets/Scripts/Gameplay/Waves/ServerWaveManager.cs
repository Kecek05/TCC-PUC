using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Server-only: manages PvPvE wave spawning.
/// - PvE: automated waves attack each player's own base on independent timers
/// - PvP: players can send extra enemies to the opponent's map via RPC
/// Spawns enemies as NetworkObjects and initializes their path + components.
/// </summary>
public class ServerWaveManager : BaseServerWaveManager
{
    [Title("Wave Configuration")]
    [SerializeField] private WaveDataSO waveData;
    [SerializeField, Required]
    private EnemyDataListSO enemyDataListSO;

    [Title("Paths (one per map)")]
    [SerializeField] private WaypointPath blueMapPath;
    [SerializeField] private WaypointPath redMapPath;

    private BaseGameFlowManager _gameFlowManager;
    private BaseEnemyNetworkPool _enemyNetworkPool;

    private List<EnemyManager> _redActiveEnemies = new();
    private List<EnemyManager> _blueActiveEnemies = new() ;

    private void Awake()
    {
        ServiceLocator.Register<BaseServerWaveManager>(this);
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseServerWaveManager>();
        base.OnDestroy();
    }

    private void Start()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        _enemyNetworkPool = ServiceLocator.Get<BaseEnemyNetworkPool>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        if (_enemyNetworkPool == null)
            _enemyNetworkPool = ServiceLocator.Get<BaseEnemyNetworkPool>();

        foreach (GameObject enemy in waveData.GetAllEnemyPrefabs())
        {
            _enemyNetworkPool.RegisterPrefab(enemy);
        }

        ServerEnemyHealth.OnDeath += ServerEnemyHealthOnOnDeath;
        _gameFlowManager.CurrentGameState.OnValueChanged += GameFlowManager_OnCurrentGameStateValueChanged;

        StartCoroutine(RunWaves(TeamType.Blue));
        StartCoroutine(RunWaves(TeamType.Red));
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        ServerEnemyHealth.OnDeath -= ServerEnemyHealthOnOnDeath;
        _gameFlowManager.CurrentGameState.OnValueChanged -= GameFlowManager_OnCurrentGameStateValueChanged;
        StopAllCoroutines();
    }

    private void ServerEnemyHealthOnOnDeath(EnemyManager enemyManager)
    {
        RemoveEnemyFromList(enemyManager.Team.GetTeamType(), enemyManager);
    }

    private void GameFlowManager_OnCurrentGameStateValueChanged(GameState previousValue, GameState newValue)
    {
        if (newValue == GameState.EndMatch)
        {
            StopAllCoroutines();
        }
    }

    private IEnumerator RunWaves(TeamType teamType)
    {
        yield return new WaitUntil(() =>
            _gameFlowManager != null &&
            _gameFlowManager.CurrentGameState.Value == GameState.InMatch);

        yield return new WaitForSeconds(waveData.InitialDelay);

        for (int waveIndex = 0; waveIndex < waveData.Waves.Count; waveIndex++)
        {
            SetCurrentWave(teamType, waveIndex + 1);
            if (waveIndex > 0)
                yield return new WaitForSeconds(waveData.DelayBetweenWaves);

            WaveEntry currentWave = waveData.Waves[waveIndex];

            // Spawn all enemies in this wave
            foreach (WaveEnemy waveEnemy in currentWave.waveEnemies)
            {
                for (int i = 0; i < waveEnemy.count; i++)
                {
                    SpawnEnemy(waveEnemy.enemyData, teamType);
                    if (i < waveEnemy.count - 1)
                        yield return new WaitForSeconds(currentWave.spawnInterval);
                }
            }

            yield return new WaitUntil(() => GetEnemyList(teamType).Count <= 0);
        }
    }

    public override void SpawnEnemy(EnemyDataSO enemyData, TeamType targetTeam, bool fromPlayer = false)
    {
        if (!IsServer) return;

        WaypointPath path = GetPath(targetTeam);
        if (path == null || path.WaypointCount < 2) return;

        Vector3 spawnPos = path.SamplePosition(0f);
        GameObject enemyObj = Instantiate(enemyData.EnemyPrefab, spawnPos, Quaternion.identity);

        EnemyManager enemyManager = enemyObj.GetComponent<EnemyManager>();

        enemyManager.ServerMovement.Initialize(path, fromPlayer);
        enemyManager.PathAssignment.SetTargetMap(targetTeam);
        enemyManager.Team.SetTeamType(targetTeam);

        if (!fromPlayer)
            AddEnemyToList(targetTeam, enemyManager);

        enemyManager.NetworkObject.Spawn();

    }

    public override void SendEnemyFromPlayer(EnemyType enemyType, ulong clientId)
    {
        TeamType senderTeam = ServiceLocator.Get<BaseTeamManager>().GetTeam(clientId);

        // Send enemy to the OPPONENT's map
        TeamType targetMap = senderTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;

        EnemyDataSO enemyData = enemyDataListSO.GetEnemyDataByType(enemyType);
        if (enemyData == null) return;

        SpawnEnemy(enemyData, targetMap, true);
    }

    public override WaypointPath GetPath(TeamType map)
    {
        return map == TeamType.Blue ? blueMapPath : redMapPath;
    }

    private void SetCurrentWave(TeamType map, int wave)
    {
        if (map == TeamType.Blue)
            BlueCurrentWave.Value = wave;
        else
            RedCurrentWave.Value = wave;
    }

    private void AddEnemyToList(TeamType team, EnemyManager enemy)
    {
        if (team == TeamType.Blue)
            _blueActiveEnemies.Add(enemy);
        else
            _redActiveEnemies.Add(enemy);
    }


    private List<EnemyManager> GetEnemyList(TeamType team)
    {
        return team == TeamType.Blue ? _blueActiveEnemies : _redActiveEnemies;
    }

    private void RemoveEnemyFromList(TeamType team, EnemyManager enemy)
    {
        if (team == TeamType.Blue)
            _blueActiveEnemies.Remove(enemy);
        else if  (team == TeamType.Red)
            _redActiveEnemies.Remove(enemy);
        else
            Debug.LogError($"Trying to remove enemy from list with invalid team {team}");
    }
}
