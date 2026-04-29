using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Random = System.Random;

public class ClientManager : BaseClientManager
{
    private ClientAuth clientAuth;
    private NetworkClient networkClient;

    public ClientAuth ClientAuth => clientAuth;
    
    private UserData userData;
    public UserData UserData => userData;

    private async void Awake()
    {
        ServiceLocator.Register<BaseClientManager>(this);
        DontDestroyOnLoad(gameObject);
        
        clientAuth = new ClientAuth();
        networkClient = new NetworkClient(NetworkManager.Singleton);

        if (await clientAuth.TryInitAsync())
        {
            userData = new UserData
            {
                PlayerName = AuthenticationService.Instance.PlayerName, //Temp
                PlayerAuthId = AuthenticationService.Instance.PlayerId,
            };
            
            userData.SetUserTrophies(UnityEngine.Random.Range(0, 1000)); //Temp
        }
    }
    
    public override async Task<bool> JoinHost(string joinCode)
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
    
    public override void ConnectClient()
    {
        string payload = JsonUtility.ToJson(userData); //serialize the payload to json
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload); //serialize the payload to bytes

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
        Debug.Log($"Setted Payload - Start Connection");
        
        bool result = NetworkManager.Singleton.StartClient();
        if (!result)
        {
            Debug.LogError("Failed to start client: StartClient returned false.");
            Loader.Load(Loader.Scene.NoNetwork);
        }
    }

    private void OnDestroy()
    {
        clientAuth?.Dispose();
        networkClient?.Dispose();
        ServiceLocator.Unregister<BaseClientManager>();
    }
}
