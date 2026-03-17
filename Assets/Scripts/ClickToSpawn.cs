using UnityEngine;
using UnityEngine.EventSystems;

public class ClickToSpawn : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Tower towerPrefab;

    public void OnPointerClick(PointerEventData eventData)
    {
        Vector3 spawnPosition = Camera.main.ScreenToWorldPoint(eventData.position);
        spawnPosition.z = 0f; // Ensure the z-coordinate is set to 0 for 2D
        Instantiate(towerPrefab, spawnPosition, Quaternion.identity);
    }
}
