using Shapes;

public class UIShape : ImmediateModeCanvas
{
    public override void DrawCanvasShapes(ImCanvasContext ctx) => DrawPanels();
}
