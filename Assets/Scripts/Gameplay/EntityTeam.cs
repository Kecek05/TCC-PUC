using Unity.Netcode;

public class EntityTeam : NetworkBehaviour, ITeamMember
{
    private NetworkVariable<TeamType> _teamType = new(writePerm: NetworkVariableWritePermission.Server);

    private TeamType _pendingTeam;
    private bool _hasPending;

    public TeamType GetTeamType() => _hasPending ? _pendingTeam : _teamType.Value;

    public void SetTeamType(TeamType teamType)
    {
        if (IsSpawned)
        {
            _teamType.Value = teamType;
        }
        else
        {
            _pendingTeam = teamType;
            _hasPending = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && _hasPending)
        {
            _teamType.Value = _pendingTeam;
            _hasPending = false;
        }
    }
}
