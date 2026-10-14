using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lip under a card face, a cost bubble or any flat UI shape: one darker copy of the graphic a little lower,
/// drawn first, the way the game's buttons and the map's tiles are built (Card Art v2).
/// </summary>
/// <remarks>
/// A mesh effect rather than a second Image: the lip takes the graphic's own sprite, colour and alpha every time
/// the mesh rebuilds, so a card that is tinted (locked, unaffordable) or resized needs no extra bookkeeping, and
/// the drop is a share of the graphic's width, so it scales with whatever the card is drawn at.
/// </remarks>
[RequireComponent(typeof(Graphic))]
public class FaceLip : BaseMeshEffect
{
    [Tooltip("How far the lip hangs below the face, as a share of the graphic's width.")]
    [SerializeField, Range(0f, 0.2f)] private float drop = 0.055f;

    [Tooltip("The lip's colour: the graphic's own colour multiplied by this.")]
    [SerializeField, Range(0f, 1f)] private float shade = 0.72f;

    private static readonly List<UIVertex> Source = new();
    private static readonly List<UIVertex> Result = new();

    /// <summary>Sets the lip's depth and darkness from code, for a face that is built at runtime.</summary>
    public void Configure(float dropShare, float shadeShare)
    {
        drop = dropShare;
        shade = shadeShare;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        vh.GetUIVertexStream(Source);

        float dy = -graphic.rectTransform.rect.width * drop;
        Result.Clear();

        // The lip first, so the face is drawn over it.
        for (int i = 0; i < Source.Count; i++)
        {
            UIVertex v = Source[i];
            v.position.y += dy;
            Color32 c = v.color;
            v.color = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), c.a);
            Result.Add(v);
        }

        Result.AddRange(Source);

        vh.Clear();
        vh.AddUIVertexTriangleStream(Result);
    }
}
