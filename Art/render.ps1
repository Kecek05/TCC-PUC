# =====================================================================================
#  Card art renderer.   powershell -ExecutionPolicy Bypass -File Art\render.ps1
#
#  Turns the pixel-free shapes in shapes.ps1 into, for every card that was still on
#  placeholder art:
#     * one .svg source per card under Art/Vector/   (editable, resolution-independent)
#     * the .png sprites Unity imports, under Assets/Sprites/
#
#  Every constant below was MEASURED off the four hand-drawn towers and the wave enemies,
#  so the generated art sits beside Circle/Square/Slam/Dart without looking imported:
#
#     canvas 748, drop shadow +22,+30 at level 1, easing down with the shape
#     level 2 is 0.466x level 1 and level 3 is 0.241x   (CircleTower1/2/3)
#     troops: white silhouette + black outline ~8% outward   (BaseEnemy1/2)
#     spells: pure white, edge to edge, no shadow            (meteorite/snowflakes)
#
#  TWO THINGS ARE SOLVED RATHER THAN AUTHORED, because the four references were drawn by
#  hand and a flat number reproduces neither:
#
#  1. WHERE the shape sits. A tower's three sprites stack on the prefab's Level1/2/3
#     renderers, so they have to be concentric. Centring each level's BOUNDING BOX drifts
#     for anything that is not symmetric about its own centre: a triangle's bbox centre is
#     nowhere near the point it shrinks toward, so the nested copies crawl upward. The
#     hand-drawn pentagon shows the right answer - SlamTower1/2/3 have bbox centres at
#     y 342 / 358.5 / 369, converging on 374 rather than sitting on it, which is the
#     signature of anchoring the SHAPE's centre and letting the bbox fall where it falls.
#     Every shape therefore declares the point it scales about, defaulting to (500,500).
#
#  2. HOW BIG the nested levels are. 0.466x works for a circle but drops level 2 straight
#     onto level 1's wall on a triangle or a hexagram, whose holes are far tighter. So the
#     ratio is the reference one where it fits and binary-searched down where it does not,
#     probing with the real silhouette (a triangle nests in a triangular hole at nearly
#     its full width; the largest circle that fits there is less than half of it).
#
#  Art is authored WHITE. Colour is a tint (CardDataSO.CardColor and the prefab's
#  SpriteRenderer.m_Color), exactly as Dart/Square/Slam already do it - so a card can be
#  recoloured without re-rendering, and the black shadow stays black under any tint.
# =====================================================================================

[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$SkipSvg
)

$ErrorActionPreference = 'Stop'
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
. (Join-Path $PSScriptRoot 'shapes.ps1')

# --- measured style constants ---------------------------------------------------------

$TowerCanvas = 748
$TroopCanvas = 748
$SpellCanvas = 512

$LevelRatio = @(1.0, 0.466, 0.241)   # CircleTower1/2/3, measured
$NestMargin = 0.90                   # breathing room when a shape must nest tighter
$EdgePad = 5.0                       # keeps antialiasing off the canvas edge

$ShadowDx = 22.0
$ShadowDy = 30.0

# BaseEnemy1 measures 496px of white inside 582px of black on a 748 canvas - troops sit
# well inside their sprite, unlike the towers which fill it. A first pass at 700 ran them
# off the edge of the canvas.
$TroopOuter = 582.0                  # total extent INCLUDING the black outline
$TroopOutlineFrac = 0.173
$SpellOuter = 500.0

$White = [System.Windows.Media.Brushes]::White
$Black = [System.Windows.Media.Brushes]::Black
$Invariant = [System.Globalization.CultureInfo]::InvariantCulture

# --- geometry helpers -----------------------------------------------------------------

function New-Geometry([string]$pathData, [bool]$evenOdd) {
    $g = [System.Windows.Media.Geometry]::Parse($pathData)
    if ($evenOdd) {
        if ($g.IsFrozen) { $g = $g.Clone() }
        $g.FillRule = [System.Windows.Media.FillRule]::EvenOdd
    }
    $g
}

