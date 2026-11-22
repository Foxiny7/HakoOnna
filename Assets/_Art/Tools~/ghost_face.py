"""The face of the HakoOnna ghost: a jaw that opens the mouth wide, and a few bones for expression.
Used by ghost_pose_lib.py (before ghost_soft).

The Tripo head already has a mouth: the lips are two separate edge loops (0.6 to 2.4 mm apart around z = 1.0415 in
the 1.2 m rest pose) around a small box-shaped cavity with a palate plate and a tongue plate. It only lacks a jaw.
build(arm, mesh) adds, all under Head:
  Jaw                 hinged in front of the ears; the lower lip and the chin follow it, the cheeks stretch between
                      it and the skull
  MouthCorner_L / _R  the corners of the mouth (moved, not turned). They make the open mouth an oval: see corner()
  LipUpper            the upper lip: raise it to bare the upper teeth
  Lid_L / _R          the upper eyelids: raise them and the eyes go wide
  TeethUpper / TeethLower   two even rows of ten (a child's teeth, only the canines pointed) with gums, right
                      behind the lips (under Head / Jaw; the lower one is posed by follow_lip() to ride on the
                      lower lip). In the rest pose both rows are drawn back into the mouth (RECESS) so nothing
                      shows between the closed lips; the scream slides them out.
Tripo's cavity (a few big triangles that tear into spikes when the mouth opens), its two plates and the skin webs it
left across the slit are removed. In their place: a lining hung from the inner edge of the lips (it opens with
them; its first 5 mm are lip, in Tripo's material), a tongue lying on its floor, and the teeth. The rest uses the
second material, HakoOnna_Mouth, whose small generated texture (MOUTH_TEX) has columns for teeth, blood, black, the
lining (dark red fading to black), tongue and gums. The Tripo texture is not touched.
The teeth stand right behind the lip seam, each row rooted in the flesh of its own lip; the rows are rigid arcs and
the lips stretch around them. Every new face looks into the mouth: Unity culls back faces.
Pose spec (ghost_pose_lib.solve): "Jaw": {"local": [((1, 0, 0), degrees)]}, the others {"move": (x, y, z)} in the
head's rest frame (x her left, -y out of the face, z up); expression(k) gives the whole scream at strength k: the
jaw opens 27 degrees, what a person's does in a scream.
2026-11-22, the user: it has to read as a real person's mouth. A first version opened 48 degrees with scattered
fangs, a second kept the corners open like a slot; this one follows the photo the user gave: a wide oval, the whole
upper row of teeth under the upper lip, the lower row along the lower lip, the tongue on the floor.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector

HINGE = Vector((0.0, 0.005, 1.075))    # jaw joint, rest pose at 1.2 m
LIP_Z, CORNER_X = 1.0415, 0.0165       # the lip seam and half the width of the mouth
MOUTH_MAT = "HakoOnna_Mouth"
MOUTH_TEX = os.path.join("Textures", "Characters", "HakoOnna_Mouth.png")   # under Assets/_Art
RECESS = Vector((0.0, 0.0090, 0.0075))   # mouth shut, each row of teeth sits this far back and inside its own lip
# columns of MOUTH_TEX (u): tooth (v = root .. tip) | blood | black | walls (v = lips .. back) | tongue | gums
U_TOOTH, U_WALL = 0.0625, 0.4375
UV_TOOTH_ROOT, UV_TOOTH_TIP = (U_TOOTH, 0.02), (U_TOOTH, 0.94)
UV_BLOOD, UV_BLACK, UV_TONGUE, UV_GUM = (0.1875, 0.5), (0.3125, 0.5), (0.5625, 0.5), (0.6875, 0.5)
def smooth(x, a, b):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


CORNER_R = 0.022     # how far the pull of a corner of the mouth reaches


def ball(centre, radius, keep):
    """Weight of a round soft patch: 1 at the centre, 0 at the radius; only where keep(co)."""
    centre = Vector(centre)

    def w(c):
        d = (c - centre).length
        return (1.0 - (d / radius) ** 2) ** 2 if d < radius and keep(c) else 0.0
    return w


def corner(side, a):
    """Weight of a corner of the mouth, which is at |x| = a: where the slit between the lips ends (measured on the
    mesh; the painted lips go on a little further, but there upper and lower lip are one surface).
    An open mouth is an oval: the upper lip stays on the skull in the middle and curves down to the corner, the
    lower lip drops with the jaw in the middle and curves up to it, and both end in the same point. So along the
    lower lip the corner takes 1 - sqrt(1 - (x / a)^2) of a vertex (nothing in the middle, everything at the
    corner: the lower half of an ellipse), along the upper lip (x / a)^4 (it runs almost straight across the teeth
    and only turns down near the corner), fading with the height above / below the lip line and with the depth
    into the mouth; beyond the corner, into the cheek, it fades in a ball."""
    at = Vector((side * a, -0.066, LIP_Z))

    def w(c):
        x = c.x * side
        if x <= 0.0:
            return 0.0
        if x >= a:
            d = (c - at).length
            return (1.0 - (d / CORNER_R) ** 2) ** 2 if d < CORNER_R else 0.0
        dz = abs(c.z - LIP_Z)
        if dz >= CORNER_R:
            return 0.0
        t = x / a
        along = t ** 4 if c.z >= LIP_Z else 1.0 - math.sqrt(1.0 - t * t)
        return along * (1.0 - (dz / CORNER_R) ** 2) ** 2 * smooth(c.y, -0.040, -0.062)
    return w


def regions(a):
    """Soft regions of the face skin: (bone, where it sits, weight(co) -> 0..1 taken from Head / Jaw)."""
    return [("MouthCorner_L", (a, -0.066, LIP_Z), corner(1.0, a)),
            ("MouthCorner_R", (-a, -0.066, LIP_Z), corner(-1.0, a)),
            ("LipUpper", (0.0, -0.080, 1.049), ball((0.0, -0.080, 1.049), 0.026, lambda c: c.z > LIP_Z and c.y < -0.0625)),
            ("Lid_L", (0.0234, -0.071, 1.101), ball((0.0234, -0.071, 1.101), 0.019, lambda c: c.z > 1.0925)),
            ("Lid_R", (-0.0234, -0.071, 1.101), ball((-0.0234, -0.071, 1.101), 0.019, lambda c: c.z > 1.0925))]


def expression(k, jaw=27.0):
    """Pose-spec entries of the scream at strength k (0 = the sculpted face, 1 = full)."""
    k = max(0.0, k)
    out = {"Jaw": {"local": [((1, 0, 0), jaw * k)]},
           "LipUpper": {"move": (0.0, -0.0008 * k, 0.0032 * k)},
           "Lid_L": {"move": (0.0, 0.0, 0.0030 * k)}, "Lid_R": {"move": (0.0, 0.0, 0.0030 * k)}}
    e = smooth(k, 0.0, 0.45)   # the teeth come out of the lips early
    out["TeethUpper"] = {"move": (0.0, -RECESS.y * e, -RECESS.z * e)}
    out["TeethLower"] = {"move": (0.0, -RECESS.y * e, RECESS.z * e)}
    # the corners go a little less than half the way the middle of the lower lip goes with the jaw, and apart: a
    # mouth open in a scream is about half as wide again as it is shut
    th = math.radians(jaw * k)
    rel = Vector((0.0, -0.0724, LIP_Z)) - HINGE
    gone = Vector((0.0, rel.y * math.cos(th) - rel.z * math.sin(th), rel.y * math.sin(th) + rel.z * math.cos(th))) - rel
    for name, s in (("MouthCorner_L", 1.0), ("MouthCorner_R", -1.0)):
        out[name] = {"move": (s * 0.0095 * k, gone.y * 0.45, gone.z * 0.45)}
    return out


def cavity_verts(me, body):
    """Skin vertices on the inside of the mouth: the face itself is right in front of them. Grown from the deep
    ones along the edges, so a fold of the chin that happens to sit behind the lower lip does not count."""
    from mathutils.bvhtree import BVHTree
    members = set(body)
    polys = [p.vertices[:] for p in me.polygons if p.vertices[0] in members]
    bvh = BVHTree.FromPolygons([v.co for v in me.vertices], polys)

    def covered(j):
        c = me.vertices[j].co
        # not wider than the cavity: further out, the skin on the side of the jaw has the cheek in front of it too
        return (abs(c.x) < 0.0215 and -0.075 < c.y < -0.025 and 1.03 < c.z < 1.062
                and bvh.ray_cast(c + Vector((0, -0.0008, 0)), Vector((0, -1, 0)), 0.045)[0] is not None)
    adj = {}
    for e in me.edges:
        a, b = e.vertices
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)
    out = {j for j in body if me.vertices[j].co.y > -0.055 and covered(j)}
    stack = list(out)
    while stack:
        for k in adj.get(stack.pop(), ()):
            if k not in out and covered(k):
                out.add(k)
                stack.append(k)
    # Tripo closes the floor of the mouth with big triangles from the edge of the lower lip to a point deep in the
    # neck: neither end counts as inside, so take those faces by their middle. (Only faces on the lip edge: the skin
    # under the chin is behind the chin too.)
    faces = set()
    for p in me.polygons:
        if p.vertices[0] not in members or any(v in out for v in p.vertices):
            continue
        cs = [me.vertices[v].co for v in p.vertices]
        if (any(abs(c.z - LIP_Z) < 0.0015 and c.y < -0.058 and abs(c.x) < 0.02 for c in cs)
                and all(abs(c.x) < 0.03 and -0.078 < c.y < -0.005 and 0.985 < c.z < 1.065 for c in cs)):
            mid = sum(cs, Vector()) / len(cs)
            if mid.y > -0.070 and bvh.ray_cast(mid + Vector((0, -0.0008, 0)), Vector((0, -1, 0)), 0.08)[0] is not None:
                faces.add(p.index)
    return out, faces


def mouth_material(art_dir):
    path = os.path.join(art_dir, MOUTH_TEX)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    n = 64
    img = bpy.data.images.new("HakoOnna_Mouth", n, n, alpha=False)
    rnd = random.Random(7)
    px = []
    for y in range(n):
        v = y / (n - 1)
        for x in range(n):
            col = x * 8 // n
            if col == 0:      # tooth: dark only in the gum, ivory with a little stain from the gum line on
                k = smooth(v, 0.04, 0.30)
                s = 0.94 + 0.06 * rnd.random()
                c = (0.30 + 0.50 * k) * s, (0.05 + 0.69 * k) * s, (0.05 + 0.60 * k) * s
            elif col == 1:    # blood
                c = 0.30 + 0.05 * rnd.random(), 0.01, 0.02
            elif col == 2:    # black
                c = 0.012, 0.0, 0.0
            elif col == 3:    # the walls of the mouth: dark red at the lips, black at the back
                k = 1.0 - smooth(v, 0.0, 0.8)
                c = 0.012 + 0.26 * k, 0.01 * k, 0.02 * k
            elif col == 4:    # tongue
                s = 0.92 + 0.08 * rnd.random()
                c = 0.50 * s, 0.17 * s, 0.19 * s
            else:             # gums
                s = 0.92 + 0.08 * rnd.random()
                c = 0.42 * s, 0.10 * s, 0.12 * s
            px += [c[0], c[1], c[2], 1.0]
    img.pixels = px
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    mat = bpy.data.materials.new(MOUTH_MAT)
    mat.use_nodes = True
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    bsdf = next(nd for nd in nt.nodes if nd.type == "BSDF_PRINCIPLED")
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.nodes.active = tex
    bsdf.inputs["Roughness"].default_value = 1.0
    return mat


def find_plates(me, islands, body):
    plates = []
    for k, mem in enumerate(islands):
        if k != body and all(abs(me.vertices[j].co.x) < 0.02 and -0.075 < me.vertices[j].co.y < -0.03
                             and 1.025 < me.vertices[j].co.z < 1.06 for j in mem):
            plates.append((sum(me.vertices[j].co.z for j in mem) / len(mem), mem))
    plates.sort()
    return [mem for _, mem in plates]   # lower one (the tongue) first


def jaw_weights(mesh, islands, body, plates, inside, log):
    """Head weight of the lower face, the floor of the mouth and the tongue plate moves to Jaw."""
    me = mesh.data
    vg = mesh.vertex_groups
    head = vg["Head"].index
    jaw = vg.new(name="Jaw")
    # Tripo lets the neck pull on the front of the face (the middle of the lower lip is 20 % neck): harmless while
    # the head sits straight on the neck, but in the lunges the head is thrown back and the lips slide off the
    # skull and its teeth. From the chin up the face belongs to the head.
    fixed = 0
    for j in islands[body]:
        v = me.vertices[j]
        k = smooth(v.co.z, 1.005, 1.030) * smooth(v.co.y, -0.030, -0.045)
        other = [(g.group, g.weight) for g in v.groups if g.group != head and g.weight > 1e-4]
        if k < 1e-3 or not other:
            continue
        gain = 0.0
        for gi, w in other:
            vg[gi].add([j], w * (1 - k), "REPLACE")
            gain += w * k
        vg["Head"].add([j], next((g.weight for g in v.groups if g.group == head), 0.0) + gain, "REPLACE")
        fixed += 1
    log("face: %d vertices of the front of the face taken off the neck" % fixed)
    moved = 0
    for j in islands[body]:
        v = me.vertices[j]
        c = v.co
        if not (0.98 < c.z < 1.085 and c.y < 0.03):
            continue
        wh = next((g.weight for g in v.groups if g.group == head), 0.0)
        if wh < 1e-3:
            continue
        ax = abs(c.x)
        if j in inside:                                               # the cavity behind the lips
            w = smooth(c.z, 1.050, 1.043)
        elif ax < CORNER_X and c.y < -0.05:                           # the lips themselves: split at the seam
            w = 1.0 if c.z < LIP_Z else 0.0
        else:                                                         # cheeks: stretch, wider away from the mouth
            h = min(0.03, 0.004 + 0.6 * max(0.0, ax - CORNER_X))
            w = smooth(c.z, LIP_Z + h, LIP_Z - h)
        w *= smooth(c.y, 0.03, 0.0) * smooth(c.z, 0.985, 1.005)       # not the nape, not the throat
        if w < 1e-3:
            continue
        vg["Head"].add([j], wh * (1 - w), "REPLACE")
        jaw.add([j], wh * w, "REPLACE")
        moved += 1
    if plates:
        for j in plates[0]:
            for gi in [g.group for g in me.vertices[j].groups]:
                vg[gi].remove([j])
            jaw.add([j], 1.0, "REPLACE")
    log("jaw: %d skin vertices follow it, %d plates in the mouth (the lower one is the tongue)" % (moved, len(plates)))


def region_weights(mesh, islands, body, table, log):
    """Each REGIONS bone takes a soft round patch of the face skin from Head / Jaw."""
    me = mesh.data
    vg = mesh.vertex_groups
    src = [vg["Head"].index, vg["Jaw"].index]
    counts = []
    for name, at, weight in table:
        g = vg.new(name=name)
        n = 0
        for j in islands[body]:
            v = me.vertices[j]
            if abs(v.co.x) > 0.06 or v.co.y > 0.0 or not 0.99 < v.co.z < 1.14:
                continue
            w = weight(v.co)
            if w < 1e-3:
                continue
            had = [(gr.group, gr.weight) for gr in v.groups if gr.group in src]
            tot = sum(x for _, x in had)
            if tot < 1e-3:
                continue
            for gi, x in had:
                vg[gi].add([j], x * (1 - w), "REPLACE")
            g.add([j], tot * w, "REPLACE")
            n += 1
        counts.append("%s %d" % (name, n))
    log("face regions: " + ", ".join(counts))


def add_mouth(mesh, mat, inside, inside_faces, plates, slit, log):
    me = mesh.data
    me.materials.append(mat)
    slot = len(me.materials) - 1
    vg = mesh.vertex_groups
    gu, gl = vg["TeethUpper"].index, vg["TeethLower"].index
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    dl = bm.verts.layers.deform.verify()
    uvl = bm.loops.layers.uv.verify()
    rnd = random.Random(11)

    # Tripo's own cavity goes, and both plates with it: its walls are a few big triangles that tear into spikes when
    # the mouth opens. What is left is a hole whose rim is the inner edge of the lips; a new lining is hung from
    # that rim further down.
    doomed = [f for f in bm.faces if f.index in inside_faces or any(v.index in inside for v in f.verts)]
    # and the webs: Tripo left skin triangles across the slit, from one lip to the other. Mouth shut they are 0.6 mm
    # tall and nobody sees them; mouth open they are pink blades across it. (Not the ones at the very ends of the
    # slit: those close the corners.)
    webs = [f for f in bm.faces if f not in set(doomed)
            and all(abs(v.co.z - LIP_Z) < 0.0015 and v.co.y < -0.055 for v in f.verts)
            and max(v.co.z for v in f.verts) >= LIP_Z > min(v.co.z for v in f.verts)
            and abs(sum(v.co.x for v in f.verts) / len(f.verts)) < slit - 0.002]
    doomed += webs
    gone = [bm.verts[j] for mem in plates for j in mem] + [v for v in bm.verts if v.index in inside]
    going = set(gone)
    touched = {e for f in doomed for e in f.edges}
    walls = len(doomed)
    bmesh.ops.delete(bm, geom=doomed, context="FACES_ONLY")
    wire = [e for e in touched if e.is_valid and not e.link_faces and not (e.verts[0] in going or e.verts[1] in going)]
    touched -= set(wire)
    bmesh.ops.delete(bm, geom=wire, context="EDGES")
    # the rim of the hole: the edges that lost one of their two faces
    rim_edges = [e for e in touched if e.is_valid and len(e.link_faces) == 1
                 and not (e.verts[0] in going or e.verts[1] in going)]

    gj = vg["Jaw"].index
    gh = vg["Head"].index

    def vert(co, wl, uv, teeth=True, lower=None):
        """wl = share of the lower side. The teeth and gums sit on TeethUpper / TeethLower, drawn back into the
        mouth by RECESS in the rest pose."""
        co = Vector(co)
        if teeth:
            co += Vector((0.0, RECESS.y, RECESS.z * (1.0 - 2.0 * wl)))
        v = bm.verts.new(co)
        a, b = (gu, gl) if teeth else (gh, gj if lower is None else lower)
        if wl < 1.0:
            v[dl][a] = 1.0 - wl
        if wl > 0.0:
            v[dl][b] = wl
        return (v, uv)

    def face(vs, toward=None, skin=False):
        """toward: a point the face must look at (Unity culls back faces). skin: Tripo's material, smooth."""
        f = bm.faces.new([v for v, _ in vs])
        f.material_index = 0 if skin else slot
        f.smooth = skin
        for loop, (_, uv) in zip(f.loops, vs):
            loop[uvl].uv = uv
        if toward is not None:
            f.normal_update()
            if f.normal.dot(toward - f.calc_center_median()) < 0.0:
                f.normal_flip()

    seam = [v.co.copy() for v in bm.verts if abs(v.co.z - LIP_Z) < 0.0012 and v.co.y < -0.058 and abs(v.co.x) < 0.02]
    front_y = (min(c.y for c in seam if abs(c.x) < 0.0035) if seam else -0.073) + 0.0028   # right behind the lips

    # The dental arch: half an ellipse from the incisors back to the premolars, walked by arc length.
    def curve(half_width, depth, y0):
        table, prev, length = [(0.0, Vector((0.0, y0, 0.0)))], Vector((0.0, y0, 0.0)), 0.0
        for k in range(1, 201):
            th = math.radians(88.0) * k / 200
            q = Vector((half_width * math.sin(th), y0 + depth * (1.0 - math.cos(th)), 0.0))
            length += (q - prev).length
            table.append((length, q))
            prev = q

        def at(dist):   # dist from the middle, her left positive
            d = min(abs(dist), table[-1][0] - 1e-6)
            k = next(i for i in range(1, len(table)) if table[i][0] >= d)
            (s0, q0), (s1, q1) = table[k - 1], table[k]
            q = q0.lerp(q1, (d - s0) / max(s1 - s0, 1e-9))
            return Vector((q.x if dist >= 0 else -q.x, q.y, 0.0))
        return at

    # (width, crown length, how much the tip narrows, half thickness): incisor, incisor, canine, premolar, premolar.
    # A child's teeth: even, flat-edged incisors, only the canines come to a point.
    ROWS = {True: ((0.0074, 0.0072, 0.92, 0.0010), (0.0060, 0.0064, 0.90, 0.0010), (0.0060, 0.0072, 0.60, 0.0013),
                   (0.0056, 0.0056, 0.82, 0.0016), (0.0056, 0.0052, 0.82, 0.0016)),
            False: ((0.0048, 0.0056, 0.92, 0.0009), (0.0052, 0.0057, 0.92, 0.0009), (0.0056, 0.0062, 0.60, 0.0012),
                    (0.0056, 0.0048, 0.82, 0.0015), (0.0056, 0.0046, 0.82, 0.0015))}
    GAP = 0.0003                                   # between teeth
    GUM_Z = {True: 0.0020, False: 0.0015}          # the gum line above (upper row) / below (lower row) the lip seam, mouth open
    count = 0
    for upper in (True, False):
        at = curve(0.0185, 0.0240, front_y) if upper else curve(0.0170, 0.0225, front_y + 0.0022)   # lower row inside
        d = -1.0 if upper else 1.0
        wj = 0.0 if upper else 1.0
        gum_z = LIP_Z + GUM_Z[True] if upper else LIP_Z - GUM_Z[False]
        edges = []   # arc positions of the gaps, for the gum
        for sign in (1.0, -1.0):
            pos = 0.0
            for width, crown, narrow, thick in ROWS[upper]:
                mid = sign * (pos + width / 2)
                c = at(mid)
                tangent = at(mid + 0.0004) - at(mid - 0.0004)
                side = tangent.normalized() if tangent.length > 1e-7 else Vector((1, 0, 0))
                out = Vector((side.y, -side.x, 0.0))                    # toward the lips
                crown *= 0.96 + 0.08 * rnd.random()                     # not perfectly even
                lean = side * (0.0003 * (rnd.random() - 0.5))
                hw = (width - GAP) / 2
                rings = []
                for z, k_w, k_t, uv in ((gum_z - d * 0.0022, 0.86, 0.9, UV_TOOTH_ROOT),          # in the gum
                                        (gum_z + d * 0.0006, 1.0, 1.0, (U_TOOTH, 0.40)),        # the gum line
                                        (gum_z + d * crown * 0.72, 0.5 + 0.5 * narrow, 0.8, (U_TOOTH, 0.80)),
                                        (gum_z + d * crown, narrow, 0.45, UV_TOOTH_TIP)):       # the biting edge
                    rings.append([vert(c + lean * (k_w < 1.0) + side * (sx * hw * k_w) + out * (sy * thick * k_t) + Vector((0, 0, z)),
                                       wj, uv) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
                for r0, r1 in zip(rings, rings[1:]):
                    for k in range(4):
                        n = (k + 1) % 4
                        quad = [r0[k], r0[n], r1[n], r1[k]]
                        face(quad if upper else quad[::-1])
                face(rings[-1][::-1] if upper else rings[-1])
                count += 1
                pos += width
            edges.append(sign * pos)
        # the gum: a band along the outside of the row, from the gum line into the lip (kept close to the teeth:
        # the lower lip is thin, a band standing off them came out through its skin)
        n_seg = 14
        strip = []
        for k in range(n_seg + 1):
            dist = edges[1] + (edges[0] - edges[1]) * k / n_seg
            c = at(dist)
            tangent = at(dist + 0.0004) - at(dist - 0.0004)
            side = tangent.normalized() if tangent.length > 1e-7 else Vector((1, 0, 0))
            out = Vector((side.y, -side.x, 0.0))
            strip.append((vert(c + out * 0.0013 + Vector((0, 0, gum_z + d * 0.0004)), wj, UV_GUM),
                          vert(c + out * 0.0010 + Vector((0, 0, gum_z - d * 0.0028)), wj, UV_GUM)))
        for g0, g1 in zip(strip, strip[1:]):
            quad = [g0[0], g1[0], g1[1], g0[1]]
            face(quad if upper else quad[::-1])
    def new_vert(co, weights, uv):
        v = bm.verts.new(co)
        for gi, w in weights.items():
            v[dl][gi] = w
        return (v, uv)

    # The lining: from the rim of the hole (the inner edge of the lips) back into the head, one strip per rim edge.
    # Its rings up to the first copy the weights of the lip vertex they hang from, so the lining opens with the
    # lips, whatever shape they take; the deep ring and the back lean on the skull and the jaw.
    # The first 5 mm are lip, not lining: Tripo's material, with the colour the lip has along its edge (the uv of the
    # lip face there, mirrored a little way into it). The lips do not quite meet (0.6 to 2.4 mm, the upper one
    # overhanging), so with a dark lining right from the edge the shut mouth was a jagged black slit.
    # Then dark red, black at the back.
    LIP_IN, RING = 0.005, 0.016
    # First the lips are shut: Tripo left them 0.6 mm apart in the middle and, where its webs were, up to 2.4 mm
    # near the corners, which from the front is a gap one looks straight through. Both edges go to the height
    # half way between them (at most 1.2 mm; the jaw opens them again).
    rim_verts = {v for e in rim_edges for v in e.verts}
    # The rim is one loop round the mouth. Cut at its two ends, the corners of the mouth, it is two arcs: the edge
    # of the upper lip, from which the roof of the lining hangs, and the edge of the lower lip with the floor.
    # (Not "above / below the lip line": at the corners both edges are on the same side of it.)
    beside = {}
    for e in rim_edges:
        a, b = e.verts
        beside.setdefault(a, []).append(b)
        beside.setdefault(b, []).append(a)
    loop, prev = [min(rim_verts, key=lambda v: v.co.x)], None
    while True:
        step = [v for v in beside[loop[-1]] if v is not prev]
        if not step or step[0] is loop[0]:
            break
        prev = loop[-1]
        loop.append(step[0])
    if len(loop) != len(rim_verts):
        raise RuntimeError("the rim of the mouth is not one loop: %d of %d vertices" % (len(loop), len(rim_verts)))
    far = max(range(len(loop)), key=lambda i: loop[i].co.x)
    arcs = sorted((loop[:far + 1], loop[far:] + loop[:1]), key=lambda arc: sum(v.co.z for v in arc) / len(arc))
    floor_arc, roof_arc = set(arcs[0]), set(arcs[1])
    # (Tripo ends the slit in two vertices one above the other; both are the corner, or the lining folds there)
    ends = {v for v in rim_verts if min((v.co - loop[0].co).length, (v.co - loop[far].co).length) < 0.0012}
    lip_side = {v: 0.0 if v in ends else 1.0 if v in roof_arc else -1.0 for v in rim_verts}   # roof 1, floor -1
    roof_edges = {e for e in rim_edges if e.verts[0] in roof_arc and e.verts[1] in roof_arc}

    def profile(verts):
        by_x = {}
        for v in verts:
            by_x.setdefault(round(v.co.x, 4), []).append(v.co.z)
        pts = sorted((x, sum(zs) / len(zs)) for x, zs in by_x.items())

        def z_at(x):
            if x <= pts[0][0]:
                return pts[0][1]
            for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
                if x <= x1:
                    return z0 + (z1 - z0) * (x - x0) / max(x1 - x0, 1e-9)
            return pts[-1][1]
        return z_at
    z_up, z_low = profile(roof_arc), profile(floor_arc)
    shut = 0.0
    for v in rim_verts:
        z = (z_up(v.co.x) + z_low(v.co.x)) / 2
        shut = max(shut, abs(z - v.co.z))
        v.co.z = z
    inner_uv = {}
    for e in rim_edges:
        f = e.link_faces[0]
        at = {l.vert: l[uvl].uv.copy() for l in f.loops if l.vert in e.verts}
        far = [l[uvl].uv for l in f.loops if l.vert not in e.verts]
        into = (sum(far, Vector((0.0, 0.0))) / len(far) - (at[e.verts[0]] + at[e.verts[1]]) / 2) * 0.25
        inner_uv[e] = {v: (at[v], at[v] + into) for v in e.verts}
    spoke = {}
    for v in rim_verts:
        up = lip_side[v]
        open_ = max(0.0, 1.0 - (abs(v.co.x) / slit) ** 2)   # it spreads up / down in the middle, not at the corners
        own = dict(v[dl].items())
        deep = {gi: 0.5 * w for gi, w in own.items()}              # half with the lip, half with the skull / the jaw:
        # as wide as the open mouth, further in. The floor is shallow and partly on the skull: the skin under the
        # chin is half on the neck and does not drop with the jaw, a deep floor rigid on the jaw ends up below it
        # and the skin shows inside the mouth (two pale specks beside the tongue).
        for gi, w in (((gh, 0.5),) if up > 0 else ((gj, 0.2), (gh, 0.3)) if up < 0 else ((gj, 0.1), (gh, 0.4))):
            deep[gi] = deep.get(gi, 0.0) + w
        depth = 0.011 if up > 0 else 0.006
        # the first ring runs straight back, past the teeth where they sit when the mouth is shut: they are then
        # above the roof and under the floor of the lining, and nothing of them shows between the lips
        spoke[v] = (v,
                    new_vert(v.co + Vector((0.0, LIP_IN, 0.0)), own, (U_WALL, 0.05)),
                    new_vert(v.co + Vector((0.0, RING, 0.0)), own, (U_WALL, 0.45)),
                    new_vert(Vector((v.co.x * 1.15, max(v.co.y + 0.028, -0.040), LIP_Z + up * (0.003 + depth * open_))),
                             deep, (U_WALL, 0.98)))
    back = new_vert(Vector((0.0, -0.033, LIP_Z)), {gh: 0.5, gj: 0.5}, (U_WALL, 0.98))
    for e in rim_edges:
        va, vb = e.verts
        (a0, a1, a2, a3), (b0, b1, b2, b3) = spoke[va], spoke[vb]
        (ua, ua_in), (ub, ub_in) = inner_uv[e][va], inner_uv[e][vb]
        # every face looks into the mouth: the roof down, the floor up. (Not "at the axis of the mouth": shut,
        # roof and floor lie in one plane with it.)
        inward = Vector((0.0, -0.058, LIP_Z + (-0.024 if e in roof_edges else 0.024)))
        face([(a0, ua), (b0, ub), (b1[0], ub_in), (a1[0], ua_in)], inward, skin=True)
        face([a1, b1, b2, a2], inward)
        face([a2, b2, b3, a3], inward)
        face([a3, b3, back], inward + Vector((0.0, -0.02, 0.0)))
    # The tongue: a low hump on the floor of the mouth, behind the lower teeth, on the jaw.
    rows = []
    for t, half in ((0.0, 0.0040), (0.15, 0.0085), (0.40, 0.0110), (0.70, 0.0115), (1.0, 0.0110)):
        hump = 0.0030 * math.sin(math.pi * min(1.0, t * 1.3))
        # it lies on the floor of the lining and leans on the skull as much as that floor does, or the floor rises
        # over it when the mouth opens
        ws = {gi: w for gi, w in ((gj, 1.0 - 0.3 * t), (gh, 0.3 * t)) if w > 0.0}
        rows.append([new_vert(Vector((half * u, -0.0625 + 0.028 * t, LIP_Z - 0.0010 + hump * (1.0 - 0.7 * u * u) - 0.0025 * u * u)),
                              ws, UV_TONGUE) for u in (-1.0, -0.5, 0.0, 0.5, 1.0)])
    for q0, q1 in zip(rows, rows[1:]):
        for k in range(4):
            face([q0[k], q0[k + 1], q1[k + 1], q1[k]], Vector((0.0, -0.05, LIP_Z + 0.05)))
    if gone:
        bmesh.ops.delete(bm, geom=gone, context="VERTS")
    open_edges = [e for e in bm.edges if len(e.link_faces) == 1 and all(
        abs(v.co.x) < 0.03 and -0.085 < v.co.y < -0.063 and abs(v.co.z - LIP_Z) < 0.012 and v[dl].get(gu, 0.0) + v[dl].get(gl, 0.0) < 0.5
        for v in e.verts)]
    holes = len(open_edges)
    if holes:
        log("open edges at: " + "; ".join("(%.4f %.4f %.4f)-(%.4f %.4f %.4f)" % (*e.verts[0].co, *e.verts[1].co) for e in open_edges[:8]))
    bm.to_mesh(me)
    bm.free()
    me.update()
    log("mouth: %d cavity faces replaced by a lining of %d spokes (%d open edges left at the lips), %d teeth with gums, "
        "tongue; material slot %d (%s)" % (walls, len(spoke), holes, count, slot, mat.name))
    log("mouth: %d of the removed faces were webs across the slit; lips shut (their edges moved up to %.1f mm)"
        % (len(webs), shut * 1000))


def build(arm, mesh, islands, art_dir, log=print):
    me = mesh.data
    body = max(range(len(islands)), key=lambda k: len(islands[k]))
    plates = find_plates(me, islands, body)
    inside, inside_faces = cavity_verts(me, islands[body])
    # where the slit between the lips ends: the outermost lip vertices that touch the cavity
    edge = {v for p in me.polygons if any(v in inside for v in p.vertices) for v in p.vertices} - inside
    edge = [me.vertices[v].co for v in edge if abs(me.vertices[v].co.z - LIP_Z) < 0.002 and me.vertices[v].co.y < -0.058]
    slit = max(abs(c.x) for c in edge) if edge else 0.013
    table = regions(slit)
    log("mouth slit: half width %.4f m (%d lip vertices on the cavity)" % (slit, len(edge)))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    b = eb.new("Jaw")
    b.head, b.tail = HINGE, Vector((0.0, -0.060, 1.020))
    b.parent = eb["Head"]
    for name, at, weight in table:
        b = eb.new(name)
        b.head, b.tail = Vector(at), Vector(at) + Vector((0.0, -0.012, 0.0))
        b.parent = eb["Head"]
    for name, parent, z in (("TeethUpper", "Head", LIP_Z + 0.004), ("TeethLower", "Jaw", LIP_Z - 0.004)):
        b = eb.new(name)
        b.head, b.tail = Vector((0.0, -0.066, z)), Vector((0.0, -0.078, z))
        b.parent = eb[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for name in ("TeethUpper", "TeethLower"):
        mesh.vertex_groups.new(name=name)
    jaw_weights(mesh, islands, body, plates, inside, log)
    region_weights(mesh, islands, body, table, log)
    # the middle of the lower lip, for follow_lip(): rest position and weights by bone name
    me = mesh.data
    names = {g.index: g.name for g in mesh.vertex_groups}
    edge = [v for v in (me.vertices[j] for j in islands[body])
            if abs(v.co.z - (LIP_Z - 0.0003)) < 0.0008 and v.co.y < -0.060 and abs(v.co.x) < 0.02]
    mid = min(edge, key=lambda v: (abs(v.co.x), v.co.y))
    lip = (mid.co.copy(), {names[g.group]: g.weight for g in mid.groups if g.weight > 1e-4})
    log("lower lip middle at %s: %s" % ([round(x, 4) for x in lip[0]], {k: round(w, 2) for k, w in lip[1].items()}))
    add_mouth(mesh, mouth_material(art_dir), inside, inside_faces, plates, slit, log)
    return {"lip": lip}


def follow_lip(rig, spec, G, P):
    """TeethLower rides on the middle of the lower lip. The corner bones hold the lip back from the jaw, so a row
    of teeth fixed to the jaw would come out through the skin under the lip."""
    c0, ws = rig.face["lip"]
    tot = sum(ws.values())
    p = Vector()
    for b, w in ws.items():
        p += (P[b] + G[b] @ (c0 - rig.head0[b])) * (w / tot)
    g = G["Head"].slerp(G["Jaw"], ws.get("Jaw", 0.0) / tot)
    b = "TeethLower"
    move = Vector(spec.get(b, {}).get("move", (0, 0, 0)))
    G[b] = g
    P[b] = p + g @ (rig.head0[b] - c0 + move)
