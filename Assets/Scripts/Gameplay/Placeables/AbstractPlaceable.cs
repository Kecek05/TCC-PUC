using UnityEngine;

public abstract class AbstractPlaceable : MonoBehaviour, IPlaceable
{
    [SerializeField] private Transform placeablePoint;
    
    private bool _occupied;
    public bool Occupied => _occupied;
    
    private TowerDataHolder _occupiedTower;
    public TowerDataHolder OccupiedTower => _occupiedTower;
    
    public Transform PlaceablePoint => placeablePoint;

    public void Occupy(TowerDataHolder towerDataHolder)
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
