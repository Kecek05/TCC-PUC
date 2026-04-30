using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkServer : IDisposable
{
    private NetworkManager networkManager;
    private PlayersDataManager _playersDataManager;
    
    public NetworkServer(NetworkManager _networkManager)
    {
        networkManager  = _networkManager;
        _playersDataManager = new PlayersDataManager();
        
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
        
        networkManager.OnServerStarted += NetworkManager_OnServerStarted;
    }

    private void NetworkManager_OnServerStarted()
    {
        networkManager.SceneManager.OnLoadComplete += SceneManager_OnLoadComplete;
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

        string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
        UserData userData = JsonUtility.FromJson<UserData>(payload);

        if (userData == null)
        {
            GameLog.Error($"ApprovalCheck: payload from client {request.ClientNetworkId} deserialized to null UserData. Payload='{payload}'. Rejecting.");
            response.Approved = false;
            response.Reason = "Invalid connection payload";
            return;
        }

        GameLog.Info($"ApprovalCheck, Name: {userData.PlayerName}, Trophies: {userData.UserTrophies}, AuthId: {userData.PlayerAuthId}");

        _playersDataManager.RegisterUserData(userData, request.ClientNetworkId);

        response.CreatePlayerObject = false;
        response.Approved = true;
    }

    private void SceneManager_OnLoadComplete(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
    {
        if(sceneName != Loader.Scene.GameScene.ToString()) return; //Only Spawn players in Game Scene

        Debug.Log($"Client {clientId} / AuthId {_playersDataManager.GetAuthIdByClientId(clientId)} loaded scene {sceneName}");
        
        //New client

        PlayerData newPlayerData = new PlayerData()
        {
            UserData = _playersDataManager.ClientIdToUserData[clientId],
            ClientId = clientId,
        };

        _playersDataManager.RegisterClient(newPlayerData);

        Debug.Log($"SceneManager_OnLoadComplete, New client - Player Data - userData Auth Id {newPlayerData.UserData.PlayerAuthId} - clientId {newPlayerData.ClientId} ");
    }

    public void Dispose()
    {
        if (networkManager != null)
        {
            networkManager.ConnectionApprovalCallback -= ApprovalCheck;
            networkManager.OnServerStarted -= NetworkManager_OnServerStarted;

            if(networkManager.SceneManager != null)
                networkManager.SceneManager.OnLoadComplete -= SceneManager_OnLoadComplete;
        }

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }
    }
}
