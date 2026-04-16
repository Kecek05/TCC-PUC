using Unity.Netcode;

public abstract class BaseTeamManager : NetworkBehaviour
{
    public abstract bool BothTeamsAssigned();
    public abstract TeamType GetTeam(ulong clientId);
    public abstract bool IsOnTeam(ulong clientId, TeamType team);
    public abstract TeamType GetLocalTeam();
    public abstract bool HasLocalTeamBeenAssigned();
}
