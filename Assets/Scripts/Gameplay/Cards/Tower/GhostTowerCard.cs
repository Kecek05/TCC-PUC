using Sirenix.OdinInspector;
using UnityEngine;

public class GhostTowerCard : MonoBehaviour
{
    [Title("References")] 
    [SerializeField] private GameObject gfxObject;
    [SerializeField] private Transform rangeGfx;
    [SerializeField] private SpriteRenderer gfxSprite;

    private void Awake()
    {
        SetVisible(false);
    }

    public void SetPosition(Vector2 position)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
    }

    public void SetRange(float range)
    {
        rangeGfx.localScale = Vector3.one * (range * 2);
    }

    public void SetVisible(bool visible)
    {
        gfxObject.SetActive(visible);
        rangeGfx.gameObject.SetActive(visible);
    }
    
    public void SetSprite(Sprite sprite)
    {
        gfxSprite.sprite = sprite;
    }
}
