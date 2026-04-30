using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class ClientManager : BaseClientManager
{
    private NetworkClient networkClient;

    private void Awake()
    {
        ServiceLocator.Register<BaseClientManager>(this);
        DontDestroyOnLoad(gameObject);

        UserData = new UserData();

        ClientAuth = new ClientAuth();
        //TODO: Refactor the creation of NetworkClient too.
        networkClient = new NetworkClient(NetworkManager.Singleton, this);

        DoAuth();
    }

    private async void DoAuth()
    {
        try
        {
            if (await ClientAuth.TryInitAsync())
            {
                UserData.SetPlayerName(AuthenticationService.Instance.PlayerName); //Temp
                UserData.SetPlayerAuthId(AuthenticationService.Instance.PlayerId);
                UserData.SetUserTrophies(UnityEngine.Random.Range(0, 1000)); //Temp
                GameLog.Info($"Player authenticated. PlayerId: {AuthenticationService.Instance.PlayerId}, PlayerName: {AuthenticationService.Instance.PlayerName}");
                Loader.Load(Loader.Scene.MainMenu);
            }
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
            return;
        }
    }

    public override async Task<bool> JoinHost(string joinCode)
    {
        return await networkClient.JoinRelay(joinCode);
    }

    public override void ConnectClient()
    {
        networkClient.ConnectClient(UserData);
    }

    public override void DisconnectClient()
    {
        networkClient.Disconnect();
    }

    private void OnDestroy()
    {
        ClientAuth?.Dispose();
        networkClient?.Dispose();
        ServiceLocator.Unregister<BaseClientManager>();
    }
}
