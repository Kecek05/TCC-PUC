using Sirenix.OdinInspector;
using UnityEngine;

public class TowerPlacementFeedback : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private SpriteRenderer gfx1SpriteRenderer;
    [SerializeField] private GameObject gfxObject;
    
    public void ShowGFX(Sprite spawnSprite)
    {
        gfx1SpriteRenderer.sprite = spawnSprite;
        gfxObject.SetActive(true);
    }

    public void HideGFX()
    {
        gfxObject.SetActive(false);
        Destroy(gameObject);
    }
}
