using UnityEngine;

public interface IPlaceable
{
    public bool Occupied { get; }
    
    public Transform PlaceablePoint { get; }
    
    public void Place();
    
    public bool IsOccupied();
}
