"""The 8 hiding poses (art bible 6.2 table) and the 8 lunge poses (2026-11-21: the moment the container opens she
braces on its edges and shoves her face at the explorer; the hiding pose is only the first frame).

Frame: the reference box (Blender X right as the player sees it, Y into the container, Z up, metres in the house);
the Tansu poses are in the Drawer_0 frame. Her left is +X when she faces the player (-Y).
Rig at 1.2 m: hips-to-shoulder 0.26, hips-to-neck 0.32, upper arm 0.164, forearm 0.139, hand 0.16, thigh 0.286, shin 0.301.
"""
import math
from mathutils import Vector


def N(x, y, z):
    return Vector((x, y, z)).normalized()


def lean(deg):
    """Body up / belly pair when the trunk tips back by deg from upright facing -Y (90 = lying face up)."""
    r = math.radians(deg)
    return {"up": Vector((0, math.sin(r), math.cos(r))), "front": Vector((0, -math.cos(r), math.sin(r)))}


CLAW = {"curl": (15, 55, 45), "spread": 10, "thumb": (10, 20, 20)}
HOOK = {"curl": (55, 85, 55), "spread": 4, "thumb": (20, 30, 30)}
SPREAD = {"curl": (4, 12, 12), "spread": 24, "thumb": (-10, 5, 5)}
GRIP = {"curl": (45, 75, 55), "spread": 3, "thumb": (30, 30, 30)}


# ---------------------------------------------------------------- hiding poses (first frame of the lunge)


def oshiire(ctx):
    """Kneels with her back to the opening, head wrung round 180 deg, arms folded back over the shoulders."""
    floor = ctx["zone"][0].z
    x0, y0, hz = -0.50, -0.27, floor + 0.19
    return {
        "root": {"pos": (x0, y0, hz), "up": (0, 0, 1), "front": (0, 1, 0)},
        "Spine": {"up": N(0, 0.05, 1), "front": (0, 1, 0)},
        "Chest": {"up": (0, 0, 1), "front": (0, 1, 0)},
        "UpperChest": {"up": N(0, -0.05, 1), "front": N(0.25, 0.97, 0)},
        "Neck": {"up": N(0, -0.1, 1), "front": N(0.94, 0.34, 0)},
        "Head": {"face": "camera", "crown": (0, 0, 1)},
        "leg_L": {"to": (x0 - 0.07, y0 - 0.04, floor + 0.06), "hint": N(0, 1, -0.2)},
        "leg_R": {"to": (x0 + 0.07, y0 - 0.04, floor + 0.06), "hint": N(0, 1, -0.2)},
        "Left_Foot": {"dir": N(0, -1, -0.1), "sole": (0, 0, 1)},
        "Right_Foot": {"dir": N(0, -1, -0.1), "sole": (0, 0, 1)},
        "Left_Shoulder": {"rot": [((0, 1, 0), 20)]},
        "Right_Shoulder": {"rot": [((0, 1, 0), -20)]},
        "arm_L": {"to": (x0 - 0.03, y0 - 0.10, hz + 0.30), "hint": N(0, -0.3, 1), "reverse": True},
        "arm_R": {"to": (-0.20, -0.43, hz + 0.33), "hint": N(0.3, -0.2, 1), "reverse": True},
        "Left_Hand": {"dir": N(0.3, 0, -1), "palm": (0, 1, 0)},
        "Right_Hand": {"dir": N(1, -0.1, -0.1), "palm": (0, -1, 0)},
        "hand_L": SPREAD, "hand_R": HOOK,
        "hair": {"mode": "gravity"},
    }


