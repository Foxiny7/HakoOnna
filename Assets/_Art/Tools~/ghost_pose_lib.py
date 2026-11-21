"""HakoOnna ghost poses in Blender: 8 hiding poses (art bible 6.2 / 6.5) and 8 jump-scare lunges (4.3).

blender -b --factory-startup --python ghost_pose_lib.py -- <out_dir> <kinds|all> [--blend f.blend] [--fbx f.fbx]
        [--eevee] [--no-lunge]
<out_dir> gets report.json and, per container, <Kind>_player/front/side.png (hiding pose) and
<Kind>_lunge_player/climax/side.png; ghost_pose_sheets.py turns them into two contact sheets. Use absolute
paths: Blender does not write renders to relative ones.
Actions: Pose_<Kind> (hiding pose = first frame of the lunge) and Lunge_<Kind> (end of the lunge), keyed on
frames 1-2 of one armature; the FBX carries both as separate takes.

Poses are written as data in ghost_pose_specs.py, in the frame of each reference box (Blender: X right, Y into the
container, Z up; the Tansu pose is in the Drawer_0 frame). Each bone gets a world-space orientation:
  root          {"pos", "up", "front"}        Hips head position, body up / belly direction
  torso bones   {"up", "front"}               Spine, Chest, UpperChest, Neck
  Head          {"face": vec|"camera", "crown"}
  arm_L/arm_R/leg_L/leg_R  {"to", "hint", "reverse"}   two-bone IK to the wrist / ankle; "reverse" bends the elbow
                or knee the wrong way
  Left_Hand ... {"dir", "palm"}               finger direction, direction out of the palm
  Left_Foot ... {"dir", "sole"}               toe direction, direction out of the sole
  hand_L/hand_R {"curl": (prox, mid, tip), "spread", "thumb": (a, b, c)}   finger curls in degrees
  Left_Shoulder ... {"rot": [(axis, deg)]}    extra world-axis rotation on top of the parent
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

ART = os.path.dirname(HERE)   # Assets/_Art
GHOST_FBX = os.path.join(ART, "Models", "External", "Tripo", "GhostGirl",
                         "tripo_convert_48543c68-2071-4064-a813-fbffebcd98c8.fbx")
REF_DIR = os.path.join(ART, "Source~", "PoseReference")
HEIGHT = 1.2
KINDS = ["Oshiire", "YofukuTansu", "ChaTansu", "GetaBako", "Tansu", "Nagamochi", "Tsuzura", "Komebitsu"]
UP, FRONT, SOLE = Vector((0, 0, 1)), Vector((0, -1, 0)), Vector((0, 0, -1))
CHAINS = {"arm_L": ("Left_UpperArm", "Left_LowerArm", "Left_Hand", "arm"),
          "arm_R": ("Right_UpperArm", "Right_LowerArm", "Right_Hand", "arm"),
          "leg_L": ("Left_UpperLeg", "Left_LowerLeg", "Left_Foot", "leg"),
          "leg_R": ("Right_UpperLeg", "Right_LowerLeg", "Right_Foot", "leg")}
FINGERS = ("Index", "Middle", "Ring", "Pinky")
JOINTS = ("Proximal", "Intermediate", "Distal")


def vec(v):
    return Vector(v) if not isinstance(v, Vector) else v.copy()


def align(a1, b1, a2, b2):
    """Rotation taking a1 exactly onto b1 and a2 as close as possible onto b2."""
    a1, b1 = vec(a1).normalized(), vec(b1).normalized()
    a2, b2 = vec(a2), vec(b2)
    a2 = a2 - a1 * a2.dot(a1)
    b2 = b2 - b1 * b2.dot(b1)
    if a2.length < 1e-6 or b2.length < 1e-6:
        return a1.rotation_difference(b1)
    a2.normalize()
    b2.normalize()
    A = Matrix((a1, a2, a1.cross(a2))).transposed()
    B = Matrix((b1, b2, b1.cross(b2))).transposed()
    return (B @ A.transposed()).to_quaternion()


def twist_about(q, axis):
    axis = axis.normalized()
    r = Vector((q.x, q.y, q.z))
    p = axis * r.dot(axis)
    t = Quaternion((q.w, p.x, p.y, p.z))
    if t.magnitude < 1e-9:
        return 0.0
    t.normalize()
    return 2.0 * math.atan2(Vector((t.x, t.y, t.z)).dot(axis), t.w)


# ---------------------------------------------------------------------------- rig


class Rig:
    def __init__(self, arm, mesh):
        self.arm, self.mesh = arm, mesh
        bones = arm.data.bones
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in bones}
        self.head0 = {b.name: b.head_local.copy() for b in bones}
        self.tail0 = {b.name: b.tail_local.copy() for b in bones}
        self.R0 = {b.name: b.matrix_local.to_quaternion() for b in bones}
        self.order = []

        def walk(b):
            self.order.append(b.name)
            for c in b.children:
                walk(c)
        for b in bones:
            if b.parent is None:
                walk(b)
        h = self.head0
        self.hand_dir0, self.palm0, self.curl0, self.thumb0 = {}, {}, {}, {}
        for side, s in (("Left", 1), ("Right", -1)):
            hd = (h[side + "_MiddleProximal"] - h[side + "_Hand"]).normalized()
            knuckle = (h[side + "_PinkyProximal"] - h[side + "_IndexProximal"]) * s
            knuckle.normalize()
            palm = knuckle.cross(hd).normalized()          # points out of the palm (toward the body at rest)
            self.hand_dir0[side], self.palm0[side], self.curl0[side] = hd, palm, knuckle
            td = (h[side + "_ThumbIntermediate"] - h[side + "_ThumbProximal"]).normalized()
            self.thumb0[side] = td.cross(palm).normalized()
        self.foot_dir0 = {side: (h[side + "_Toes"] - h[side + "_Foot"]).normalized() for side in ("Left", "Right")}

    def length(self, a, b):
        return (self.head0[b] - self.head0[a]).length


def two_bone(S, T, l1, l2, hint):
    d_vec = T - S
    d = d_vec.length
    u = d_vec.normalized() if d > 1e-6 else Vector((0, 0, -1))
    d = min(max(d, abs(l1 - l2) + 1e-4), (l1 + l2) * 0.9995)
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    r = math.sqrt(max(l1 * l1 - a * a, 0.0))
    h = vec(hint) - u * vec(hint).dot(u)
    if h.length < 1e-6:
        h = u.orthogonal()
    h.normalize()
    return S + u * a + h * r, S + u * d


def solve(rig, spec, ctx):
    G, P, ik = {}, {}, {}
    chain_of = {v[0]: k for k, v in CHAINS.items()}
    for b in rig.order:
        p = rig.parent[b]
        if p is None:
            r = spec["root"]
            P[b] = vec(r["pos"])
            G[b] = align(UP, r["up"], FRONT, r["front"])
            continue
        P[b] = P[p] + G[p] @ (rig.head0[b] - rig.head0[p])
        G[b] = G[p].copy()
        if b in chain_of and chain_of[b] in spec:
            cname = chain_of[b]
            upper, lower, end, kind = CHAINS[cname]
            c = spec[cname]
            l1, l2 = rig.length(upper, lower), rig.length(lower, end)
            hint = vec(c["knee"]) - P[b] if "knee" in c else c.get("hint", (0, 0, 1))   # "knee": where the joint should go
            E, T = two_bone(P[b], vec(c["to"]), l1, l2, hint)
            ctx.setdefault("ik_err", {})[cname] = round((vec(c["to"]) - T).length * 100, 1)
            seg1, seg2 = E - P[b], T - E
            perp = seg2 - seg1.normalized() * seg2.dot(seg1.normalized())
            if "front" in c:
                front = vec(c["front"])
            elif perp.length > 1e-4:
                sign = 1.0 if kind == "arm" else -1.0
                if c.get("reverse"):
                    sign = -sign
                front = perp * sign
            else:
                front = G[p] @ FRONT
            G[b] = align((rig.head0[lower] - rig.head0[upper]), seg1, FRONT, front)
            ik[lower] = (T, end)
            continue
        if b in ik:
            T, end = ik[b]
            cur = G[p] @ (rig.head0[end] - rig.head0[b])
            G[b] = cur.rotation_difference(T - P[b]) @ G[p]
            continue
        s = spec.get(b)
        side = "Left" if b.startswith("Left_") else ("Right" if b.startswith("Right_") else None)
        if s is not None:
            if "rot" in s:
                q = Quaternion()
                for axis, deg in s["rot"]:
                    q = Quaternion(vec(axis).normalized(), math.radians(deg)) @ q
                G[b] = q @ G[p]
            elif b == "Head":
                face = s["face"]
                if face == "camera":
                    face = (vec(ctx["eye"]) - (P[b] + vec(s.get("crown", UP)).normalized() * 0.06)).normalized()
                G[b] = align(FRONT, face, UP, s.get("crown", UP))
            elif b.endswith("_Hand"):
                G[b] = align(rig.hand_dir0[side], s["dir"], rig.palm0[side], s["palm"])
            elif b.endswith("_Foot"):
                G[b] = align(rig.foot_dir0[side], s["dir"], SOLE, s["sole"])
            else:
                G[b] = align(UP, s["up"], FRONT, s["front"])
            if "twist" in s:
                axis = G[b] @ (rig.tail0[b] - rig.head0[b]).normalized()
                G[b] = Quaternion(axis, math.radians(s["twist"])) @ G[b]
        # fingers
        for hs, hside in (("hand_L", "Left"), ("hand_R", "Right")):
            if side != hside or hs not in spec:
                continue
            hspec = spec[hs]
            for fi, f in enumerate(FINGERS):
                for ji, j in enumerate(JOINTS):
                    if b != "%s_%s%s" % (side, f, j):
                        continue
                    curl = hspec.get("fingers", {}).get(f, hspec.get("curl", (0, 0, 0)))
                    q = Quaternion(rig.curl0[side], math.radians(curl[ji]))
                    if ji == 0:
                        spread = hspec.get("spread", 0.0) * (1.5 - fi) / 1.5
                        q = Quaternion(rig.palm0[side] * (1 if side == "Left" else -1), math.radians(spread)) @ q
                    G[b] = G[p] @ q
            if b.startswith(side + "_Thumb") and not b.endswith("End"):
                ji = ("Proximal", "Intermediate", "Distal").index(b[len(side + "_Thumb"):])
                t = hspec.get("thumb", (0, 0, 0))
                G[b] = G[p] @ Quaternion(rig.thumb0[side], math.radians(t[ji]))
    # forearm takes half of the wrist twist (no twist bones in this rig)
    for side in ("Left", "Right"):
        lower, hand = side + "_LowerArm", side + "_Hand"
        if hand in spec:
            axis = (P[hand] - P[lower]).normalized()
            tw = twist_about(G[hand] @ G[lower].inverted(), axis)
            G[lower] = Quaternion(axis, 0.5 * tw) @ G[lower]
    if "Neck_Twist_A" in G:
        G["Neck_Twist_A"] = G["Neck"].slerp(G["Head"], 0.5)
    pose_hair(rig, spec, ctx, G, P)
    return G, P


HEAD_CENTRE = Vector((-0.011, -0.012, 0.06))   # scalp centre relative to the Head bone, rest


def seg_closest(p, a, b):
    ab = b - a
    t = 0.0 if ab.length_squared < 1e-12 else max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return a + ab * t


def capsules(G, P):
    caps = [(P["Hips"], P["Neck"], 0.085), (P["Neck"], P["Head"], 0.045),
            (P["Left_UpperLeg"], P["Right_UpperLeg"], 0.08)]
    hc = P["Head"] + G["Head"] @ HEAD_CENTRE
    caps.append((hc, hc, 0.075))
    for s in ("Left", "Right"):
        caps += [(P[s + "_UpperArm"], P[s + "_LowerArm"], 0.04), (P[s + "_LowerArm"], P[s + "_Hand"], 0.035),
                 (P[s + "_UpperLeg"], P[s + "_LowerLeg"], 0.06), (P[s + "_LowerLeg"], P[s + "_Foot"], 0.045)]
    return caps, hc


def pose_hair(rig, spec, ctx, G, P):
    """Hang the hair chains: "gravity" (down, pushed outside the head/body/floor), "spread" (fanned out on the
    floor around the head) or "follow" (rigid with the head). Per-chain directions in hair["chains"] win."""
    hs = spec.get("hair", {"mode": "gravity"})
    mode = hs.get("mode", "gravity")
    if mode == "follow" or "Hair_BC_1" not in rig.head0:
        return
    floor = ctx["zone"][0].z + hs.get("floor", 0.09)   # the hair shell is a few cm thick around its bones
    g = vec(hs.get("g", (0, 0, -1))).normalized()
    caps, hc = capsules(G, P)
    for name, ang, za, zt, n in HAIR:
        over = hs.get("chains", {}).get(name)
        for i in range(n):
            b = "%s_%d" % (name, i + 1)
            p = rig.parent[b]
            P[b] = P[p] + G[p] @ (rig.head0[b] - rig.head0[p])
            seg = rig.tail0[b] - rig.head0[b]
            L, rest_dir = seg.length, seg.normalized()
            if over:
                d = vec(over[min(i, len(over) - 1)]).normalized()
            elif mode == "spread":
                radial = P[b] - hc
                radial.z = 0.0
                if radial.length < 1e-4:
                    radial = G["Head"] @ Vector((math.sin(math.radians(ang)), math.cos(math.radians(ang)), 0))
                    radial.z = 0.0
                radial.normalize()
                d = (radial * (0.5 if i == 0 else 1.0) + Vector((0, 0, -1.0 if i == 0 else -0.08))).normalized()
            else:
                out = P[b] - hc
                out = out - g * out.dot(g)
                d = (g + out.normalized() * hs.get("push", 0.3)).normalized() if out.length > 1e-4 else g
            tail = P[b] + d * L
            zl, zh = ctx["zone"]
            walls = all(zl[k] < P[b][k] < zh[k] for k in range(3))   # only while the hair is in the container
            for _ in range(2):
                tail.z = max(tail.z, floor)
                if walls:
                    for k in (0, 1):
                        tail[k] = min(max(tail[k], zl[k] + 0.04), zh[k] - 0.04)
                for a, bb, r in caps:
                    q = seg_closest(tail, a, bb)
                    off = tail - q
                    if off.length < r + 0.012:
                        tail = q + (off.normalized() if off.length > 1e-6 else Vector((0, 0, 1))) * (r + 0.012)
            tail.z = max(tail.z, floor)
            G[b] = (G[p] @ rest_dir).rotation_difference(tail - P[b]) @ G[p]


def apply(rig, G, P):
    for b in rig.order:
        pb = rig.arm.pose.bones[b]
        pb.rotation_mode = "QUATERNION"
        p = rig.parent[b]
        Gp = G[p] if p else Quaternion()
        R0 = rig.R0[b]
        pb.rotation_quaternion = R0.inverted() @ Gp.inverted() @ G[b] @ R0
        pb.location = (R0.inverted() @ (P[b] - rig.head0[b])) if p is None else Vector()
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()


def rest(rig):
    for pb in rig.arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
    bpy.context.view_layer.update()


# ---------------------------------------------------------------------------- scene


def load_ghost():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=GHOST_FBX, use_anim=False, automatic_bone_orientation=False, ignore_leaf_bones=False)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    mesh = next(o for o in new if o.type == "MESH")
    arm.scale = (HEIGHT, HEIGHT, HEIGHT)
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    arm.name, mesh.name = "Armature", "HakoOnna_Mesh"
    fix_unweighted(mesh)
    add_hair_rig(arm, mesh)
    return Rig(arm, mesh)


# Hair chains: (name, angle around the head: 0 = back, +90 = her left, 180 = front; attach z; tip z; bones).
# Heights are the rest pose at 1.2 m, read from the Tripo hair shell (bl_hair2.py survey).
HAIR = [("Hair_BC", 0, 1.03, 0.49, 3), ("Hair_BL", 30, 1.03, 0.54, 3), ("Hair_BR", -30, 1.03, 0.54, 3),
        ("Hair_SL", 56, 1.03, 0.78, 2), ("Hair_SR", -56, 1.03, 0.78, 2),
        ("Hair_FL", 125, 1.02, 0.82, 2), ("Hair_FR", -135, 1.02, 0.82, 2)]


def hair_island(mesh):
    import bmesh
    me = mesh.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    top = max(me.vertices, key=lambda v: v.co.z).index
    seen, stack = set(), [top]
    while stack:
        j = stack.pop()
        if j in seen:
            continue
        seen.add(j)
        for e in bm.verts[j].link_edges:
            k = e.other_vert(bm.verts[j]).index
            if k not in seen:
                stack.append(k)
    bm.free()
    return sorted(seen)


def add_hair_rig(arm, mesh):
    """Tripo binds the long hair to the hips and arms, so it smears across the body in strong poses.
    Give it 7 chains under Head and re-weight only the hair shell to them (Head keeps the scalp)."""
    me = mesh.data
    hair = hair_island(mesh)
    scalp = [me.vertices[j].co for j in hair if me.vertices[j].co.z > 1.08]
    cx = sum(c.x for c in scalp) / len(scalp)
    cy = sum(c.y for c in scalp) / len(scalp)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    for name, ang, za, zt, n in HAIR:
        a = math.radians(ang)
        r = 0.085
        x, y = cx + r * math.sin(a), cy + r * math.cos(a)
        parent = eb["Head"]
        for i in range(n):
            b = eb.new("%s_%d" % (name, i + 1))
            b.head = (x, y, za - (za - zt) * i / n)
            b.tail = (x, y, za - (za - zt) * (i + 1) / n)
            b.parent = parent
            b.use_connect = i > 0
            parent = b
    bpy.ops.object.mode_set(mode="OBJECT")
    groups = {}
    for name, ang, za, zt, n in HAIR:
        for i in range(n):
            g = mesh.vertex_groups.get("%s_%d" % (name, i + 1)) or mesh.vertex_groups.new(name="%s_%d" % (name, i + 1))
            groups[(name, i)] = g
    order = sorted(HAIR, key=lambda h: h[1])
    names = {g.index: g.name for g in mesh.vertex_groups}
    for j in hair:
        v = me.vertices[j]
        c = v.co
        phi = math.degrees(math.atan2(c.x - cx, c.y - cy))
        # two neighbouring chains by angle
        best = sorted(order, key=lambda h: abs((phi - h[1] + 180) % 360 - 180))
        c1, c2 = best[0], best[1]
        d1 = abs((phi - c1[1] + 180) % 360 - 180)
        d2 = abs((phi - c2[1] + 180) % 360 - 180)
        gap = abs((c2[1] - c1[1] + 180) % 360 - 180)
        if gap > 80 or d1 + d2 > gap + 1e-3:
            chain_w = [(c1, 1.0)]
        else:
            chain_w = [(c1, d2 / (d1 + d2)), (c2, d1 / (d1 + d2))]
        s = max(0.0, min(1.0, (c.z - (c1[2] - 0.05)) / 0.06))
        head_w = s * s * (3 - 2 * s)   # 1 above the attach line, 0 a few cm below it
        new = {}
        if head_w > 0:
            orig = [(names[g.group], g.weight) for g in v.groups]
            tot = sum(w for _, w in orig) or 1.0
            for nm, w in orig:
                new[nm] = new.get(nm, 0) + head_w * w / tot
        for ch, cw in chain_w:
            name, ang, za, zt, n = ch
            t = max(0.0, min(1.0, (za - c.z) / (za - zt))) * n - 0.5
            i0 = int(math.floor(t))
            f = t - i0
            for i, w in ((i0, 1 - f), (i0 + 1, f)):
                i = max(0, min(n - 1, i))
                key = "%s_%d" % (name, i + 1)
                new[key] = new.get(key, 0) + (1 - head_w) * cw * w
        top = sorted(new.items(), key=lambda kv: -kv[1])[:4]
        tot = sum(w for _, w in top) or 1.0
        for g in list(v.groups):
            mesh.vertex_groups[g.group].remove([j])
        for nm, w in top:
            if w > 1e-4:
                mesh.vertex_groups[nm].add([j], w / tot, "REPLACE")
    print("hair rig: %d chains, %d hair vertices re-weighted" % (len(HAIR), len(hair)))


def fix_unweighted(mesh):
    """Tripo leaves the odd vertex with no weights; it would stay at the bind pose (a spike in Blender and Unity).
    Copy the weights of its nearest weighted neighbour along the edges."""
    me = mesh.data
    adj = {}
    for e in me.edges:
        a, b = e.vertices
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)
    weighted = lambda i: sum(g.weight for g in me.vertices[i].groups) > 1e-4
    fixed = 0
    for v in me.vertices:
        if weighted(v.index):
            continue
        seen, ring = {v.index}, [v.index]
        src = None
        while ring and src is None:
            nxt = []
            for i in ring:
                for j in adj.get(i, []):
                    if j not in seen:
                        seen.add(j)
                        nxt.append(j)
                        if src is None and weighted(j):
                            src = j
            ring = nxt
        if src is None:
            continue
        for g in me.vertices[src].groups:
            mesh.vertex_groups[g.group].add([v.index], g.weight, "REPLACE")
        fixed += 1
    print("fixed unweighted vertices:", fixed)


def load_box(kind):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(REF_DIR, "HakoOnna_Pose_%s.glb" % kind))
    objs = [o for o in bpy.data.objects if o not in before]
    root = next(o for o in objs if o.name.startswith("Container_"))
    bpy.context.view_layer.update()
    drawer = next((o for o in objs if o.name.startswith("Drawer_0")), None)
    if drawer is not None:   # the Tansu pose lives in the drawer's frame
        root.location = root.location - drawer.matrix_world.translation
        bpy.context.view_layer.update()

    def find(prefix):
        return next(o for o in objs if o.name.startswith(prefix))

    def bbox(o):
        pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
        return (Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)]))
    zlo, zhi = bbox(find("SafeZone"))
    flo, fhi = bbox(find("FaceTarget"))
    cam = find("PlayerEye")
    cam.data.clip_start = 0.01
    ctx = {"kind": kind, "objs": objs, "zone": (zlo + Vector((.003,) * 3), zhi - Vector((.003,) * 3)),
           "face": (flo + fhi) / 2, "eye": cam.matrix_world.translation.copy(), "cam": cam,
           "opening": bbox(find("Opening"))}
    return ctx


def grab_targets(ctx, F, opt):
    """Arms for the grab (art bible 4.3): the camera gets pulled in until it is 0.2 m from her eyes; both hands
    reach past her face and clutch the explorer's head beside that camera, so they fill both edges of the screen."""
    opt = opt if isinstance(opt, dict) else {}
    cam = ctx["cam"]
    C = cam.matrix_world.translation.copy()
    q = cam.matrix_world.to_quaternion()
    E = F + (C - F).normalized() * 0.2
    rot = (q @ Vector((0, 0, -1))).rotation_difference(F - E) @ q
    right, up = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0))
    f = (F - E).normalized()
    spec = {}
    for name, k, s in (("Left", "L", 1.0), ("Right", "R", -1.0)):   # she faces the camera: her left is its right
        W = E + f * opt.get("ahead", 0.12) + right * (s * opt.get("spread", 0.11)) - up * opt.get("drop", 0.02)
        spec["arm_" + k] = {"to": W, "hint": (right * s + up * opt.get("elbow_up", -0.6) + f * opt.get("elbow_back", 0.2)).normalized()}
        spec[name + "_Hand"] = {"dir": (-f * 0.9 - right * (s * 0.35)).normalized(), "palm": (-right * s).normalized()}
        spec["hand_" + k] = {"curl": (30, 55, 40), "spread": 12, "thumb": (15, 20, 20)}
    return spec


