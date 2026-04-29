using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkServer : IDisposable
{
    private NetworkManager networkManager;
    private ServerAuthenticationService serverAuthenticationService;
    
    public NetworkServer(NetworkManager _networkManager)
    {
        networkManager  = _networkManager;
        serverAuthenticationService = new ServerAuthenticationService();
        
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
        
        networkManager.OnServerStarted += NetworkManager_OnServerStarted;
    }

    private void NetworkManager_OnServerStarted()
    {
        networkManager.SceneManager.OnLoadComplete += SceneManager_OnLoadComplete;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string payload = System.Text.Encoding.UTF8.GetString(request.Payload); //Deserialize the payload to jason

        UserData userData = JsonUtility.FromJson<UserData>(payload); //Deserialize the payload to UserData

        Debug.Log($"ApprovalCheck, Name: {userData.PlayerName}, Trophies: {userData.UserTrophies}, AuthId: {userData.PlayerAuthId} ");

        serverAuthenticationService.RegisterUserData(userData, request.ClientNetworkId);
        
        response.Approved = true; //Connection is approved
        response.CreatePlayerObject = false;
    }

    private void SceneManager_OnLoadComplete(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
    {
        if(sceneName != Loader.Scene.GameScene.ToString()) return; //Only Spawn players in Game Scene

        Debug.Log($"Client {clientId} / AuthId {serverAuthenticationService.GetAuthIdByClientId(clientId)} loaded scene {sceneName}");
        
        //New client

        PlayerData newPlayerData = new PlayerData()
        {
            UserData = serverAuthenticationService.ClientIdToUserData[clientId],
            ClientId = clientId,
        };

        serverAuthenticationService.RegisterClient(newPlayerData);

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
