using Sirenix.OdinInspector;
using UnityEngine;

public class GhostTowerCard : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject gfxObject;
    [SerializeField] private Transform rangeGfx;
    [Tooltip("Second ring shown only while hovering an upgradeable tower: the range it gains after upgrade.")]
    [SerializeField] private Transform nextRangeGfx;
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

    public void SetNextRange(float range)
    {
        if (nextRangeGfx != null) nextRangeGfx.localScale = Vector3.one * (range * 2);
    }

    public void SetVisible(bool visible)
    {
        gfxObject.SetActive(visible);
        rangeGfx.gameObject.SetActive(visible);
        // The "next range" ring is upgrade-hover only — never part of the normal placement ghost.
        if (nextRangeGfx != null) nextRangeGfx.gameObject.SetActive(false);
    }

    /// <summary>
    /// Upgrade-hover preview: no tower sprite (the tower already exists), the base ring shows the tower's
    /// current range and the second ring shows the range it would have after this upgrade. At max level
    /// <paramref name="hasNext"/> is false and only the current ring is shown.
    /// </summary>
    public void ShowUpgradePreview(Vector2 position, float currentRange, float nextRange, bool hasNext)
    {
        SetPosition(position);
        gfxObject.SetActive(false);

        rangeGfx.gameObject.SetActive(true);
        SetRange(currentRange);

        if (nextRangeGfx != null)
        {
            nextRangeGfx.gameObject.SetActive(hasNext);
            if (hasNext) SetNextRange(nextRange);
        }
    }

    public void SetSprite(Sprite sprite)
    {
        gfxSprite.sprite = sprite;
    }
}
