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

public class HostConnectionData : IDisposable
{
    private Allocation _allocation;
    private string _joinCode;
    private string _lobbyId;
    private MatchServerControllers _controllers;

    public HostConnectionData(Allocation allocation, string joinCode, string lobbyId, NetworkManager networkManager)
    {
        _allocation = allocation;
        _joinCode = joinCode;
        _lobbyId = lobbyId;
        _controllers = new MatchServerControllers(networkManager);
    }
    public string JoinCode => _joinCode;
    public string LobbyId => _lobbyId;

    public void Dispose()
    {
        _controllers?.Dispose();
    }
}

public class HostManager : BaseHostManager
{
    private const int MAX_CONNECTIONS = 1;

    public event Action OnFailToStartHost;
    public event Action OnHostInGameScene;
    public event Action OnHostShutdown;
    
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
        if (CurrentHostConnectionData != null)
        {
            GameLog.Error("HostManager: Tried to StartHostAsync but it's already hosting. Aborting load.");
            OnFailToStartHost?.Invoke();
            return;
        }
        
        Allocation allocation = await CreateAllocation();
        if (allocation == null)
        {
            GameLog.Error("HostManager: Failed to create Relay allocation. Aborting load.");
            OnFailToStartHost?.Invoke();
            return;
        }
        
        string joinCode = await GetJoinCode(allocation);
        if (joinCode == null)
        {
            GameLog.Error("HostManager: Failed to get Join Code. Aborting load.");
            OnFailToStartHost?.Invoke();
            return;
        };
        
        //Create the lobby, before .StartHost an after get joinCode
        Lobby lobby = await CreateLobby(joinCode);
        if (lobby == null)
        {
            GameLog.Error("HostManager: Failed to create Lobby. Aborting load.");
            OnFailToStartHost?.Invoke();
            return;
        }
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
        NetworkManager.Singleton.NetworkConfig.ConnectionData = _clientManager.UserData.TranslateToBytes();
        
        CurrentHostConnectionData = new HostConnectionData(allocation, joinCode, lobby.Id, NetworkManager.Singleton);
        
        if (!NetworkManager.Singleton.StartHost())
        {
            GameLog.Error("HostManager: StartHost() returned false. Aborting load.");
            OnFailToStartHost?.Invoke();
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
        OnHostInGameScene?.Invoke();
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
            OnFailToStartHost?.Invoke();
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
            OnFailToStartHost?.Invoke();
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
            OnFailToStartHost?.Invoke();
            return null;
        }
    }
    
    /// <summary>
    /// Call this to shutdown the host. Doesn't go to Main Menu
    /// </summary>
    public override async void ShutdownHostAsync()
    {
        if (CurrentHostConnectionData == null) return;
        
        StopCoroutine(nameof(HeartbeatLobby));

        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(CurrentHostConnectionData.LobbyId);
        }
        catch (LobbyServiceException lobbyEx)
        {
            GameLog.Exception(lobbyEx);
        }

        CurrentHostConnectionData.Dispose();
        CurrentHostConnectionData = null;
        Debug.Log("NETMANAGER - Call network dispose on Host game manager");
        OnHostShutdown?.Invoke();
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
        CurrentHostConnectionData?.Dispose();
        ServiceLocator.Unregister<BaseHostManager>();
    }
}
