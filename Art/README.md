# Card art — vector sources

Every card that was still on placeholder art now has its own vector shape. The shapes are
authored here as code, rendered to `.svg` sources and to the `.png` sprites Unity imports.

```
Art/shapes.ps1        the shape library - geometry only, no pixels
Art/render.ps1        pixels, levels, shadows, the card manifest
Art/Vector/**.svg     editable source, one per card
Assets/Sprites/**.png what Unity imports
```

Re-render everything:

```powershell
powershell -ExecutionPolicy Bypass -File Art\render.ps1
```

It overwrites only the generated files and never touches the four hand-drawn towers
(Circle, Square, Slam, Dart) or the wave enemies. After running it, let Unity import, then
re-apply the sprite settings if the import defaults come back wrong (see *Import settings*).

## Why a script instead of a drawing app

The three tower levels have to nest concentrically and the whole set has to stay
consistent with art that already exists. Both are constraint problems, not drawing
problems — see *Solved, not authored* below. Authoring the geometry once and deriving
everything else means a level-2 sprite can never drift from its level 1, and re-balancing
a shape is one number rather than three redraws.

`Geometry.Parse` in WPF speaks the same path mini-language as SVG, so one string is both
what lands in the `.svg` and what the rasteriser draws. They cannot disagree.

## The style, measured

Nothing here was invented — every constant came off the existing sprites:

| | value | measured on |
|---|---|---|
| tower canvas | 748×748 | CircleTower1 |
| level ratios | 1 : 0.466 : 0.241 | CircleTower1/2/3 |
| drop shadow | +22, +30 at level 1 | CircleTower1, SquareTower1 |
| troop silhouette | 496 white inside 582 black | BaseEnemy1 |
| spell icon | 512×512, edge to edge, no shadow | meteorite, snowflakes |
| pixels-per-unit | towers 1300, troops 2000, spells 1300 | the sprite `.meta` files |

Art is authored **white**. Colour is a tint — `CardDataSO.CardColor` for the card and
`SpriteRenderer.color` on the prefab — which is how Dart, Square and Slam already work. A
card can be recoloured without re-rendering, and the baked black shadow stays black under
any tint because anything times zero is zero.

## Solved, not authored

Two things the four hand-drawn towers only *look* consistent about:

**Where the shape sits.** Centring each level's bounding box drifts for anything not
symmetric about its own centre — a triangle's bbox centre is nowhere near the point it
shrinks toward, so the nested copies crawl upward. The hand-drawn pentagon shows the right
answer: SlamTower1/2/3 have bbox centres at y 342 / 358.5 / 369, *converging* on 374 rather
than sitting on it, which is the signature of anchoring the shape's own centre. Every shape
declares that point, defaulting to (500,500).

**How big the nested levels are.** 0.466× works for a circle and drops level 2 straight onto
level 1's wall on a triangle or a hexagram. `Fit-Next` binary-searches the largest size at
which the next level's ink misses this one's, probing with the real silhouette rather than a
circle — a triangle nests in a triangular hole at nearly its full width, while the largest
circle that fits there is less than half of it. Ten of the twelve towers clear the reference
ratio and are drawn at it; Needle, Shard and Ancora nest tighter because their shapes demand it.

## The shape vocabulary

A tower is an **outline**: the outer contour plus a shrunken copy of itself, filled even-odd.
A constant-width stroke was tried first and cannot do the job — 114px leaves a circle looking
like a ring but fills a triangle or a star in solid.

- **Damage towers are closed figures and carry a colour.** Stinger a triangle (continuing
  Slam's pentagon and Dart's hexagon downward), Needle a spindle, Beacon a lens, Mortar an
  octagon, Chain a hexagram, Shard a four-point splinter.
- **Support towers are open or broken figures and stay white.** Prism three fat arcs, Anel the
  same idea at a finer grain, Tourniquet two opposed clamps, Ancora an anchor, Espelho a
  diamond cut along its mirror line, Fonte a droplet.
- **Troops are filled silhouettes in the arrowhead family**, the way BaseEnemy1 and its
  variants are — split (Cisma), echoed (Miragem), blunt (Ram), swept (Shadow), in formation
  (Swarm), crowned (MiniBoss).
- **Spell icons are filled illustrations** at 512, matching meteorite and snowflakes.

Every tower silhouette must keep its **centre open**, because levels 2 and 3 nest inside it.
That constraint is why Ancora's stock stops at the crossbar instead of running the full height,
and why Espelho is split horizontally rather than down the middle.

## Import settings

Unity's defaults are wrong for these (pixels-per-unit especially). The settings are applied
once per file and stored in the `.meta`, so they survive; if a file is ever re-added from
scratch, set: texture type **Sprite (2D and UI)**, mode **Single**, pivot **Center**,
**Alpha Is Transparency** on, mipmaps off, wrap **Clamp**, and the pixels-per-unit from the
table above.

## Editing a shape

Change it in `shapes.ps1` and re-run `render.ps1`. Editing the `.svg` by hand works for a
one-off, but the next render overwrites it — port the change back into the library.

Numbers live in a 1000×1000 design box centred on (500,500); nothing in `shapes.ps1` knows
about pixels.
