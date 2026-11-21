"""Posing reference boxes for the hiding poses (art bible 6.2 / 6.5).

usage: pose_reference_boxes.py <Assets/_Art/Editor dir> <out dir>

Reads the container sizes and board thickness from ContainerPrefabBuilder.cs and the furniture scale from
HouseBlockoutGenerator.Rescale.cs, rebuilds each container's boards the way the builder does (keep the
per-kind layout in kinds() in sync with ContainerPrefabBuilder.Build), and writes
  HakoOnna_Pose_<Kind>.glb    one binary glTF per container kind
  PoseReference_Overview.png  every file seen from the player camera and from a 3/4 view
Everything is already multiplied by the furniture scale: 1 unit = 1 m in the house.

Frame: origin = the container root (bottom centre). A Unity local point (x, y, z) is written to glTF as
(-x, y, z), the usual Unity <-> glTF handedness flip. Blender's glTF importer then shows the opening toward
-Y (front view, numpad 1), and a Blender -> FBX -> Unity round trip lands in the container's local frame.
In Unity the posed model goes under the container root at position 0 with scale 1/1.25 (art bible 6.5);
the Tansu pose goes under Drawer_0 instead.

Nodes in each file (under Container_<Kind>):
  Shell            boards that stay put (Unity: Back, Side_L/R, Bottom, Top or Front, Interior, Shelf, ...)
  Open_*           the moving part in its open state (doors / fusuma / lid); Tansu: Drawer_0 > Open_Drawer_0
  SafeZone_1.5cm   rods along the edges of the space the body may use with the container closed
  Opening          rods around what the player looks through
  FaceTarget       marker at the face position of the art bible 6.2 pose table
  PlayerEye        camera where the game's inspection camera ends up (eye 1.58 m, FOV 75), aimed at FaceTarget
  Floor            4 x 4 m floor at y = 0
"""
import json, math, os, re, struct, sys
from PIL import Image, ImageDraw, ImageFont

editor_dir, out_dir = sys.argv[1], sys.argv[2]
EYE = 1.58     # explorer eye height planned in art bible 6.1 (GameConfig still says 1.45)
FOV = 75.0     # player camera vertical field of view (PhaseOneSetup)
CLEAR = 0.015  # art bible 6.5: keep 1.5 cm off walls, lids and doors; touching the floor is fine

builder = open(os.path.join(editor_dir, "ContainerPrefabBuilder.cs"), encoding="utf-8").read()
rescale = open(os.path.join(editor_dir, "HouseBlockoutGenerator.Rescale.cs"), encoding="utf-8").read()
K = float(re.search(r"const float K = ([\d.]+)f", builder).group(1))
S = float(re.search(r"FurnitureScale = ([\d.]+)f", rescale).group(1))
SIZES = {m[0]: tuple(map(float, m[1:])) for m in re.findall(
    r"\{\s*ContainerKind\.(\w+),\s*new Vector3\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f\)\s*\}", builder)}

# ---------------------------------------------------------------- small vector helpers


def add(a, b): return tuple(x + y for x, y in zip(a, b))
def sub(a, b): return tuple(x - y for x, y in zip(a, b))
def mul(a, k): return tuple(x * k for x in a)
def dot(a, b): return sum(x * y for x, y in zip(a, b))
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a): return mul(a, 1 / math.sqrt(dot(a, a)))
def scene(p): return mul(p, S)                  # Unity local metres of the prefab -> house metres
def gl(p): return (-p[0], p[1], p[2])            # Unity -> glTF (also right for directions)


def rot(euler, v):
    """Unity rotation for the single-axis eulers the builder uses (about x or about y)."""
    x, y, z = v
    if euler[0]:
        c, s = math.cos(math.radians(euler[0])), math.sin(math.radians(euler[0]))
        y, z = y * c - z * s, y * s + z * c
    if euler[1]:
        c, s = math.cos(math.radians(euler[1])), math.sin(math.radians(euler[1]))
        x, z = x * c + z * s, -x * s + z * c
    return (x, y, z)


