using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A flat-coloured annulus centred on its rect: everything between <see cref="InnerRadius"/> and
/// <see cref="OuterRadius"/> is filled. With no hole it is a disc that can grow to cover the screen; with a hole it
/// is a screen-covering ring whose hole can open. That pair is the circle wipe (MatchSearch, scene switches)
/// without a mask or a custom shader.
/// </summary>
public class IrisGraphic : MaskableGraphic
{
    private const int Segments = 72;

    [SerializeField] private float innerRadius;
    [SerializeField] private float outerRadius;

    public float InnerRadius
    {
        get => innerRadius;
        set { innerRadius = Mathf.Max(0f, value); SetVerticesDirty(); }
    }

    public float OuterRadius
    {
        get => outerRadius;
        set { outerRadius = Mathf.Max(0f, value); SetVerticesDirty(); }
    }

    /// <summary>The radius that covers the whole rect from its centre (half its diagonal), plus a margin.</summary>
    public float CoverRadius => rectTransform.rect.size.magnitude * 0.5f + 8f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float outer = outerRadius;
        float inner = Mathf.Min(innerRadius, outer);
        if (outer <= 0.01f || outer - inner <= 0.01f) return;

        Vector2 centre = rectTransform.rect.center;
        Color32 c = color;

        for (int i = 0; i <= Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));
            vh.AddVert(centre + dir * inner, c, Vector4.zero);
            vh.AddVert(centre + dir * outer, c, Vector4.zero);
        }

        for (int i = 0; i < Segments; i++)
        {
            int k = i * 2;
            vh.AddTriangle(k, k + 1, k + 3);
            vh.AddTriangle(k, k + 3, k + 2);
        }
    }
}
