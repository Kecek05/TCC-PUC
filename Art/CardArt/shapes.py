"""Card art v2: the geometry of every tower (three level sprites) and every troop card.

Design units: a 1000 box centred on (0,0), y DOWN (the SVG convention). A tower body reaches +-500.
Everything is built with true offsets (shapely buffers), so a wall is the same thickness all the way round and a
nested level sits at an even gap from the one outside it, whatever the silhouette. The shapes themselves are
pixel-free; the few measured constants at the bottom (canvas, shadow) are what render.py turns them into files with.

The rules the shapes follow (Art/README.md has the reasoning):
  * one primitive, one idea;
  * spikes outside, simple inside: the hollow of a body is always a plain cell;
  * a level adds a part: level 1 the body, level 2 the chamber, level 3 the core;
  * nothing thinner than 5% of the sprite.
"""
import math
from shapely.geometry import Polygon, Point, LineString, MultiPolygon, box
from shapely.ops import unary_union
from shapely import affinity

QS = 24

# ---------------------------------------------------------------- primitives

def circ(r, cx=0, cy=0):
    return Point(cx, cy).buffer(r, quad_segs=48)


def pol(R, deg, cx=0, cy=0):
    a = math.radians(deg)
    return (cx + R * math.cos(a), cy + R * math.sin(a))


def ngon(n, R, rot=-90, cx=0, cy=0):
    return Polygon([pol(R, rot + i * 360 / n, cx, cy) for i in range(n)])


def rc(g, r):
    """Round the convex corners."""
    if r <= 0:
        return g
    return g.buffer(-r, join_style=1, quad_segs=QS).buffer(r, join_style=1, quad_segs=QS)


def rcc(g, r):
    """Round the concave corners (fillet)."""
    if r <= 0:
        return g
    return g.buffer(r, join_style=1, quad_segs=QS).buffer(-r, join_style=1, quad_segs=QS)


def soft(g, r, rin=None):
    return rcc(rc(g, r), r if rin is None else rin)


def inset(g, d, r=0):
    h = g.buffer(-d, join_style=2, mitre_limit=10)
    return rc(h, r)


def arc(R, mid, sweep, w, cap=1, cx=0, cy=0, n=72):
    pts = [pol(R, mid - sweep / 2 + sweep * i / n, cx, cy) for i in range(n + 1)]
    return LineString(pts).buffer(w / 2, cap_style=cap, join_style=1, quad_segs=QS)


def line(pts, w, cap=1, join=1):
    return LineString(pts).buffer(w / 2, cap_style=cap, join_style=join, quad_segs=QS)


def rot(g, deg, origin=(0, 0)):
    return affinity.rotate(g, deg, origin=origin)


def mirx(g):
    return affinity.scale(g, -1, 1, origin=(0, 0))


def miry(g):
    return affinity.scale(g, 1, -1, origin=(0, 0))


def U(*gs):
    return unary_union([g for g in gs if g is not None and not g.is_empty])


# ---------------------------------------------------------------- the level grammar

W1, G1, W2, G2 = 140, 72, 80, 52


def nested(body, w1=W1, g1=G1, w2=W2, g2=G2, r_cell=36, r2=26, r_hole=14, r_core=20):
    """A primitive nested three times: body ring, chamber ring, solid core."""
    cell = inset(body, w1, r_cell)
    l1 = body.difference(cell)
    o2 = inset(cell, g1, r2)
    h2 = inset(o2, w2, r_hole)
    l2 = o2.difference(h2)
    l3 = inset(h2, g2, r_core)
    return l1, l2, l3


def arrowhead(s=1.0, cx=0, cy=0, r=22):
    """The troop family's body: a point, two swept wings, a notch."""
    p = Polygon([(0, -430), (335, 385), (0, 190), (-335, 385)])
    p = rc(p, r)
    p = affinity.scale(p, s, s, origin=(0, 0))
    return affinity.translate(p, cx, cy)


# ---------------------------------------------------------------- towers

def t_circle():
    return nested(circ(500))