def yofuku(ctx):
    """Hangs head-down, feet braced up in the top corners, knees and elbows bent the wrong way, hair hanging."""
    inv = {"front": (0, -1, 0)}
    return {
        "root": {"pos": (0, 0.0, 1.70), "up": (0, 0, -1), "front": (0, -1, 0)},
        "Spine": dict(up=N(0, 0.05, -1), **inv),
        "Chest": dict(up=N(0, 0.08, -1), **inv),
        "UpperChest": dict(up=N(0, 0.1, -1), **inv),
        "Neck": dict(up=N(0, -0.3, -1), **inv),
        "Head": {"face": "camera", "crown": (0, 0, -1)},
        "leg_L": {"to": (-0.42, 0.10, 2.10), "hint": N(-1, 0, -0.6), "reverse": True},
        "leg_R": {"to": (0.42, 0.10, 2.10), "hint": N(1, 0, -0.6), "reverse": True},
        "Left_Foot": {"dir": N(-1, 0, 0.1), "sole": (0, 0, 1)},
        "Right_Foot": {"dir": N(1, 0, 0.1), "sole": (0, 0, 1)},
        "arm_L": {"to": (-0.38, -0.02, 1.37), "hint": N(-0.3, 0, 1), "reverse": True},
        "arm_R": {"to": (0.38, -0.02, 1.37), "hint": N(0.3, 0, 1), "reverse": True},
        "Left_Hand": {"dir": N(-1, 0, -0.1), "palm": (0, -1, 0)},
        "Right_Hand": {"dir": N(1, 0, -0.1), "palm": (0, -1, 0)},
        "hand_L": CLAW, "hand_R": CLAW,
        "hair": {"mode": "gravity", "push": 0.12},
    }


def chatansu(ctx):
    """Sits on the floor with both legs hooked behind her shoulders, face between her soles, hands at the sill."""
    floor = ctx["zone"][0].z
    return {
        "root": {"pos": (0, 0.06, floor + 0.13), "up": N(0, 0.35, 0.94), "front": N(0, -0.94, 0.35)},
        "Spine": {"up": N(0, 0.1, 1), "front": N(0, -1, 0.1)},
        "Chest": {"up": N(0, -0.25, 1), "front": N(0, -1, -0.25)},
        "UpperChest": {"up": N(0, -0.45, 0.9), "front": N(0, -0.9, -0.45)},
        "Neck": {"up": N(0, -0.25, 1), "front": N(0, -1, -0.25)},
        "Head": {"face": "camera", "crown": N(0, 0.2, 1)},
        "leg_L": {"to": (0.15, -0.17, 0.52), "hint": N(0.4, 1, 1)},
        "leg_R": {"to": (-0.15, -0.17, 0.52), "hint": N(-0.4, 1, 1)},
        "Left_Foot": {"dir": N(0.1, 0, 1), "sole": (0, -1, 0)},
        "Right_Foot": {"dir": N(-0.1, 0, 1), "sole": (0, -1, 0)},
        "arm_L": {"to": (0.25, -0.21, 0.24), "hint": N(0.5, 0, -1), "reverse": True},
        "arm_R": {"to": (-0.25, -0.21, 0.24), "hint": N(-0.5, 0, -1), "reverse": True},
        "Left_Hand": {"dir": N(0.6, -1, -0.5), "palm": (0, 0, -1)},
        "Right_Hand": {"dir": N(-0.6, -1, -0.5), "palm": (0, 0, -1)},
        "hand_L": CLAW, "hand_R": CLAW,
        "hair": {"mode": "gravity", "g": (0, -0.4, -1)},
    }


def getabako(ctx):
    """Braced like an insect in the upper half; neck snapped onto the left shoulder; left arm a mantis claw."""
    return {
        "root": {"pos": (0, 0.0, 1.14), "up": (0, 0, 1), "front": (0, -1, 0)},
        "Spine": {"up": N(0, -0.1, 1), "front": N(0, -1, -0.1)},
        "Chest": {"up": N(0, -0.2, 1), "front": N(0, -1, -0.2)},
        "UpperChest": {"up": N(0, -0.25, 1), "front": N(0, -1, -0.25)},
        "Neck": {"up": N(0.8, -0.1, 0.6), "front": (0, -1, 0)},
        "Head": {"face": "camera", "crown": N(1, 0, -0.1)},
        "leg_L": {"to": (0.44, 0.05, 0.98), "hint": N(0.3, -0.2, 1), "reverse": True},
        "leg_R": {"to": (-0.44, 0.05, 0.98), "hint": N(-0.3, -0.2, 1), "reverse": True},
        "Left_Foot": {"dir": N(0, -1, 0.3), "sole": (1, 0, 0)},
        "Right_Foot": {"dir": N(0, -1, 0.3), "sole": (-1, 0, 0)},
        "arm_R": {"to": (-0.28, -0.20, 1.40), "hint": N(-1, 0, 1), "reverse": True},
        "Right_Hand": {"dir": N(-1, -0.3, 0), "palm": (0, -1, 0)},
        "arm_L": {"to": (0.12, -0.18, 1.24), "hint": N(0, -1, 0.4)},
        "Left_Hand": {"dir": N(0, 0.7, -0.7), "palm": N(0, 0.7, 0.7)},
        "hand_L": HOOK, "hand_R": HOOK,
        "hair": {"mode": "gravity"},
    }


