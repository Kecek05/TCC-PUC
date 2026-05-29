using System.Collections.Generic;
using Shapes;
using UI.Game.Shapes.ImmediateComponents;
using UnityEngine;

[ExecuteAlways]
public class UIShape : ImmediateModeCanvas
{
    static readonly List<(int order, int sibling, ImmediateModePanel panel)> _sorted = new();

    public override void DrawCanvasShapes(ImCanvasContext ctx)
    {
        _sorted.Clear();
        foreach (var panel in GetComponentsInChildren<ImmediateModePanel>(includeInactive: false))
        {
            int order = (panel as ISortableImmediatePanel)?.SortingOrder ?? 0;
            _sorted.Add((order, panel.transform.GetSiblingIndex(), panel));
        }
        _sorted.Sort((a, b) =>
            a.order != b.order
                ? a.order - b.order
                : a.sibling - b.sibling);

        using (Draw.Scope)
        {
            foreach (var (_, _, panel) in _sorted)
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