"""The lunge of the HakoOnna ghost as an animation: from the hiding pose (A) to the grab (B) in FRAMES steps.
Used by ghost_pose_lib.py.

Both ends are solved poses; in between every bone turns from its A rotation to its B rotation in its parent's
frame, but not all at once: the head whips out first and overshoots, the trunk follows, the arms start late and
arrive last, the fingers close at the very end, the mouth tears open on the way (ghost_face.expression). The hips
dip back for a moment before they go (anticipation). Hair and cloth are carried along with a small delay; their
swing comes from HakoOnnaGhost at run time. The last frame is exactly B.
The game drives it with one number (HakoOnnaGhost.Lunge 0..1 = time through this clip), so nothing changes there.
"""
import math

from mathutils import Quaternion

import ghost_face as FACE

FRAMES = 24   # steps; the clip has FRAMES + 1 keys
FINGERS = ("Thumb", "Index", "Middle", "Ring", "Pinky")
FACE_BONES = ("Jaw", "MouthCorner_L", "MouthCorner_R", "LipUpper", "Lid_L", "Lid_R", "TeethUpper", "TeethLower")


def timing(b):
    """(start, end, overshoot) of a bone's move, as fractions of the lunge."""
    if b in FACE_BONES:
        return 0.12, 0.80, 0.18
    if b.startswith("Hair_"):
        return 0.06, 0.86, 0.0
    if b.startswith(("Sleeve_", "SleeveBag_")):
        return 0.22, 1.0, 0.0
    if b.startswith("Hem_"):
        return 0.05, 0.95, 0.0
    if any(f in b for f in FINGERS):
        return 0.50, 1.0, 0.0
    if b.endswith("_Hand"):
        return 0.30, 1.0, 0.10
    if b.endswith("_LowerArm"):
        return 0.22, 0.96, 0.12
    if b.endswith(("_UpperArm", "_Shoulder")):
        return 0.14, 0.92, 0.10
    if b.startswith(("Neck", "Head")) or b.endswith("_Eye"):
        return 0.0, 0.68, 0.24
    if b in ("Spine", "Chest", "UpperChest"):
        return 0.03, 0.82, 0.12
    return 0.0, 0.86, 0.06   # legs, feet


def progress(t, start, end, over):
    x = max(0.0, min(1.0, (t - start) / (end - start)))
    return x * x * (3 - 2 * x) + over * math.sin(math.pi * x) * x * x


def slerp(a, b, w):
    """From a to b the short way, also past either end (w outside 0..1); mathutils' own slerp clamps."""
    d = a.inverted() @ b
    if d.w < 0.0:
        d = -d
    axis, angle = d.to_axis_angle()
    return a @ Quaternion(axis, angle * w) if abs(angle) > 1e-7 else a.copy()


def locals_of(rig, G, P):
    q, o = {}, {}
    for b in rig.order:
        p = rig.parent[b]
        if p is not None:
            q[b] = G[p].inverted() @ G[b]
            o[b] = G[p].inverted() @ (P[b] - P[p]) - (rig.head0[b] - rig.head0[p])
    return q, o


def frames(rig, A, B, n=FRAMES):
    """[(t, G, P)] for t = 0 .. 1; A and B are the (G, P) of the two solved poses."""
    (GA, PA), (GB, PB) = A, B
    qA, oA = locals_of(rig, GA, PA)
    qB, oB = locals_of(rig, GB, PB)
    out = []
    for k in range(n + 1):
        t = k / n
        if k == n:
            out.append((t, dict(GB), dict(PB)))
            break
        G, P = {}, {}
        for b in rig.order:
            p = rig.parent[b]
            if p is None:   # the hips: a short dip back first, then out
                w = progress(t, 0.0, 0.80, 0.10) - 0.06 * math.sin(math.pi * min(1.0, t / 0.16))
                G[b] = slerp(GA[b], GB[b], w)
                P[b] = PA[b] + (PB[b] - PA[b]) * w
                continue
            w = progress(t, *timing(b))
            G[b] = G[p] @ slerp(qA[b], qB[b], w)
            P[b] = P[p] + G[p] @ (rig.head0[b] - rig.head0[p] + oA[b] + (oB[b] - oA[b]) * w)
        # the teeth come out early, and the lower row rides on the lip wherever the jaw and the corners put it
        face = FACE.expression(min(1.25, max(0.0, progress(t, *timing("Jaw")))))
        P["TeethUpper"] = P["Head"] + G["Head"] @ (rig.head0["TeethUpper"] - rig.head0["Head"] + FACE.Vector(face["TeethUpper"]["move"]))
        FACE.follow_lip(rig, face, G, P)
        out.append((t, G, P))
    return out
