# Card art

Every card's art is generated: the towers and troops from geometry written as code, the spell emblems from Codex
renders. The design it implements is the **Card Art Redesign** canvas
(https://claude.ai/artifact/WXNvaiWccKprNdVeUFa7uC).

```
Art/CardArt/shapes.py          the shape library: towers (three levels each) and troop cards
Art/CardArt/render.py          pixels: canvases, shadows, outlines, files
Art/CardArt/spells/            the Codex brief (prompts.tsv, run_one.sh) and the raw renders
Art/CardArt/wave-originals/    the hand-drawn wave enemies, as they were before the re-ink
Art/Vector/**.svg              editable source, one per card, written by render.py
Art/SpellFx/                   the spell EFFECT sprites and sounds (a separate recipe, see its README)
Assets/Sprites/**.png          what Unity imports
```

Re-render everything (needs `pip install pillow numpy shapely`):

```
python Art/CardArt/render.py
```

It writes every sprite under the file name the game already imports, so the `.meta` beside it (pixels per unit,
mesh type, guid) is untouched and no prefab or card asset has to be re-pointed. `--out <folder>` renders into a
staging folder instead of `Assets/`.

## The rules of the set

1. **One primitive, one idea.** A cut, a split or a repeat. Never an illustration.
2. **Spikes outside, simple inside.** The hollow of a tower body is always a plain cell, so what nests in it never
   tangles. The first generation nested shrunken copies of the outline, which turned a star into noise.
3. **A level adds a part:** level 1 is the body, level 2 the chamber, level 3 the core. Only the five primitives
   (Circle, Stinger, Square, Slam, Dart) nest copies of themselves, because there the copy is the idea.
4. **Nothing under 5% of the sprite.** Walls are 11 to 17%, gaps 5% or more: every tier survives at 46 px, the
   size a tower is drawn at on a 540 px wide screen.
5. **Closed and coloured deals damage. Open and white supports.** Colour still means that colour's damage.
6. **Troops share one line weight** (34 px at 2000 pixels per unit), and a card emblem is never a lane body.
7. **Spells are filled emblems from one hand.**

## Towers

Three sprites per tower on one 748 px canvas with one pivot, stacked by the prefab's `GFX/Level1`, `Level2`,
`Level3`. A 500-unit body lands 339 px from the pivot, which leaves room for the hard shadow: +22, +30 px at
level 1, easing to 0.76 and 0.58 of that for levels 2 and 3. `<Name>Tower1_NO_SHADOW.png` is the level 1 face
alone (the placement ghost uses it).

Walls and gaps are true offsets (shapely buffers), not scaled copies, so a wall is the same thickness all the way
round whatever the silhouette. Three families:

- **Primitives**: a polygon nested three times, corners rounded so the walls can be thick.
- **Specials** (Mortar, Needle, Beacon, Chain, Shard): a ring, one feature outside it, and a signature inside,
  which is what the tower fires. Mortar's level 2 is its magazine of four chambers; Needle's level 3 its crosshair.
- **Support** (Prism, Tourniquet, Anel, Ancora, Espelho, Fonte): open figures built from separate strokes, which
  assemble as they level. Ancora is flukes, then shank and stock, then the eye.

New art with padding around its shape must import with a **Tight** sprite mesh: status overlays and the level
badge are sized from the mesh, not from the texture's rect.

## Troops

Arrowheads, each one a single cut away from the basic body. `<Name>.png` is the lane body inside its black line,
`<Name>_NO_OUTLINE.png` the body alone. Where a card shows a group (Swarm, Miragem) the group is a separate
`<Name>_Card.png` and the lane sprite is one body: enemy prefabs are all at scale 1, so a troop's size on the lane
is whatever its sprite draws (`TROOPS` in `shapes.py` carries that size; a Swarm unit is 0.6 of a standard body).

The wave enemies (`BaseEnemy1`, `_Fast`, `_Tank`, `BaseEnemy2_Normal`, `BaseEnemyBoss`) keep their hand-drawn
silhouettes. `render.py` re-inks them from `wave-originals/`: the white body is kept pixel for pixel and the black
line is redrawn round it at the shared weight, from a distance field.

## Spells

`spells/run_one.sh <name> "<subject>"` asks Codex for one emblem, white on pure black; `prompts.tsv` holds the seven
subjects and the one style brief they share. `render.py` turns each raw render into `<icon>.png` (white, alpha =
brightness, 512 px, no shadow) and `<icon>_card.png` (the same at 0.9 over a hard shadow), which is what a spell's
`CardImage` points at.

## On a card

A card's image is the sprite that carries its own shadow or line: a tower's shadowed level 1, a troop's outlined
emblem, a spell's `_card` emblem. The face's lip and the cost bubble's lip are a mesh effect (`FaceLip`), and how
big each family's art is drawn on the face lives in `CardFaces.asset` for the hand and in each card's
`CustomSizeCardInMenu` for the deck page.

## Editing a shape

Change it in `shapes.py` and re-run `render.py`. Editing an `.svg` by hand works for a one-off, but the next render
overwrites it: port the change back into the library. Numbers live in a 1000 x 1000 design box centred on (0,0),
y down.
