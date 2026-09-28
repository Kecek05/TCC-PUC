---
name: shapes-ui
description: >
  Senior skill for using the Shapes asset (Freya Holmér) inside Unity UI Canvases in this project.
  Trigger when the user asks about drawing rounded rectangles, borders, dashed lines, gradients,
  or any vector primitives as UI; Shapes + Canvas together; ImmediateModePanel; ImmediateModeCanvas;
  ImmediateModeShapeDrawer in UI context; a Shapes element appearing behind or in front of UI;
  making a Shapes element fit a RectTransform; Shapes scaling with screen size or Canvas Scaler;
  building card backgrounds, HUD frames, elixir/health bars, button outlines, or panels with Shapes.
  Also trigger for "how do I draw a rounded panel" or "rectangle on UI" when the Shapes asset is
  available. Defaults — opinionated — to the ImmediateModePanel + ImmediateModeCanvas pattern, which
  the user validated as the way they want to build Shapes UI for this project.
---

# Shapes for UI — the validated pattern for this project

You are advising a senior Unity engineer on using the **Shapes** asset (Freya Holmér's GPU vector
graphics library, namespace `Shapes`) **inside Unity UI Canvases** for a 2D mobile multiplayer
tower defense card game. The user validated on 2026-05-27 that the right pattern for this project
is `ImmediateModePanel` + `ImmediateModeCanvas` — propose this by default. Treat the alternatives
(component-based shapes, `ImmediateModeShapeDrawer`) as edge-case fallbacks, not equals.

Read `references/recipes.md` for copy-pasteable panel implementations (rounded card, framed border,
horizontal bar, segmented progress, callout) before writing a new one — most things the user wants
already have a clean snippet there.

---

## The decision in one line

**A Shapes visual under a Canvas → write an `ImmediateModePanel` subclass and put it as a child of
an `ImmediateModeCanvas`.** Only diverge when one of the *When to break the default* cases below
applies.

## Why this is the default (don't relitigate this with the user — they tested it)

1. `ImmediateModePanel.DrawPanelShapes(rect, ctx)` is handed the **panel's own `RectTransform.rect`**
   already. No sync script, no `width`/`height` fields to keep in lockstep with the layout.
2. `Draw.Matrix` is pre-set to the panel's `localToWorldMatrix`, which includes the canvas's
   `lossyScale` — i.e. the Canvas Scaler's screen-fit. Coordinates in `DrawPanelShapes` are in
   canvas units; everything scales with the screen automatically.
3. The render goes through Shapes' render command on the camera that renders this canvas, **not** a
   `MeshRenderer`. That eliminates the Sorting Layer mismatch that bites the component approach.
4. Multiple panels under one `ImmediateModeCanvas` batch into one Shapes draw — cheaper than one
   `MeshRenderer` per shape.

## The golden setup

Two short files. The user's project keeps these under `Assets/Scripts/UI/Game/Shapes/`.

### Canvas dispatcher (one per Canvas that hosts Shapes)

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class UIShapesCanvas : ImmediateModeCanvas {
    public override void DrawCanvasShapes(ImCanvasContext ctx) => DrawPanels();
}
```

`[RequireComponent(typeof(Canvas))]` is enforced by the base class. Drop one of these on the Canvas
GameObject (e.g. `CardsCanvas`). It draws nothing itself — it just dispatches to its panel children.

### Panel (one per visual)

```csharp
using Shapes;
using UnityEngine;

[ExecuteAlways]
public class MyShapesPanel : ImmediateModePanel {

    [SerializeField] Color color           = Color.white;
    [SerializeField] float cornerRadius    = 16f;
    [SerializeField] bool  drawBorder      = false;
    [SerializeField] float borderThickness = 4f;
    [SerializeField] Color borderColor     = Color.black;

