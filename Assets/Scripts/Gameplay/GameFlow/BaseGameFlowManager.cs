using System.Collections;
using Unity.Netcode;
public abstract class BaseGameFlowManager : NetworkBehaviour
{
    public NetworkVariable<GameState> CurrentGameState = new NetworkVariable<GameState>(writePerm: NetworkVariableWritePermission.Server);
    
    protected abstract IEnumerator HandleGameFlow();

    public abstract void SetGameState(GameState newState);
}
