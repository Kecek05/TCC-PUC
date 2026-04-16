using Unity.Netcode;

public abstract class BaseServerEndGameManager : NetworkBehaviour
{
    public NetworkVariable<TeamType> WinnerTeam = new(writePerm: NetworkVariableWritePermission.Server);
}
