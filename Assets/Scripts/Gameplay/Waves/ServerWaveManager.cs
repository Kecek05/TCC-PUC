using System;
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
    private BaseTeamManager _teamManager;

    private List<EnemyManager> _redActiveEnemiesFromWave = new();
    private List<EnemyManager> _blueActiveEnemiesFromWave = new();
    
    private Dictionary<TeamType, WaveEntry> _currentWaves = new();
    private Dictionary<TeamType, int> _remainingEnemiesOfWave = new();

    private void Awake()
    {
        ServiceLocator.Register<BaseServerWaveManager>(this);
    }

    public override void OnNetworkSpawn()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        _enemyNetworkPool = ServiceLocator.Get<BaseEnemyNetworkPool>();
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        foreach (GameObject enemy in waveData.GetAllEnemyPrefabs())
        {
            _enemyNetworkPool.RegisterPrefab(enemy);
        }

        ServerEnemyHealth.OnDeath += ServerEnemyHealthOnOnDeath;
        _gameFlowManager.CurrentGameState.OnValueChanged += GameFlowManager_OnCurrentGameStateValueChanged;

        StartCoroutine(RunWaves(TeamType.Blue));
        StartCoroutine(RunWaves(TeamType.Red));
    }
    
    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseServerWaveManager>();
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
        CheckLastWaveEnded(enemyManager.Team.GetTeamType());
    }

    private void CheckLastWaveEnded(TeamType teamType)
    {
        bool isWaveEmpty = GetEnemyList(teamType).Count <= 0;
        bool isLastWave = GetCurrentWaveNetworkVariable(teamType).Value >= waveData.Waves.Count;
        if (isLastWave && isWaveEmpty)
        {
            TriggerOnTeamDefeatLastWave(teamType);
        }
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
            WaveEntry currentWave = waveData.Waves[waveIndex];
            SetCurrentWave(teamType, waveIndex + 1, currentWave);
            if (waveIndex > 0)
                yield return new WaitForSeconds(waveData.DelayBetweenWaves);

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

    public override NetworkVariable<int> GetLocalCurrentWave()
    {
        return _teamManager.GetLocalTeam() == TeamType.Blue ? BlueCurrentWave : RedCurrentWave;
    }

    public override NetworkVariable<int> GetEnemyCurrentWave()
    {
        return _teamManager.GetLocalTeam() == TeamType.Blue ? RedCurrentWave : BlueCurrentWave;
    }

    private void SetCurrentWave(TeamType teamType, int wave, WaveEntry waveEntry)
    {
        _currentWaves[teamType] = waveEntry;
        _remainingEnemiesOfWave[teamType] = waveEntry.GetTotalEnemiesCount();
        
        GetCurrentWaveProgressNetworkVariable(teamType).Value = 0f;
        GetCurrentWaveNetworkVariable(teamType).Value = wave;
    }

    private void AddEnemyToList(TeamType team, EnemyManager enemy)
    {
        GetEnemyList(team).Add(enemy);
    }

    private void RemoveEnemyFromList(TeamType teamType, EnemyManager enemy)
    {
        if (teamType == TeamType.None)
        {
            Debug.LogError($"Trying to remove enemy from list with invalid team {teamType}");
            return;
        }
        
        if (GetEnemyList(teamType).Remove(enemy))
        {
            //Enemy is from Wave
            UpdateWaveProgress(teamType);
        }
    }
    
    private void UpdateWaveProgress(TeamType teamType)
    {
        if (!_remainingEnemiesOfWave.ContainsKey(teamType))
        {
            Debug.LogError($"Trying to update wave progress for team {teamType} that  doesn't exist");
            return;
        }
        
        _remainingEnemiesOfWave[teamType]--;

        int total = _currentWaves[teamType].GetTotalEnemiesCount();
        int killed = total - _remainingEnemiesOfWave[teamType];
        GetCurrentWaveProgressNetworkVariable(teamType).Value = (float)killed / total;
    }
    
    private List<EnemyManager> GetEnemyList(TeamType team)
    {
        return team == TeamType.Blue ? _blueActiveEnemiesFromWave : _redActiveEnemiesFromWave;
    }
    
    private NetworkVariable<float> GetCurrentWaveProgressNetworkVariable(TeamType teamType)
    {
        return teamType == TeamType.Blue ? BlueCurrentWaveProgress : RedCurrentWaveProgress;
    }
    
    private NetworkVariable<int> GetCurrentWaveNetworkVariable(TeamType teamType)
    {
        return teamType == TeamType.Blue ? BlueCurrentWave : RedCurrentWave;
    }
}
