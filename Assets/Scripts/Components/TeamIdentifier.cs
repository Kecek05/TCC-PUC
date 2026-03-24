using UnityEngine;

public class TeamIdentifier : MonoBehaviour
{
    [SerializeField] private TeamType teamType;

    public TeamType TeamType => teamType;

}

public enum TeamType
{
    Blue = 0,
    Red = 1
}