# Miter keeps a star's or a spindle's points sharp, which is the whole look of the towers.
# The troops' outline pen has to be round instead: mitred at 8% of a 500px silhouette, the
# spike off an arrowhead's tip ran the black bbox to 722px against the reference's 583.
function New-Pen([System.Windows.Media.Brush]$brush, [double]$thickness, [bool]$round = $false) {
    $p = New-Object System.Windows.Media.Pen $brush, $thickness
    $p.LineJoin = $(if ($round) { [System.Windows.Media.PenLineJoin]::Round } else { [System.Windows.Media.PenLineJoin]::Miter })
    $p.MiterLimit = 10
    $p.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $p.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $p.Freeze()
    $p
}

function New-Fit([double]$scale, [double]$ax, [double]$ay, [double]$strokeDesign, [double]$canvas) {
    @{
        Scale        = $scale
        StrokeDesign = $strokeDesign
        Tx           = $canvas / 2.0 - $scale * $ax
        Ty           = $canvas / 2.0 - $scale * $ay
    }
}

function New-Xf($fit) {
    $tg = New-Object System.Windows.Media.TransformGroup
    $tg.Children.Add((New-Object System.Windows.Media.ScaleTransform $fit.Scale, $fit.Scale))
    $tg.Children.Add((New-Object System.Windows.Media.TranslateTransform $fit.Tx, $fit.Ty))
    $tg
}

# The biggest this shape can be drawn with its anchor on the canvas centre: each of the
# four directions is checked separately, because the drop shadow only eats room on the
# right and the bottom.
function Get-FillScale($geom, [double]$ax, [double]$ay, [double]$strokeDesign, [double]$canvas) {
    $b = $geom.Bounds
    $h = $strokeDesign / 2.0
    $half = $canvas / 2.0 - $EdgePad
    $cands = @(
        $half / [math]::Max(1e-6, ($ax - $b.X + $h))
        $half / [math]::Max(1e-6, ($ay - $b.Y + $h))
        ($half - $ShadowDx) / [math]::Max(1e-6, ($b.X + $b.Width - $ax + $h))
        ($half - $ShadowDy) / [math]::Max(1e-6, ($b.Y + $b.Height - $ay + $h))
    )
    ($cands | Measure-Object -Minimum).Minimum
}

# Centres a bounding box and fits it to `outer`. Troops and spells only - they have no
# nested levels, and BaseEnemy1's silhouette is bbox-centred on the canvas.
function Fit-Box($geom, [double]$outer, [double]$canvas) {
    $b = $geom.Bounds
    $scale = $outer / [math]::Max($b.Width, $b.Height)
    New-Fit $scale ($b.X + $b.Width / 2.0) ($b.Y + $b.Height / 2.0) 0 $canvas
}

# The ink a level puts on the canvas, in canvas coordinates: the widened pen for a stroked
# shape, the even-odd annulus for a ring. This is what the next level down must miss.
function New-Ink($geom, $fit) {
    $g = if ($fit.StrokeDesign -gt 0) { $geom.GetWidenedPathGeometry((New-Pen $White $fit.StrokeDesign)) }
         else { $geom.Clone() }
    $g.Transform = (New-Xf $fit)
    $g
}

# Builds one tower level: path data, whether it is filled, and the stroke in DESIGN units.
#   ring   -> outer contour + a shrunken copy of itself, even-odd. Level 3 drops the hole
#             and goes solid, which is what CircleTower3 and SquareTower3 already do.
#   stroke -> the open path, pen-drawn; level 3 thickens so it still reads at a fifth size.
function Build-Level($shape, [int]$level) {
    $isL3 = ($level -eq 3)
    if ($shape.kind -eq 'ring') {
        $d = & $shape.make 1.0
        if (-not $isL3) { $d = $d + ' ' + (& $shape.make $shape.hole) }
        return @{ d = $d; fill = $true; evenOdd = (-not $isL3); strokeDesign = 0.0 }
    }
    $d = & $shape.make 1.0
    $span = (New-Geometry $d $false).Bounds
    $frac = [double]$shape.strokeFrac
    if ($isL3) { $frac = [math]::Max($frac * 2.4, 0.22) }
    @{ d = $d; fill = $false; evenOdd = $false
       strokeDesign = $frac * [math]::Max($span.Width, $span.Height) }
}

