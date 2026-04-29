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

public class HostManager : BaseHostManager
{
    private const int MAX_CONNECTIONS = 1;
    
    private NetworkServer networkServer;
    
    private Allocation allocation;
    
    private string lobbyId;
    
    private void Awake()
    {
        ServiceLocator.Register<BaseHostManager>(this);
        DontDestroyOnLoad(gameObject);
    }
    
    public override async Task StartHostAsync()
    {
        if (!await CreateAllocation()) return;
        

        try
        {
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log(JoinCode);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            // OnFailToStartHost?.Invoke();
            return;
        }
        
        GameLog.Info($"Relay created. Join code: {JoinCode}");
        
        //Create the lobby, before .StartHost an after get joinCode
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
        
        try
        {
            CreateLobbyOptions lobbyOptions = new();
            lobbyOptions.IsPrivate = false;
            lobbyOptions.Data = new Dictionary<string, DataObject>()
            {
                {
                    "JoinCode", new DataObject(visibility: DataObject.VisibilityOptions.Member, value : JoinCode)
                }
            };


            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync($"Player's Lobby", MAX_CONNECTIONS, lobbyOptions);

            lobbyId = lobby.Id;

            StartCoroutine(HeartbeatLobby(15f));

        } catch (LobbyServiceException lobbyEx)
        {
            GameLog.Exception(lobbyEx);
            // OnFailToStartHost?.Invoke();
            return;
        }
        
        networkServer = new NetworkServer(NetworkManager.Singleton);
        
        NetworkManager.Singleton.NetworkConfig.ConnectionData = ServiceLocator.Get<BaseClientManager>().UserData.TranslateToBytes();
        
        if (!NetworkManager.Singleton.StartHost())
        {
            GameLog.Error("HostManager: StartHost() returned false. Aborting load.");
            // OnFailToStartHost?.Invoke();
            return;
        }

        Loader.LoadHostNetwork(Loader.Scene.GameScene);
        
        while(SceneManager.GetActiveScene().name != Loader.Scene.GameScene.ToString())
        {
            //Not in game
            GameLog.Info("Not in game scene");
            await Task.Delay(100);
        }
        
        Debug.Log("In game scene");
    }

    private async Task<bool> CreateAllocation()
    {
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(MAX_CONNECTIONS);
            return true;
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
            // OnFailToStartHost?.Invoke();
            return false;
        }
    }

    private async Task<bool> GetJoinCode()
    {
        
    }
    
    /// <summary>
    /// Call this to shutdown the host. Doesn't go to Main Menu
    /// </summary>
    public override async void ShutdownHostAsync()
    {
        if (string.IsNullOrEmpty(lobbyId)) return;
        
        StopCoroutine(nameof(HeartbeatLobby));

        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
        }
        catch (LobbyServiceException lobbyEx)
        {
            GameLog.Exception(lobbyEx);
        }
        lobbyId = string.Empty;

        networkServer?.Dispose();
        Debug.Log("NETMANAGER - Call network dispose on Host game manager");
    }
    
    private IEnumerator HeartbeatLobby(float delayHeartbeatSeconds)
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
        networkServer?.Dispose();
        ServiceLocator.Unregister<BaseHostManager>();
    }
}