def box_quads(center, size, pivot=(0, 0, 0), euler=(0, 0)):
    """The six faces of a builder box (prefab metres, Unity frame) as (corners, outward normal) in glTF house metres."""
    quads = []
    for a in range(3):
        u, v = [i for i in range(3) if i != a]
        for sgn in (-1, 1):
            corners = []
            for cu, cv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = [0, 0, 0]
                p[a], p[u], p[v] = sgn, cu, cv
                local = tuple(center[i] + p[i] * size[i] / 2 for i in range(3))
                corners.append(gl(scene(add(pivot, rot(euler, local)))))
            n = [0, 0, 0]
            n[a] = sgn
            quads.append((corners, gl(rot(euler, tuple(n)))))
    return quads


def wire_box(lo, hi):
    """12 edges of an axis-aligned box given in Unity house metres, as glTF segments."""
    c = [(x, y, z) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
    return [(gl(c[i]), gl(c[j])) for i in range(8) for j in range(i + 1, 8)
            if sum(c[i][k] != c[j][k] for k in range(3)) == 1]


def loop(points):
    return [(gl(points[i]), gl(points[(i + 1) % len(points)])) for i in range(len(points))]


def rods(segments, t):
    """Thin square rods along segments so the outlines import as ordinary triangles everywhere."""
    quads = []
    for a, b in segments:
        d = sub(b, a)
        axis = max(range(3), key=lambda i: abs(d[i]))
        lo = tuple(min(a[i], b[i]) - (0 if i == axis else t / 2) for i in range(3))
        hi = tuple(max(a[i], b[i]) + (0 if i == axis else t / 2) for i in range(3))
        center, size = mul(add(lo, hi), .5), sub(hi, lo)
        for k in range(3):
            u, v = [i for i in range(3) if i != k]
            for sgn in (-1, 1):
                corners = []
                for cu, cv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                    p = [0, 0, 0]
                    p[k], p[u], p[v] = sgn, cu, cv
                    corners.append(tuple(center[i] + p[i] * size[i] / 2 for i in range(3)))
                n = [0, 0, 0]
                n[k] = sgn
                quads.append((corners, tuple(n)))
    return quads


def octahedron(c, r):
    c = gl(c)
    pts = [add(c, p) for p in ((r, 0, 0), (-r, 0, 0), (0, r, 0), (0, -r, 0), (0, 0, r), (0, 0, -r))]
    tris = []
    for x in (0, 1):
        for y in (2, 3):
            for z in (4, 5):
                tri = [pts[x], pts[y], pts[z]]
                tris.append((tri, norm(sub(mul(add(add(tri[0], tri[1]), tri[2]), 1 / 3), c))))
    return tris


def floor_quads(half=2.0, step=0.5, y=-0.002):
    quads, n = [], int(2 * half / step)
    for i in range(n):
        for j in range(n):
            x0, z0 = -half + i * step, -half + j * step
            quads.append(([(x0, y, z0), (x0 + step, y, z0), (x0 + step, y, z0 + step), (x0, y, z0 + step)], (0, 1, 0)))
    return quads

# ---------------------------------------------------------------- the containers (mirror ContainerPrefabBuilder.Build)


def shell(w, h, d, top, front):
    boxes = [((0, h / 2, -d / 2 + K / 2), (w, h, K)),            # Back
             ((-w / 2 + K / 2, h / 2, 0), (K, h, d)),            # Side_L
             ((w / 2 - K / 2, h / 2, 0), (K, h, d)),             # Side_R
             ((0, K / 2, 0), (w - 2 * K, K, d))]                 # Bottom
    if top:
        boxes.append(((0, h - K / 2, 0), (w, K, d)))             # Top
        boxes.append(((0, h / 2, -d / 2 + K + .003), (w - 2 * K, h - 2 * K, .005)))   # Interior
    else:
        boxes.append(((0, K + .003, 0), (w - 2 * K, .005, d - 2 * K)))                 # Interior
    if front:
        boxes.append(((0, h / 2, d / 2 - K / 2), (w - 2 * K, h, K)))                    # Front
    return boxes


def zone(lo, hi, floor_free=True):
    """Unity prefab-metre cavity -> house-metre safe zone: CLEAR off every side except the floor."""
    lo, hi = scene(lo), scene(hi)
    return ((lo[0] + CLEAR, lo[1] + (0 if floor_free else CLEAR), lo[2] + CLEAR),
            (hi[0] - CLEAR, hi[1] - CLEAR, hi[2] - CLEAR))


def kinds():
    out = []
    for kind in ("Oshiire", "YofukuTansu", "ChaTansu", "GetaBako", "Tansu", "Nagamochi", "Tsuzura", "Komebitsu"):
        w, h, d = SIZES[kind]
        c = {"kind": kind, "w": w, "h": h, "d": d, "moving": [], "drawer": None}
        cab_back = -d / 2 + K + .0055      # inner face of the dark Interior panel on the back wall
        if kind == "Oshiire":
            c["shell"] = shell(w, h, d, True, False) + [
                ((0, h * .5, 0), (w - 2 * K, K, d - K)),                                  # Shelf
                ((-w / 4, h / 2, d / 2 + .012), (w / 2, h - .02, .022))]                  # Fusuma_L (fixed, back track)
            c["moving"] = [("Open_Fusuma_R", (0, 0, 0), (0, 0),
                            [((w / 4 - w / 2 + .03, h / 2, d / 2 + .038), (w / 2, h - .02, .022))])]
            # The upper tier is hers; the closed fusuma's inner face (back track) is the front limit.
            c["zone"] = zone((-(w / 2 - K), h * .5 + K / 2, cab_back), (w / 2 - K, h - K, d / 2 + .001))
            c["opening"] = [scene(p) for p in ((.03, K, d / 2), (w / 2 - K, K, d / 2), (w / 2 - K, h - K, d / 2), (.03, h - K, d / 2))]
            c["face"] = (S * w / 4, 1.70, S * d / 2 - .25)
            c["anchor"], c["mode"] = scene((w / 4, h * .5 + K, 0)), "door"
        elif kind in ("YofukuTansu", "ChaTansu", "GetaBako"):
            c["shell"] = shell(w, h, d, True, False)
            if kind == "GetaBako":
                doors = [((-w / 2, 0, d / 2), (0, -100), [((w / 2, h / 2, .0125), (w - .005, h - .02, .025))])]
            else:
                dw = w / 2
                doors = [((-w / 2, 0, d / 2), (0, -100), [((dw / 2, h / 2, .0125), (dw - .005, h - .02, .025))]),
                         ((w / 2, 0, d / 2), (0, 100), [((-dw / 2, h / 2, .0125), (dw - .005, h - .02, .025))])]
            c["moving"] = [("Open_Door" if len(doors) == 1 else "Open_Doors", p, e, b) for p, e, b in doors]
            c["zone"] = zone((-(w / 2 - K), K, cab_back), (w / 2 - K, h - K, d / 2))
            c["opening"] = [scene(p) for p in ((-(w / 2 - K), K, d / 2), (w / 2 - K, K, d / 2), (w / 2 - K, h - K, d / 2), (-(w / 2 - K), h - K, d / 2))]
            c["face"] = {"YofukuTansu": (0, 1.20, S * d / 2 - .25), "ChaTansu": (0, .60, S * d / 2 - .20),
                         "GetaBako": (0, 1.50, S * d / 2 - .18)}[kind]
            c["anchor"], c["mode"] = scene((0, K + .01, 0)), "door"
        elif kind == "Tansu":
            dh = (h - 2 * K) / 3
            y_bottom = [h - K - (i + 1) * dh for i in range(3)]
            front = ((w - 2 * K - .006, dh - .006, .025))
            c["shell"] = shell(w, h, d, True, False) + [((0, y_bottom[i] + dh / 2, d / 2 + .0125), front) for i in (1, 2)]
            iw, tray_len, tray_z = w - 2 * K - .02, d - K - .01, (K + .01) / 2
            parts = [((0, dh / 2, d / 2 + .0125), front),                                   # Front
                     ((0, .008, tray_z), (iw, .012, tray_len)),                             # TrayBottom
                     ((-iw / 2, dh * .35, tray_z), (.012, dh * .7, tray_len)),              # TraySide_L
                     ((iw / 2, dh * .35, tray_z), (.012, dh * .7, tray_len)),               # TraySide_R
                     ((0, dh * .35, tray_z - tray_len / 2), (iw, dh * .7, .012))]           # TrayBack
            origin = (0, y_bottom[0], d * .75)   # Drawer_0 pulled out by its openOffset
            lo = scene((-(iw / 2 - .006), .014, tray_z - tray_len / 2 + .006))
            hi = scene((iw / 2 - .006, dh * .7, d / 2))
            # Everything under Drawer_0 is relative to the drawer origin; the top of the zone is the tray side (art bible: <= 0.33 m).
            c["drawer"] = {"origin": scene(origin), "parts": parts,
                           "zone": ((lo[0] + CLEAR, lo[1], lo[2] + CLEAR), (hi[0] - CLEAR, hi[1], hi[2] - CLEAR)),
                           "face": (0, S * .014 + .13, S * d / 2 - .12)}
            c["zone"] = tuple(add(p, scene(origin)) for p in c["drawer"]["zone"])
            top = S * (y_bottom[0] + dh * .7)
            c["opening"] = [(-S * (iw / 2 - .006), top, S * d / 2), (S * (iw / 2 - .006), top, S * d / 2),
                            (S * (iw / 2 - .006), top, S * (d / 2 + d * .75)), (-S * (iw / 2 - .006), top, S * (d / 2 + d * .75))]
            c["face"] = add(scene(origin), c["drawer"]["face"])
            c["anchor"], c["mode"] = add(scene(origin), scene((0, .02, 0))), "drawer"
        else:  # lidded: Nagamochi / Tsuzura / Komebitsu
            c["shell"] = shell(w, h, d, False, True)
            x_in = w / 2 - K
            if kind == "Nagamochi":
                # The two iron bands (Fit_Band) only wrap the front and back faces; until 2026-11-20 they were solid slabs through the chest.
                c["shell"] += [((sx * (w / 2 - .15), h / 2, sz * (d / 2 + .002)), (.04, h, .004)) for sx in (-1, 1) for sz in (-1, 1)]
            c["moving"] = [("Open_Lid", (0, h, -d / 2), (-105, 0), [((0, .0175, d / 2), (w + .02, .035, d + .02))])]
            c["zone"] = zone((-x_in, K + .0055, -(d / 2 - K)), (x_in, h, d / 2 - K))
            c["opening"] = [scene(p) for p in ((-(w / 2 - K), h, -(d / 2 - K)), (w / 2 - K, h, -(d / 2 - K)),
                                               (w / 2 - K, h, d / 2 - K), (-(w / 2 - K), h, d / 2 - K))]
            c["face"] = {"Nagamochi": (0, S * (K + .0055) + .30, -.15), "Tsuzura": (0, .50, 0),
                         "Komebitsu": (0, S * h - .10, 0)}[kind]
            c["anchor"], c["mode"] = scene((0, K + .01, 0)), "lid"
        out.append(c)
    return out


def inspection_camera(c):
    """NetworkPlayer.InspectionPosition with the player at the container's InteractPoint, fully opened."""
    eye = (0, EYE, S * (c["d"] / 2 + .6))
    focus = add(c["anchor"], (0, .08, 0))
    off = sub(focus, eye)
    dist = math.sqrt(dot(off, off))
    dest = add(eye, mul(off, max(0, min(dist - .5, .48)) / dist))
    if c["mode"] in ("drawer", "lid"):
        ax, ay, az = c["anchor"]
        y = max(EYE - .25, ay + .72) if c["mode"] == "drawer" else max(EYE, S * c["h"] + .45)
        dest = (ax, y, az + .17)
    return dest


def look_basis(eye, target):
    f = norm(sub(target, eye))
    up = (0, 1, 0) if abs(f[1]) < .9 else (0, 0, -1)   # looking down: the far side of the container at the top
    x = norm(cross(f, up))
    z = mul(f, -1)
    return x, cross(z, x), z


def quat(x, y, z):
    m = [[x[0], y[0], z[0]], [x[1], y[1], z[1]], [x[2], y[2], z[2]]]
    tr = m[0][0] + m[1][1] + m[2][2]
    if tr > 0:
        s = math.sqrt(tr + 1) * 2
        return [(m[2][1] - m[1][2]) / s, (m[0][2] - m[2][0]) / s, (m[1][0] - m[0][1]) / s, s / 4]
    if m[0][0] > m[1][1] and m[0][0] > m[2][2]:
        s = math.sqrt(1 + m[0][0] - m[1][1] - m[2][2]) * 2
        return [s / 4, (m[0][1] + m[1][0]) / s, (m[0][2] + m[2][0]) / s, (m[2][1] - m[1][2]) / s]
    if m[1][1] > m[2][2]:
        s = math.sqrt(1 + m[1][1] - m[0][0] - m[2][2]) * 2
        return [(m[0][1] + m[1][0]) / s, s / 4, (m[1][2] + m[2][1]) / s, (m[0][2] - m[2][0]) / s]
    s = math.sqrt(1 + m[2][2] - m[0][0] - m[1][1]) * 2
    return [(m[0][2] + m[2][0]) / s, (m[1][2] + m[2][1]) / s, s / 4, (m[1][0] - m[0][1]) / s]

# ---------------------------------------------------------------- glTF writer


class Glb:
    def __init__(self):
        self.bin, self.views, self.accessors, self.meshes = bytearray(), [], [], []

    def _view(self, data, target):
        while len(self.bin) % 4:
            self.bin.append(0)
        self.views.append({"buffer": 0, "byteOffset": len(self.bin), "byteLength": len(data), "target": target})
        self.bin += data
        return len(self.views) - 1

    def _vec3(self, pts, bounds):
        data = struct.pack("<%df" % (3 * len(pts)), *[v for p in pts for v in p])
        acc = {"bufferView": self._view(data, 34962), "componentType": 5126, "count": len(pts), "type": "VEC3"}
        if bounds:
            f32 = struct.unpack("<%df" % (3 * len(pts)), data)
            acc["min"] = [min(f32[i::3]) for i in range(3)]
            acc["max"] = [max(f32[i::3]) for i in range(3)]
        self.accessors.append(acc)
        return len(self.accessors) - 1

    def mesh(self, name, faces, material):
        """faces: (corners, wanted normal) in glTF space; winding is fixed to match the normal."""
        pos, nrm, idx = [], [], []
        for corners, n in faces:
            if dot(cross(sub(corners[1], corners[0]), sub(corners[2], corners[0])), n) < 0:
                corners = corners[::-1]
            base = len(pos)
            pos += corners
            nrm += [n] * len(corners)
            for k in range(1, len(corners) - 1):
                idx += [base, base + k, base + k + 1]
        data = struct.pack("<%dH" % len(idx), *idx)
        self.accessors.append({"bufferView": self._view(data, 34963), "componentType": 5123, "count": len(idx), "type": "SCALAR"})
        indices = len(self.accessors) - 1
        prim = {"attributes": {"POSITION": self._vec3(pos, True), "NORMAL": self._vec3(nrm, False)},
                "indices": indices, "material": material}
        self.meshes.append({"name": name, "primitives": [prim]})
        return len(self.meshes) - 1


MATERIALS = [("M_Board", (.46, .37, .27)), ("M_MovingPart", (.30, .22, .15)), ("M_SafeZone", (.15, .85, .35)),
             ("M_Opening", (1.0, .78, .10)), ("M_FaceTarget", (.95, .12, .10)), ("M_Floor", (.16, .16, .17))]
MAT = {name: i for i, (name, _) in enumerate(MATERIALS)}
COLOR = dict(MATERIALS)


def build(c):
    """Returns (glb bytes, render list) for one container."""
    g, nodes, kids = Glb(), [], []
    render = []   # (faces, colour) in glTF space for the overview

    def node(entry, parent_kids):
        nodes.append(entry)
        parent_kids.append(len(nodes) - 1)
        return len(nodes) - 1

    def mesh_node(name, faces, mat, parent_kids, offset=(0, 0, 0), extras=None):
        entry = {"name": name, "mesh": g.mesh(name, faces, MAT[mat])}
        if extras:
            entry["extras"] = extras
        node(entry, parent_kids)
        render.append(([([add(p, offset) for p in corners], n) for corners, n in faces], COLOR[mat]))

    nodes.append({"name": "Container_" + c["kind"], "children": kids})
    shell_faces = [q for center, size in c["shell"] for q in box_quads(center, size)]
    mesh_node("Shell", shell_faces, "M_Board", kids)
    groups = {}   # a pair of doors shares one Open_Doors mesh
    for name, pivot, euler, boxes in c["moving"]:
        groups.setdefault(name, []).extend(q for center, size in boxes for q in box_quads(center, size, pivot, euler))
    for name, faces in groups.items():
        mesh_node(name, faces, "M_MovingPart", kids)
    zone_lo, zone_hi = c["zone"]
    size = [round(hi - lo, 3) for lo, hi in zip(zone_lo, zone_hi)]
    face_extras = {"height_m": round(c["face"][1], 3)}
    zone_extras = {"size_m": size, "rule": "body stays inside with the container closed; floor contact allowed"}
    if c["drawer"]:
        dr = c["drawer"]
        dkids = []
        origin = gl(dr["origin"])
        node({"name": "Drawer_0", "translation": list(origin), "children": dkids,
              "extras": {"note": "pose rides on the drawer: parent the character to this empty"}}, kids)
        faces = [q for center, size_ in dr["parts"] for q in box_quads(center, size_)]
        mesh_node("Open_Drawer_0", faces, "M_MovingPart", dkids, origin)
        mesh_node("SafeZone_1.5cm", rods(wire_box(*dr["zone"]), .006), "M_SafeZone", dkids, origin, zone_extras)
        mesh_node("FaceTarget", octahedron(dr["face"], .06), "M_FaceTarget", dkids, origin, face_extras)
    else:
        mesh_node("SafeZone_1.5cm", rods(wire_box(zone_lo, zone_hi), .006), "M_SafeZone", kids, extras=zone_extras)
        mesh_node("FaceTarget", octahedron(c["face"], .06), "M_FaceTarget", kids, extras=face_extras)
    mesh_node("Opening", rods(loop(c["opening"]), .008), "M_Opening", kids)
    mesh_node("Floor", floor_quads(), "M_Floor", kids)

    eye = gl(inspection_camera(c))
    basis = look_basis(eye, gl(c["face"]))
    node({"name": "PlayerEye", "camera": 0, "translation": list(eye), "rotation": quat(*basis)}, kids)

    nodes[0]["extras"] = {
        "kind": c["kind"], "units": "metres in the house (prefab x %.2f)" % S,
        "safe_zone_m": size, "face_height_m": round(c["face"][1], 3), "eye_height_m": EYE,
        "front": "the opening faces -Y in Blender (+Z of the Unity prefab)"}
    doc = {"asset": {"version": "2.0", "generator": "HakoOnna pose_reference_boxes.py"},
           "scene": 0, "scenes": [{"name": c["kind"], "nodes": [0]}], "nodes": nodes,
           "meshes": g.meshes,
           "materials": [{"name": n, "doubleSided": True, "pbrMetallicRoughness": {
               "baseColorFactor": list(col) + [1.0], "metallicFactor": 0.0, "roughnessFactor": .9}} for n, col in MATERIALS],
           "cameras": [{"name": "PlayerEye", "type": "perspective", "perspective": {
               "yfov": math.radians(FOV), "aspectRatio": 16 / 9, "znear": .01, "zfar": 100.0}}],
           "accessors": g.accessors, "bufferViews": g.views, "buffers": [{"byteLength": len(g.bin)}]}
    js = json.dumps(doc, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    js += b" " * (-len(js) % 4)
    bn = bytes(g.bin) + b"\0" * (-len(g.bin) % 4)
    glb = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bn))
    glb += struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(bn), 0x004E4942) + bn
    return glb, render, (eye, basis)