def tansu(ctx):
    """Drawer frame. On her back, head at the drawer front, folded in three, hands hooked on the front panel."""
    floor = ctx["zone"][0].z
    return {
        "root": {"pos": (0, 0.16, floor + 0.185), "up": N(0, -0.75, -0.66), "front": N(0, -0.66, 0.75)},
        "Spine": {"up": N(0, -0.94, -0.34), "front": N(0, -0.34, 0.94)},
        "Chest": {"up": (0, -1, 0), "front": (0, 0, 1)},
        "UpperChest": {"up": N(0, -0.985, 0.17), "front": N(0, 0.17, 0.985)},
        "Neck": {"up": N(0, -1, 0.05), "front": N(0, 0.05, 1)},
        "Head": {"face": "camera", "crown": (0, -1, 0)},
        "leg_L": {"to": (-0.10, 0.15, floor + 0.25), "hint": N(0, -1, 0.2), "reverse": True},
        "leg_R": {"to": (0.10, 0.15, floor + 0.25), "hint": N(0, -1, 0.2), "reverse": True},
        "Left_Foot": {"dir": N(0, 1, 0.2), "sole": (0, 0, 1)},
        "Right_Foot": {"dir": N(0, 1, 0.2), "sole": (0, 0, 1)},
        "arm_L": {"to": (-0.20, -0.27, floor + 0.22), "hint": N(-1, 0, 1), "reverse": True},
        "arm_R": {"to": (0.20, -0.27, floor + 0.22), "hint": N(1, 0, 1), "reverse": True},
        "Left_Hand": {"dir": N(0, -0.3, 1), "palm": (0, -1, 0)},
        "Right_Hand": {"dir": N(0, -0.3, 1), "palm": (0, -1, 0)},
        "hand_L": HOOK, "hand_R": HOOK,
        "hair": {"mode": "spread"},
    }


def nagamochi(ctx):
    """Kneels toward the player and bends back until her head rests on her heels; hands around her ankles."""
    return {
        "root": dict(pos=(0, -0.05, 0.41), **lean(30)),
        "Spine": lean(70),
        "Chest": lean(110),
        "UpperChest": lean(150),
        "Neck": lean(200),
        "Head": {"face": "camera", "crown": (0, 1, 0)},
        "leg_L": {"to": (0.07, 0.10, 0.14), "hint": N(0, -1, -0.3)},
        "leg_R": {"to": (-0.07, 0.10, 0.14), "hint": N(0, -1, -0.3)},
        "Left_Foot": {"dir": N(0, 1, -0.15), "sole": (0, 0, 1)},
        "Right_Foot": {"dir": N(0, 1, -0.15), "sole": (0, 0, 1)},
        "arm_L": {"to": (0.13, 0.09, 0.18), "hint": N(1, 0, 0.3)},
        "arm_R": {"to": (-0.13, 0.09, 0.18), "hint": N(-1, 0, 0.3)},
        "Left_Hand": {"dir": N(-0.5, 0, -1), "palm": N(-1, 0, 0.3)},
        "Right_Hand": {"dir": N(0.5, 0, -1), "palm": N(1, 0, 0.3)},
        "hand_L": GRIP, "hand_R": GRIP,
        "hair": {"mode": "spread"},
    }


