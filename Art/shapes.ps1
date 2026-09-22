# =====================================================================================
#  Card art shape library.
#
#  Every shape is authored once, pixel-free, in a 1000x1000 design box centred on
#  (500,500). render.ps1 owns pixels, levels, shadows and colour. Keeping the geometry
#  pixel-free is what lets one definition serve the 748px tower sprite, its two nested
#  twins and the .svg source file.
#
#  Path data is the SVG mini-language, which WPF's Geometry.Parse also speaks - so the
#  string built here is literally what lands in the .svg AND what the rasteriser draws.
#  The two can never drift.
#
#  A shape is a SCRIPTBLOCK taking a scale k, not a fixed string, because a tower is
#  drawn as an outline: the outer contour at k=1 plus a shrunken copy of ITSELF at
#  k=hole, filled even-odd. A constant-width stroke (which is what the first pass tried)
#  cannot do that job - 114px of stroke leaves a circle looking like a ring but fills a
#  triangle or a star in solid. Deriving the hole from the shape means every silhouette
#  reads as an outline, however spiky it is, and the wall thickness follows the form.
#
#  kind:
#    ring   - closed contour; drawn as outer + inner(hole) with even-odd fill
#    stroke - open path (arcs, an anchor, a split mirror); drawn with a pen
#    solid  - filled silhouette (troops and spells, matching BaseEnemy1 / meteorite)
# =====================================================================================