# The largest scale, as a fraction of level 1's, at which the next level still misses this
# one's ink. Capped at the measured ratio - a shape that clears it at the reference size
# is drawn at the reference size, so ten of the twelve towers match Circle exactly.
function Fit-Next($prevInk, $shape, [int]$nextLevel, [double]$ax, [double]$ay,
                  [double]$scale1, [double]$cap, [double]$canvas) {
    $lv = Build-Level $shape $nextLevel
    $geom = New-Geometry $lv.d $lv.evenOdd
    $misses = {
        param($k)
        if ($k -le 0.01) { return $true }
        try {
            $f = New-Fit ($scale1 * $k) $ax $ay $lv.strokeDesign $canvas
            $d = $prevInk.FillContainsWithDetail((New-Ink $geom $f))
            return ($d -eq [System.Windows.Media.IntersectionDetail]::Empty)
        } catch { return $false }
    }
    if (& $misses $cap) { return $cap }
    $lo = 0.0; $hi = $cap
    for ($i = 0; $i -lt 16; $i++) {
        $mid = ($lo + $hi) / 2
        if (& $misses $mid) { $lo = $mid } else { $hi = $mid }
    }
    $lo * $NestMargin
}

# --- drawing --------------------------------------------------------------------------
#
#  The dark pass is drawn first and the white on top, so dark only shows where white does
#  not cover it: offset it and you get the towers' drop shadow, leave it in place and
#  widen the pen and you get the troops' outline. Same code, both families.