def tsuzura(ctx):
    """Packed in: sits folded, knees bent the wrong way so her feet stand beside her ears; hand caged over her face."""
    floor = ctx["zone"][0].z
    return {
        "root": dict(pos=(0, -0.05, floor + 0.11), **lean(20)),
        "Spine": lean(17),
        "Chest": lean(14),
        "UpperChest": lean(12),
        "Neck": lean(27),
        "Head": {"face": "camera", "crown": (0, 1, 0)},
        "leg_L": {"to": (0.14, -0.03, 0.55), "hint": N(0.3, -1, 0), "reverse": True},
        "leg_R": {"to": (-0.14, -0.03, 0.55), "hint": N(-0.3, -1, 0), "reverse": True},
        "Left_Foot": {"dir": N(0, 0.2, 1), "sole": (-1, 0, 0)},
        "Right_Foot": {"dir": N(0, 0.2, 1), "sole": (1, 0, 0)},
        "arm_L": {"to": (0.05, -0.07, 0.53), "hint": N(1, -0.5, 0)},
        "Left_Hand": {"dir": N(-0.2, 0.7, 0.68), "palm": N(0, 0.15, -1)},
        "arm_R": {"to": (0.16, 0.06, 0.52), "hint": (0, 1, 0)},
        "Right_Hand": {"dir": N(1, -0.2, 0.4), "palm": (0, 0, -1)},
        "hand_L": SPREAD, "hand_R": CLAW,
        "hair": {"mode": "gravity"},
    }


def komebitsu(ctx):
    """Kneels with her throat stretched back to the lid; arms come up behind her back and hook under the rim."""
    return {
        "root": dict(pos=(0, -0.13, 0.465), **lean(5)),
        "Spine": lean(8),
        "Chest": lean(15),
        "UpperChest": lean(25),
        "Neck": lean(65),
        "Head": {"face": "camera", "crown": N(0, 1, 0.3)},
        "leg_L": {"to": (0.07, 0.18, 0.145), "hint": N(0, -1, -0.3)},
        "leg_R": {"to": (-0.07, 0.18, 0.145), "hint": N(0, -1, -0.3)},
        "Left_Foot": {"dir": N(0, 1, -0.15), "sole": (0, 0, 1)},
        "Right_Foot": {"dir": N(0, 1, -0.15), "sole": (0, 0, 1)},
        "arm_L": {"to": (0.16, 0.18, 0.72), "hint": N(0, 0.3, -1), "reverse": True},
        "arm_R": {"to": (-0.16, 0.18, 0.72), "hint": N(0, 0.3, -1), "reverse": True},
        "Left_Hand": {"dir": N(0, 0.35, 1), "palm": N(0, 1, -0.3)},
        "Right_Hand": {"dir": N(0, 0.35, 1), "palm": N(0, 1, -0.3)},
        "hand_L": HOOK, "hand_R": HOOK,
        "hair": {"mode": "gravity"},
    }


POSES = {"Oshiire": oshiire, "YofukuTansu": yofuku, "ChaTansu": chatansu, "GetaBako": getabako,
         "Tansu": tansu, "Nagamochi": nagamochi, "Tsuzura": tsuzura, "Komebitsu": komebitsu}


# ---------------------------------------------------------------- lunge poses (the jump scare, 2026-11-21)
# The hiding pose is frame 0; this is where she ends up: hips still inside, head thrust at the explorer, both
# hands out of the opening clutching the explorer's head (the game pulls the camera in until her face is 0.2 m
# away, art bible 4.3, then she drags the explorer in). The l_* functions below still carry the arms of the
# same-day variant with the hands braced on the container (BRACE_LUNGES); grab() swaps them for the grab.


def lunge_body(ctx, face, trunk, crown=(0, 0, 1)):
    """Trunk and head so her eyes land on `face`, looking at the player camera, with the trunk along `trunk`
    (hips -> neck). The hips follow from the face (main() shifts them until the eyes land exactly)."""
    F, C = Vector(face), Vector(ctx["eye"])
    f = (C - F).normalized()
    c = Vector(crown) - f * Vector(crown).dot(f)
    c.normalize()
    neck = F - c * 0.098 - f * 0.071
    t = Vector(trunk).normalized()
    hips = neck - t * 0.325
    front = f - t * f.dot(t)
    if front.length < 1e-3:
        front = c - t * c.dot(t)
    front.normalize()
    nup = (t + c).normalized()
    nfront = (f - nup * f.dot(nup)).normalized()
    return {"root": {"pos": hips, "up": t, "front": front}, "Spine": {"up": t, "front": front},
            "Chest": {"up": t, "front": front}, "UpperChest": {"up": (t * 2 + nup).normalized(), "front": front},
            "Neck": {"up": nup, "front": nfront}, "Head": {"face": "camera", "crown": c},
            "hair": {"mode": "gravity"}, "_face": F.copy()}