def boards_bvh(ctx):
    """The container's boards (Shell) and its open door / lid / drawer (Open_*) as one BVH, world space."""
    from mathutils.bvhtree import BVHTree
    verts, polys = [], []
    for o in ctx["objs"]:
        if o.type == "MESH" and o.name.startswith(("Shell", "Open_")):
            mw = o.matrix_world
            base = len(verts)
            verts += [mw @ v.co for v in o.data.vertices]
            polys += [[base + i for i in p.vertices] for p in o.data.polygons]
    return BVHTree.FromPolygons(verts, polys)


def lunge_check(rig, ctx, F):
    """Lunge end pose: face-to-camera distance, hips still inside the container, vertices inside a board."""
    bvh = ctx.get("bvh") or boards_bvh(ctx)
    ctx["bvh"] = bvh
    dg = bpy.context.evaluated_depsgraph_get()
    ev = rig.mesh.evaluated_get(dg)
    me = ev.to_mesh()
    mw = rig.mesh.matrix_world
    names = {g.index: g.name for g in rig.mesh.vertex_groups}
    src = rig.mesh.data.vertices
    inside, worst, groups = 0, 0.0, {}
    for v in me.vertices:
        w = mw @ v.co
        loc, n, i, d = bvh.find_nearest(w, 0.2)
        if loc is None:
            continue
        depth = -(w - loc).dot(n)
        if depth > 0.003:
            inside += 1
            worst = max(worst, depth)
            vg = src[v.index].groups
            if len(vg):
                g = max(vg, key=lambda g: g.weight)
                groups[names[g.group]] = groups.get(names[g.group], 0) + 1
    ev.to_mesh_clear()
    e = eyes(rig)
    lo, hi = ctx["zone"]
    hips = rig.arm.matrix_world @ rig.arm.pose.bones["Hips"].head
    hips_in = all(lo[i] - 0.02 <= hips[i] <= hi[i] + 0.02 for i in range(3))
    return {"eye_cam_m": round((e - ctx["eye"]).length, 3), "face_err_cm": round((e - F).length * 100, 1),
            "hips_in_box": hips_in, "in_boards": inside, "in_boards_worst_cm": round(worst * 100, 1),
            "in_boards_by_bone": sorted(groups.items(), key=lambda kv: -kv[1])[:5],
            "ik_err_cm": dict(ctx.get("ik_err", {})), "eyes": [round(x, 3) for x in e]}