[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

# --- primitives -----------------------------------------------------------------------

function F([double]$v) { $v.ToString('F2', [System.Globalization.CultureInfo]::InvariantCulture) }

function Poly($pts, [bool]$close = $true) {
    $d = "M $(F $pts[0][0]),$(F $pts[0][1])"
    for ($i = 1; $i -lt $pts.Count; $i++) { $d += " L $(F $pts[$i][0]),$(F $pts[$i][1])" }
    if ($close) { $d += ' Z' }
    $d
}

function ScalePts($pts, [double]$k, [double]$cx = 500, [double]$cy = 500) {
    $o = @()
    foreach ($p in $pts) { $o += , @(($cx + ($p[0] - $cx) * $k), ($cy + ($p[1] - $cy) * $k)) }
    $o
}

function NGon([int]$n, [double]$r, [double]$rot = -90, [double]$cx = 500, [double]$cy = 500) {
    $pts = @()
    for ($i = 0; $i -lt $n; $i++) {
        $a = ($rot + $i * 360.0 / $n) * [math]::PI / 180
        $pts += , @(($cx + $r * [math]::Cos($a)), ($cy + $r * [math]::Sin($a)))
    }
    Poly $pts
}

function StarPath([int]$n, [double]$rOut, [double]$rIn, [double]$rot = -90, [double]$cx = 500, [double]$cy = 500) {
    $pts = @()
    for ($i = 0; $i -lt $n * 2; $i++) {
        $r = if ($i % 2 -eq 0) { $rOut } else { $rIn }
        $a = ($rot + $i * 180.0 / $n) * [math]::PI / 180
        $pts += , @(($cx + $r * [math]::Cos($a)), ($cy + $r * [math]::Sin($a)))
    }
    Poly $pts
}

# Open arc. Angles in degrees, 0 = +x (right), 90 = +y (DOWN, the SVG convention).
function ArcPath([double]$r, [double]$startDeg, [double]$sweepDeg, [double]$cx = 500, [double]$cy = 500) {
    $a0 = $startDeg * [math]::PI / 180
    $a1 = ($startDeg + $sweepDeg) * [math]::PI / 180
    $x0 = $cx + $r * [math]::Cos($a0); $y0 = $cy + $r * [math]::Sin($a0)
    $x1 = $cx + $r * [math]::Cos($a1); $y1 = $cy + $r * [math]::Sin($a1)
    $large = if ([math]::Abs($sweepDeg) -gt 180) { 1 } else { 0 }
    $sweep = if ($sweepDeg -gt 0) { 1 } else { 0 }
    "M $(F $x0),$(F $y0) A $(F $r),$(F $r) 0 $large $sweep $(F $x1),$(F $y1)"
}

# A ring of `count` equal arcs with equal gaps. The cue that says "support tower".
function DashedRing([double]$r, [int]$count, [double]$duty, [double]$rot = -90) {
    $step = 360.0 / $count
    $parts = @()
    for ($i = 0; $i -lt $count; $i++) { $parts += (ArcPath $r ($rot + $i * $step) ($step * $duty)) }
    $parts -join ' '
}

function FullCircle([double]$r, [double]$cx = 500, [double]$cy = 500) {
    "M $(F ($cx - $r)),$(F $cy) A $(F $r),$(F $r) 0 1 1 $(F ($cx + $r)),$(F $cy) A $(F $r),$(F $r) 0 1 1 $(F ($cx - $r)),$(F $cy) Z"
}

# Vesica / pointed oval: two equal arcs meeting at top and bottom. bulge > 1 rounds it out.
function Vesica([double]$k, [double]$halfHeight = 500, [double]$arcR = 580, [double]$cx = 500, [double]$cy = 500) {
    $h = $halfHeight * $k; $r = $arcR * $k
    "M $(F $cx),$(F ($cy - $h)) A $(F $r),$(F $r) 0 0 1 $(F $cx),$(F ($cy + $h)) A $(F $r),$(F $r) 0 0 1 $(F $cx),$(F ($cy - $h)) Z"
}

# Teardrop: a point on top, a circle at the bottom, joined by its two tangents.
# Scaled about its own bbox centre (500,520) so a shrunken copy stays concentric.
function Droplet([double]$k) {
    $tipY = 520 - 480 * $k
    $tx = 284.30 * $k
    $ty = 520 - 46.40 * $k
    $r = 340 * $k
    "M 500.00,$(F $tipY) L $(F (500 + $tx)),$(F $ty) A $(F $r),$(F $r) 0 1 1 $(F (500 - $tx)),$(F $ty) Z"
}

# One small arrowhead of the troop family, placed anywhere in the design box. Used to build
# a formation out of several bodies.
function Chevron([double]$cx, [double]$cy, [double]$w, [double]$h) {
    Poly @(@($cx, ($cy - $h / 2)), @(($cx + $w / 2), ($cy + $h / 2)), @($cx, ($cy + $h / 6)), @(($cx - $w / 2), ($cy + $h / 2)))
}

# A torn seam: a lens profile (widest in the middle) with both edges broken into zigzags.
# Distinct from a lightning bolt, which is what a plain zigzag polygon reads as - and which
# belongs to Chain, not to a slow zone.
# The zigzag is a FRACTION of the local half-width, never a fixed offset: a fixed one is
# larger than the profile near the tapered ends, so each edge crossed the centreline and
# the tear broke into a stack of loose diamonds.
function JaggedLens([double]$maxW = 235, [double]$top = 110, [double]$bottom = 890,
                    [int]$n = 9, [double]$zigFrac = 0.55, [double]$waist = 0.30, [double]$cx = 500) {
    $h = $bottom - $top
    $halfWidth = { param($i) $maxW * ($waist + (1 - $waist) * [math]::Sin([math]::PI * $i / $n)) }
    $pts = @(, @($cx, $top))
    for ($i = 1; $i -lt $n; $i++) {
        $w = & $halfWidth $i
        $z = if ($i % 2 -eq 1) { $w * $zigFrac } else { - $w * $zigFrac }
        $pts += , @(($cx - $w + $z), ($top + $h * $i / $n))
    }
    $pts += , @($cx, $bottom)
    for ($i = $n - 1; $i -ge 1; $i--) {
        $w = & $halfWidth $i
        # SAME sign as the left edge at this index, so the tear snakes sideways at roughly
        # constant width. Opposing signs pinch both edges inward together and the shape
        # falls apart into a stack of loose diamonds.
        $z = if ($i % 2 -eq 1) { $w * $zigFrac } else { - $w * $zigFrac }
        $pts += , @(($cx + $w + $z), ($top + $h * $i / $n))
    }
    Poly $pts
}

# --- the shapes -----------------------------------------------------------------------

function Get-ShapeLibrary {
    $lib = @{}

    # ---- damage towers: closed figures, carry an attack colour -----------------------
    $lib['Stinger'] = @{
        kind = 'ring'; hole = 0.56
        make = { param($k) NGon 3 (500 * $k) -90 }
        note = 'Triangle. The cheapest turret gets the simplest polygon - it continues Slam(5) and Dart(6) downward.'
    }
    $lib['Needle'] = @{
        kind = 'ring'; hole = 0.50
        make = { param($k) Poly (ScalePts @(@(500, 0), @(700, 500), @(500, 1000), @(300, 500)) $k) }
        note = 'Tall narrow spindle. Long reach, one heavy shot.'
    }
    $lib['Beacon'] = @{
        kind = 'ring'; hole = 0.55
        make = { param($k) Vesica $k }
        note = 'Vesica / lens. A beam emitter that locks onto one target and bores in.'
    }
    $lib['Mortar'] = @{
        kind = 'ring'; hole = 0.62
        make = { param($k) NGon 8 (500 * $k) 22.5 }
        note = 'Octagon. Heavy, chunky, sits still - a magazine of shells.'
    }
    $lib['Chain'] = @{
        kind = 'ring'; hole = 0.52
        make = { param($k) StarPath 6 (500 * $k) (255 * $k) -90 }
        note = 'Hexagram. Six spikes reaching outward = the bolt jumping on.'
    }
    $lib['Shard'] = @{
        kind = 'ring'; hole = 0.50
        make = { param($k) StarPath 4 (500 * $k) (150 * $k) -90 }
        note = 'Four-point splinter, deeply concave. Fragments spraying off a kill.'
    }

    # ---- support towers: open / broken figures, stay white ---------------------------
    $lib['Prism'] = @{
        kind = 'stroke'; strokeFrac = 0.100
        make = { param($k) DashedRing 460 3 0.72 }
        note = 'Three fat arcs. A slow field: coarse, wide, and it grips nothing on its own.'
    }
    $lib['Torniquete'] = @{
        kind = 'stroke'; strokeFrac = 0.105
        make = { param($k) ((ArcPath 460 20 140), (ArcPath 460 200 140)) -join ' ' }
        note = 'Two opposed C-clamps closing on nothing. Strips armour, deals no damage.'
    }
    $lib['Anel'] = @{
        kind = 'stroke'; strokeFrac = 0.075
        make = { param($k) DashedRing 460 8 0.58 }
        note = 'A fine dashed halo. Prism''s family at a finer grain - it buffs allies rather than gripping enemies.'
    }
    # The stem deliberately stops AT the crossbar instead of running the full height. Every
    # tower silhouette has to keep its centre open, because levels 2 and 3 are the same
    # shape nested concentrically inside it - a shaft through the middle leaves nowhere for
    # them to go.
    $lib['Ancora'] = @{
        kind = 'stroke'; strokeFrac = 0.090; anchor = @(500, 635)
        make = { param($k) 'M 500,150 L 500,330 M 130,330 L 870,330 M 160,330 Q 160,940 500,940 Q 840,940 840,330' }
        note = 'Stock, crossbar and flukes. An anchor: it holds one enemy where it stands.'
    }
    # Split across the mirror line, not down it - the horizontal cut is what leaves a centre
    # for the nested levels, and two chevrons facing apart read as a reflection anyway.
    $lib['Espelho'] = @{
        kind = 'stroke'; strokeFrac = 0.090
        # The 160-unit gap is deliberate: the stroke plus its round caps eats ~70 of it, and
        # at 90 the two halves closed up into a plain diamond.
        make = { param($k) ((Poly @(@(110, 420), @(500, 30), @(890, 420)) $false),
                            (Poly @(@(890, 580), @(500, 970), @(110, 580)) $false)) -join ' ' }
        note = 'A diamond cut along its mirror line. What this tower sends goes to the other field.'
    }
    # Droplet scales about (500,520), not (500,500) - the anchor has to be the point the
    # shape shrinks toward, or the nested levels drift off centre.
    $lib['Fonte'] = @{
        kind = 'ring'; hole = 0.58; anchor = @(500, 520)
        make = { param($k) Droplet $k }
        note = 'Droplet. Generates mana and nothing else.'
    }

    # ---- troop cards: filled silhouettes, the arrowhead family -----------------------
    $lib['Cisma'] = @{
        kind = 'solid'
        make = { param($k) ((Poly @(@(450, 60), @(450, 735), @(100, 940))), (Poly @(@(550, 60), @(550, 735), @(900, 940)))) -join ' ' }
        note = 'An arrowhead cut in half. It splits when killed, and the halves split again.'
    }
    $lib['Miragem'] = @{
        kind = 'solid'
        make = { param($k) ((Poly @(@(500, 140), @(790, 880), @(500, 685), @(210, 880))),
                            (Poly @(@(178, 345), @(352, 940), @(178, 795), @(20, 940))),
                            (Poly @(@(822, 345), @(980, 940), @(822, 795), @(648, 940)))) -join ' ' }
        note = 'One attacker flanked by two smaller echoes. Two of the three are lies.'
    }
    # Shoulders, then a flat nose. A plain trapezoid read as a banner rather than as one of
    # the arrowheads; the shoulders put it back in the family while keeping the blunt tip.
    $lib['Ram'] = @{
        kind = 'solid'
        make = { param($k) Poly @(@(430, 150), @(570, 150), @(645, 330), @(915, 850), @(500, 665), @(85, 850), @(355, 330)) }
        note = 'A blunt-nosed wedge, flat where the others are pointed - slow and heavy, until it dashes.'
    }
    # Swarm and MiniBoss were not flagged as placeholders but had no art of their own either:
    # Swarm's card was pointing at Robot.png from the Quantum Console demo scene, and MiniBoss
    # was borrowing BaseEnemy1 from the basic troop.
    $lib['Swarm'] = @{
        kind = 'solid'
        make = { param($k) (@((Chevron 500 200 270 280),
                              (Chevron 330 500 270 280), (Chevron 670 500 270 280),
                              (Chevron 170 800 270 280), (Chevron 500 800 270 280), (Chevron 830 800 270 280)) -join ' ') }
        note = 'Six bodies in formation. Ten fragile fodder that flood the lane at once.'
    }
    $lib['MiniBoss'] = @{
        kind = 'solid'
        # A crowned arrowhead. Side barbs were tried twice and both times the silhouette read
        # as a six-point star - indistinguishable from Chain's hexagram and pointing nowhere.
        # Keeping the outline convex below the crown is what holds the arrow reading.
        make = { param($k) Poly @(@(500, 30), @(640, 330), @(790, 110), @(820, 390), @(920, 920),
                                  @(500, 640), @(80, 920), @(180, 390), @(210, 110), @(360, 330)) }
        note = 'A crowned arrowhead. The heaviest single body a player can send.'
    }
    $lib['Shadow'] = @{
        kind = 'solid'
        make = { param($k) 'M 500,40 C 590,300 700,620 820,940 L 500,735 L 180,940 C 300,620 410,300 500,40 Z' }
        note = 'Swept, concave, narrow. A lone runner nothing can slow down.'
    }

    # ---- spell icons: filled silhouettes, no shadow ----------------------------------
    $lib['Ferrugem'] = @{
        kind = 'solid'; evenOdd = $true
        make = { param($k) ('M 500,55 L 880,200 L 880,545 C 880,762 702,902 500,965 C 298,902 120,762 120,545 L 120,200 Z ' +
                            (FullCircle 82 382 420) + ' ' + (FullCircle 58 612 558) + ' ' + (FullCircle 44 452 668)) }
        note = 'A shield eaten through by pits. Ferrugem corrodes armour off whatever walks into it.'
    }
    # A leaf blade on a thin shaft, with a crossguard. The first pass used a triangle the
    # same width as the shaft, which read as a pencil.
    $lib['Lance'] = @{
        kind = 'solid'
        make = { param($k) ('M 955,45 Q 905.30,250.30 700,300 Q 749.70,94.70 955,45 Z ' +
                            'M 750.30,190.30 L 809.70,249.70 L 169.70,889.70 L 110.30,830.30 Z ' +
                            'M 819,343.60 L 783.60,379 L 621,216.40 L 656.40,181 Z') }
        note = 'Leaf blade, crossguard, shaft. One narrow line of damage straight down the lane.'
    }
    $lib['Rift'] = @{
        kind = 'solid'
        make = { param($k) JaggedLens }
        note = 'A jagged tear, widest in the middle. The rift that slows what walks into it.'
    }

    $lib
}
