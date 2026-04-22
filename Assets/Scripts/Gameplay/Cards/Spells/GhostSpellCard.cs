using Sirenix.OdinInspector;
using UnityEngine;

public class GhostSpellCard : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject gfxObject;
    [SerializeField] private SpriteRenderer gfxSprite;

    private void Awake()
    {
        SetVisible(false);
    }

    public void SetPosition(Vector2 position)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
    }

    public void SetVisible(bool visible)
    {
        gfxObject.SetActive(visible);
    }

    public void SetSprite(Sprite sprite)
    {
        gfxSprite.sprite = sprite;
    }
}