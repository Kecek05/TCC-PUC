using UnityEngine;

public interface IPlaceable
{
    public bool Occupied { get; }
    public TowerManager OccupiedTower { get; }
    
    public Transform PlaceablePoint { get; }
    
    public void Occupy(TowerManager towerManager);
    
    public bool IsOccupied();
}
