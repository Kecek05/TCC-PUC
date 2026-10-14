using UnityEngine;

/// <summary>
/// How big a unit actually LOOKS, for everything drawn around it (an aim bracket, an ice block, a level badge, a
/// status): the extent of its sprite's mesh, not of its rect.
/// </summary>
/// <remarks>
/// A sprite's <see cref="Sprite.bounds"/> is its whole texture rect, padding included, and the tower art does not
/// agree on padding: the generated towers fill their canvas, while Dart's shape sits in under half of its own. Sized
/// off the rect, a bracket or an ice block came out twice the tower. The mesh hugs the drawn shape as long as the
/// sprite is imported with a Tight mesh; a Full Rect one reports its rect again, so art with padding must be Tight.
/// </remarks>
public static class VisibleBounds
{
    /// <summary>The drawn shape's size in the sprite's own units (before any transform).</summary>
    public static Vector2 SpriteSize(Sprite sprite)
    {
        if (sprite == null) return Vector2.zero;

        Vector2[] vertices = sprite.vertices;
        if (vertices == null || vertices.Length == 0) return sprite.bounds.size;

        Vector2 min = vertices[0], max = vertices[0];
        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector2.Min(min, vertices[i]);
            max = Vector2.Max(max, vertices[i]);
        }
        return max - min;
    }

    /// <summary>The drawn shape's size in the world, at the renderer's current scale.</summary>
    public static Vector2 WorldSize(SpriteRenderer renderer)
    {
        if (renderer == null) return Vector2.zero;
        Vector3 scale = renderer.transform.lossyScale;
        Vector2 size = SpriteSize(renderer.sprite);
        return new Vector2(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y));
    }

    /// <summary>
    /// The renderer that is a unit's body: a tower's level-1 art (the outer shape every tier nests in), an enemy's
    /// own sprite rather than its health bar.
    /// </summary>
    public static SpriteRenderer BodyOf(Component unit)
    {
        if (unit == null) return null;

        ClientTowerGFX tower = unit.GetComponentInChildren<ClientTowerGFX>();
        if (tower != null && tower.ArtRenderer != null) return tower.ArtRenderer;

        SpriteRenderer fallback = null;
        foreach (SpriteRenderer renderer in unit.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.name.Contains("Health")) continue;
            if (renderer.name == "EnemyGFX") return renderer;
            fallback ??= renderer;
        }
        return fallback;
    }

    /// <summary>A unit's longest visible side in the world, or <paramref name="fallback"/> when it has no body.</summary>
    public static float Across(Component unit, float fallback)
    {
        Vector2 size = WorldSize(BodyOf(unit));
        float across = Mathf.Max(size.x, size.y);
        return across > 0.02f && across < 10f ? across : fallback;
    }
}