def lunge_hips(hips, trunk, front, neck_up, crown):
    """Lunge built from the hips outward (the hips must stay in the box); the face just turns to the camera."""
    t = Vector(trunk).normalized()
    fr = Vector(front) - t * Vector(front).dot(t)
    fr.normalize()
    nup = Vector(neck_up).normalized()
    nfr = (fr - nup * fr.dot(nup)).normalized()
    return {"root": {"pos": Vector(hips), "up": t, "front": fr}, "Spine": {"up": t, "front": fr},
            "Chest": {"up": t, "front": fr}, "UpperChest": {"up": (t + nup * 0.5).normalized(), "front": fr},
            "Neck": {"up": nup, "front": nfr}, "Head": {"face": "camera", "crown": Vector(crown)},
            "hair": {"mode": "gravity"}}


def out_of(ctx, dist):
    """Point `dist` outside the opening on the line from the player camera to the hiding face."""
    C, Fh = Vector(ctx["eye"]), Vector(ctx["face"])
    lo, hi = ctx["opening"]
    d = (Fh - C).normalized()
    if hi.z - lo.z < 0.03:          # lid / drawer: the opening is horizontal
        t = (lo.z - C.z) / d.z
    else:                           # doors / fusuma: the opening is the plane y = lo.y
        t = (lo.y - C.y) / d.y
    O = C + d * t
    return O + (C - O).normalized() * dist


def l_oshiire(ctx):
    """Turns round on the shelf, kneels at its front edge and lunges out and down at the player: right hand on
    the shelf edge, left hand hooked on the jamb where the fusuma is stacked."""
    floor = ctx["zone"][0].z
    s = lunge_body(ctx, out_of(ctx, 0.15), (0.15, -0.85, 0.5))
    s.update({
        "leg_L": {"to": (-0.36, -0.22, floor + 0.08), "knee": (-0.36, -0.52, floor + 0.09)},
        "leg_R": {"to": (-0.57, -0.25, floor + 0.08), "knee": (-0.57, -0.55, floor + 0.09)},
        "Left_Foot": {"dir": N(0, 1, -0.2), "sole": (0, 0, 1)},
        "Right_Foot": {"dir": N(0, 1, -0.2), "sole": (0, 0, 1)},
        "arm_L": {"to": (-0.10, -0.53, floor + 0.30), "hint": N(1, 0.3, 0.6), "reverse": True},
        "arm_R": {"to": (-0.64, -0.50, floor + 0.04), "hint": N(-1, 0.3, 0.4), "reverse": True},
        "Left_Hand": {"dir": N(1, -0.3, 0), "palm": (0, 1, 0)},
        "Right_Hand": {"dir": N(0, -1, -0.5), "palm": (0, 0, -1)},
        "hand_L": HOOK, "hand_R": HOOK,
    })
    return s


def l_yofuku(ctx):
    """Still hanging by her feet from the top corners, she swings her upper body out of the wardrobe; arms flung
    wide, fingertips on the inside of both side boards at the opening; the face upside down."""
    F = out_of(ctx, 0.12)
    F.z += 0.08
    s = lunge_body(ctx, F, (0, -0.3, -0.95), crown=(0, 0, -1))
    s.update({
        "leg_L": {"to": (-0.38, -0.12, 2.10), "hint": N(-1, 0, -0.6), "reverse": True},
        "leg_R": {"to": (0.38, -0.12, 2.10), "hint": N(1, 0, -0.6), "reverse": True},
        "Left_Foot": {"dir": N(-1, 0, 0.1), "sole": (0, 0, 1)},
        "Right_Foot": {"dir": N(1, 0, 0.1), "sole": (0, 0, 1)},
        "arm_L": {"to": (-0.40, -0.33, 1.42), "hint": N(-0.3, 0, 1), "reverse": True},
        "arm_R": {"to": (0.40, -0.33, 1.42), "hint": N(0.3, 0, 1), "reverse": True},
        "Left_Hand": {"dir": N(-1, -0.15, 0), "palm": (0, -1, 0)},
        "Right_Hand": {"dir": N(1, -0.15, 0), "palm": (0, -1, 0)},
        "hand_L": SPREAD, "hand_R": SPREAD,
    })
    s["hair"] = {"mode": "gravity", "push": 0.12}
    return s


