using UnityEngine;

public abstract class AbstractPlaceable : MonoBehaviour, IPlaceable
{
    [SerializeField] private Transform placeablePoint;
    
    private bool occupied;
    public bool Occupied => occupied;
    public Transform PlaceablePoint => placeablePoint;

    public virtual void Place()
    {
        if (occupied) 
            return;
        
        occupied = true;
    }

    public bool IsOccupied()
    {
        return occupied;
    }
}
