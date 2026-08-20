using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class ServerWaveManager : BaseServerWaveManager
{
    [Title("Wave Configuration")]
    [SerializeField, Required] private WaveDataSO waveData;
    [SerializeField, Required]
    private EnemyDataListSO enemyDataListSO;

    [Title("Randomization")]
    [Tooltip("Off (default): a fresh random wave sequence each match. On: a reproducible sequence from the seed " +
             "below. Either way both players always face the identical sequence.")]
    [SerializeField] private bool useFixedSeed;
    [SerializeField, ShowIf(nameof(useFixedSeed))] private int seed = 12345;

    [Title("Paths (one per map)")]
    [SerializeField, Required] private WaypointPath blueMapPath;
    [SerializeField, Required] private WaypointPath redMapPath;

    private BaseGameFlowManager _gameFlowManager;
    private BaseEnemyNetworkPool _enemyNetworkPool;
    private BaseTeamManager _teamManager;

    private List<EnemyManager> _redActiveEnemiesFromWave = new();
    private List<EnemyManager> _blueActiveEnemiesFromWave = new();
    
    private List<ResolvedWave> _resolvedWaves;
    private Dictionary<TeamType, ResolvedWave> _currentWaves = new();
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

        // Roll every wave range ONCE here, on the server. Both teams read this same plan, so the two maps
        // always get the exact same enemies, counts, spawn intervals and inter-wave delays.
        System.Random rng = new(useFixedSeed ? seed : Environment.TickCount);
        _resolvedWaves = waveData.ResolveWaves(rng);

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

        for (int waveIndex = 0; waveIndex < _resolvedWaves.Count; waveIndex++)
        {
            ResolvedWave currentWave = _resolvedWaves[waveIndex];
            SetCurrentWave(teamType, waveIndex + 1, currentWave);
            if (currentWave.DelayBeforeWave > 0f)
                yield return new WaitForSeconds(currentWave.DelayBeforeWave);

            // Spawn all enemies of this wave. Each enemy line uses ITS OWN spawn interval, so different enemy
            // types in the same wave can spawn at different cadences. No extra delay is inserted between lines.
            foreach (ResolvedWaveEnemy waveEnemy in currentWave.Enemies)
            {
                for (int i = 0; i < waveEnemy.Count; i++)
                {
                    SpawnEnemy(waveEnemy.EnemyData, teamType);
                    if (i < waveEnemy.Count - 1)
                        yield return new WaitForSeconds(waveEnemy.SpawnInterval);
                }
            }

            yield return new WaitUntil(() => GetEnemyList(teamType).Count <= 0);
        }
    }

    public override void SpawnEnemy(EnemyDataSO enemyData, TeamType targetTeam, bool fromPlayer = false,
        CardLevelScale? cardScale = null)
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

        // Before Spawn, like every other pre-spawn write here: OnNetworkSpawn is where the stats are read.
        enemyManager.SetCardLevelScale(cardScale ?? MatchCardLevels.WaveScale());

        if (!fromPlayer)
            AddEnemyToList(targetTeam, enemyManager);

        enemyManager.NetworkObject.Spawn();
    }

    public override void SendEnemyFromPlayer(EnemyType enemyType, string senderAuthId,
        CardLevelScale? cardScale = null)
    {
        TeamType senderTeam = _teamManager.GetTeam(senderAuthId);

        // Send enemy to the OPPONENT's map
        TeamType targetMap = senderTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;

        EnemyDataSO enemyData = enemyDataListSO.GetEnemyDataByType(enemyType);
        if (enemyData == null) return;

        // targetMap is the VICTIM's map, so the scale has to be passed in - it cannot be derived here.
        SpawnEnemy(enemyData, targetMap, true, cardScale);
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

    public override int GetTotalWaves() => waveData.Waves.Count;

    private void SetCurrentWave(TeamType teamType, int wave, ResolvedWave waveEntry)
    {
        _currentWaves[teamType] = waveEntry;
        _remainingEnemiesOfWave[teamType] = waveEntry.GetTotalEnemiesCount();

        GetCurrentWaveProgressNetworkVariable(teamType).Value = 0f;
        GetCurrentWaveNetworkVariable(teamType).Value = wave;

        TriggerOnNewWave(teamType, wave);
    }

    private void AddEnemyToList(TeamType team, EnemyManager enemy)
    {
        GetEnemyList(team).Add(enemy);
    }

    private void RemoveEnemyFromList(TeamType teamType, EnemyManager enemy)
    {
        if (teamType == TeamType.None)
        {
            GameLog.Error($"Trying to remove enemy from list with invalid team {teamType}");
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
            GameLog.Error($"Trying to update wave progress for team {teamType} that  doesn't exist");
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
        return teamType == TeamType.Blue ? BlueCurrentWaveProgressNormalized : RedCurrentWaveProgressNormalized;
    }
    
    private NetworkVariable<int> GetCurrentWaveNetworkVariable(TeamType teamType)
    {
        return teamType == TeamType.Blue ? BlueCurrentWave : RedCurrentWave;
    }
}
