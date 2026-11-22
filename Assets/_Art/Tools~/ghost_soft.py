"""Soft parts of the HakoOnna ghost: hair, kimono sleeves and hem. Used by ghost_pose_lib.py.

build(arm, mesh)   adds the bone chains to the Tripo rig and re-weights the cloth / hair to them:
  hair    Hair_M<nn>_<i>   the long hair mass (the big shell and the strand cards down the back), chains around the head
          Hair_L<nn>_<i>   one chain per long strand card in front of the face / shoulders
                           (its first bone is the part lying on the face)
          Hair_F<nn>_<i>   the short fringe and cheek cards, grouped by direction
                           Hair lying on the wet face stays stuck to it and only falls away when the head hangs
                           upside down.
  sleeves Sleeve_<L|R>_<U|L>        co-located with the upper / lower arm: the sleeve tube turns about the arm so its
                                    bag hangs on the low side whatever the arm's own roll is
          SleeveBag_<L|R>_<A|B>_<i> two short chains through the hanging bag, children of those
  hem     Hem_<nn>_<i>     the skirt below the knees, chains around it on the lower legs. The skirt above stays on
                           the legs: the Tripo legs end at mid-thigh, there is nothing under the skirt to show.
  The strand cards carry painted skin between their strands; build() subdivides them once and drops the faces that
  are mostly skin (trim_cards), or a card hanging free would show as a pale flap. Tripo also ties the rags at the
  bottom of each sleeve to the thigh or the trunk next to them; plan_sleeve() gives them back to the arm.
settle(rig, spec, ctx, G, P)   hangs the chains for one pose: a damped particle relaxation under gravity with the
  body capsules, the container boards (ctx["bvh"]) and its safe zone as colliders. The body is taken from the rest
  pose into the pose in BLEND steps while the chains hang, so hair and cloth come to rest on the side of the body
  they would really slide to (started from the finished pose they begin inside it and pop out anywhere). A
  collider never pushes a particle further out than it sits in the rest pose, so the rest pose stays what Tripo
  sculpted. The chains of the hair mass are tied to their neighbours (one sheet, not ropes). The first bone of a
  chain may leave its parent (pose location): a root that would sink into the body is kept on its surface.
Options in the pose spec: "hair": {"mode": "gravity" | "spread" | "follow", "g", "push", "floor", "chains"},
  "cloth": {"mode": "gravity" | "follow", "g", "roll", "stiff_bag", "stiff_hem", "floor"}.
"""
import math

import bmesh
import bpy
from mathutils import Quaternion, Vector

HEAD_CENTRE = Vector((-0.011, -0.012, 0.06))   # scalp centre relative to the Head bone, rest
ATTACH = 1.03          # the hair mass leaves the head here (rest pose, 1.2 m model)
CARD_ROOT = 1.12       # long strand cards leave the forehead here ...
JAW = 1.0              # ... and the face here
FRINGE_ROOT = 1.14     # the fringe hangs from the hairline
HEM_TOP, HEM_FULL, HEM_ROOT = 0.41, 0.34, 0.375   # around the knee (0.363), where Tripo's own blend sits
BLEND, ITERS, DAMP, STEP = 60, 260, 0.88, 0.0012   # steps from the rest pose into the pose, then to settle
#            body margin, wall margin, floor margin, stiffness toward the pose (0 = limp)
KIND = {"mass": (0.012, 0.04, 0.05, 0.0), "lock": (0.008, 0.02, 0.02, 0.0), "fringe": (0.012, 0.02, 0.02, 0.0),
        "bag": (0.015, 0.025, 0.02, 0.4), "hem": (0.02, 0.05, 0.045, 0.35)}


def vec(v):
    return Vector(v) if not isinstance(v, Vector) else v.copy()


def smooth(x, a, b):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def dang(a, b):
    return (a - b + 180.0) % 360.0 - 180.0


def pct(vals, q):
    s = sorted(vals)
    return s[max(0, min(len(s) - 1, int(len(s) * q)))]


def seg_closest(p, a, b):
    ab = b - a
    t = 0.0 if ab.length_squared < 1e-12 else max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return a + ab * t


def capsules(G, P):
    # the trunk bone by bone (it is rarely straight in these poses), wide enough to hold the kimono as well
    caps = [(P["Hips"], P["Spine"], 0.095), (P["Spine"], P["Chest"], 0.095), (P["Chest"], P["UpperChest"], 0.095),
            (P["UpperChest"], P["Neck"], 0.085), (P["Left_UpperArm"], P["Right_UpperArm"], 0.065),
            (P["Neck"], P["Head"], 0.045), (P["Left_UpperLeg"], P["Right_UpperLeg"], 0.08)]
    hc = P["Head"] + G["Head"] @ HEAD_CENTRE
    caps.append((hc, hc, 0.075))
    for s in ("Left", "Right"):
        caps += [(P[s + "_UpperArm"], P[s + "_LowerArm"], 0.04), (P[s + "_LowerArm"], P[s + "_Hand"], 0.035),
                 (P[s + "_UpperLeg"], P[s + "_LowerLeg"], 0.06), (P[s + "_LowerLeg"], P[s + "_Foot"], 0.045)]
    return caps, hc


