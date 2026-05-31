using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace UI.Game.Shapes.ImmediateComponents
{

[ExecuteAlways]
public class RectangleImmediateUI : ImmediateModePanel, ISortableImmediatePanel
{
    // ── Rendering ─────────────────────────────────────────────
    [Header("Rendering")]
    [SerializeField] ShapesBlendMode blendMode = ShapesBlendMode.Transparent;
    [SerializeField] CompareFunction zTest     = CompareFunction.LessEqual;
    [Tooltip("Higher = drawn on top. Tiebreak by hierarchy sibling index. Only meaningful within one UIShape canvas.")]
    [SerializeField] int sortingOrder;
    public int SortingOrder => sortingOrder;

    // ── Shape ─────────────────────────────────────────────────
    [Header("Rectangle")]
    [SerializeField] Rectangle.RectangleType type = Rectangle.RectangleType.RoundedSolid;
    [SerializeField] Color color = Color.white;

    // ── Corners (Rounded* types only) ─────────────────────────
    [Header("Corners")]
    [SerializeField] Rectangle.RectangleCornerRadiusMode cornerRadiusMode = Rectangle.RectangleCornerRadiusMode.Uniform;
    [SerializeField, Min(0f)] float cornerRadius = 16f;

    [Tooltip("Per-corner radii, order is clockwise from bottom-left: (BL, TL, TR, BR). Only used when CornerRadiusMode = PerCorner.")]
    [SerializeField] Vector4 cornerRadii = new Vector4(16f, 16f, 16f, 16f);

    // ── Border (HardBorder / RoundedBorder only) ──────────────
    [Header("Border")]
    [SerializeField, Min(0f)] float thickness = 4f;
    [SerializeField] ThicknessSpace thicknessSpace = ThicknessSpace.Meters;

    // ── Dashes (border types only) ────────────────────────────
    [Header("Dashes")]
    [SerializeField] bool dashed;
    [Tooltip("When true, dash spacing automatically tracks dash size on edits — same behavior as the Rectangle component's 'Match Dash Spacing To Size'.")]
    [SerializeField] bool matchDashSpacingToSize = true;
    [SerializeField] DashStyle dashStyle = DashStyle.defaultDashStyle;

    // ── Fill (overrides solid color when enabled) ─────────────
    [Header("Fill (Gradient)")]
    [SerializeField] bool useFill;
    [SerializeField] GradientFill fill = GradientFill.defaultFill;

    void OnValidate()
    {
        cornerRadius = Mathf.Max(0f, cornerRadius);
        cornerRadii = new Vector4(
            Mathf.Max(0f, cornerRadii.x),
            Mathf.Max(0f, cornerRadii.y),
            Mathf.Max(0f, cornerRadii.z),
            Mathf.Max(0f, cornerRadii.w)
        );
        thickness = Mathf.Max(0f, thickness);

        if (matchDashSpacingToSize)
            dashStyle.UniformSize = dashStyle.size;
    }

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx)
    {
        // Push our state. ImmediateModeCanvas.DrawPanels wraps each panel
        // in a Draw.Scope, so these assignments don't leak to siblings.
        Draw.BlendMode      = blendMode;
        Draw.ZTest          = zTest;
        Draw.Color          = color;
        Draw.ThicknessSpace = thicknessSpace;

        Draw.UseGradientFill = useFill;
        if (useFill) Draw.GradientFill = fill;

        Draw.UseDashes = dashed;
        if (dashed) Draw.DashStyle = dashStyle;

        bool isBorder  = type == Rectangle.RectangleType.HardBorder
                      || type == Rectangle.RectangleType.RoundedBorder;
        bool isRounded = type == Rectangle.RectangleType.RoundedSolid
                      || type == Rectangle.RectangleType.RoundedBorder;

        Vector4 radii = !isRounded
            ? Vector4.zero
            : cornerRadiusMode == Rectangle.RectangleCornerRadiusMode.Uniform
                ? new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius)
                : cornerRadii;

        if (isBorder)
            Draw.RectangleBorder(rect, thickness, radii, color);
        else
            Draw.Rectangle(rect, radii, color);
    }

    public void SetDashCount(float dashCount)
    {
        dashStyle.size = dashCount;
    }

    public float GetDashCount()
    {
        return dashStyle.size;
    }

    public void SetDashFill(float dashFill)
    {
        dashStyle.spacing = dashFill;
    }

    public void SetDashOffset(float dashOffset)
    {
        dashStyle.offset = dashOffset;
    }

    public float GetDashOffset()
    {
        return dashStyle.offset;
    }
}

}