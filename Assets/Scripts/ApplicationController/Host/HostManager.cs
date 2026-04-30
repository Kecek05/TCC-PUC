using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionData : IDisposable
{
    private Allocation _allocation;
    private string _joinCode;
    private string _lobbyId;
    private NetworkServer _networkServer;

    public ConnectionData(Allocation allocation, string joinCode, string lobbyId,  NetworkServer networkServer)
    {
        _allocation = allocation;
        _joinCode = joinCode;
        _lobbyId = lobbyId;
    }
    
    public Allocation Allocation => _allocation;
    public string JoinCode => _joinCode;
    public string LobbyId => _lobbyId;
    public NetworkServer NetworkServer => _networkServer;

    public void Dispose()
    {
        _networkServer?.Dispose();
    }
}

public class HostManager : BaseHostManager
{
    private const int MAX_CONNECTIONS = 1;

    private ConnectionData _currentConnectionData;
    
    private BaseClientManager _clientManager;
    
    private void Awake()
    {
        ServiceLocator.Register<BaseHostManager>(this);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
    }

    public override async Task StartHostAsync()
    {
        if (_currentConnectionData != null)
        {
            GameLog.Error("HostManager: Tryed to StartHostAsync but it's already hosting. Aborting load.");
            return;
        }
        
        Allocation allocation = await CreateAllocation();
        if (allocation == null)
        {
            GameLog.Error("HostManager: Failed to create Relay allocation. Aborting load.");
            return;
        }
        
        string joinCode = await GetJoinCode(allocation);
        if (joinCode == null)
        {
            GameLog.Error("HostManager: Failed to get Join Code. Aborting load.");
            return;
        };
        
        //Create the lobby, before .StartHost an after get joinCode
        Lobby lobby = await CreateLobby(joinCode);
        if (lobby == null)
        {
            GameLog.Error("HostManager: Failed to create Lobby. Aborting load.");
            return;
        }
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
        NetworkManager.Singleton.NetworkConfig.ConnectionData = _clientManager.UserData.TranslateToBytes();
        
        NetworkServer networkServer = new NetworkServer(NetworkManager.Singleton);
        
        _currentConnectionData = new ConnectionData(allocation, joinCode, lobby.Id, networkServer);
        
        if (!NetworkManager.Singleton.StartHost())
        {
            GameLog.Error("HostManager: StartHost() returned false. Aborting load.");
            // OnFailToStartHost?.Invoke();
            return;
        }

        GameLog.Info($"Relay created. Join code: {joinCode}");
        Loader.LoadHostNetwork(Loader.Scene.GameScene);
        
        while(SceneManager.GetActiveScene().name != Loader.Scene.GameScene.ToString())
        {
            //Not in game
            GameLog.Info("Not in game scene");
            await Task.Delay(100);
        }
        
        Debug.Log("In game scene");
    }

    private async Task<Allocation> CreateAllocation()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_CONNECTIONS);
            return allocation;
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
            // OnFailToStartHost?.Invoke();
            return null;
        }
    }

    private async Task<string> GetJoinCode(Allocation allocation)
    {
        try
        {
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log(joinCode);
            return joinCode;
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
            // OnFailToStartHost?.Invoke();
            return null;
        }
    }

    private async Task<Lobby> CreateLobby(string joinCode)
    {
        try
        {
            CreateLobbyOptions lobbyOptions = new();
            lobbyOptions.IsPrivate = false;
            lobbyOptions.Data = new Dictionary<string, DataObject>()
            {
                {
                    "JoinCode", new DataObject(visibility: DataObject.VisibilityOptions.Member, value : joinCode)
                }
            };


            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync($"Player's Lobby", MAX_CONNECTIONS, lobbyOptions);
            StartCoroutine(HeartbeatLobby(15f, lobby.Id));
            
            return lobby;
        } catch (LobbyServiceException lobbyEx)
        {
            GameLog.Exception(lobbyEx);
            // OnFailToStartHost?.Invoke();
            return null;
        }
    }
    
    /// <summary>
    /// Call this to shutdown the host. Doesn't go to Main Menu
    /// </summary>
    public override async void ShutdownHostAsync()
    {
        if (_currentConnectionData == null) return;
        
        StopCoroutine(nameof(HeartbeatLobby));

        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(_currentConnectionData.LobbyId);
        }
        catch (LobbyServiceException lobbyEx)
        {
            GameLog.Exception(lobbyEx);
        }

        _currentConnectionData.Dispose();
        _currentConnectionData = null;
        Debug.Log("NETMANAGER - Call network dispose on Host game manager");
    }
    
    private IEnumerator HeartbeatLobby(float delayHeartbeatSeconds, string lobbyId)
    {
        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(delayHeartbeatSeconds); //optimization

        while(true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);

            yield return delay;
        }
    }
    
    private void OnDestroy()
    {
        _currentConnectionData?.Dispose();
        ServiceLocator.Unregister<BaseHostManager>();
    }
}
