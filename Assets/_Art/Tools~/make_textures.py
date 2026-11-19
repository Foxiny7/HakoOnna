"""Round-2 derived textures for the HakoOnna house (run once; outputs are committed).

usage: python -I make_textures.py <External dir> <Decals out dir> <font.ttf>

1. Cedar planks: rotate the aged diffuse / normal / mask 90 deg CCW so the boards run along u
   (normal vectors are rotated too: R' = 1 - G, G' = R).
2. Print each diffuse's average colour in linear space (used to calibrate material tints).
3. Decals (RGBA PNG): blood writing x4, soot plume, ash patch, blood drag smear, ceiling water stain, dust mote.
   Blood writing uses the OFL font given on the command line (Noto Serif SC, kana included).
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ext_dir, out_dir, font_path = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out_dir, exist_ok=True)


# ----------------------------------------------------------------------------- helpers
def to_lin(c):
    c = c / 255.0
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def noise(h, w, cells, seed, octaves=1):
    """Smooth value noise in [0,1], shape (h, w); cells = grid cells across the larger side."""
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        c = cells * 2 ** o
        gy, gx = max(2, int(round(c * h / max(h, w))) + 2), max(2, int(round(c * w / max(h, w))) + 2)
        g = rng.random((gy, gx)).astype(np.float32)
        out += amp * np.asarray(Image.fromarray(g, "F").resize((w, h), Image.BICUBIC))
        norm += amp
        amp *= .5
    return np.clip(out / norm, 0, 1)


def warp(a, dx, dy):
    """Bilinear sample a at (x + dx, y + dy)."""
    h, w = a.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    x = np.clip(xx + dx, 0, w - 1.001)
    y = np.clip(yy + dy, 0, h - 1.001)
    x0, y0 = x.astype(int), y.astype(int)
    fx, fy = x - x0, y - y0
    return (a[y0, x0] * (1 - fx) * (1 - fy) + a[y0, x0 + 1] * fx * (1 - fy)
            + a[y0 + 1, x0] * (1 - fx) * fy + a[y0 + 1, x0 + 1] * fx * fy)


def save_rgba(rgb, alpha, path, fill=None):
    """rgb float (h,w,3) 0..1, alpha (h,w) 0..1. Transparent texels get the average colour (no dark fringes)."""
    a = np.clip(alpha, 0, 1)
    if fill is None:
        wsum = a.sum() + 1e-6
        fill = (rgb * a[..., None]).reshape(-1, 3).sum(0) / wsum
    rgb = np.where(a[..., None] > .02, rgb, fill)
    img = np.dstack([np.clip(rgb, 0, 1), a]) * 255
    Image.fromarray(img.round().astype(np.uint8), "RGBA").save(path, optimize=True)
    print("decal", os.path.basename(path), img.shape[1], "x", img.shape[0])


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ----------------------------------------------------------------------------- 1. cedar rotation
ced = os.path.join(ext_dir, "PolyHaven", "japanese_cedar_planks")
src = {
    "diff": os.path.join(ced, "japanese_cedar_planks_diff_aged.jpg"),
    "nor": os.path.join(ced, "japanese_cedar_planks_nor_gl_1k.jpg"),
    "mask": os.path.join(ced, "japanese_cedar_planks_mask.png"),
}
if os.path.exists(src["diff"]):
    rot = Image.Transpose.ROTATE_90  # counter-clockwise as displayed (u right, v up)
    Image.open(src["diff"]).transpose(rot).save(os.path.join(ced, "japanese_cedar_planks_diff_aged_u.jpg"), quality=92)
    n = np.asarray(Image.open(src["nor"]).convert("RGB").transpose(rot)).astype(np.int16)
    n2 = np.dstack([255 - n[..., 1], n[..., 0], n[..., 2]]).astype(np.uint8)
    Image.fromarray(n2, "RGB").save(os.path.join(ced, "japanese_cedar_planks_nor_gl_u.jpg"), quality=95)
    Image.open(src["mask"]).transpose(rot).save(os.path.join(ced, "japanese_cedar_planks_mask_u.png"), optimize=True)
    for p in src.values():
        os.remove(p)
    print("cedar rotated (boards along u)")

# ----------------------------------------------------------------------------- 2. averages
print("\nlinear averages (r g b) | sRGB of the average")
for root, _, files in os.walk(ext_dir):
    for f in sorted(files):
        if not (("_diff_" in f) or f.endswith("_Color.jpg")):
            continue
        a = np.asarray(Image.open(os.path.join(root, f)).convert("RGB").reduce(4)).astype(np.float32)
        lin = to_lin(a).reshape(-1, 3).mean(0)
        srgb = np.where(lin <= .0031308, lin * 12.92, 1.055 * lin ** (1 / 2.4) - .055) * 255
        print(f"{f:48s} lin {lin[0]:.4f} {lin[1]:.4f} {lin[2]:.4f} | srgb {srgb.round().astype(int)}")
for root, _, files in os.walk(ext_dir):
    for f in sorted(files):
        if f.endswith("_mask.png") or f.endswith("_mask_u.png"):
            m = np.asarray(Image.open(os.path.join(root, f))).astype(np.float32) / 255
            print(f"{f:48s} smooth(A) {m[..., 3].mean():.3f}  ao(G) {m[..., 1].mean():.3f}  metal(R) {m[..., 0].mean():.3f}")


# ----------------------------------------------------------------------------- 3. decals
def blood_writing(name, text, vertical, char_px, seed, weight=820, tilt=10.0, shaky=1.0, drips=3):
    """Finger-written blood: per-character jitter, wobbly strokes, ragged edges, dry gaps and drips."""
    rng = np.random.default_rng(seed)
    font = ImageFont.truetype(font_path, char_px)
    font.set_variation_by_axes([weight])
    n = len(text)
    pad = int(char_px * .45)
    drip_room = int(char_px * 1.3)
    if vertical:
        W, H = int(char_px * 1.5) + 2 * pad, int(char_px * (n * 1.02)) + 2 * pad + drip_room
    else:
        W, H = int(char_px * (n * 1.04)) + 2 * pad, int(char_px * 1.3) + 2 * pad + drip_room
    mask = Image.new("L", (W, H), 0)
    for i, ch in enumerate(text):
        cell = int(char_px * 1.6)
        g = Image.new("L", (cell, cell), 0)
        ImageDraw.Draw(g).text((cell / 2, cell / 2), ch, fill=255, font=font, anchor="mm")
        s = rng.uniform(.88, 1.12)
        g = g.resize((max(1, int(cell * s)), max(1, int(cell * s * rng.uniform(.92, 1.08)))), Image.BICUBIC)
        g = g.rotate(rng.uniform(-tilt, tilt), resample=Image.BICUBIC, expand=True)
        if vertical:
            cx = W / 2 + rng.normal(0, char_px * .07 * shaky)
            cy = pad + char_px * (i * 1.02 + .5) + rng.normal(0, char_px * .05 * shaky)
        else:
            cx = pad + char_px * (i * 1.04 + .5) + rng.normal(0, char_px * .05 * shaky)
            cy = pad + char_px * .65 + rng.normal(0, char_px * .09 * shaky) + i * char_px * .03 * shaky
        mask.paste(Image.new("L", g.size, 255), (int(cx - g.width / 2), int(cy - g.height / 2)), g)
    # finger-thick strokes: grow the glyphs a little before roughening
    grow = max(3, int(char_px * .035) | 1)
    mask = mask.filter(ImageFilter.MaxFilter(grow))
    m = np.asarray(mask).astype(np.float32) / 255
    # wobble (finger, not brush) + ragged edge
    amp = char_px * .03 * shaky
    m = warp(m, (noise(H, W, 6, seed + 1) - .5) * 2 * amp, (noise(H, W, 6, seed + 2) - .5) * 2 * amp)
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(char_px * .02))).astype(np.float32) / 255
    edge = noise(H, W, 40, seed + 3, 2)
    m = smoothstep(.38 + .12 * edge, .5 + .12 * edge, m)
    # drips from the lower edges of strokes
    img = Image.fromarray((m * 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    below = np.zeros_like(m, bool)
    off = max(2, char_px // 40)
    below[:-off] = (m[:-off] > .6) & (m[off:] < .1)
    ys, xs = np.nonzero(below)
    used = []
    want = drips * n
    for k in rng.permutation(len(ys))[: want * 12]:
        x, y = int(xs[k]), int(ys[k])
        if any(abs(x - ux) < char_px * .18 for ux, _ in used):
            continue
        used.append((x, y))
        length = char_px * rng.choice([rng.uniform(.08, .3), rng.uniform(.3, .9), rng.uniform(.8, 1.25)], p=[.45, .4, .15])
        w = rng.uniform(.018, .04) * char_px
        steps = max(4, int(length / 4))
        px = x
        for j in range(steps):
            t0, t1 = j / steps, (j + 1) / steps
            ww = w * (1 - .55 * t1)
            nx = px + rng.normal(0, .25)
            d.line([(px, y + t0 * length), (nx, y + t1 * length)], fill=255, width=max(1, int(round(ww))))
            px = nx
        r = w * rng.uniform(.6, .95)
        d.ellipse([px - r, y + length - r * .6, px + r, y + length + r * 1.3], fill=255)
        if len(used) >= want:
            break
    m = np.asarray(img).astype(np.float32) / 255
    # dry finger: a few thin streaky gaps inside the strokes (kept light so the words stay readable)
    streak = noise(H, W, 30, seed + 4, 2)
    streak = np.asarray(Image.fromarray(streak, "F").resize((W, H // 6 + 1)).resize((W, H), Image.BICUBIC))
    a = m * np.clip(.72 + .6 * streak, 0, 1)
    a = np.where(a > .35, np.clip(a * 1.25, 0, 1), a * .9)
    tone = noise(H, W, 10, seed + 5, 3)
    dark = np.array([.17, .012, .01]); fresh = np.array([.38, .025, .02])
    rgb = dark + (fresh - dark) * tone[..., None]
    save_rgba(rgb, a, os.path.join(out_dir, name + ".png"))


blood_writing("T_BloodWriting_DeteSoto", "ここから出して", False, 170, 11, weight=900, tilt=12, shaky=1.3, drips=2)
blood_writing("T_BloodWriting_Akeruna", "開けるな", True, 230, 23, weight=900, tilt=7, shaky=.8, drips=4)
blood_writing("T_BloodWriting_Mitsuketa", "みつけた", False, 230, 37, weight=800, tilt=12, shaky=1.2, drips=3)
blood_writing("T_BloodWriting_Madairu", "まだいる", True, 200, 41, weight=850, tilt=9, shaky=1.0, drips=3)

# soot plume above a stove / hearth: narrow source at the bottom, spreading toward the ceiling (v = 1 at the top)
H, W = 1024, 512
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
t = 1 - yy / (H - 1)                                 # 0 bottom .. 1 top
half = (.18 + .32 * t ** .8) * W                     # plume half width in px
wob = (noise(H, W, 5, 51) - .5) * W * .18
dist = np.abs(xx - W / 2 - wob) / half
a = (1 - smoothstep(.45, 1.0, dist)) * smoothstep(0, .35, t) * (.45 + .55 * t)
patch = noise(H, W, 9, 52, 4)
a *= np.clip(.35 + 1.1 * patch, 0, 1) * .92
rgb = np.dstack([np.full((H, W), .045), np.full((H, W), .038), np.full((H, W), .033)])
save_rgba(rgb, a, os.path.join(out_dir, "T_SootPlume.png"))

# ash / incense dust patch (floor), roughly round with powdery edge and darker flecks
H = W = 512
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
r = np.hypot((xx - W / 2) / (W / 2), (yy - H / 2) / (H / 2))
r = r + (noise(H, W, 5, 61, 2) - .5) * .55
powder = noise(H, W, 140, 62, 2)
a = (1 - smoothstep(.3, .95, r)) * np.clip(.45 + .5 * noise(H, W, 10, 65, 3) + .3 * (powder - .5), 0, 1)
fleck = (noise(H, W, 160, 63) > .8) & (noise(H, W, 12, 66) > .55)
base = .40 + .08 * noise(H, W, 20, 64, 2)
rgb = np.dstack([base, base * .985, base * .96])
rgb[fleck] *= .3
a = np.where(fleck, np.maximum(a, .7 * (1 - smoothstep(.45, .9, r))), a)
save_rgba(rgb, a * .85, os.path.join(out_dir, "T_AshPatch.png"))

# blood drag smear (floor): starts heavy on the left, thins and breaks up to the right (u along the drag)
H, W = 256, 1024
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
u = xx / (W - 1)
center = H / 2 + (noise(H, W, 3, 71) - .5) * H * .35
half = H * (.36 - .2 * u)
across = np.abs(yy - center) / half
# long parallel streaks: fine noise across the drag, nearly constant along it (sampled in the drag's own frame)
rows = noise(H * 4, 8, 90, 72, 2)                     # (4H, 8): fine across, coarse along
rows = np.asarray(Image.fromarray(rows, "F").resize((W, H * 4), Image.BICUBIC))
idx = np.clip(((yy - center) * 2 + H * 2).astype(int), 0, H * 4 - 1)
streak = rows[idx, xx.astype(int)]
a = (1 - smoothstep(.55, 1.0, across)) * np.clip(1.2 - u * .85, 0, 1)
a *= smoothstep(.15 + .55 * u, .45 + .5 * u, streak + .25 * (1 - across))
a = np.clip(a * 1.15, 0, 1)
tone = noise(H, W, 12, 73, 2)
rgb = np.dstack([.13 + .1 * tone, .018 + .01 * tone, .014 + .008 * tone])
save_rgba(rgb, a, os.path.join(out_dir, "T_BloodDrag.png"))

# ceiling water stain: irregular blotch with two darker tide lines
H = W = 512
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
r = np.hypot((xx - W / 2) / (W / 2), (yy - H / 2) / (H / 2)) + (noise(H, W, 4, 81, 3) - .5) * .5
body = (1 - smoothstep(.55, .85, r)) * (.18 + .25 * noise(H, W, 12, 82, 3))
tide = np.exp(-((r - .8) / .035) ** 2) * .75 + np.exp(-((r - .55) / .03) ** 2) * .35
a = np.clip(body + tide * (.6 + .4 * noise(H, W, 30, 83)), 0, 1) * (1 - smoothstep(.86, .95, r))
rgb = np.dstack([np.full((H, W), .33), np.full((H, W), .22), np.full((H, W), .11)])
rgb *= (1 - .45 * np.clip(tide, 0, 1))[..., None]
save_rgba(rgb, a, os.path.join(out_dir, "T_CeilingStain.png"))

# dust mote sprite for the particle systems
H = W = 64
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
r = np.hypot(xx - W / 2 + .5, yy - H / 2 + .5) / (W / 2)
a = np.exp(-(r / .42) ** 2) * (1 - smoothstep(.85, 1, r))
save_rgba(np.ones((H, W, 3)), a, os.path.join(out_dir, "T_DustMote.png"), fill=np.ones(3))