# ---------------------------------------------------------------------------- build


def islands_of(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    isl, out = [-1] * len(bm.verts), []
    for v in bm.verts:
        if isl[v.index] >= 0:
            continue
        k = len(out)
        isl[v.index] = k
        stack, mem = [v], []
        while stack:
            a = stack.pop()
            mem.append(a.index)
            for e in a.link_edges:
                b = e.other_vert(a)
                if isl[b.index] < 0:
                    isl[b.index] = k
                    stack.append(b)
        out.append(mem)
    bm.free()
    return out


def read_weights(mesh):
    names = {g.index: g.name for g in mesh.vertex_groups}
    return [{names[g.group]: g.weight for g in v.groups if g.weight > 0} for v in mesh.data.vertices]


def write_weights(mesh, new):
    vg = mesh.vertex_groups
    for j, w in new.items():
        top = sorted(w.items(), key=lambda kv: -kv[1])[:4]   # Unity skins with 4 influences
        tot = sum(x for _, x in top)
        if tot <= 1e-6:
            continue
        for gi in [g.group for g in mesh.data.vertices[j].groups]:
            vg[gi].remove([j])
        for nm, x in top:
            if x / tot > 1e-4:
                (vg.get(nm) or vg.new(name=nm)).add([j], x / tot, "REPLACE")


def centreline(pts, z_top, z_bot, n):
    """n + 1 points from z_top down to z_bot through the middle of pts (centroid per height band)."""
    dz = (z_top - z_bot) / n
    out, last = [], None
    for i in range(n + 1):
        z = z_top - dz * i
        band = [p for p in pts if abs(p.z - z) <= dz * 0.5]
        if len(band) >= 2:
            last = (sum(p.x for p in band) / len(band), sum(p.y for p in band) / len(band))
        elif last is None:
            near = sorted(pts, key=lambda p: abs(p.z - z))[:6]
            last = (sum(p.x for p in near) / len(near), sum(p.y for p in near) / len(near))
        out.append(Vector((last[0], last[1], z)))
    return out


def bone_weights(z, z_top, z_bot, n):
    """Blend along a chain of n bones by height: [(bone index, weight)]."""
    t = max(0.0, min(1.0, (z_top - z) / (z_top - z_bot))) * n - 0.5
    i0 = int(math.floor(t))
    f = t - i0
    out = {}
    for i, w in ((i0, 1 - f), (i0 + 1, f)):
        i = max(0, min(n - 1, i))
        out[i] = out.get(i, 0.0) + w
    return out.items()


class Plan:
    def __init__(self):
        self.bones, self.weights, self.chains, self.rolls, self.notes = [], {}, [], [], []

    def chain(self, name, kind, parent, pts, face=0):
        names = []
        for i in range(len(pts) - 1):
            b = "%s_%d" % (name, i + 1)
            self.bones.append((b, pts[i], pts[i + 1], names[-1] if names else parent, i > 0))
            names.append(b)
        self.chains.append({"name": name, "kind": kind, "parent": parent, "bones": names, "face": face,
                            "x0": [p.copy() for p in pts]})
        return names


def plan_hair(plan, co, W, isl, body, log):
    def limb(k):
        tot = lim = 0.0
        for j in isl[k]:
            for nm, w in W[j].items():
                tot += w
                if nm.startswith(("Left_", "Right_")) and "Shoulder" not in nm:
                    lim += w
        return lim > 0.5 * tot

    def face_interior(k):   # eyeballs and the two tooth rows
        return all(abs(co[j].x) < 0.046 and -0.075 < co[j].y < -0.034 and 1.02 < co[j].z < 1.112 for j in isl[k])

    hair = [k for k in range(len(isl)) if k != body and not limb(k) and not face_interior(k)]
    shell = max(hair, key=lambda k: len(isl[k]))
    scalp = [co[j] for j in isl[shell] if co[j].z > 1.08]
    cx = sum(c.x for c in scalp) / len(scalp)
    cy = sum(c.y for c in scalp) / len(scalp)

    def ang(c):   # 0 = back, +90 = her left, 180 = front
        return math.degrees(math.atan2(c.x - cx, c.y - cy))

    def centroid(k):
        cs = [co[j] for j in isl[k]]
        return Vector((sum(c.x for c in cs), sum(c.y for c in cs), sum(c.z for c in cs))) / len(cs)

    mass_isl = [k for k in hair if k == shell or abs(ang(centroid(k))) < 80]
    cards = [k for k in hair if k not in mass_isl]
    mass = [j for k in mass_isl for j in isl[k]]

    # the hair mass: chains wherever it hangs at least 10 cm below the attach line
    chains = []
    for a in [i * 22.5 - 180.0 for i in range(16)]:
        sec = [co[j] for j in mass if abs(dang(ang(co[j]), a)) <= 11.25]
        low = [c for c in sec if c.z < ATTACH]
        if len(low) < 12:
            continue
        tip = pct([c.z for c in low], 0.04)
        if ATTACH - tip < 0.10:
            continue
        n = max(2, min(5, int(math.ceil((ATTACH - tip) / 0.115))))
        pts = centreline([c for c in sec if c.z < ATTACH + 0.03], ATTACH, tip, n)
        names = plan.chain("Hair_M%02d" % len(chains), "mass", "Head", pts)
        chains.append((a, tip, n, names))
    log("hair mass: %d vertices, chains %s" % (len(mass), [(round(a), round(t, 2), n) for a, t, n, _ in chains]))
    for j in mass:
        c = co[j]
        phi = ang(c)
        best = sorted(chains, key=lambda h: abs(dang(phi, h[0])))
        c1, c2 = best[0], best[1]
        d1, d2 = abs(dang(phi, c1[0])), abs(dang(phi, c2[0]))
        gap = abs(dang(c2[0], c1[0]))
        cw = [(c1, 1.0)] if gap > 50 or d1 + d2 > gap + 1e-3 else [(c1, d2 / (d1 + d2)), (c2, d1 / (d1 + d2))]
        head_w = smooth(c.z, ATTACH - 0.05, ATTACH + 0.01)
        new = {}
        if head_w > 0:
            tot = sum(W[j].values()) or 1.0
            for nm, w in W[j].items():
                new[nm] = new.get(nm, 0) + head_w * w / tot
        cw = [(h, w * smooth(c.z, h[1] - 0.08, h[1] - 0.01)) for h, w in cw]   # a shorter chain ends above here
        tot = sum(w for _, w in cw)
        if tot < 1e-4:
            cw, tot = [(c1, 1.0)], 1.0
        for (a, tip, n, names), w in cw:
            for i, bw in bone_weights(c.z, ATTACH, tip, n):
                new[names[i]] = new.get(names[i], 0) + (1 - head_w) * w / tot * bw
        plan.weights[j] = new

    # strand cards in front: a chain per long card, one per direction for the short ones
    def card_chain(name, kind, verts, root):
        zmin = pct([co[j].z for j in verts], 0.03)
        if root - zmin < 0.03:
            return False
        if kind == "lock":   # the first bone is the part lying on the face, the rest hangs below the jaw
            n = max(1, min(3, int(round((JAW - zmin) / 0.10))))
            zs = [root] + [JAW - (JAW - zmin) * i / n for i in range(n + 1)]
            face = 1
        else:
            n = 1 if root - zmin < 0.07 else 2
            zs = [root - (root - zmin) * i / n for i in range(n + 1)]
            face = n
        line = centreline([co[j] for j in verts], root, zmin, 8)

        def at(z):
            t = max(0.0, min(7.999, (root - z) / (root - zmin) * 8))
            p = line[int(t)].lerp(line[int(t) + 1], t - int(t))
            return Vector((p.x, p.y, z))
        names = plan.chain(name, kind, "Head", [at(z) for z in zs], face)
        nb = len(names)
        for j in verts:
            z = co[j].z
            head_w = smooth(z, root - 0.015, root + 0.02)
            new = {"Head": head_w}
            k = 0
            while k < nb - 1 and z < zs[k + 1]:
                k += 1
            t = k + max(0.0, min(1.0, (zs[k] - z) / (zs[k] - zs[k + 1]))) - 0.5   # position along the chain, in bones
            i0 = int(math.floor(t))
            for i, bw in ((i0, 1 - (t - i0)), (i0 + 1, t - i0)):
                i = max(0, min(nb - 1, i))
                new[names[i]] = new.get(names[i], 0) + (1 - head_w) * bw
            plan.weights[j] = new
        return True

    long_cards = sorted([k for k in cards if min(co[j].z for j in isl[k]) < JAW - 0.08], key=lambda k: centroid(k).x)
    for i, k in enumerate(long_cards):
        top = max(co[j].z for j in isl[k])
        card_chain("Hair_L%02d" % i, "lock", isl[k], min(CARD_ROOT, top - 0.03))
    groups = {}
    for k in cards:
        if k in long_cards:
            continue
        sector = int(round(dang(ang(centroid(k)), 0) / 30.0)) % 12
        groups.setdefault(sector, []).extend(isl[k])
    made = 0
    for sector in sorted(groups):
        if card_chain("Hair_F%02d" % made, "fringe", groups[sector], FRINGE_ROOT):
            made += 1
    log("hair cards: %d long, %d short in %d groups (%d with a chain)"
        % (len(long_cards), len(cards) - len(long_cards), len(groups), made))
    return [j for k in cards for j in isl[k]]


def plan_sleeve(plan, side, tag, co, W, body, head0, log):
    UA, LA = side + "_UpperArm", side + "_LowerArm"
    A, B, C = head0[UA], head0[LA], head0[side + "_Hand"]
    segs = [(A, B), (B, C)]
    len_u = (B - A).length
    cloth = {}
    sx = 1.0 if side == "Left" else -1.0
    legs = ("Hips", "Left_UpperLeg", "Right_UpperLeg", "Left_LowerLeg", "Right_LowerLeg")
    fixed = 0
    for j in body:
        arm_w = W[j].get(UA, 0) + W[j].get(LA, 0)
        # Tripo ties the rags at the bottom of the sleeve to the thigh (or the waist) they hang next to
        out = co[j].x * sx
        stray = ()
        if co[j].z > 0.57 and (out > 0.125 or (arm_w >= 0.25 and out > 0.08)):
            stray = legs + (("Spine", "Chest") if out > 0.125 and co[j].z < 0.80 else ())
        leg_w = sum(W[j].get(b, 0) for b in stray)
        if leg_w > 0:
            for b in stray:
                W[j].pop(b, None)
            W[j][LA] = W[j].get(LA, 0) + leg_w
            arm_w += leg_w
            fixed += 1
        if arm_w < 0.02:
            continue
        best = None
        for si, (a, b) in enumerate(segs):
            q = seg_closest(co[j], a, b)
            d = (co[j] - q).length
            if best is None or d < best[0]:
                best = (d, si, q)
        d, si, q = best
        cloth[j] = (si, (q - A).length if si == 0 else len_u + (q - B).length, co[j] - q)
    d0 = []
    for si, (a, b) in enumerate(segs):   # where the slack of the sleeve hangs, seen from the arm
        axis = (b - a).normalized()
        acc = Vector()
        for sj, s, off in cloth.values():
            if sj == si and off.length > 0.05:
                acc += off * off.length
        acc -= axis * acc.dot(axis)
        d0.append(acc.normalized() if acc.length > 1e-6 else None)
    for si in (0, 1):
        if d0[si] is None:
            axis = (segs[si][1] - segs[si][0]).normalized()
            d = d0[1 - si] - axis * d0[1 - si].dot(axis)
            d0[si] = d.normalized()
    rolls = []
    for si, (key, parent, gain) in enumerate((("U", UA, 0.5), ("L", LA, 1.0))):
        name = "Sleeve_%s_%s" % (tag, key)
        a, b = segs[si]
        plan.bones.append((name, a, b, parent, False))
        plan.rolls.append({"bone": name, "parent": parent, "axis": (b - a).normalized(), "d0": d0[si], "gain": gain})
        rolls.append(name)
    depth = {j: off.dot(d0[si]) for j, (si, s, off) in cloth.items()}
    deep = [j for j in cloth if depth[j] > 0.06]
    bags = []
    if len(deep) >= 10:
        hmax = pct([depth[j] for j in deep], 0.97)
        hmid = (0.035 + hmax) / 2
        s_a, s_b = pct([cloth[j][1] for j in deep], 0.2), pct([cloth[j][1] for j in deep], 0.8)
        for key, s in (("A", s_a), ("B", s_b)):
            si = 0 if s < len_u else 1
            a, b = segs[si]
            R = a + (b - a).normalized() * (s if si == 0 else s - len_u)
            names = plan.chain("SleeveBag_%s_%s" % (tag, key), "bag", rolls[si], [R, R + d0[si] * hmid, R + d0[si] * hmax])
            bags.append((s, names))
        log("sleeve %s: %d cloth vertices (%d taken off the legs / waist), %d in the bag, depth %.3f, chains at %.3f / %.3f"
            % (tag, len(cloth), fixed, len(deep), hmax, s_a, s_b))
    for j, (si, s, off) in cloth.items():
        w = dict(W[j])
        wu, wl = w.pop(UA, 0.0), w.pop(LA, 0.0)
        f = smooth(depth[j], 0.035, 0.085) if bags else 0.0
        w[rolls[0]], w[rolls[1]] = wu * (1 - f), wl * (1 - f)
        if f > 0:
            (sa, na), (sb, nb) = bags
            ca = max(0.0, min(1.0, (sb - s) / max(sb - sa, 1e-4)))
            w2 = smooth(depth[j], hmid - 0.02, hmid + 0.03)
            for names, cw in ((na, ca), (nb, 1 - ca)):
                w[names[0]] = w.get(names[0], 0) + f * (wu + wl) * cw * (1 - w2)
                w[names[1]] = w.get(names[1], 0) + f * (wu + wl) * cw * w2
        plan.weights[j] = w


def plan_hem(plan, co, W, body, log):
    legs = ("Left_UpperLeg", "Left_LowerLeg", "Right_UpperLeg", "Right_LowerLeg", "Hips")
    skirt = [j for j in body if co[j].z < HEM_TOP + 0.03 and sum(W[j].get(b, 0) for b in legs) > 0.5]
    ring = [co[j] for j in skirt if HEM_FULL - 0.01 < co[j].z < HEM_TOP + 0.02]
    cx = sum(c.x for c in ring) / len(ring)
    cy = sum(c.y for c in ring) / len(ring)

    def ang(c):
        return math.degrees(math.atan2(c.x - cx, c.y - cy))

    chains = []
    for k in range(8):
        a = dang(k * 45.0, 0)
        sec = [j for j in skirt if abs(dang(ang(co[j]), a)) <= 22.5]
        if len(sec) < 6:
            continue
        zlo = pct([co[j].z for j in sec], 0.05)
        if HEM_ROOT - zlo < 0.06:
            continue
        n = 2 if HEM_ROOT - zlo > 0.10 else 1
        left = sum(W[j].get("Left_UpperLeg", 0) + W[j].get("Left_LowerLeg", 0) for j in sec)
        right = sum(W[j].get("Right_UpperLeg", 0) + W[j].get("Right_LowerLeg", 0) for j in sec)
        parent = "Left_LowerLeg" if left >= right else "Right_LowerLeg"
        names = plan.chain("Hem_%02d" % len(chains), "hem", parent, centreline([co[j] for j in sec], HEM_ROOT, zlo, n))
        chains.append((a, zlo, n, names))
    log("hem: %d skirt vertices below %.2f m, chains %s"
        % (len(skirt), HEM_TOP, [(round(a), round(z, 2), n, plan.chains[-len(chains) + i]["parent"][:1])
                                 for i, (a, z, n, _) in enumerate(chains)]))
    if len(chains) < 2:
        return
    for j in skirt:
        c = co[j]
        r = smooth(c.z, HEM_TOP, HEM_FULL)
        if r <= 0:
            continue
        phi = ang(c)
        best = sorted(chains, key=lambda h: abs(dang(phi, h[0])))
        c1, c2 = best[0], best[1]
        d1, d2 = abs(dang(phi, c1[0])), abs(dang(phi, c2[0]))
        gap = abs(dang(c2[0], c1[0]))
        cw = [(c1, 1.0)] if gap > 100 or d1 + d2 > gap + 1e-3 else [(c1, d2 / (d1 + d2)), (c2, d1 / (d1 + d2))]
        tot = sum(W[j].values()) or 1.0
        new = {nm: (1 - r) * w / tot for nm, w in W[j].items()}
        for (a, zlo, n, names), w in cw:
            for i, bw in bone_weights(c.z, HEM_ROOT, zlo, n):
                new[names[i]] = new.get(names[i], 0) + r * w * bw
        plan.weights[j] = new


def trim_cards(mesh, verts, log):
    """The strand cards carry painted skin between and below their strands: invisible while a card lies on the
    face, a pale flap once it hangs free. Subdivide the cards once and drop the faces that are mostly skin."""
    import numpy as np
    me = mesh.data
    img = None
    for m in me.materials[:1]:   # the Tripo material; the mouth has its own (ghost_face.py)
        if m and m.node_tree:
            for nd in m.node_tree.nodes:
                if nd.type == "TEX_IMAGE" and nd.image and any(l.to_socket.name == "Base Color"
                                                               for o in nd.outputs for l in o.links):
                    img = nd.image
    if img is None or not img.size[0]:
        log("trim cards: no base colour image, skipped")
        return
    w, h = img.size
    ch = img.channels
    px = np.empty(w * h * ch, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, ch)

    def skin(uv):
        r, g, b = px[min(h - 1, max(0, int(uv.y % 1.0 * h))), min(w - 1, max(0, int(uv.x % 1.0 * w))), :3]
        lum = 0.3 * r + 0.59 * g + 0.11 * b
        return lum > 0.38 or (lum > 0.2 and r - g > 0.12)   # pale skin, or the blush / blood on it

    bm = bmesh.new()
    bm.from_mesh(me)
    vs = set(verts)
    tag = bm.faces.layers.int.new("card")
    faces = [f for f in bm.faces if all(v.index in vs for v in f.verts)]
    for f in faces:
        f[tag] = 1
    bmesh.ops.subdivide_edges(bm, edges=list({e for f in faces for e in f.edges}), cuts=1, use_grid_fill=True)
    uvl = bm.loops.layers.uv.active
    kill, total = [], 0
    for f in bm.faces:
        if not f[tag]:
            continue
        total += 1
        uvs = [l[uvl].uv.copy() for l in f.loops]
        c = sum(uvs, Vector((0, 0))) / len(uvs)
        samples = [c] + [u.lerp(c, 0.25) for u in uvs] + [u.lerp(c, 0.6) for u in uvs]
        if sum(skin(u) for u in samples) >= 0.5 * len(samples):
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.faces.layers.int.remove(tag)
    bm.to_mesh(me)
    bm.free()
    me.update()
    log("trim cards: %d of %d card faces were painted skin, removed" % (len(kill), total))


def build(arm, mesh, log=print):
    me = mesh.data
    co = [v.co.copy() for v in me.vertices]
    W = read_weights(mesh)
    head0 = {b.name: b.head_local.copy() for b in arm.data.bones}
    isl = islands_of(me)
    body = max(range(len(isl)), key=lambda k: len(isl[k]))
    plan = Plan()
    card_verts = plan_hair(plan, co, W, isl, body, log)
    for side, tag in (("Left", "L"), ("Right", "R")):
        plan_sleeve(plan, side, tag, co, W, isl[body], head0, log)
    plan_hem(plan, co, W, isl[body], log)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    for name, head, tail, parent, connect in plan.bones:
        b = eb.new(name)
        b.head, b.tail = head, tail
        b.parent = eb[parent]
        b.use_connect = connect
    bpy.ops.object.mode_set(mode="OBJECT")
    write_weights(mesh, plan.weights)
    trim_cards(mesh, card_verts, log)
    log("soft rig: %d bones in %d chains + %d sleeve bones, %d vertices re-weighted"
        % (sum(len(c["bones"]) for c in plan.chains), len(plan.chains), len(plan.rolls), len(plan.weights)))
    return {"chains": plan.chains, "rolls": plan.rolls}


# ---------------------------------------------------------------------------- settle


def prepare(rig):
    """Rest-pose clearances of every chain particle (and segment midpoint) to the body capsules."""
    ident = {b: Quaternion() for b in rig.head0}
    rest_caps, _ = capsules(ident, rig.head0)
    soft = rig.soft
    for ch in soft["chains"]:
        x0 = ch["x0"]
        ch["L"] = [0.0] + [(x0[i] - x0[i - 1]).length for i in range(1, len(x0))]
        ch["clr"] = [[(p - seg_closest(p, a, b)).length for a, b, r in rest_caps] for p in x0]
        mids = [x0[0]] + [(x0[i] + x0[i - 1]) * 0.5 for i in range(1, len(x0))]
        ch["clr_mid"] = [[(p - seg_closest(p, a, b)).length for a, b, r in rest_caps] for p in mids]
    mass = [ch for ch in soft["chains"] if ch["kind"] == "mass"]
    links = []
    for k, a in enumerate(mass):   # the chains are in the order of their angle around the head
        b = mass[(k + 1) % len(mass)]
        if b is a:
            continue
        for ca, cb in ((a, b), (b, a)):
            for i in range(1, len(ca["x0"])):
                j = min(range(1, len(cb["x0"])), key=lambda j: (cb["x0"][j] - ca["x0"][i]).length)
                d = (cb["x0"][j] - ca["x0"][i]).length
                if d < 0.12 and (cb["name"], j, ca["name"], i, d) not in links:
                    links.append((ca["name"], i, cb["name"], j, d))
    soft["links"] = links
    names = {b for ch in soft["chains"] for b in ch["bones"]} | {r["bone"] for r in soft["rolls"]}
    soft["body"] = [b for b in rig.order if b not in names]
    soft["ready"] = True


def roll_of(r, Gpar, g, cs):
    """Turn of a sleeve about its arm that puts the slack of the sleeve on the low side."""
    if cs.get("mode", "gravity") == "follow":
        return Quaternion()
    a = (Gpar @ r["axis"]).normalized()
    d = Gpar @ r["d0"]
    gp = g - a * g.dot(a)
    m = gp.length
    if m < 1e-4:
        return Quaternion()
    t = gp / m
    th = math.atan2(a.dot(d.cross(t)), d.dot(t)) * smooth(m, 0.15, 0.5) * r["gain"] * cs.get("roll", 1.0)
    return Quaternion(a, th)


def frames_of(rig, G, P, cs, g_cloth):
    """The body on its way from the rest pose to this pose, hips already in place: BLEND + 1 frames of
    (G, P, capsules, head centre). Hair and cloth start where Tripo sculpted them, outside the body, and are
    carried along, so they end up on the side of the body they would really slide to."""
    soft = rig.soft
    order = soft["body"]
    local = {b: G[rig.parent[b]].inverted() @ G[b] for b in order if rig.parent[b] is not None}
    frames = []
    for k in range(BLEND + 1):
        t = smooth(k / BLEND, 0.0, 1.0)
        Gt, Pt = {}, {}
        for b in order:
            p = rig.parent[b]
            if p is None:
                Gt[b], Pt[b] = G[b], P[b]
            else:
                Gt[b] = Gt[p] @ Quaternion().slerp(local[b], t)
                Pt[b] = Pt[p] + Gt[p] @ (rig.head0[b] - rig.head0[p])
        for r in soft["rolls"]:
            par = r["parent"]
            Gt[r["bone"]] = roll_of(r, Gt[par], g_cloth, cs) @ Gt[par]
            Pt[r["bone"]] = Pt[par] + Gt[par] @ (rig.head0[r["bone"]] - rig.head0[par])
        caps, hc = capsules(Gt, Pt)
        frames.append((Gt, Pt, caps, hc))
    return frames


def settle(rig, spec, ctx, G, P):
    soft = rig.soft
    if not soft.get("ready"):
        prepare(rig)
    hs, cs = spec.get("hair") or {}, spec.get("cloth") or {}
    zone, bvh = ctx.get("zone"), ctx.get("bvh")
    inverted = smooth(-(G["Head"] @ Vector((0, 0, 1))).z, 0.0, 0.5)
    g_hair = vec(hs.get("g", (0, 0, -1))).normalized()
    g_cloth = vec(cs.get("g", (0, 0, -1))).normalized()
    frames = frames_of(rig, G, P, cs, g_cloth)
    Gf, Pf = frames[-1][0], frames[-1][1]
    stats = {"relaxed": 0, "contacts": 0}
    for r in soft["rolls"]:
        G[r["bone"]], P[r["bone"]] = Gf[r["bone"]], Pf[r["bone"]]

    def strand(ch):
        """A chain to relax: {"ch", "frame", "pin", margins}, or its finished points when it does not hang freely."""
        kind, par = ch["kind"], ch["parent"]
        is_hair = kind in ("mass", "lock", "fringe")
        opt = hs if is_hair else cs
        mode = opt.get("mode", "gravity")
        mb, mw, mf, stiff = KIND[kind]
        stiff = opt.get("stiff_" + kind, stiff)
        mf = opt.get("floor", mf)
        face = ch["face"]   # leading bones lying on the wet face: stuck to it unless the head hangs upside down
        stuck = 1.0 - inverted if face else 0.0
        x0, L, n = ch["x0"], ch["L"], len(ch["bones"])
        rel = [x0[i] - rig.head0[par] for i in range(n + 1)]

        def follow(k):
            Gt, Pt = frames[k][0], frames[k][1]
            return [Pt[par] + Gt[par] @ rel[i] for i in range(n + 1)]
        over = hs.get("chains", {}).get(ch["name"]) if is_hair else None
        if over:
            x = [follow(BLEND)[0]]
            for i in range(n):
                x.append(x[-1] + vec(over[min(i, len(over) - 1)]).normalized() * L[i + 1])
            return x
        if mode == "follow" or stiff >= 0.98 or (face >= n and stuck >= 0.98):
            return follow(BLEND)
        g = g_hair if is_hair else g_cloth
        push = opt.get("push", 0.9 if mode == "spread" else 0.05)
        ks = [0.0] + [max(stiff, stuck) if i <= face else stiff for i in range(1, n + 1)]

        def frame(k):
            f = follow(k)
            centre = frames[k][3] if is_hair else frames[k][1]["Chest"]
            out = f[0] - centre
            out -= g * out.dot(g)
            bias = out.normalized() * push if out.length > 1e-4 else Vector()
            field = [None] + [g * (1 - ks[i]) + (f[i] - f[i - 1]).normalized() * ks[i] + bias for i in range(1, n + 1)]
            return f, field
        return {"ch": ch, "frame": frame, "pin": [True] + [k >= 0.98 for k in ks[1:]], "mb": mb, "mw": mw, "mf": mf}

    jobs = [(ch, strand(ch)) for ch in soft["chains"]]
    # the hair mass is one sheet: its chains are relaxed together and tied to their neighbours (soft["links"])
    groups = [[s for ch, s in jobs if isinstance(s, dict) and ch["kind"] == "mass"]]
    groups += [[s] for ch, s in jobs if isinstance(s, dict) and ch["kind"] != "mass"]
    caps = [f[2] for f in frames]
    for group in groups:
        if not group:
            continue
        index = {s["ch"]["name"]: k for k, s in enumerate(group)}
        links = [(index[a], i, index[b], j, d) for a, i, b, j, d in soft.get("links", []) if a in index and b in index]
        relax(group, links, caps, zone, bvh)
        for s in group:
            stats["relaxed"] += 1
            stats["contacts"] += sum(s["hits"])
    for ch, s in jobs:
        x = s["x"] if isinstance(s, dict) else s
        names, par, x0 = ch["bones"], ch["parent"], ch["x0"]
        for i, b in enumerate(names):
            p = par if i == 0 else names[i - 1]
            P[b] = x[i]
            rest_dir = (x0[i + 1] - x0[i]).normalized()
            G[b] = (G[p] @ rest_dir).rotation_difference(x[i + 1] - x[i]) @ G[p]
    ctx["soft_stats"] = stats


def relax(group, links, caps_of, zone, bvh):
    """Damped Verlet for a group of chains: the body moves from the rest pose into the pose over BLEND steps
    (strand["frame"](k) gives the rigid positions and the per-particle pull of step k), then the chains settle.
    Fixed lengths from each root; colliders are the body capsules, the container boards and its safe zone; `links`
    (strand a, particle i, strand b, particle j, rest distance) keep neighbouring chains from drifting apart.
    Leaves the points in strand["x"] and the contacts in strand["hits"]."""
    caps = caps_of[0]

    def body(p, c, mb):
        hit = False
        for j, (a, b, r) in enumerate(caps):
            lim = min(r + mb, c[j])
            if lim <= 1e-3:
                continue
            q = seg_closest(p, a, b)
            off = p - q
            d = off.length
            if d < lim:
                p = q + (off / d if d > 1e-6 else Vector((0, 0, 1))) * lim
                hit = True
        return p, hit

    def world(p, head, mw, mf):
        hit = False
        if bvh is not None:
            m = mw * 0.5
            loc, nrm, idx, dist = bvh.find_nearest(p, 0.12)
            if loc is not None:
                d = p - loc
                dn = d.dot(nrm)
                if dist < m:                                   # within the margin of a board, an edge or a corner
                    p = loc + (d / dist if dn > 0 and dist > 1e-6 else nrm) * m
                    hit = True
                elif dn < 0 and (d - nrm * dn).length < 0.005:   # straight behind a face: inside the board
                    p = loc + nrm * m
                    hit = True
        if zone is not None:
            lo, hi = zone
            inside = all(lo[k] < head[k] < hi[k] for k in range(3))   # only while this bone is in the container
            if inside:
                for k in (0, 1):
                    v = min(max(p[k], lo[k] + mw), hi[k] - mw)
                    if v != p[k]:
                        p[k] = v
                        hit = True
                if p.z > hi.z - mw * 0.5:
                    p.z = hi.z - mw * 0.5
                    hit = True
            if p.z < lo.z + mf and (inside or (lo.x < p.x < hi.x and lo.y < p.y < hi.y)):   # over the box's floor
                p.z = lo.z + mf
                hit = True
        return p, hit

    def constrain(s):
        x, L, pin, ch = s["x"], s["ch"]["L"], s["pin"], s["ch"]
        for i in range(1, len(x)):
            if pin[i]:
                continue
            p, h1 = body(x[i], ch["clr"][i], s["mb"])
            p, h2 = world(p, x[i - 1], s["mw"], s["mf"])
            mid = (p + x[i - 1]) * 0.5
            m2, h3 = body(mid, ch["clr_mid"][i], s["mb"])
            if h3:
                p = p + (m2 - mid) * 2.0
            d = p - x[i - 1]
            x[i] = x[i - 1] + (d.normalized() if d.length > 1e-9 else s["field"][i].normalized()) * L[i]
            s["hits"][i] = h1 or h2 or h3

    for s in group:
        s["start"], s["field"] = s["frame"](0)
        s["x"] = [p.copy() for p in s["start"]]
        s["prev"] = [p.copy() for p in s["start"]]
        s["hits"] = [False] * len(s["start"])
    for it in range(BLEND + ITERS):
        if 0 < it <= BLEND:
            caps = caps_of[it]
        moved = 0.0
        for s in group:
            if 0 < it <= BLEND:
                s["start"], s["field"] = s["frame"](it)
            x, prev, pin, start, field = s["x"], s["prev"], s["pin"], s["start"], s["field"]
            # the root too stays out of the body: with the head thrown back the nape sinks into the neck, and hair
            # starting from inside the body would come out on whichever side is nearer
            x[0], s["hits"][0] = (start[0], False) if pin[1] else body(start[0], s["ch"]["clr"][0], s["mb"])
            for i in range(1, len(x)):
                if pin[i]:
                    x[i] = start[i]
                    continue
                v = (x[i] - prev[i]) * DAMP
                prev[i] = x[i].copy()
                x[i] = x[i] + v + field[i] * STEP
            constrain(s)
        for a, i, b, j, d0 in links:
            sa, sb = group[a], group[b]
            d = sb["x"][j] - sa["x"][i]
            over = d.length - (d0 * 1.25 + 0.01)
            if over > 0:
                c = d.normalized() * over
                pa, pb = sa["pin"][i], sb["pin"][j]
                if not pa:
                    sa["x"][i] += c * (1.0 if pb else 0.5)
                if not pb:
                    sb["x"][j] -= c * (1.0 if pa else 0.5)
        for s in group:
            if links:
                constrain(s)
            constrain(s)
            moved = max([moved] + [(s["x"][i] - s["prev"][i]).length for i in range(1, len(s["x"])) if not s["pin"][i]])
        if it > BLEND + 40 and moved < 4e-5:
            break
