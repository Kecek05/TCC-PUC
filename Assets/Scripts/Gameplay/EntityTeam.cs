using Unity.Netcode;

public class EntityTeam : NetworkBehaviour, ITeamMember
{
    private NetworkVariable<TeamType> _teamType = new(writePerm: NetworkVariableWritePermission.Server);
    public TeamType GetTeamType() => _teamType.Value;
    public void SetTeamType(TeamType teamType)
    {
        _teamType.Value = teamType;
    }
}
