using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class ImediateTest : ImmediateModeShapeDrawer
{
    public override void DrawShapes( Camera cam ){

        using( Draw.Command( cam ) ){
            Draw.ZTest = CompareFunction.Always; // to make sure it draws on top of everything like a HUD
            // set up static parameters. these are used for all following Draw.Line calls
            Draw.LineGeometry = LineGeometry.Flat2D;
            Draw.ThicknessSpace = ThicknessSpace.Meters;
            Draw.Thickness = 30; // 4px wide

            // set static parameter to draw in the local space of this object
            Draw.Matrix = transform.localToWorldMatrix;

            // draw lines
            Draw.Rectangle( new Rect( -250f, 0f, 700, 700 ), Color.blue );
        }
    }
}
