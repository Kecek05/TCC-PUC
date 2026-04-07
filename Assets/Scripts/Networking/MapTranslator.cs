using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MapTranslator : NetworkBehaviour
{
    public static MapTranslator Instance { get; private set; }

    [SerializeField] private float mapOffset = 10f;
    [SerializeField] private Transform player1Map;
    [SerializeField] private Transform player2Map;

    private bool _playerRedInitialized = false;
    private bool _playerBlueInitialized = false;
    
    private bool _isInitialized = false;
    
    private bool _needsTranslation;
    public bool IsInitialized =>  _isInitialized;
    
    public bool BothPlayersInitialized => _playerRedInitialized && _playerBlueInitialized;

    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        StartCoroutine(HandleSpawn());
    }

    private IEnumerator HandleSpawn()
    {
        // Dedicated server never translates
        yield return new WaitUntil(() => 
            GameFlowManager.Instance != null &&
            GameFlowManager.CurrentGameState.Value == GameState.InMatch);
        
        if (IsServer && !IsClient)
        {
            _isInitialized = true;
            yield break;
        }

        _needsTranslation = TeamManager.Instance.GetLocalTeam() == TeamType.Blue;
        
        if (_needsTranslation)
            RepositionSceneObjects();

        _isInitialized = true;
        
        InitializeTeamServerRpc(TeamManager.Instance.GetLocalTeam());
    }

    [Rpc(SendTo.Server)]
    private void InitializeTeamServerRpc(TeamType teamType)
    {
        if  (teamType == TeamType.Blue)
            _playerBlueInitialized = true;
        else if   (teamType == TeamType.Red)
            _playerRedInitialized = true;
        else 
            Debug.LogError("Team Type not supported");
    }

    public Vector3 LocalToServer(Vector3 localPos)
    {
        if (!_needsTranslation) return localPos;
        return new Vector3(localPos.x,  localPos.y + mapOffset, localPos.z);
    }

    public Vector3 ServerToLocal(Vector3 serverPos, TeamType teamType)
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