def remove(objs):
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)


def check(rig, ctx):
    """Vertices outside the safe zone (floor contact allowed), the worst overshoot and the bones that own them."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = rig.mesh.evaluated_get(dg)
    me = ev.to_mesh()
    mw = rig.mesh.matrix_world
    lo, hi = ctx["zone"]
    out, worst = 0, [0.0] * 6
    groups = {}
    names = {g.index: g.name for g in rig.mesh.vertex_groups}
    src = rig.mesh.data.vertices
    pts = []
    for v in me.vertices:
        w = mw @ v.co
        pts.append(w)
        o = [lo.x - w.x, w.x - hi.x, lo.y - w.y, w.y - hi.y, lo.z - 0.002 - w.z, w.z - hi.z]
        m = max(o)
        if m > 0:
            out += 1
            for i in range(6):
                worst[i] = max(worst[i], o[i])
            vg = src[v.index].groups
            if len(vg):
                g = max(vg, key=lambda g: g.weight)
                groups[names[g.group]] = groups.get(names[g.group], 0) + 1
    ev.to_mesh_clear()
    bb_lo = Vector([min(p[i] for p in pts) for i in range(3)])
    bb_hi = Vector([max(p[i] for p in pts) for i in range(3)])
    top = sorted(groups.items(), key=lambda kv: -kv[1])[:6]
    return {"outside": out, "of": len(pts), "worst_cm": dict(zip(["-x", "+x", "-y", "+y", "-z", "+z"], [round(x * 100, 1) for x in worst])),
            "by_bone": top, "body_lo": [round(x, 3) for x in bb_lo], "body_hi": [round(x, 3) for x in bb_hi]}


def eyes(rig):
    pb = rig.arm.pose.bones
    m = rig.arm.matrix_world
    return (m @ pb["Left_Eye"].head + m @ pb["Right_Eye"].head) / 2


# ---------------------------------------------------------------------------- rendering


def setup_render(eevee=False):
    sc = bpy.context.scene
    if eevee:
        ids = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
        sc.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in ids else "BLENDER_EEVEE_NEXT"
    else:
        sc.render.engine = "BLENDER_WORKBENCH"
        sh = sc.display.shading
        sh.light = "STUDIO"
        sh.color_type = "TEXTURE"
        sh.show_cavity = True
        sh.cavity_type = "BOTH"
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("W")
    w.color = (0.03, 0.03, 0.035)
    sc.world = w


def ortho_cam(name, loc, target, scale):
    cd = bpy.data.cameras.new(name)
    cd.type = "ORTHO"
    cd.ortho_scale = scale
    cd.clip_start, cd.clip_end = 0.01, 50
    co = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(co)
    co.location = loc
    co.rotation_euler = (vec(target) - vec(loc)).to_track_quat("-Z", "Y").to_euler()
    return co


def render_views(ctx, out_prefix):
    sc = bpy.context.scene
    lo, hi = ctx["zone"]
    c = (lo + hi) / 2
    size = hi - lo
    shells = [o for o in ctx["objs"] if o.name.startswith(("Shell", "Open_"))]
    for o in ctx["objs"]:
        if o.name.startswith("FaceTarget"):
            o.hide_render = True
    sc.camera = ctx["cam"]
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    sc.render.filepath = out_prefix + "_player.png"
    bpy.ops.render.render(write_still=True)
    for o in shells:
        o.hide_render = True
    sc.render.resolution_x, sc.render.resolution_y = 540, 540
    s = max(size) * 1.25 + 0.15
    for tag, d in (("front", Vector((0, -1, 0))), ("side", Vector((1, 0, 0)))):
        cam = ortho_cam("Cam_" + tag, c + d * 4.0, c, s)
        sc.camera = cam
        sc.render.filepath = out_prefix + "_" + tag + ".png"
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(cam, do_unlink=True)
    for o in shells:
        o.hide_render = False


def render_lunge(ctx, out_prefix, F, body_lo, body_hi):
    """Lunge end pose: the inspection camera as it is, the camera pulled in to 0.2 m from her eyes (the climax
    frame of art bible 4.3), and a side view without the boards that also shows where the camera is."""
    sc = bpy.context.scene
    cam = ctx["cam"]
    for o in ctx["objs"]:
        if o.name.startswith("FaceTarget"):
            o.hide_render = True
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    sc.camera = cam
    sc.render.filepath = out_prefix + "_player.png"
    bpy.ops.render.render(write_still=True)
    C = cam.matrix_world.translation.copy()
    q = cam.matrix_world.to_quaternion()
    fwd = q @ Vector((0, 0, -1))
    E = F + (C - F).normalized() * 0.2
    cc = bpy.data.objects.new("Cam_climax", cam.data.copy())
    bpy.context.scene.collection.objects.link(cc)
    cc.location = E
    cc.rotation_mode = "QUATERNION"
    cc.rotation_quaternion = fwd.rotation_difference(F - E) @ q
    sc.camera = cc
    sc.render.filepath = out_prefix + "_climax.png"
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cc, do_unlink=True)
    # side view: boards hidden, a small marker where the camera is
    shells = [o for o in ctx["objs"] if o.name.startswith(("Shell", "Open_"))]
    for o in shells:
        o.hide_render = True
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.03, location=C)
    mk = bpy.context.active_object
    lo, hi = ctx["zone"]
    pts = [lo, hi, C, Vector(body_lo), Vector(body_hi)]
    blo = Vector([min(p[i] for p in pts) for i in range(3)])
    bhi = Vector([max(p[i] for p in pts) for i in range(3)])
    c = (blo + bhi) / 2
    s = max(bhi.y - blo.y, bhi.z - blo.z) * 1.1 + 0.1
    sc.render.resolution_x, sc.render.resolution_y = 540, 540
    oc = ortho_cam("Cam_side", c + Vector((4.0, 0, 0)), c, s)
    sc.camera = oc
    sc.render.filepath = out_prefix + "_side.png"
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(oc, do_unlink=True)
    bpy.data.objects.remove(mk, do_unlink=True)
    for o in shells:
        o.hide_render = False


# ---------------------------------------------------------------------------- keys and export


def key_action(rig, name):
    arm = rig.arm
    if arm.animation_data is None:
        arm.animation_data_create()
    act = bpy.data.actions.get(name) or bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data.action = act
    for pb in arm.pose.bones:
        for f in (1, 2):
            pb.keyframe_insert("rotation_quaternion", frame=f)
            pb.keyframe_insert("location", frame=f)
    arm.animation_data.action = None   # unassign, or the keys would override the next pose set from Python
    return act


def two_sided_copy(src):
    """Export copy of the mesh with every face doubled back to back (reversed winding, negated normals). The kimono,
    sleeves and hair are single sheets; URP Lit drawn with Cull Off lights back faces with the front normal (black
    under the flashlight), so Unity gets real back faces and draws with Cull Back."""
    me = src.data.copy()
    normals = [tuple(c.vector) for c in me.corner_normals]
    bm = bmesh.new()
    bm.from_mesh(me)
    faces = list(bm.faces)
    orig_faces, orig_verts = set(faces), set(bm.verts)
    nrm, i = {}, 0
    for f in faces:   # bmesh keeps the mesh's face and corner order
        for loop in f.loops:
            nrm[(f, loop.vert)] = normals[i]
            i += 1
    ret = bmesh.ops.duplicate(bm, geom=faces)
    face_of = {}
    for a, b in ret["face_map"].items():
        if a in orig_faces:
            face_of[b] = a
        else:
            face_of[a] = b
    vert_of = {}
    for a, b in ret["vert_map"].items():
        if a in orig_verts:
            vert_of[b] = a
        else:
            vert_of[a] = b
    bmesh.ops.reverse_faces(bm, faces=list(face_of))
    corner = []
    for f in bm.faces:
        if f in orig_faces:
            corner += [nrm[(f, loop.vert)] for loop in f.loops]
        else:
            of = face_of[f]
            corner += [tuple(-c for c in nrm[(of, vert_of[loop.vert])]) for loop in f.loops]
    bm.to_mesh(me)
    bm.free()
    me.normals_split_custom_set(corner)
    obj = src.copy()   # same parent, Armature modifier and vertex groups
    obj.data = me
    bpy.context.scene.collection.objects.link(obj)
    print("two-sided export mesh: %d -> %d faces" % (len(src.data.polygons), len(me.polygons)))
    return obj


def export_fbx(rig, path):
    """FBX for Unity (Assets/_Art/Models/Characters/HakoOnna/HakoOnna.fbx, set up by HakoOnnaGhostImport.cs):
    armature + mesh in the rest pose and one take per action (Armature|Pose_<Kind>, Armature|Lunge_<Kind>).
    The mesh goes out two-sided (two_sided_copy); the .blend keeps the single-sided one.
    FBX_SCALE_ALL keeps every node at scale 1 in metres. Unity (Bake Axis Conversion on) maps Blender (X, Y, Z) to
    (X, Z, Y), i.e. Blender +Y onto Unity +Z, but the containers need their opening (Blender -Y) on Unity +Z, so the
    armature is turned 180 degrees about Z for the export: Blender (X, Y, Z) then lands on Unity (-X, Z, -Y), the
    container's local frame (pose_reference_boxes.py). Checked bone by bone in Unity (HakoOnnaGhostSetup.Check)."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    rig.arm.animation_data.action = None
    rest(rig)
    turn = rig.arm.rotation_euler.copy()
    rig.arm.rotation_euler.z += math.pi
    name = rig.mesh.name
    out = two_sided_copy(rig.mesh)
    rig.mesh.name, out.name = name + "_single", name
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    rig.arm.select_set(True)
    out.select_set(True)
    try:
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                                 axis_up="Y", add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
                                 bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                                 bake_anim_simplify_factor=0.0, path_mode="STRIP", embed_textures=False)
    finally:
        me = out.data
        bpy.data.objects.remove(out)
        bpy.data.meshes.remove(me)
        rig.mesh.name = name
        rig.arm.rotation_euler = turn
        bpy.context.view_layer.update()
    print("EXPORTED", path)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_dir, which = argv[0], argv[1]
    opts = argv[2:]
    blend = opts[opts.index("--blend") + 1] if "--blend" in opts else None
    fbx = opts[opts.index("--fbx") + 1] if "--fbx" in opts else None
    eevee = "--eevee" in opts
    kinds = KINDS if which == "all" else which.split(",")
    os.makedirs(out_dir, exist_ok=True)
    import ghost_pose_specs as SP

    bpy.ops.wm.read_factory_settings(use_empty=True)
    rig = load_ghost()
    setup_render(eevee)
    report = {}
    for kind in kinds:
        ctx = load_box(kind)
        spec = SP.POSES[kind](ctx)
        G, P = solve(rig, spec, ctx)
        apply(rig, G, P)
        res = check(rig, ctx)
        e = eyes(rig)
        res["eyes"] = [round(x, 3) for x in e]
        res["face_target"] = [round(x, 3) for x in ctx["face"]]
        report[kind] = res
        print("POSE", kind, json.dumps(res))
        render_views(ctx, os.path.join(out_dir, kind))
        if blend or fbx:
            key_action(rig, "Pose_" + kind)
        if kind in getattr(SP, "LUNGES", {}) and "--no-lunge" not in opts:
            ctx["ik_err"] = {}
            lspec = SP.LUNGES[kind](ctx)
            F = vec(lspec["_face"]) if "_face" in lspec else None
            for _ in range(4):   # the trunk chain decides where the eyes end up: shift the hips until they land on F
                ctx["ik_err"] = {}
                G, P = solve(rig, lspec, ctx)
                apply(rig, G, P)
                if F is None:   # built from the hips: the face lands wherever the body puts it
                    F = eyes(rig)
                    break
                err = F - eyes(rig)
                if err.length < 0.002:
                    break
                lspec["root"]["pos"] = vec(lspec["root"]["pos"]) + err
            if lspec.get("grab"):   # hands go to the explorer's head once the face is placed
                lspec.update(grab_targets(ctx, F, lspec["grab"]))
                ctx["ik_err"] = {}
                G, P = solve(rig, lspec, ctx)
                apply(rig, G, P)
            body = check(rig, ctx)
            lres = lunge_check(rig, ctx, F)
            lres["hips"] = [round(x, 3) for x in lspec["root"]["pos"]]
            report["Lunge_" + kind] = lres
            print("LUNGE", kind, json.dumps(lres))
            render_lunge(ctx, os.path.join(out_dir, kind + "_lunge"), F, body["body_lo"], body["body_hi"])
            if blend or fbx:
                key_action(rig, "Lunge_" + kind)
        if blend is None:
            remove(ctx["objs"])
        else:   # keep every box in its own collection for the .blend
            col = bpy.data.collections.new("Box_" + kind)
            bpy.context.scene.collection.children.link(col)
            for o in ctx["objs"]:
                for uc in list(o.users_collection):
                    uc.objects.unlink(o)
                col.objects.link(o)
            col.hide_viewport = True
            col.hide_render = True
    with open(os.path.join(out_dir, "report.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    if fbx:
        export_fbx(rig, fbx)
    if blend:   # open on the first container: its box shown, its hiding pose on the rig
        first = kinds[0]
        for col in bpy.data.collections:
            if col.name.startswith("Box_"):
                col.hide_viewport = col.hide_render = col.name != "Box_" + first
        rig.arm.animation_data.action = bpy.data.actions.get("Pose_" + first)
        bpy.context.scene.frame_set(1)
        try:
            bpy.ops.file.pack_all()
        except Exception as e:
            print("pack_all:", e)
        bpy.ops.wm.save_as_mainfile(filepath=blend, compress=True)
        print("SAVED", blend)


if __name__ == "__main__":
    main()
