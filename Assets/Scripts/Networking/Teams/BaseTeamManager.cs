using Unity.Netcode;

public abstract class BaseTeamManager : NetworkBehaviour
{
    public abstract bool BothTeamsAssigned();
    public abstract TeamType GetTeam(ulong clientId);
    public abstract bool IsOnTeam(ulong clientId, TeamType team);
    
    /// <summary>
    /// Don't use this to check if a Local Team Has Been Assigned, use <see cref="HasLocalTeamBeenAssigned"/> insted.
    /// </summary>
    /// <returns></returns>
    public abstract TeamType GetLocalTeam(bool isLocal = true);
    public abstract TeamType GetEnemyTeam();
    public abstract bool HasLocalTeamBeenAssigned();
}
