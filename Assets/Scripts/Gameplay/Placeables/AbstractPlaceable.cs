using UnityEngine;

public abstract class AbstractPlaceable : MonoBehaviour, IPlaceable
{
    [SerializeField] private Transform placeablePoint;
    
    private bool _occupied;
    public bool Occupied => _occupied;
    
    private TowerManager _occupiedTower;
    public TowerManager OccupiedTower => _occupiedTower;
    
    public Transform PlaceablePoint => placeablePoint;

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