def t_square():
    return nested(rc(box(-492, -492, 492, 492), 170), r_cell=64, r2=40, r_hole=22, r_core=30)


def t_slam():
    return nested(rc(ngon(5, 512), 55), r_cell=30, r2=22, r_hole=12, r_core=16)


def t_dart():
    return nested(rc(ngon(6, 507, 0), 50), r_cell=30, r2=22, r_hole=12, r_core=16)


def t_stinger():
    return nested(rc(ngon(3, 614), 115), w1=112, g1=48, w2=62, g2=34, r_cell=40, r2=26, r_hole=12, r_core=12)


def t_mortar():
    body = rc(ngon(8, 540, 22.5), 40)
    cell = inset(body, 168, 30)
    l1 = body.difference(cell)
    o2 = inset(cell, 70, 24)
    h2 = inset(o2, 88, 14)
    gaps = U(rot(box(-37, -400, 37, 400), 45), rot(box(-37, -400, 37, 400), -45))
    l2 = o2.difference(h2).difference(gaps)
    l2 = rc(l2, 10)
    l3 = circ(116)
    return l1, l2, l3


def t_needle():
    h, w = 500.0, 312.0
    R = (h * h + w * w) / (2 * w)
    body = rc(circ(R, R - w, 0).intersection(circ(R, -(R - w), 0)), 7)
    l1 = body.difference(circ(190))
    l2 = rc(Polygon([(0, -138), (50, 0), (0, 138), (-50, 0)]), 7)
    tick = rc(Polygon([(86, -42), (86, 42), (148, 0)]), 7)
    l3 = U(tick, mirx(tick))
    return l1, l2, l3


def t_beacon():
    pts = []
    for i in range(16):
        pts.append(pol(500 if i % 2 == 0 else 378, -90 + i * 22.5))
    body = soft(Polygon(pts), 9, 14)
    l1 = body.difference(circ(252))
    l2 = circ(186).difference(circ(112))
    l3 = circ(64)
    return l1, l2, l3


def t_chain():
    lobes = [circ(126, *pol(374, a)) for a in (-90, 30, 150)]
    body = rcc(U(circ(374), *lobes), 46)
    l1 = body.difference(circ(240))
    l2 = circ(172).difference(circ(100))
    l3 = circ(52)
    return l1, l2, l3


def t_shard():
    spikes = []
    for i in range(4):
        spikes.append(rot(Polygon([(0, -500), (184, -270), (-184, -270)]), i * 90))
    body = soft(U(circ(356), *spikes), 9, 18)
    l1 = body.difference(circ(232))
    kite = Polygon([(72, 0), (120, 46), (190, 0), (120, -46)])
    l2 = U(*[rc(rot(kite, 45 + i * 90), 7) for i in range(4)])
    l3 = rc(Polygon([(0, -52), (52, 0), (0, 52), (-52, 0)]), 7)
    return l1, l2, l3


def t_prism():
    l1 = U(*[arc(436, a, 86, 128) for a in (-90, 30, 150)])
    l2 = U(*[arc(258, a, 76, 84) for a in (90, 210, 330)])
    l3 = rc(ngon(3, 150), 26)
    return l1, l2, l3


def _bracket(half, r, gap, w):
    """Left square bracket: a rounded square's left side, open at top and bottom by `gap` each side of centre."""
    pts = [(-gap, -half)]
    pts.append((-half + r, -half))
    for i in range(1, 13):
        a = math.radians(-90 - i * 7.5)
        pts.append((-half + r + r * math.cos(a), -half + r + r * math.sin(a)))
    pts.append((-half, half - r))
    for i in range(1, 13):
        a = math.radians(180 - i * 7.5)
        pts.append((-half + r + r * math.cos(a), half - r + r * math.sin(a)))
    pts.append((-gap, half))
    return LineString(pts).buffer(w / 2, cap_style=1, join_style=1, quad_segs=QS)


def t_torniquete():
    left = _bracket(436, 120, 150, 128)
    l1 = U(left, mirx(left))
    top = rot(_bracket(262, 70, 98, 84), 90)
    l2 = U(top, miry(top))
    plate = rc(box(-146, -146, 146, 146), 34)
    crack = rot(box(-23, -400, 23, 400), 45)
    l3 = rc(plate.difference(crack), 8)
    return l1, l2, l3


