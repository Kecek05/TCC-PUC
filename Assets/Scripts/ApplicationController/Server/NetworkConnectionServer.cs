using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkConnectionServer : IDisposable, IOnPlayerConnected, IOnPlayerLoaded, IMatchAdmission
{
    /// <summary>
    /// Arg: AuthId
    /// </summary>
    public event Action<string> OnPlayerLoaded;
    /// <summary>
    /// Arg: AuthId
    /// </summary>
    public event Action<string> OnClientConnected;
    public event Action<OnCardPlayerConnectedEventArgs> OnPlayerConnected;
    
    private NetworkManager _networkManager;
    private BasePlayersDataManager _playersDataManager;

    // Once false, ApprovalCheck rejects new clients (the match is committed, e.g. a bot filled the slot).
    private bool _acceptingPlayers = true;

    public NetworkConnectionServer(NetworkManager networkManager, BasePlayersDataManager playersDataManager)
    {
        _networkManager  = networkManager;
        _playersDataManager =  playersDataManager;

        _networkManager.ConnectionApprovalCallback += ApprovalCheck;

        _networkManager.OnServerStarted += NetworkManager_OnServerStarted;
    }

    private void NetworkManager_OnServerStarted()
    {
        _networkManager.SceneManager.OnLoadComplete += SceneManager_OnLoadComplete;
        _networkManager.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
    }

    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        GameLog.Info($"Client connected: {clientId} - {_playersDataManager.GetAuthIdByClientId(clientId)}");
        OnClientConnected?.Invoke(_playersDataManager.GetAuthIdByClientId(clientId));
    }

    // IMatchAdmission: stop admitting new clients once the match is committed (e.g. a bot took the slot).
    public void StopAcceptingPlayers()
    {
        _acceptingPlayers = false;
        GameLog.Info("NetworkConnectionServer: no longer accepting new players (match committed).");
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (!_acceptingPlayers)
        {
            GameLog.Info($"ApprovalCheck: match already started; rejecting late joiner {request.ClientNetworkId}.");
            response.Approved = false;
            response.Reason = "Match already started";
            return;
        }

        if (request.Payload == null || request.Payload.Length == 0)
        {
            GameLog.Error($"ApprovalCheck: empty payload from client {request.ClientNetworkId}. Rejecting.");
            response.Approved = false;
            response.Reason = "Empty connection payload";
            return;
        }
        
        UserData userData = UserData.TranslateFromBytes(request.Payload);

        if (userData == null)
        {
            GameLog.Error($"ApprovalCheck: payload from client {request.ClientNetworkId} deserialized to null UserData. Rejecting.");
            response.Approved = false;
            response.Reason = "Invalid connection payload";
            return;
        }

        GameLog.Info($"ApprovalCheck, Name: {userData.PlayerName}, Trophies: {userData.UserTrophies}, AuthId: {userData.PlayerAuthId}");

        OnPlayerConnected?.Invoke(new OnCardPlayerConnectedEventArgs()
        {
            UserData = userData,
            ClientId = request.ClientNetworkId,
        });
        
        response.CreatePlayerObject = false;
        response.Approved = true;
    }

    private void SceneManager_OnLoadComplete(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
    {
        if(sceneName != Loader.Scene.GameScene.ToString()) return; //Only Spawn players in Game Scene
        
        OnPlayerLoaded?.Invoke(_playersDataManager.GetAuthIdByClientId(clientId));
    }

    public void Dispose()
    {
        // Unsubscribe only. Stopping the NetworkManager is owned by HostManager
        // (single, explicit shutdown ordering), not by this event wrapper.
        if (_networkManager != null)
        {
            _networkManager.ConnectionApprovalCallback -= ApprovalCheck;
            _networkManager.OnServerStarted -= NetworkManager_OnServerStarted;

            if(_networkManager.SceneManager != null)
                _networkManager.SceneManager.OnLoadComplete -= SceneManager_OnLoadComplete;

            _networkManager.OnClientConnectedCallback -= NetworkManager_OnClientConnectedCallback;
        }
    }
}
