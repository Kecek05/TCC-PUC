using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkClient : IDisposable
{
    private NetworkManager networkManager;
    private BaseClientManager _clientManager;

    public NetworkClient(NetworkManager networkManager, BaseClientManager clientManager)
    {
        _clientManager = clientManager;
        this.networkManager = networkManager;
        
        networkManager.OnClientStarted += NetworkManager_OnClientStarted;
        networkManager.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
        networkManager.OnClientDisconnectCallback += NetworkManager_OnClientDisconnectCallback;
    }

    private void NetworkManager_OnClientStarted()
    {
        GameLog.Info("Client started");
    }
    
    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        GameLog.Info($"Client connected: {clientId}");
    }
    
    private void NetworkManager_OnClientDisconnectCallback(ulong clientId)
    {
        GameLog.Info($"Client disconnected: {clientId}");
    }

    public async Task<bool> JoinRelay(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            transport.SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
            return false;
        }
        
        Loader.LoadClient();
        return true;
    }

    public void ConnectClient(UserData userData)
    {
        NetworkManager.Singleton.NetworkConfig.ConnectionData = userData.TranslateToBytes();
        Debug.Log($"Setted Payload - Start Connection");
        
        if (!NetworkManager.Singleton.StartClient())
        {
            Debug.LogError("Failed to start client: StartClient returned false.");
            Loader.Load(Loader.Scene.NoNetwork);
        }
    }

    public void Disconnect()
    {
        Debug.Log("Client Disconnect");
        //Check if is host first
        BaseHostManager hostManager = ServiceLocator.Get<BaseHostManager>();
        if (networkManager != null && hostManager != null && networkManager.IsHost)
        {
            hostManager.ShutdownHostAsync();
        }

        if(networkManager.IsConnectedClient)
            networkManager.Shutdown();

        if (SceneManager.GetActiveScene().name != Loader.Scene.MainMenu.ToString())
        {
            Loader.Load(Loader.Scene.MainMenu);
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