def t_anel():
    l1 = U(*[circ(80, *pol(420, -90 + i * 45)) for i in range(8)])
    l2 = rc(U(box(-50, -262, 50, 262), box(-262, -50, 262, 50)), 22)
    l3 = U(*[circ(42, *pol(196, 45 + i * 90)) for i in range(4)])
    return l1, l2, l3


def t_ancora():
    cy = -30
    hook = arc(330, 90, 196, 128, cap=2, cy=cy)
    flukes = []
    for sgn in (1, -1):
        a = math.radians(-8)
        px, py = 330 * math.cos(a), cy + 330 * math.sin(a)
        tx, ty = -math.sin(-a) * -1, -math.cos(a)          # tangent pointing up and a touch inward
        tx, ty = -0.139, -0.990
        nx, ny = 0.990, -0.139
        tri = Polygon([(px + nx * 150, py + ny * 150), (px - nx * 150, py - ny * 150), (px + tx * 190, py + ty * 190)])
        flukes.append(tri if sgn == 1 else mirx(tri))
    l1 = soft(U(hook, *flukes), 14, 10)
    shank = line([(0, -258), (0, 96)], 100)
    stock = line([(-150, -258), (150, -258)], 84)
    l2 = U(shank, stock)
    l3 = circ(78, 0, -418).difference(circ(30, 0, -418))
    return l1, l2, l3


def t_espelho():
    top = line([(-396, -118), (0, -404), (396, -118)], 128)
    l1 = U(top, miry(top))
    itop = line([(-190, -72), (0, -209), (190, -72)], 84)
    l2 = U(itop, miry(itop))
    l3 = rc(Polygon([(0, -98), (76, 80), (0, 38), (-76, 80)]), 8)
    return l1, l2, l3


def t_fonte():
    cy, r = 150, 330
    d = cy + 500
    th = math.acos(r / d)
    tp = (r * math.sin(th), cy - r * math.cos(th))
    drop = rc(U(circ(r, 0, cy), Polygon([(0, -500), tp, (-tp[0], tp[1])])), 26)
    cell = inset(drop, 138, 24)
    l1 = drop.difference(cell)
    l2 = circ(120, 0, cy)
    l3 = circ(46, 0, -66)
    return l1, l2, l3


TOWERS = [
    # name, tint token, tint hex, family, builder
    ("Circle", "plain", "#ffffff", "pure", t_circle),
    ("Stinger", "plain", "#ffffff", "pure", t_stinger),
    ("Square", "pink", "#ff4280", "pure", t_square),
    ("Slam", "purple", "#7242ff", "pure", t_slam),
    ("Dart", "orange", "#ff8242", "pure", t_dart),
    ("Mortar", "orange", "#ff8242", "special", t_mortar),
    ("Needle", "pink", "#ff4280", "special", t_needle),
    ("Beacon", "pink", "#ff4280", "special", t_beacon),
    ("Chain", "orange", "#ff8242", "special", t_chain),
    ("Shard", "purple", "#7242ff", "special", t_shard),
    ("Prism", "plain", "#ffffff", "support", t_prism),
    ("Torniquete", "plain", "#ffffff", "support", t_torniquete),
    ("Anel", "plain", "#ffffff", "support", t_anel),
    ("Ancora", "plain", "#ffffff", "support", t_ancora),
    ("Espelho", "plain", "#ffffff", "support", t_espelho),
    ("Fonte", "plain", "#ffffff", "support", t_fonte),
]

# ---------------------------------------------------------------- troops

def tr_swarm():
    return U(arrowhead(0.48, 0, -238), arrowhead(0.48, -262, 196), arrowhead(0.48, 262, 196))


def tr_swarm_unit():
    return arrowhead(1.0)


