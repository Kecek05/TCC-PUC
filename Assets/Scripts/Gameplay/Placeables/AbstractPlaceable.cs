using System;
using Sirenix.OdinInspector;
using UnityEngine;

public abstract class AbstractPlaceable : MonoBehaviour, IPlaceable
{
    [Title("References")]
    [SerializeField] private Transform placeablePoint;
    [SerializeField] private TeamIdentifier teamIdentifier;

    private bool _occupied;
    public bool Occupied => _occupied;

    private TowerManager _occupiedTower;
    public TowerManager OccupiedTower => _occupiedTower;

    public Transform PlaceablePoint => placeablePoint;

    private TeamType _registeredTeam = TeamType.None;

    protected virtual void Awake()
    {
        _registeredTeam = teamIdentifier.GetTeamType();
        PlaceableRegistry.Register(this, _registeredTeam);
    }

    protected virtual void OnDestroy()
    {
        if (_registeredTeam != TeamType.None)
            PlaceableRegistry.Unregister(this, _registeredTeam);
    }

    public void Occupy(TowerManager towerDataHolder)
    {
        if (_occupied)
            return;

        _occupiedTower =  towerDataHolder;
        _occupied = true;
    }

    public bool IsOccupied()
    {
        return _occupied;
    }
}