def l_chatansu(ctx):
    """Stands up at the front of the cabinet and leans out and up at the player; arms flung wide to the side
    boards at the opening, fingertips on them."""
    floor = ctx["zone"][0].z
    s = lunge_body(ctx, out_of(ctx, 0.18), (0, -0.6, 0.8))
    s.update({
        "leg_L": {"to": (0.09, -0.20, floor + 0.07), "hint": N(0, -1, 0.3)},
        "leg_R": {"to": (-0.09, -0.20, floor + 0.07), "hint": N(0, -1, 0.3)},
        "Left_Foot": {"dir": N(0, -1, -0.1), "sole": (0, 0, -1)},
        "Right_Foot": {"dir": N(0, -1, -0.1), "sole": (0, 0, -1)},
        "arm_L": {"to": (0.40, -0.36, 0.82), "hint": N(0, 0.5, 1), "reverse": True},
        "arm_R": {"to": (-0.40, -0.36, 0.82), "hint": N(0, 0.5, 1), "reverse": True},
        "Left_Hand": {"dir": N(1, -0.1, 0), "palm": (0, -1, 0)},
        "Right_Hand": {"dir": N(-1, -0.1, 0), "palm": (0, -1, 0)},
        "hand_L": SPREAD, "hand_R": SPREAD,
    })
    return s


def l_getabako(ctx):
    """Keeps her insect brace on the side boards and shoves her head and shoulders out of the door; both hands
    hooked round the door frame."""
    s = lunge_body(ctx, out_of(ctx, 0.18), (0, -0.64, 0.77))
    s.update({
        "leg_L": {"to": (0.44, 0.05, 0.95), "hint": N(0.3, -0.2, 1), "reverse": True},
        "leg_R": {"to": (-0.44, 0.05, 0.95), "hint": N(-0.3, -0.2, 1), "reverse": True},
        "Left_Foot": {"dir": N(0, -1, 0.3), "sole": (1, 0, 0)},
        "Right_Foot": {"dir": N(0, -1, 0.3), "sole": (-1, 0, 0)},
        "arm_L": {"to": (0.38, -0.29, 1.27), "hint": N(0, 0.5, 1), "reverse": True},
        "arm_R": {"to": (-0.38, -0.29, 1.27), "hint": N(0, 0.5, 1), "reverse": True},
        "Left_Hand": {"dir": N(1, -0.2, 0), "palm": (0, -1, 0)},
        "Right_Hand": {"dir": N(-1, -0.2, 0), "palm": (0, -1, 0)},
        "hand_L": HOOK, "hand_R": HOOK,
    })
    return s


def l_tansu(ctx):
    """Drawer frame. Sits up out of the drawer head first, toward the player: the face rises upside down, hands
    hooked over the drawer front on both sides of it, legs still folded flat in the drawer."""
    floor = ctx["zone"][0].z
    s = lunge_body(ctx, (0, -0.24, 0.48), (0, -0.8, 0.6), crown=(0, -1, 0))
    s.update({
        "leg_L": {"to": (-0.10, 0.27, floor + 0.20), "hint": N(0, -1, 1), "reverse": True},
        "leg_R": {"to": (0.10, 0.27, floor + 0.20), "hint": N(0, -1, 1), "reverse": True},
        "Left_Foot": {"dir": N(0, 1, 0.2), "sole": (0, 0, 1)},
        "Right_Foot": {"dir": N(0, 1, 0.2), "sole": (0, 0, 1)},
        "arm_L": {"to": (-0.20, -0.36, 0.42), "hint": N(-1, 0, 0.5), "reverse": True},
        "arm_R": {"to": (0.20, -0.36, 0.42), "hint": N(1, 0, 0.5), "reverse": True},
        "Left_Hand": {"dir": N(0, -0.6, 0.8), "palm": N(0, -0.8, -0.6)},
        "Right_Hand": {"dir": N(0, -0.6, 0.8), "palm": N(0, -0.8, -0.6)},
        "hand_L": HOOK, "hand_R": HOOK,
    })
    return s


