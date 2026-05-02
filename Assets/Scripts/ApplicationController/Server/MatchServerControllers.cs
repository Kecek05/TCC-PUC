using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MatchServerControllers : IDisposable
{
    private NetworkConnectionServer _networkConnectionServer;
    private BasePlayersDataManager _playersDataManager;

    public MatchServerControllers(NetworkManager networkManager)
    {
        _playersDataManager = new PlayersDataManager();
        _networkConnectionServer = new NetworkConnectionServer(networkManager, _playersDataManager);

        _networkConnectionServer.OnPlayerConnected += _playersDataManager.Handle_OnPlayerConnected;

        ServiceLocator.Register<IOnPlayerLoaded>(_networkConnectionServer);
        ServiceLocator.Register<BasePlayersDataManager>(_playersDataManager);
    }

    public void Dispose()
    {
        if (_networkConnectionServer != null)
        {
            _networkConnectionServer.OnPlayerConnected -= _playersDataManager.Handle_OnPlayerConnected;
            _networkConnectionServer.Dispose();
        }
        
        ServiceLocator.Unregister<IOnPlayerLoaded>();
        ServiceLocator.Unregister<BasePlayersDataManager>();
    }
}
