
public class ServerEndGameManager_DEBUG : BaseServerEndGameManager
{
    private void Awake()
    {
        ServiceLocator.Register<BaseServerEndGameManager>(this);
    }
}