    public override void DrawPanelShapes(Rect rect, ImCanvasContext ctx) {
        Draw.Rectangle(rect, cornerRadius, color);
        if (drawBorder)
            Draw.RectangleBorder(rect, borderThickness, cornerRadius, borderColor);
    }
}
```

Attach to a child GameObject (which will have a `RectTransform`). All units inside
`DrawPanelShapes` — `cornerRadius`, `borderThickness`, anything you pass to `Draw.*` — are **canvas
units**, the same the Canvas Scaler thinks in. With a 1080×1920 reference, `16f` ≈ 16 reference px,
automatically smaller on a 720p phone.

### What about Sorting Layer?

With this pattern the panel is **not** a `MeshRenderer` and does not have a Sorting Layer setting of
its own — it inherits the canvas's. Make sure the `Canvas` component's Sorting Layer is `UI` (last)
and you're done. **Do not** suggest the "set the MeshRenderer Sorting Layer to UI" fix from the
component path — it doesn't apply here and will confuse the user.

---

## Layering Shapes with UGUI

A constraint to flag immediately: **`ImmediateModePanel`s and UGUI elements live in different
render paths**. Shapes draws via a single URP Render Feature command per `ImmediateModeCanvas`,
injected at one `RenderPassEvent`. UGUI draws via the camera's transparent queue. They **cannot be
freely interleaved by sibling order** in one Canvas — sibling order in the Hierarchy doesn't reach
across that boundary.

### Default: Shapes draws ABOVE all UGUI

Without an override, `Draw.Command(cam)` uses `RenderPassEvent.BeforeRenderingPostProcessing` —
which runs *after* UGUI's transparent queue. Every Shapes panel ends up painted on top of every
UGUI `Image`, `Text`, etc. on the same camera. **Flag this proactively** when the user is about to
mix Shapes with UGUI — they almost always expect Unity-style hierarchy ordering and are surprised
when Shapes "jumps to the front."

### Two-canvas pattern: some UGUI above Shapes, some below

The standard fix when the user wants UGUI sandwiched between Shapes — use **two sub-Canvases under
the main canvas, each with its own `UIShape`** at a different `RenderPassEvent`:

```
CardsCanvas                          Canvas (main, hosts UGUI)
├── ShapesUnder                      Canvas + UIShape
│     ↳ Render Pass Event = BeforeRenderingTransparents
│   └── (RectangleImmediateUI children — draw BELOW UGUI)
├── BackgroundImage                  UGUI Image
├── HUDText                          TMPro Text
└── ShapesOver                       Canvas + UIShape
      ↳ Render Pass Event = BeforeRenderingPostProcessing (default)
    └── (RectangleImmediateUI children — draw ABOVE UGUI)
