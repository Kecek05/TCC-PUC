using UnityEngine;

public interface IPlaceable
{
    bool occupied { get; set; }
    void Place();
}
