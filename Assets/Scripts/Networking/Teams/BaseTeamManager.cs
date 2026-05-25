using System;
using Unity.Netcode;

public abstract class BaseTeamManager : NetworkBehaviour
{
    public abstract bool BothTeamsAssigned();
    public abstract TeamType GetTeam(string authId);
    public abstract bool IsOnTeam(string authId, TeamType team);

    /// <summary>
    /// Assigns a team to the given AuthId. Normally invoked indirectly via the
    /// IOnPlayerLoaded event when a real client finishes loading the GameScene; AI bots
    /// (no scene-load event) call this directly after registering themselves.
    /// </summary>
    public abstract void AssignTeam(string authId);

    /// <summary>
    /// Don't use this to check if a Local Team Has Been Assigned, use <see cref="HasLocalTeamBeenAssigned"/> insted.
    /// </summary>
    /// <returns></returns>
    public abstract TeamType GetLocalTeam(bool isLocal = true);
    public abstract TeamType GetEnemyTeam();
    public abstract bool HasLocalTeamBeenAssigned();
}
