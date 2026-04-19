using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkClient : IDisposable
{
    private NetworkManager networkManager;
    private BaseClientManager _clientManager;

    public NetworkClient(NetworkManager networkManager)
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        this.networkManager = networkManager;
        
        networkManager.OnClientStarted += NetworkManager_OnClientStarted;
        networkManager.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
        networkManager.OnClientDisconnectCallback += NetworkManager_OnClientDisconnectCallback;
    }

    private void NetworkManager_OnClientStarted()
    {
        Debug.Log("Client started");
    }
    
    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
    }
    
    private void NetworkManager_OnClientDisconnectCallback(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
    }

    public async void JoinRelay(string joinCode)
    {
        
    }

    private void ConnectClient()
    {
        string payload = JsonUtility.ToJson(_clientManager.UserData); //serialize the payload to json
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload); //serialize the payload to bytes

        // Debug.Log($"ConnectClient, UserData: {userData.userName}, Pearls: {userData.userPearls}, AuthId: {userData.userAuthId} ");

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
        // Debug.Log($"Setted Payload - Start Connection");
        
        bool result = NetworkManager.Singleton.StartClient();
        if (!result)
        {
            Debug.LogError("Failed to start client: StartClient returned false.");
            Loader.Load(Loader.Scene.NoNetwork);
        }
    }

    public void Dispose()
    {
        if (networkManager != null)
        {
            networkManager.OnClientStarted -= NetworkManager_OnClientStarted;
            networkManager.OnClientConnectedCallback -= NetworkManager_OnClientConnectedCallback;
            networkManager.OnClientDisconnectCallback -= NetworkManager_OnClientDisconnectCallback;
            
            networkManager = null;
        }
    }
}
