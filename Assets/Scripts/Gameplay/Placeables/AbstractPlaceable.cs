using UnityEngine;

public class AbstractPlaceable : MonoBehaviour, IPlaceable
{
    
    private bool _occupied;
    public bool occupied => _occupied;

    public void Place()
    {
        
    }
}
