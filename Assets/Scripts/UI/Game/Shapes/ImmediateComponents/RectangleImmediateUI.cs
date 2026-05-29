using Shapes;
using UnityEngine;

[ExecuteAlways]
public class RectangleImmediateUI : ImmediateModePanel
{
    [SerializeField] Color color           = new Color(0.18f, 0.95f, 0.77f, 1f);
    [SerializeField] float cornerRadius    = 16f;   // canvas units
    [SerializeField] bool  drawBorder      = false;
    [SerializeField] float borderThickness = 4f;    // canvas units
    [SerializeField] Color borderColor     = Color.white;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        // 'rect' is this RectTransform's rect, in canvas units.
        // Draw.Matrix is already set so coords are scaled by Canvas Scaler.
        Draw.Rectangle(rect, cornerRadius, color);

        if (drawBorder)
            Draw.RectangleBorder(rect, borderThickness, cornerRadius, borderColor);
    }
}
