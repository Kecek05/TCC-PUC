using UnityEngine;

public interface IPlaceable
{
    public bool Occupied { get; }
    public TowerDataHolder OccupiedTower { get; }
    
    public Transform PlaceablePoint { get; }
    
    public void Occupy(TowerDataHolder towerDataHolder);
    
    public bool IsOccupied();
}
