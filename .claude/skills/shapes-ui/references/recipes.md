# Shapes UI Recipes

Copy-paste-ready `ImmediateModePanel` subclasses for the patterns this project keeps reaching for.
All assume a parent `UIShapesCanvas` (subclass of `ImmediateModeCanvas`) exists on the Canvas
GameObject — see SKILL.md for that one-liner.

Coordinates and sizes in every `DrawPanelShapes` body are in **canvas units** (the Canvas Scaler's
reference space), so a `16f` corner radius means 16 reference pixels regardless of device.

---

## 1. Rounded card background

A solid rounded rect filling the panel's RectTransform.

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class CardBackgroundPanel : ImmediateModePanel {
    [SerializeField] Color fill         = new Color(0.12f, 0.13f, 0.18f, 1f);
    [SerializeField] float cornerRadius = 24f;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        Draw.Rectangle(rect, cornerRadius, fill);
    }
}
```

Add to any child of `CardsCanvas`. Anchors stretched → fills parent. Drop in a LayoutGroup → tracks
the layout-driven size.

---

## 2. Framed border (outline only)

Border, no fill. Useful for highlighting selected cards, drop targets, slot frames.

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class FrameBorderPanel : ImmediateModePanel {
    [SerializeField] Color color        = Color.white;
    [SerializeField] float thickness    = 4f;
    [SerializeField] float cornerRadius = 16f;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        Draw.RectangleBorder(rect, thickness, cornerRadius, color);
    }
}
```

For an animated highlight, expose `color.a` or pulse `thickness` from a coroutine — no draw-call
overhead difference.

---

## 3. Fill + border in one panel

Common combo: solid background with a rim. Single panel keeps it batched.

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class FilledFramedPanel : ImmediateModePanel {
    [SerializeField] Color fill            = new Color(0.18f, 0.20f, 0.26f, 1f);
    [SerializeField] Color borderColor     = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] float cornerRadius    = 20f;
    [SerializeField] float borderThickness = 3f;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        Draw.Rectangle(rect, cornerRadius, fill);
        Draw.RectangleBorder(rect, borderThickness, cornerRadius, borderColor);
    }
}
```

---

## 4. Horizontal progress bar (elixir/health style)

The classic rounded "fill from left" bar. `value01` is `[0, 1]`.

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class HorizontalBarPanel : ImmediateModePanel {
    [Range(0f, 1f)] public float value01 = 1f;

    [SerializeField] Color trackColor   = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] Gradient fillGradient;
    [SerializeField] float cornerRadius = 12f;
    [SerializeField] float padding      = 4f;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        // Track
        Draw.Rectangle(rect, cornerRadius, trackColor);

        // Fill (inset by padding, scaled by value)
        Rect fillRect = new Rect(
            rect.x + padding,
            rect.y + padding,
            (rect.width - padding * 2f) * Mathf.Clamp01(value01),
            rect.height - padding * 2f
        );
        if (fillRect.width <= 0f) return;

        Color fill = fillGradient != null
            ? fillGradient.Evaluate(value01)
            : Color.white;
        Draw.Rectangle(fillRect, Mathf.Max(0f, cornerRadius - padding), fill);
    }
}
```

Set the bar's value from gameplay (`bar.value01 = elixir / maxElixir;`) — `[ExecuteAlways]` means
the editor preview updates live.

---

## 5. Segmented bar (elixir pips)

