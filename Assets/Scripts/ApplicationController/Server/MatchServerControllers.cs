using System;
using Unity.Netcode;

public class MatchServerControllers : IDisposable
{
    private NetworkConnectionServer _networkConnectionServer;
    private PlayersDataManager _playersDataManager;

    public MatchServerControllers(NetworkManager networkManager)
    {
        _playersDataManager = new PlayersDataManager();
        _networkConnectionServer = new NetworkConnectionServer(networkManager, _playersDataManager);

        _networkConnectionServer.OnPlayerConnected += _playersDataManager.Handle_OnPlayerConnected;
    }

    public void Dispose()
    {
        if (_networkConnectionServer != null)
        {
            _networkConnectionServer.OnPlayerConnected -= _playersDataManager.Handle_OnPlayerConnected;
            _networkConnectionServer.Dispose();
        }
    }
}