function Render-Png($geom, $fit, [double]$canvas, [string]$outPath,
                    [bool]$fill, [double]$darkDx, [double]$darkDy, [double]$darkGrow) {

    $tg = New-Xf $fit
    $dv = New-Object System.Windows.Media.DrawingVisual
    $dc = $dv.RenderOpen()

    if ($darkDx -ne 0 -or $darkDy -ne 0 -or $darkGrow -gt 0) {
        $darkPen = $null
        if (($fit.StrokeDesign + $darkGrow) -gt 0) {
            $darkPen = New-Pen $Black ($fit.StrokeDesign + $darkGrow) ($darkGrow -gt 0)
        }
        $dc.PushTransform((New-Object System.Windows.Media.TranslateTransform $darkDx, $darkDy))
        $dc.PushTransform($tg)
        $dc.DrawGeometry($(if ($fill) { $Black } else { $null }), $darkPen, $geom)
        $dc.Pop(); $dc.Pop()
    }

    $pen = $null
    if ($fit.StrokeDesign -gt 0) { $pen = New-Pen $White $fit.StrokeDesign }
    $dc.PushTransform($tg)
    $dc.DrawGeometry($(if ($fill) { $White } else { $null }), $pen, $geom)
    $dc.Pop()
    $dc.Close()

    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap `
        ([int]$canvas), ([int]$canvas), 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($dv)

    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $dir = Split-Path $outPath -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $fs = [System.IO.File]::Create($outPath)
    try { $enc.Save($fs) } finally { $fs.Dispose() }
}

function Write-Svg([string]$pathData, $fit, [double]$canvas, [bool]$fill, [bool]$evenOdd,
                   [string]$name, [string]$note, [string]$tintHex, [string]$outPath) {
    if ($SkipSvg) { return }
    $q = [char]34
    $fillAttr = if ($fill) { '#FFFFFF' } else { 'none' }
    $strokeAttr = if ($fit.StrokeDesign -gt 0) { '#FFFFFF' } else { 'none' }
    $rule = if ($evenOdd) { ' fill-rule=' + $q + 'evenodd' + $q } else { '' }
    $sw = (F $fit.StrokeDesign)
    $tx = (F $fit.Tx); $ty = (F $fit.Ty)
    $sc = $fit.Scale.ToString('F5', $Invariant)
    $cv = [int]$canvas
    $lines = @(
        '<?xml version="1.0" encoding="UTF-8"?>'
        "<svg xmlns=${q}http://www.w3.org/2000/svg${q} width=${q}$cv${q} height=${q}$cv${q} viewBox=${q}0 0 $cv $cv${q}>"
        "  <title>$name</title>"
        "  <desc>$note  Authored white - the game tints it $tintHex through CardDataSO.CardColor and the prefab SpriteRenderer, so recolouring needs no re-render.</desc>"
        "  <g transform=${q}translate($tx,$ty) scale($sc)${q}>"
        ("    <path d=${q}$pathData${q} fill=${q}$fillAttr${q}$rule stroke=${q}$strokeAttr${q} stroke-width=${q}$sw${q}" +
         " stroke-linejoin=${q}miter${q} stroke-miterlimit=${q}10${q} stroke-linecap=${q}round${q}/>")
        '  </g>'
        '</svg>'
    )
    $dir = Split-Path $outPath -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [System.IO.File]::WriteAllLines($outPath, $lines, (New-Object System.Text.UTF8Encoding $false))
}

# --- the card manifest ----------------------------------------------------------------
#
#  tint is what the CARD and the PREFAB get set to; it is never baked into the png.
#  Damage towers carry one of the three armour colours. Support towers (no damage at all)
#  stay white, so "coloured = deals that colour's damage" is a rule the board can be read
#  with. Troops are spread 2/2/2, with SpawnEnemy1 left neutral.

$Orange = '#FF8242'
$PinkC = '#FF4280'
$Purple = '#7242FF'
$Plain = '#FFFFFF'

$Manifest = @(
    @{ shape = 'Stinger';    family = 'tower'; tint = $Plain }
    @{ shape = 'Needle';     family = 'tower'; tint = $PinkC }
    @{ shape = 'Beacon';     family = 'tower'; tint = $PinkC }
    @{ shape = 'Mortar';     family = 'tower'; tint = $Orange }
    @{ shape = 'Chain';      family = 'tower'; tint = $Orange }
    @{ shape = 'Shard';      family = 'tower'; tint = $Purple }
    @{ shape = 'Prism';      family = 'tower'; tint = $Plain }
    @{ shape = 'Torniquete'; family = 'tower'; tint = $Plain }
    @{ shape = 'Anel';       family = 'tower'; tint = $Plain }
    @{ shape = 'Ancora';     family = 'tower'; tint = $Plain }
    @{ shape = 'Espelho';    family = 'tower'; tint = $Plain }
    @{ shape = 'Fonte';      family = 'tower'; tint = $Plain }
    @{ shape = 'Cisma';      family = 'troop'; tint = $Orange }
    @{ shape = 'Ram';        family = 'troop'; tint = $PinkC }
    @{ shape = 'Shadow';     family = 'troop'; tint = $Purple }
    @{ shape = 'Miragem';    family = 'troop'; tint = $Purple }
    @{ shape = 'Swarm';      family = 'troop'; tint = $Orange }
    @{ shape = 'MiniBoss';   family = 'troop'; tint = $PinkC }
    @{ shape = 'Ferrugem';   family = 'spell'; tint = $Plain }
    @{ shape = 'Lance';      family = 'spell'; tint = $Plain }
    @{ shape = 'Rift';       family = 'spell'; tint = $Plain }
)

# --- run ------------------------------------------------------------------------------

$lib = Get-ShapeLibrary
$spriteRoot = Join-Path $ProjectRoot 'Assets\Sprites'
$vectorRoot = Join-Path $PSScriptRoot 'Vector'
$pngs = 0

foreach ($card in $Manifest) {
    $shape = $lib[$card.shape]
    if (-not $shape) { throw "No shape defined for '$($card.shape)'" }
    $name = $card.shape
    $anchor = if ($shape.anchor) { $shape.anchor } else { @(500, 500) }
    $ax = [double]$anchor[0]; $ay = [double]$anchor[1]

    switch ($card.family) {

        'tower' {
            $dir = Join-Path $spriteRoot "Towers\$name"
            $scale1 = 0.0
            $ratios = @()
            for ($level = 1; $level -le 3; $level++) {
                $lv = Build-Level $shape $level
                $geom = New-Geometry $lv.d $lv.evenOdd

                if ($level -eq 1) {
                    $scale1 = Get-FillScale $geom $ax $ay $lv.strokeDesign $TowerCanvas
                    $k = 1.0
                }
                else {
                    $prev = Build-Level $shape ($level - 1)
                    $prevGeom = New-Geometry $prev.d $prev.evenOdd
                    $prevFit = New-Fit ($scale1 * $ratios[-1]) $ax $ay $prev.strokeDesign $TowerCanvas
                    $k = Fit-Next (New-Ink $prevGeom $prevFit) $shape $level $ax $ay $scale1 $LevelRatio[$level - 1] $TowerCanvas
                }
                $ratios += $k

                $fit = New-Fit ($scale1 * $k) $ax $ay $lv.strokeDesign $TowerCanvas
                $ease = [math]::Sqrt($k)                       # the shadow eases down with the shape
                Render-Png $geom $fit $TowerCanvas (Join-Path $dir "$($name)Tower$level.png") `
                    $lv.fill ($ShadowDx * $ease) ($ShadowDy * $ease) 0
                $pngs++

                if ($level -eq 1) {
                    Render-Png $geom $fit $TowerCanvas (Join-Path $dir "$($name)Tower1_NO_SHADOW.png") $lv.fill 0 0 0
                    $pngs++
                    Write-Svg $lv.d $fit $TowerCanvas $lv.fill $lv.evenOdd $name $shape.note $card.tint `
                        (Join-Path $vectorRoot "Towers\$name.svg")
                }
            }
            Write-Host ("  {0,-12} tower  {1,-9} levels 1 / {2:F3} / {3:F3}" -f $name, $card.tint, $ratios[1], $ratios[2])
        }

        'troop' {
            $inner = $TroopOuter / (1 + $TroopOutlineFrac)
            $geom = New-Geometry (& $shape.make 1.0) ([bool]$shape.evenOdd)
            $fit = Fit-Box $geom $inner $TroopCanvas
            # The pen straddles the contour, so half shows outside: width = the full gap.
            $grow = ($TroopOuter - $inner) / $fit.Scale
            Render-Png $geom $fit $TroopCanvas (Join-Path $spriteRoot "Enemies\$name.png") $true 0 0 $grow
            Render-Png $geom $fit $TroopCanvas (Join-Path $spriteRoot "Enemies\$($name)_NO_OUTLINE.png") $true 0 0 0
            $pngs += 2
            Write-Svg (& $shape.make 1.0) $fit $TroopCanvas $true ([bool]$shape.evenOdd) $name $shape.note $card.tint `
                (Join-Path $vectorRoot "Enemies\$name.svg")
            Write-Host ("  {0,-12} troop  {1}" -f $name, $card.tint)
        }

        'spell' {
            $geom = New-Geometry (& $shape.make 1.0) ([bool]$shape.evenOdd)
            $fit = Fit-Box $geom $SpellOuter $SpellCanvas
            Render-Png $geom $fit $SpellCanvas (Join-Path $spriteRoot "UI\Cards\$($name.ToLowerInvariant()).png") $true 0 0 0
            $pngs++
            Write-Svg (& $shape.make 1.0) $fit $SpellCanvas $true ([bool]$shape.evenOdd) $name $shape.note $card.tint `
                (Join-Path $vectorRoot "Spells\$name.svg")
            Write-Host ("  {0,-12} spell  {1}" -f $name, $card.tint)
        }
    }
}

Write-Host ''
Write-Host "$pngs png(s) under $spriteRoot"
if (-not $SkipSvg) { Write-Host "$($Manifest.Count) svg source(s) under $vectorRoot" }
