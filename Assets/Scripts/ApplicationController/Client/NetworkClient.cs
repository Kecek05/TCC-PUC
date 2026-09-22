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
        // Inside a match: just log. Leaving is driven only by the local player pressing
        // OK / Play Again (ClientManager.LeaveMatchAsync). A disconnect caused by
        // the other instance tearing down must NOT navigate this instance away —
        // each player leaves on their own action. The end screen already holds the
        // snapshot, so the connection dropping here is harmless.
        GameLog.Info($"Client disconnected: {clientId}");

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.IsServer) return;

        // Before the match scene was ever reached, the opposite is true: nothing is holding a
        // snapshot and there is no end screen to press, so doing nothing strands the player on the
        // Loading screen forever. This is the approval rejection quick match made reachable —
        // matchmaking can hand out a lobby whose host committed (bot filled the slot, or the
        // second human beat us to it) a moment before we connected, and NetworkConnectionServer
        // then refuses us. Send them back to pick again rather than leaving them staring at it.
        if (Loader.IsGameplayScene(SceneManager.GetActiveScene().name)) return;

        // No warning banner here on purpose: ScreenWarning is a scene object, so the one that could
        // show it is destroyed by the very load on the next line.
        GameLog.Info("Connection refused or dropped before the match started; returning to the Main Menu.");
        Loader.Load(Loader.Scene.MainMenu);
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

    /// <summary>
    /// Shuts down a pure client connection and completes only once Netcode has
    /// fully stopped. Host teardown is handled separately by <see cref="HostManager"/>;
    /// role selection and the scene change live in <see cref="ClientManager.LeaveMatchAsync"/>.
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (networkManager == null) return;

        if (networkManager.IsListening || networkManager.IsConnectedClient)
        {
            networkManager.Shutdown();

            while (networkManager.ShutdownInProgress)
                await Task.Yield();
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
