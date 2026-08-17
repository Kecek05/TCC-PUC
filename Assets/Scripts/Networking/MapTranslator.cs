using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class MapTranslator : BaseMapTranslator
{
    [SerializeField] private float mapOffset = 10f;
    [SerializeField, Required] private Transform player1Map;
    [SerializeField, Required] private Transform player2Map;

    private bool _playerRedInitialized = false;
    private bool _playerBlueInitialized = false;

    private bool _isInitialized = false;

    private bool _needsTranslation;

    private BaseGameFlowManager _gameFlowManager;
    private BaseTeamManager _teamManager;

    public override bool IsInitialized => _isInitialized;

    public override bool BothPlayersInitialized => _playerRedInitialized && _playerBlueInitialized;

    private void Awake()
    {
        ServiceLocator.Register<BaseMapTranslator>(this);
    }

    public override void OnDestroy()
    {
        ServiceLocator.Unregister<BaseMapTranslator>();
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        StartCoroutine(HandleSpawn());
    }

    private IEnumerator HandleSpawn()
    {
        // Dedicated server never translates
        yield return new WaitUntil(() =>
            _gameFlowManager != null &&
            _gameFlowManager.CurrentGameState.Value == GameState.LoadingMatch);

        if (IsServer && !IsClient)
        {
            _isInitialized = true;
            yield break;
        }

        _needsTranslation = _teamManager.GetLocalTeam() == TeamType.Blue;

        if (_needsTranslation)
            RepositionSceneObjects();

        _isInitialized = true;

        InitializeTeamServerRpc(_teamManager.GetLocalTeam());
    }

    [Rpc(SendTo.Server)]
    private void InitializeTeamServerRpc(TeamType teamType)
    {
        MarkPlayerInitialized(teamType);
    }

    // Server-side: set a team's init flag directly. A real client reaches this via InitializeTeamServerRpc;
    // a bot (no client) is marked here by the BotController so the FSM leaves LoadingMatch.
    public override void MarkPlayerInitialized(TeamType team)
    {
        if (team == TeamType.Blue)
            _playerBlueInitialized = true;
        else if (team == TeamType.Red)
            _playerRedInitialized = true;
        else
            GameLog.Error("Team Type not supported");
    }

    public override Vector3 LocalToServer(Vector3 localPos)
    {
        if (!_needsTranslation) return localPos;
        return new Vector3(localPos.x,  localPos.y + mapOffset, localPos.z);
    }

    // Team-aware inverse of ServerToLocal: a local point on teamType's field maps back to that
    // team's server-space side. Needed for enemy-field spells (the single-arg overload above
    // assumes the caster's own field, which is wrong when targeting the opponent's map).
    public override Vector3 LocalToServer(Vector3 localPos, TeamType teamType)
    {
        if (!_needsTranslation) return localPos;
        float y = teamType == TeamType.Blue ? localPos.y + mapOffset : localPos.y - mapOffset;
        return new Vector3(localPos.x, y, localPos.z);
    }

    public override Vector3 ServerToLocal(Vector3 serverPos, TeamType teamType)
    {
        if (!_needsTranslation) return serverPos;
        return new Vector3(serverPos.x, teamType == TeamType.Blue ? serverPos.y - mapOffset : serverPos.y + mapOffset, serverPos.z);
    }

    private void RepositionSceneObjects()
    {
        player1Map.transform.position = new Vector2(player1Map.transform.position.x, mapOffset);
        player2Map.transform.position = new Vector2(player2Map.transform.position.x, 0f);
    }
}