Discrete segments. Good for the Clash-Royale-style elixir bar.

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class SegmentedBarPanel : ImmediateModePanel {
    [SerializeField] int   segments       = 10;
    [Range(0f, 1f)] public float value01  = 0.7f;

    [SerializeField] Color emptyColor     = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] Color filledColor    = new Color(0.7f, 0.4f, 1f, 1f);
    [SerializeField] float spacing        = 2f;
    [SerializeField] float cornerRadius   = 4f;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        if (segments < 1) return;
        float segWidth = (rect.width - spacing * (segments - 1)) / segments;
        float filledCount = value01 * segments;

        for (int i = 0; i < segments; i++) {
            Rect seg = new Rect(
                rect.x + i * (segWidth + spacing),
                rect.y,
                segWidth,
                rect.height
            );
            float t = Mathf.Clamp01(filledCount - i);
            Color c = Color.Lerp(emptyColor, filledColor, t);
            Draw.Rectangle(seg, cornerRadius, c);
        }
    }
}
```

---

## 6. Circular cooldown / radial fill

Useful for ability cooldowns, card recharge timers.

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class RadialCooldownPanel : ImmediateModePanel {
    [Range(0f, 1f)] public float value01 = 1f;   // 1 = ready, 0 = full cooldown

    [SerializeField] Color color     = Color.white;
    [SerializeField] float thickness = 6f;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f - thickness * 0.5f;

        // Background ring (faint)
        Draw.Ring(center, radius, thickness, new Color(color.r, color.g, color.b, 0.2f));

        // Arc — start at top, sweep clockwise based on value
        if (value01 > 0f) {
            float startAngle = Mathf.PI * 0.5f;                       // 12 o'clock
            float endAngle   = startAngle - Mathf.PI * 2f * value01;  // CW
            Draw.Arc(center, radius, thickness, startAngle, endAngle, ArcEndCap.Round, color);
        }
    }
}
```

---

## 7. Per-corner rounded card (asymmetric corners)

E.g. card top rounded, bottom flush, like a tab or a tray.

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class TabPanel : ImmediateModePanel {
    [SerializeField] Color color          = Color.white;
    [SerializeField] float topLeftRadius  = 20f;
    [SerializeField] float topRightRadius = 20f;
    [SerializeField] float botLeftRadius  = 0f;
    [SerializeField] float botRightRadius = 0f;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        // Draw.Rectangle per-corner overload — order is BL, BR, TR, TL (clockwise from bottom-left)
        Draw.Rectangle(
            rect,
            botLeftRadius, botRightRadius, topRightRadius, topLeftRadius,
            color
        );
    }
}
```

(Double-check the overload ordering against the Shapes API in your version if in doubt — the
`Draw.Rectangle` per-corner overload takes a `Vector4` of radii in `(BL, BR, TR, TL)` order.)

---

## 8. Drop shadow effect

Shapes can't blur, but you can fake a soft shadow by stacking a few offset rounded rects.

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class ShadowedCardPanel : ImmediateModePanel {
    [SerializeField] Color fill         = Color.white;
    [SerializeField] Color shadow       = new Color(0f, 0f, 0f, 0.18f);
    [SerializeField] float cornerRadius = 20f;
    [SerializeField] Vector2 shadowOffset = new Vector2(0f, -6f);
    [SerializeField] int  shadowLayers  = 4;
    [SerializeField] float shadowSpread = 2f;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        for (int i = 0; i < shadowLayers; i++) {
            float t = (i + 1f) / shadowLayers;
            Rect r = new Rect(
                rect.x + shadowOffset.x - shadowSpread * t,
                rect.y + shadowOffset.y - shadowSpread * t,
                rect.width  + shadowSpread * 2f * t,
                rect.height + shadowSpread * 2f * t
            );
            Color c = shadow; c.a *= (1f - t);
            Draw.Rectangle(r, cornerRadius + shadowSpread * t, c);
        }
        Draw.Rectangle(rect, cornerRadius, fill);
    }
}
```

Cheap, infinite-resolution, scales with the canvas.

---

## Style and authority guidelines

- Recipes use `[ExecuteAlways]` so the panel previews in the editor without entering Play mode.
  Don't strip it.
- Public state (e.g. `value01`) is a plain field — there's no MVP/MVVM intermediary; gameplay code
  writes the value directly and the next frame's draw reflects it. Don't overarchitect a tiny panel.
- Keep one visual concept per panel class. The IM canvas batches sibling panels for free; splitting
  is cheap and keeps each script obvious.
- Inputs (touch / pointer) are not the responsibility of `ImmediateModePanel`. If a panel needs to
  receive clicks, put a transparent UGUI `Image` with `Raycast Target = true` on a *different*
  child of the same RectTransform and let the IMPanel handle visuals only.