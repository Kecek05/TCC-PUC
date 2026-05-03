using System.Threading.Tasks;

public class ClientManagerDebug : BaseClientManager
{
    private void Awake()
    {
        UserData = new UserData()
        {
            PlayerAuthId = "ID",
            PlayerName = "DebugPlayer",
            UserTrophies = -1,
        };
        
        ServiceLocator.Register<BaseClientManager>(this);
    }

    public override void ConnectClient()
    {
        
    }

    public override void DisconnectClient()
    {
        
    }

    public override Task<bool> JoinHost(string joinCode) {
        return Task.FromResult(true);
    }
}
