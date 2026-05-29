using System.Collections.Generic;
using Shapes;
using UI.Game.Shapes.ImmediateComponents;
using UnityEngine;

namespace UI.Game.Shapes
{

[ExecuteAlways]
public class UIShape : ImmediateModeCanvas
{
    // Sorted-panel buffer — reused, no per-frame GC
    static readonly List<(int order, int sibling, ImmediateModePanel panel)> Sorted = new();

    public override void DrawCanvasShapes(ImCanvasContext ctx)
    {
        Sorted.Clear();
        foreach (var panel in GetComponentsInChildren<ImmediateModePanel>(includeInactive: false))
        {
            int order = (panel as ISortableImmediatePanel)?.SortingOrder ?? 0;
            Sorted.Add((order, panel.transform.GetSiblingIndex(), panel));
        }
        Sorted.Sort((a, b) =>
            a.order != b.order
                ? a.order - b.order
                : a.sibling - b.sibling);

        using (Draw.Scope)
        {
            foreach (var (_, _, panel) in Sorted)
            {
#if UNITY_EDITOR
                if (ctx.camera.cameraType == CameraType.SceneView
                    && UnityEditor.SceneVisibilityManager.instance.IsHidden(panel.gameObject))
                    continue;
#endif
                using (Draw.Scope)
                {
                    Draw.Matrix = panel.transform.localToWorldMatrix;
                    panel.DrawPanelShapes(((RectTransform)panel.transform).rect, ctx);
                }
            }
        }
    }
}

}
