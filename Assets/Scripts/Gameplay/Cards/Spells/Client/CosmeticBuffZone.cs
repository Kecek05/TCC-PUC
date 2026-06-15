using UnityEngine;

/// <summary>
/// Cosmetic, client-side buff-zone visual. Sizes a tiled SpriteRenderer the same way the drag-time
/// GhostSpellCard does (SpriteRenderer.size in world units), so the persistent zone matches the ghost
/// preview and the actual buff radius exactly. Removes itself after the duration. Purely visual — the
/// buff is applied server-side by HasteExecutor.
/// </summary>
public class CosmeticBuffZone : MonoBehaviour
{
    [SerializeField] private SpriteRenderer zoneRenderer;

    public void Initialize(Vector2 position, float range, float duration)
    {
        transform.position = new Vector3(position.x, position.y, 0f);

        if (zoneRenderer == null) zoneRenderer = GetComponent<SpriteRenderer>();
        if (zoneRenderer != null)
            zoneRenderer.size = new Vector2(range * 2f, range * 2f);

        Destroy(gameObject, duration);
    }
}
