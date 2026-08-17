using Unity.Netcode;
using UnityEngine;

public abstract class BaseMapTranslator : NetworkBehaviour
{
    public abstract bool IsInitialized { get; }
    public abstract bool BothPlayersInitialized { get; }

    /// <summary>
    /// Server-side hook to mark a team as "map initialized" without a client RPC. Needed for a bot,
    /// which has no client to send the normal InitializeTeamServerRpc that satisfies the
    /// LoadingMatch -> MatchReady gate. Default no-op for stand-in translators.
    /// </summary>
    public virtual void MarkPlayerInitialized(TeamType team) { }

    public abstract Vector3 LocalToServer(Vector3 localPos);
    public abstract Vector3 LocalToServer(Vector3 localPos, TeamType teamType);
    public abstract Vector3 ServerToLocal(Vector3 serverPos, TeamType teamType);
}
