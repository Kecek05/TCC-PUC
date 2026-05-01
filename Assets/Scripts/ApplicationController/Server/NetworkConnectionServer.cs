using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkConnectionServer : IDisposable, IOnPlayerConnected, IOnPlayerLoaded
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
    private PlayersDataManager _playersDataManager;

    public NetworkConnectionServer(NetworkManager networkManager, PlayersDataManager  playersDataManager)
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

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
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
        if (_networkManager != null)
        {
            _networkManager.ConnectionApprovalCallback -= ApprovalCheck;
            _networkManager.OnServerStarted -= NetworkManager_OnServerStarted;

            if(_networkManager.SceneManager != null)
                _networkManager.SceneManager.OnLoadComplete -= SceneManager_OnLoadComplete;
            
            _networkManager.OnClientConnectedCallback -= NetworkManager_OnClientConnectedCallback;
        }

        if (_networkManager.IsListening)
        {
            _networkManager.Shutdown();
        }
    }
}