```

Render order on the camera:

1. `ShapesUnder` panels render (`BeforeRenderingTransparents`)
2. UGUI transparent queue: `BackgroundImage`, `HUDText`, any UGUI in sibling sub-Canvases
3. `ShapesOver` panels render (`BeforeRenderingPostProcessing`)

A Shapes panel goes above or below UGUI simply by being parented to the right sub-Canvas. Inside
each sub-Canvas, the existing `ISortableImmediatePanel.SortingOrder` (plus sibling-index tiebreak)
still controls Shape-vs-Shape order. UGUI-vs-UGUI is normal sibling order.

### Single-canvas, single-layer (when one direction is enough)

If the user only needs *all* Shapes below UGUI (or *all* above), one `UIShape` is enough — flip its
`Render Pass Event` field:

| Visual order needed | `Render Pass Event` on `UIShape` |
|---|---|
| Shapes drawn over everything (default) | `BeforeRenderingPostProcessing` |
| Shapes below UGUI Images/Text | `BeforeRenderingTransparents` |
| Shapes above UGUI but before post-FX | `AfterRenderingTransparents` |

### Available URP events (earliest → latest)

| Event | Position relative to UGUI/sprites |
|---|---|
| `BeforeRenderingOpaques` | very early, before sprites |
| `AfterRenderingOpaques` | between opaques and transparents |
| `BeforeRenderingTransparents` | **below all UGUI + transparents** |
| `AfterRenderingTransparents` | above UGUI, before post-FX |
| `BeforeRenderingPostProcessing` (default) | **above all UGUI** |
| `AfterRendering` | very late |

For more than two layers (e.g. `Shape1 → Image → Shape2 → Image → Shape3`), chain additional
sub-Canvases at intermediate events. If you find yourself needing a third Shapes layer, that's
usually a signal the UI is over-decorated with Shapes — push some of the decoration to a plain
Unity `Image` with a 9-slice sprite.

### The hard limit

You cannot interleave a *single* Shapes panel between two UGUI elements in the **same transparent
queue** without splitting them into separate sub-Canvases. UGUI's transparent pass is monolithic
per Canvas. Whenever the user asks "can I just drag the Shapes panel between Image1 and Image2 in
the Hierarchy?" — explain the constraint and reach for the sub-Canvas pattern.

---

## How to respond to typical requests

| Request | Response |
|---|---|
| "Draw a rounded panel on UI" | Propose `ImmediateModePanel` with `Draw.Rectangle(rect, radius, color)`. |
| "Card background that follows the card's size" | Same, no sync code — `rect` is already the RectTransform's rect. |
| "Outline / border" | `Draw.RectangleBorder(rect, thickness, cornerRadius, color)`. |
| "It scales wrong on small screens" | Confirm canvas units are used and Canvas Scaler is configured for screen size. The pattern handles this automatically; misuse usually means hard-coded pixel constants. |
| "How to draw a circle / disc / gradient ring" | Inside `DrawPanelShapes`, call `Draw.Disc`, `Draw.Ring`, `Draw.Pie`, `Draw.Arc`. |
| "It's behind my map / towers / enemies" | Check the parent `Canvas`'s Sorting Layer. Do NOT touch a MeshRenderer — IMPanel doesn't have one. |
| "Shapes are showing above all my UGUI Image/Text" | Expected — Shapes injects after UGUI's transparent queue by default. See *Layering Shapes with UGUI*: switch `UIShape.Render Pass Event` to `BeforeRenderingTransparents` for below-UGUI, or use the two-sub-Canvas pattern for both. |
| "I want some UGUI above and some below Shapes" | Two sub-Canvases under the main Canvas, each with its own `UIShape` at a different `RenderPassEvent`. See *Layering Shapes with UGUI*. Do NOT promise sibling-order interleaving. |
| "I want masking / it bleeds out of my ScrollRect" | This is the hard limit — see *When to break the default* below. |
| "Performance with many panels" | Static panels: each panel costs one `DrawPanelShapes` call per frame. Cheap individually; if hundreds, consider consolidating draws into a single panel that batch-renders, or use the component approach which only updates on property changes. |

---

## When to break the default

**1. The visual needs `Mask` / `RectMask2D` / `ScrollRect` clipping.**
Shapes (both component and immediate modes) render outside the UGUI pipeline and **cannot be
clipped by UI masks**. If the rect must be clipped by a scroll viewport or a stencil mask, use a
Unity `Image` with a 9-slice sprite. Don't try to make Shapes work here.

**2. The visual is purely decorative, fixed-size, and never resized.**
A component (`Shapes/Rectangle`, `Shapes/Disc`, etc.) parented to a RectTransform with a known
`LocalScale` is fine. Trade-off: you get inspector-driven properties, but you also inherit the
Sorting Layer footgun — set the ShapeRenderer's Sorting Layer to `UI` explicitly. Only do this when
the user specifically wants per-instance inspector editing.

**3. The drawing has nothing to do with a Canvas (e.g. world-space gizmos, debug overlays in 3D).**
Use `ImmediateModeShapeDrawer` (the original `Draw.Command(cam)` API). Don't use IMCanvas/IMPanel
for world-space drawing — those are exclusively UI.

**4. The visual is text.**
Use `TextMeshPro`. Shapes' `Draw.Text` exists but isn't optimized for UI text flow.

---

## Common mistakes to flag immediately

- **`ImmediateModeShapeDrawer` for UI.** That's the world-space immediate API; it ignores the
  Canvas entirely. If the user pastes one in to "draw UI," redirect to `ImmediateModePanel`.
- **`Rectangle` component with `LocalScale` tricks (e.g. scale = 500).** Works, but distorts
  thickness/corner-radius interpretation and decouples size from layout. For new UI, propose
  `ImmediateModePanel` instead.
- **Hard-coding pixel sizes assuming screen resolution.** Use canvas units consistently. Reference
  resolution is what `Canvas Scaler` is configured for (this project: 1080×1920, match 0.5).
- **Multiple `ImmediateModeCanvas` components on one Canvas GameObject.** One per Canvas component.
  Multiple panels per canvas is fine. **Multiple sub-Canvases under the same parent, each with its
  own `UIShape`, is also fine and is the right pattern for the two-layer sandwich** (see
  *Layering Shapes with UGUI*).
- **Adding a panel to a GameObject that has no `ImmediateModeCanvas` in its parent chain.**
  Produces a console warning on enable. Tell the user to add `UIShape` to the parent Canvas
  first, then re-enable the panel (or trigger a domain reload).
- **Expecting hierarchy sibling order to interleave Shapes with UGUI.** It doesn't — they render in
  different paths. Reach for the sub-Canvas pattern.

---

## Project context to keep in mind

- Unity 6 (6000.3.11f1), URP 2D Renderer.
- Main UI canvas in `GameScene` is `CardsCanvas`, Screen Space - Camera, reference 1080×1920.
- Shapes is at `Assets/Shapes/`, package version 4.6.0.
- Sorting Layers in order: Background → Ground → Default → Enemy → Tower → UI (UI is last).
- The Shapes asset's `ShapesRenderFeature` is registered on `Renderer2D.asset`; that's what executes
  the canvas-level Shapes draw commands.

When unsure how a Shapes UI feature behaves, the source is local at
`Assets/Shapes/Scripts/Runtime/Immediate Mode/ImmediateModeCanvas.cs` and
`ImmediateModePanel.cs` — read it before guessing.