def l_lid(ctx, hand_x, elbow, reverse, lean=0.10):
    """Shoots up in the box right under the player's face, hands planted on the front rim, head thrown back so
    the face stares straight up into the camera. The hips stay just under the rim (body still in the box)."""
    lo, hi = ctx["opening"]
    zlo, zhi = ctx["zone"]
    rim = lo.z
    wall = lo.y - 0.015                 # on top of the front wall, just outside the opening's edge
    hips = Vector((0, zlo.y + 0.15, zhi.z - 0.04))
    s = lunge_hips(hips, N(0, lean, 1), (0, -1, 0), N(0, 0.3, 1), N(0, 1, 0.2))
    feet_z = max(zlo.z + 0.06, hips.z - 0.50)
    ex, ey, ez = elbow
    s.update({
        "leg_L": {"to": (0.08, hips.y + 0.16, feet_z), "hint": N(0, -1, 0)},
        "leg_R": {"to": (-0.08, hips.y + 0.16, feet_z), "hint": N(0, -1, 0)},
        "Left_Foot": {"dir": N(0, -0.3, -1), "sole": (0, 1, 0)},
        "Right_Foot": {"dir": N(0, -0.3, -1), "sole": (0, 1, 0)},
        "arm_L": {"to": (hand_x, wall, rim + 0.035), "hint": N(ex, ey, ez), "reverse": reverse},
        "arm_R": {"to": (-hand_x, wall, rim + 0.035), "hint": N(-ex, ey, ez), "reverse": reverse},
        "Left_Hand": {"dir": N(0.1, -1, -0.35), "palm": (0, 0, -1)},
        "Right_Hand": {"dir": N(-0.1, -1, -0.35), "palm": (0, 0, -1)},
        "hand_L": HOOK, "hand_R": HOOK,
    })
    return s


BRACE_LUNGES = {"Oshiire": l_oshiire, "YofukuTansu": l_yofuku, "ChaTansu": l_chatansu, "GetaBako": l_getabako,
          "Tansu": l_tansu,
          "Nagamochi": lambda c: l_lid(c, 0.26, (1, 0.2, 0.6), True),
          "Tsuzura": lambda c: l_lid(c, 0.22, (1, 0.5, 0.0), False),
          "Komebitsu": lambda c: l_lid(c, 0.16, (0.6, 0.4, 0.8), True)}


def grab(spec, opt=None):
    """Drop the bracing arms; ghost_pose_lib.grab_targets() reaches both hands to the explorer's head."""
    for k in ("arm_L", "arm_R", "Left_Hand", "Right_Hand", "hand_L", "hand_R"):
        spec.pop(k, None)
    spec["grab"] = opt or True
    return spec


# Upside-down faces (the wardrobe, the drawer): her shoulders are above the face on screen, and whatever her arms
# wear hangs from them down across it.
# 2026-11-22, the user: the mouth has to show, and the hands of the wardrobe further apart. What hid the mouth in
# both was not the pose: grab_targets() sent her left hand to the camera's right, which is right for an upright
# body and crosses the arms in front of the mouth of one that hangs upside down (and left them 11 cm short of the
# explorer). Fixed there; the drawer needed nothing else.
# The wardrobe also brings the elbows down beside her head: the sleeves then hang beside her forehead instead of
# from arms held over her chin, and the hands come in from both edges of the screen. Spread 0.14 against the 0.11
# of the upright grabs; wider than that and the hands leave the screen.
GRAB_OPTS = {"YofukuTansu": {"spread": 0.14, "ahead": 0.15, "drop": 0.02, "elbow_up": -1.0, "elbow_back": 0.6},
             "Tansu": {"spread": 0.12, "ahead": 0.08, "drop": -0.09, "elbow_up": 0.4, "elbow_back": 0.0}}
LUNGES = {k: (lambda f, o: (lambda c: grab(f(c), o)))(f, GRAB_OPTS.get(k)) for k, f in BRACE_LUNGES.items()}
