using UnityEngine;

public class TeamIdentifier : MonoBehaviour, ITeamMember
{
    [SerializeField] private TeamType _teamType;

    public TeamType TeamType => _teamType;

    public TeamType GetTeamType() => _teamType;
    
    public void SetTeamType(TeamType teamType)
    {
        _teamType = teamType;
    }
}

public enum TeamType
{
    None = 0,
    Blue = 1,
    Red = 2
}

public interface ITeamMember
{
    TeamType GetTeamType();
    
    void  SetTeamType(TeamType teamType);
}