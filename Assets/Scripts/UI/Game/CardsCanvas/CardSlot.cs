using UnityEngine;

public class CardSlot : MonoBehaviour
{
    [SerializeField] private Transform slotTransform;
 
    private bool isSlotSelected;
    private bool isOccupied;
    
    public Transform SlotTransform => slotTransform;

    public bool IsOccupied => isOccupied;

    public bool TryOccupy()
    {
        if (isOccupied)
        {
            GameLog.Warn("Trying to occupy an already occupied slot.");
            return false;
        }
        
        isOccupied = true;
        return true;
    }

    public void Unoccupy()
    {
        isOccupied = false;
    }
    
    public void Select()
    {
        isSlotSelected = true;
    }

    public void Unselect()
    {
        isSlotSelected = false;
    }
}