# ---------------------------------------------------------------- overview picture


def draw(render, eye, basis, size, ortho):
    W, H = size
    img = Image.new("RGB", size, (22, 22, 26))
    dr = ImageDraw.Draw(img)
    X, Y, Z = basis
    light = norm(add(add(Z, mul(Y, .6)), mul(X, .3)))

    def cam(p):
        v = sub(p, eye)
        return (dot(v, X), dot(v, Y), -dot(v, Z))

    polys = []
    for faces, col in render:
        for corners, n in faces:
            pts = [cam(p) for p in corners]
            if ortho:
                if dot(n, Z) <= 0:
                    continue
            else:
                if dot(n, sub(corners[0], eye)) >= 0:
                    continue
                clipped = []   # keep the part in front of the near plane
                for i, a in enumerate(pts):
                    b = pts[(i + 1) % len(pts)]
                    if a[2] >= .05:
                        clipped.append(a)
                    if (a[2] >= .05) != (b[2] >= .05):
                        t = (.05 - a[2]) / (b[2] - a[2])
                        clipped.append(tuple(a[k] + (b[k] - a[k]) * t for k in range(3)))
                pts = clipped
                if len(pts) < 3:
                    continue
            shade = .35 + .65 * max(0, dot(n, light))
            polys.append((sum(p[2] for p in pts) / len(pts), pts, tuple(int(255 * min(1, ch * shade)) for ch in col)))
    if ortho:
        xs = [p[0] for _, pts, _ in polys for p in pts]
        ys = [p[1] for _, pts, _ in polys for p in pts]
        k = .9 * min(W / (max(xs) - min(xs)), H / (max(ys) - min(ys)))
        cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
        proj = lambda p: (W / 2 + (p[0] - cx) * k, H / 2 - (p[1] - cy) * k)
    else:
        f = 1 / math.tan(math.radians(FOV) / 2) * H / 2
        proj = lambda p: (W / 2 + p[0] / p[2] * f, H / 2 - p[1] / p[2] * f)
    for _, pts, col in sorted(polys, key=lambda t: -t[0]):
        dr.polygon([proj(p) for p in pts], fill=col)
    return img


