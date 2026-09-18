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

    /// <summary>Where an offline host listens. Loopback, because nothing off this machine may reach it.</summary>
    private const string LoopbackAddress = "127.0.0.1";

    public event Action OnFailToStartHost;
    public event Action OnHostInGameScene;
    public event Action OnHostShutdown;
    
    private BaseClientManager _clientManager;
    private Coroutine _heartbeatCoroutine;
    private bool _lobbyClosedToNewPlayers;

    private void Awake()
    {
        ServiceLocator.Register<BaseHostManager>(this);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
    }

    public override async Task<bool> StartHostAsync()
    {
        if (CurrentHostConnectionData != null)
        {
            GameLog.Error("HostManager: Tried to StartHostAsync but it's already hosting. Aborting load.");
            OnFailToStartHost?.Invoke();
            return false;
        }
        
        Allocation allocation = await CreateAllocation();
        if (allocation == null)
        {
            GameLog.Error("HostManager: Failed to create Relay allocation. Aborting load.");
            OnFailToStartHost?.Invoke();
            return false;
        }
        
        string joinCode = await GetJoinCode(allocation);
        if (joinCode == null)
        {
            GameLog.Error("HostManager: Failed to get Join Code. Aborting load.");
            OnFailToStartHost?.Invoke();
            return false;
        };
        
        //Create the lobby, before .StartHost an after get joinCode
        Lobby lobby = await CreateLobby(joinCode);
        if (lobby == null)
        {
            GameLog.Error("HostManager: Failed to create Lobby. Aborting load.");
            OnFailToStartHost?.Invoke();
            return false;
        }
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
        NetworkManager.Singleton.NetworkConfig.ConnectionData = _clientManager.UserData.TranslateToBytes();
        
        CurrentHostConnectionData = new HostConnectionData(allocation, joinCode, lobby.Id, NetworkManager.Singleton);
        
        if (!NetworkManager.Singleton.StartHost())
        {
            GameLog.Error("HostManager: StartHost() returned false. Aborting load.");
            OnFailToStartHost?.Invoke();
            return false;
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
        return true;
    }

    /// <summary>
    /// Hosts a match on this machine alone. Everything StartHostAsync does to make a match reachable —
    /// the Relay allocation, the join code, the discovery lobby and its heartbeat — is skipped, so this
    /// needs no internet and publishes nothing. What stays is exactly what a match needs to exist: the
    /// connection payload, a listening host, and the scene load.
    /// </summary>
    /// <remarks>
    /// The transport binds loopback on an OS-assigned port, and no relay data is set — which is what keeps a
    /// second player from ever finding this session. <see cref="ShutdownHostAsync"/> and
    /// <see cref="CloseLobbyToNewPlayers"/> both already tolerate a null lobby, so teardown is shared.
    /// </remarks>
    public override async Task<bool> StartLocalHostAsync(Loader.Scene scene)
    {
        if (CurrentHostConnectionData != null)
        {
            GameLog.Error("HostManager: Tried to StartLocalHostAsync but it's already hosting. Aborting load.");
            OnFailToStartHost?.Invoke();
            return false;
        }

        NetworkManager.Singleton.NetworkConfig.ConnectionData = _clientManager.UserData.TranslateToBytes();

        // Port 0 = let the OS pick a free one, rather than the authored 7777. Nothing ever connects to this
        // host — it is offline and publishes nothing — so the port carries no meaning here, while a fixed
        // one is a shared resource: a second Editor, a running build, or a socket this very process leaked
        // from an earlier play session all hold 7777, and StartHost() then fails outright and drops a
        // first-time player into the Main Menu with no tutorial at all. The relay paths are unaffected —
        // they call SetRelayServerData, which replaces this wholesale.
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null) transport.SetConnectionData(LoopbackAddress, 0, LoopbackAddress);

        // No allocation and no lobby id: the two teardown paths key off those being empty.
        CurrentHostConnectionData = new HostConnectionData(null, null, null, NetworkManager.Singleton);

        if (!NetworkManager.Singleton.StartHost())
        {
            GameLog.Error("HostManager: StartHost() returned false for a local host. Aborting load.");
            CurrentHostConnectionData.Dispose();
            CurrentHostConnectionData = null;
            OnFailToStartHost?.Invoke();
            return false;
        }

        GameLog.Info($"Local host started (offline, no relay). Loading {scene}.");
        Loader.LoadHostNetwork(scene);

        while (SceneManager.GetActiveScene().name != scene.ToString())
            await Task.Delay(100);

        OnHostInGameScene?.Invoke();
        return true;
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
            _heartbeatCoroutine = StartCoroutine(HeartbeatLobby(15f, lobby.Id));

            return lobby;
        } catch (LobbyServiceException lobbyEx)
        {
            GameLog.Exception(lobbyEx);
            OnFailToStartHost?.Invoke();
            return null;
        }
    }
    
    /// <summary>
    /// Call this to shutdown the host. Doesn't go to Main Menu.
    /// Owns the full host teardown: stop heartbeat, delete lobby, shutdown
    /// NetworkManager, and await until Netcode has fully stopped.
    /// </summary>
    public override async Task ShutdownHostAsync()
    {
        if (CurrentHostConnectionData == null) return;

        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }

        // A local host (tutorial) never created one, and CloseLobbyToNewPlayers may already have deleted it.
        if (!string.IsNullOrEmpty(CurrentHostConnectionData.LobbyId))
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(CurrentHostConnectionData.LobbyId);
            }
            catch (LobbyServiceException lobbyEx)
            {
                GameLog.Exception(lobbyEx);
            }
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();

            // Shutdown() only completes on a later update tick; wait until Netcode
            // has fully stopped so the next StartHost() (replay) starts clean.
            while (networkManager.ShutdownInProgress)
                await Task.Yield();
        }

        // Disposes MatchServerControllers (unsubscribes server-side listeners and
        // unregisters services). The NetworkManager is already stopped above.
        CurrentHostConnectionData.Dispose();
        CurrentHostConnectionData = null;

        // Reset for the next session: left true, the "play again" host would never stop its new heartbeat.
        _lobbyClosedToNewPlayers = false;

        OnHostShutdown?.Invoke();
    }
    
    // Stop advertising to new players without tearing down the host: stop the heartbeat and delete the
    // discovery lobby. The host keeps running on relay for the current session (host + bot). Idempotent.
    public override void CloseLobbyToNewPlayers()
    {
        if (_lobbyClosedToNewPlayers) return;
        _lobbyClosedToNewPlayers = true;

        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }

        string lobbyId = CurrentHostConnectionData?.LobbyId;
        if (!string.IsNullOrEmpty(lobbyId))
            _ = DeleteLobbySafeAsync(lobbyId);

        GameLog.Info("HostManager: lobby closed to new players (match committed).");
    }

    private async Task DeleteLobbySafeAsync(string lobbyId)
    {
        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
        }
        catch (LobbyServiceException e)
        {
            GameLog.Exception(e);
        }
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
