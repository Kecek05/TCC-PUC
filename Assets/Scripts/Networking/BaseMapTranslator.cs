using Unity.Netcode;
using UnityEngine;

public abstract class BaseMapTranslator : NetworkBehaviour
{
    public abstract bool IsInitialized { get; }
    public abstract bool BothPlayersInitialized { get; }

    public abstract Vector3 LocalToServer(Vector3 localPos);
    public abstract Vector3 ServerToLocal(Vector3 serverPos, TeamType teamType);
}