def main():
    os.makedirs(out_dir, exist_ok=True)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 15)
    except OSError:
        font = ImageFont.load_default()
    names = {"Oshiire": "押入れ 壁橱（上层）", "YofukuTansu": "洋服箪笥 西式衣柜", "ChaTansu": "茶箪笥 餐具柜",
             "GetaBako": "下駄箱 鞋柜", "Tansu": "箪笥 抽屉柜（上层抽屉）", "Nagamochi": "長持 大木箱",
             "Tsuzura": "葛籠 藤编箱", "Komebitsu": "米びつ 米柜"}
    pw, ph, label = 480, 270, 26
    sheet = Image.new("RGB", (2 * pw, 8 * (ph + label)), (12, 12, 14))
    sd = ImageDraw.Draw(sheet)
    for row, c in enumerate(kinds()):
        glb, render, (eye, basis) = build(c)
        path = os.path.join(out_dir, "HakoOnna_Pose_%s.glb" % c["kind"])
        with open(path, "wb") as fh:
            fh.write(glb)
        lo, hi = c["zone"]
        zs = [hi[i] - lo[i] for i in range(3)]
        print("%-12s %6.1f KB  safe zone %.3f x %.3f x %.3f m  face %.2f m  eye (%.2f, %.2f, %.2f)" % (
            c["kind"], len(glb) / 1024, zs[0], zs[1], zs[2], c["face"][1], -eye[0], eye[1], eye[2]))
        y = row * (ph + label)
        sd.text((8, y + 4), "%s   安全区 %.2f × %.2f × %.2f m（宽×高×深）   脸高 %.2f m" % (
            names[c["kind"]], zs[0], zs[1], zs[2], c["face"][1]), fill=(230, 230, 230), font=font)
        sheet.paste(draw(render, eye, basis, (pw, ph), False), (0, y + label))
        center = gl(mul(add(lo, hi), .5))
        side = norm((1.0, .75, 1.6))
        sheet.paste(draw(render, add(center, side), look_basis(add(center, side), center), (pw, ph), True), (pw, y + label))
    sheet.save(os.path.join(out_dir, "PoseReference_Overview.png"), optimize=True)


main()
