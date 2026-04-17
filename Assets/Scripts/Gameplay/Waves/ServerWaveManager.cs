using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

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

    private List<EnemyManager> _redActiveEnemiesFromWave = new();
    private List<EnemyManager> _blueActiveEnemiesFromWave = new();
    
    private Dictionary<TeamType, WaveEntry> _currentWaves = new();

    private void Awake()
    {
        ServiceLocator.Register<BaseServerWaveManager>(this);
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseServerWaveManager>();
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
            _currentWaves[teamType] = currentWave;

            // Spawn all enemies of this wave
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
        {
            BlueCurrentWave.Value = wave;
            BlueCurrentWaveProgress.Value = 0f;
        }
        else
        {
            RedCurrentWave.Value = wave;
            RedCurrentWaveProgress.Value = 0f;
        }
    }

    private void AddEnemyToList(TeamType team, EnemyManager enemy)
    {
        if (team == TeamType.Blue)
            _blueActiveEnemiesFromWave.Add(enemy);
        else
            _redActiveEnemiesFromWave.Add(enemy);
    }


    private List<EnemyManager> GetEnemyList(TeamType team)
    {
        return team == TeamType.Blue ? _blueActiveEnemiesFromWave : _redActiveEnemiesFromWave;
    }

    private void RemoveEnemyFromList(TeamType teamType, EnemyManager enemy)
    {
        if (teamType == TeamType.Blue)
        {
            if (_blueActiveEnemiesFromWave.Remove(enemy))
            {
                //Enemy was from wave, update Progress
                UpdateWaveProgress(teamType);
            }
        }
        else if (teamType == TeamType.Red)
        {
            if (_redActiveEnemiesFromWave.Remove(enemy))
            {
                //Enemy was from wave, update Progress
                UpdateWaveProgress(teamType);
            }
        }
        else
            Debug.LogError($"Trying to remove enemy from list with invalid team {teamType}");
    }

    private void UpdateWaveProgress(TeamType teamType)
    {
        int enemiesInWave = 0;
        foreach (WaveEnemy waveEnemy in _currentWaves[teamType].waveEnemies)
        {
            enemiesInWave += waveEnemy.count;
        }
            
        int remainingEnemies = GetEnemyList(teamType).Count;
        int killedEnemies = enemiesInWave - remainingEnemies;

        GetCurrentWaveProgressNetworkVariable(teamType).Value = (float)killedEnemies / enemiesInWave;
    }
    
    private NetworkVariable<float> GetCurrentWaveProgressNetworkVariable(TeamType teamType)
    {
        return teamType == TeamType.Blue ? BlueCurrentWaveProgress : RedCurrentWaveProgress;
    }
}
