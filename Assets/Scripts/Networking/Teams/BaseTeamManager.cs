using System;
using Unity.Netcode;

public abstract class BaseTeamManager : NetworkBehaviour
{
    public abstract bool BothTeamsAssigned();
    public abstract TeamType GetTeam(string authId);
    public abstract bool IsOnTeam(string authId, TeamType team);

    /// <summary>
    /// Don't use this to check if a Local Team Has Been Assigned, use <see cref="HasLocalTeamBeenAssigned"/> insted.
    /// </summary>
    /// <returns></returns>
    public abstract TeamType GetLocalTeam(bool isLocal = true);
    public abstract TeamType GetEnemyTeam();
    public abstract bool HasLocalTeamBeenAssigned();

    /// <summary>
    /// Display name (from Unity Authentication, sent in the client connection
    /// payload) of the player assigned to <paramref name="team"/>. Synced to all
    /// clients alongside the team assignment. Returns empty if unknown.
    /// </summary>
    public abstract string GetPlayerName(TeamType team);
}