def tr_cisma():
    big = rc(Polygon([(0, -470), (372, 400), (0, 196), (-372, 400)]), 22)
    zig = [(0, -520), (34, -330), (-34, -150), (34, 30), (-34, 210), (0, 420)]
    cut = LineString(zig).buffer(34, cap_style=2, join_style=2, mitre_limit=6)
    parts = big.difference(cut)
    geoms = list(parts.geoms) if hasattr(parts, "geoms") else [parts]
    out = []
    for g in geoms:
        if g.area < 2000:
            continue
        side = 1 if g.centroid.x > 0 else -1
        out.append(rot(g, 5 * side, origin=(0, 400)))
    return rc(U(*out), 8)


def tr_ram():
    poly = rc(Polygon([(-200, -450), (200, -450), (425, 395), (0, 218), (-425, 395)]), 30)
    return poly.difference(box(-600, -318, 600, -274))


def tr_miniboss():
    head = arrowhead(0.76, 0, -128)
    big = arrowhead(1.22, 0, -14, r=26)
    band = big.intersection(box(-600, 40, 600, 600)).difference(head.buffer(58, join_style=2, mitre_limit=6))
    parts = [g for g in polys(band) if g.area > 4000]
    return U(head, rc(U(*parts), 10))


def _bez(p0, p1, p2, p3, n=24):
    out = []
    for i in range(n + 1):
        t = i / n
        a = (1 - t) ** 3; b = 3 * (1 - t) ** 2 * t; c = 3 * (1 - t) * t * t; d = t ** 3
        out.append((a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1]))
    return out


def tr_shadow():
    right = _bez((0, -480), (74, -210), (196, 130), (330, 430))
    left = [(-x, y) for (x, y) in reversed(right)]
    return rc(Polygon(right + [(0, 214)] + left), 14)


def tr_miragem():
    real = arrowhead(0.62, 0, -70)
    ghost = arrowhead(0.47, 0, 0)
    ghost = ghost.difference(inset(ghost, 46, 6))
    return U(real, affinity.translate(ghost, -322, 196), affinity.translate(ghost, 322, 196))


def tr_miragem_unit():
    return arrowhead(1.0)


# name, tint, the card emblem, the lane body (None = the emblem is the body), the body's size on the lane.
# The size is the silhouette's longest side as a share of the standard 496 px: enemy prefabs are all at scale 1,
# so a troop's size on the lane is whatever its sprite draws.
TROOPS = [
    ("Swarm", "#ff8242", tr_swarm, tr_swarm_unit, 0.6),
    ("Cisma", "#ff8242", tr_cisma, None, 1.0),
    ("Ram", "#ff4280", tr_ram, None, 1.0),
    ("MiniBoss", "#ff4280", tr_miniboss, None, 1.0),
    ("Shadow", "#7242ff", tr_shadow, None, 1.0),
    ("Miragem", "#7242ff", tr_miragem, tr_miragem_unit, 1.0),
]

TROOP_OUTLINE = 34

# ---------------------------------------------------------------- export

CANVAS = 748
U2PX = 339.0 / 500.0            # a 500-unit body lands 339 px from the pivot: room for the +22,+30 shadow
SHADOW = (22.0, 30.0)
SHADOW_EASE = (1.0, 0.76, 0.58)


def polys(g):
    if g.is_empty:
        return []
    if isinstance(g, Polygon):
        return [g]
    return [p for p in g.geoms if isinstance(p, Polygon)]


def path_d(g, scale, ox, oy, tol=0.35):
    g = g.simplify(tol / scale, preserve_topology=True)
    out = []
    for p in polys(g):
        for ring in [p.exterior] + list(p.interiors):
            c = list(ring.coords)[:-1]
            if len(c) < 3:
                continue
            s = "M" + " L".join(f"{ox + x * scale:.1f},{oy + y * scale:.1f}" for x, y in c) + "Z"
            out.append(s)
    return " ".join(out)


def svg(d, canvas=CANVAS, fill="#FFFFFF", extra=""):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{canvas}" height="{canvas}" viewBox="0 0 {canvas} {canvas}">'
            f'{extra}<path d="{d}" fill="{fill}" fill-rule="evenodd"/></svg>')
