using Unity.Mathematics;
using Unity.Netcode;
using Unity.Services.Authentication;
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
                playerName = AuthenticationService.Instance.PlayerName, //Temp
                playerAuthId = AuthenticationService.Instance.PlayerId,
            };
            
            userData.SetUserTrophies(UnityEngine.Random.Range(0, 1000)); //Temp
        }
    }

    private void OnDestroy()
    {
        clientAuth?.Dispose();
        ServiceLocator.Unregister<BaseClientManager>();
    }
}
