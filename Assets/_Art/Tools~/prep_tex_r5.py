"""Round 5: composite foliage card atlases from the approved CC0 downloads.
usage: prep_tex_r5.py <ph_maps dir> <acg dir> <out External dir>

Sources (all CC0):
  ambientCG LeafSet028   autumn maple leaves, 4 x 4 on a 1024 x 512 sheet, stems to the left
  ambientCG LeafSet022   oval evergreen leaves, 3 x 2 on a 1024 x 1024 sheet, stems down
  Poly Haven pine_sapling_small "twig" maps: a UV unwrap (needle strips, cones), not branch cards;
                         only the needle and twig colours are sampled from it

Outputs (1024 x 1024 RGBA, alpha = coverage, four 512 px cells; a sprig grows up from the bottom centre of its cell):
  Foliage/maple_sprig/maple_sprig_diff_a.png   cells 0-2 sprigs, cell 3 = 4 x 4 single leaves (fallen leaves)
  Foliage/shrub_sprig/shrub_sprig_diff_a.png   cells 0-2 leafy shoots, cell 3 = half-dead shoot
  Foliage/pine_tuft/pine_tuft_diff_a.png       cells 0-2 needle tufts, cell 3 = browning tuft
Cell index: 0 = top-left, 1 = top-right, 2 = bottom-left, 3 = bottom-right (image space).
"""
import math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ph, acg, out = sys.argv[1], sys.argv[2], sys.argv[3]
RND = random.Random(20261121)
CELL, SS = 512, 2            # cell size in the atlas, supersampling while drawing
BIG = CELL * SS


# ----------------------------------------------------------------------------- sprites
def cut_sprite(col, opa, box):
    """Crop one leaf, alpha from the opacity map; returns RGBA trimmed to the alpha bounding box."""
    rgb = col.crop(box).convert("RGB")
    a = opa.crop(box).convert("L").point(lambda v: 0 if v < 40 else min(255, int((v - 40) * 1.4)))
    im = Image.merge("RGBA", (*rgb.split(), a))
    bb = a.point(lambda v: 255 if v > 96 else 0).getbbox()
    return im.crop(bb) if bb else None


def maple_sprites():
    """Each: (RGBA with the petiole base at the left edge, pivot (x, y), redness 0..1). Leaf tip points to +x."""
    col = Image.open(os.path.join(acg, "LeafSet028", "LeafSet028_1K-JPG_Color.jpg"))
    opa = Image.open(os.path.join(acg, "LeafSet028", "LeafSet028_1K-JPG_Opacity.jpg"))
    res = []
    for r in range(4):
        for c in range(4):
            sp = cut_sprite(col, opa, (c * 256, r * 128, (c + 1) * 256, (r + 1) * 128))
            if sp is None:
                continue
            a = np.asarray(sp.split()[3], dtype=np.float32) / 255.0
            thick = (a > .5).sum(axis=0)
            blade = np.nonzero(thick > 12)[0]
            if len(blade) == 0:
                continue
            x0 = max(0, int(blade[0]) - 14)                    # keep 14 px of petiole
            sp = sp.crop((x0, 0, sp.size[0], sp.size[1]))
            a = a[:, x0:]
            rows = np.nonzero(a[:, :3].sum(axis=1) > .3)[0]
            py = float(rows.mean()) if len(rows) else sp.size[1] / 2
            px = np.asarray(sp.convert("RGB"), dtype=np.float32) / 255.0
            w = a[..., None]
            avg = (px * w).sum((0, 1)) / max(w.sum(), 1e-3)
            red = float(np.clip((avg[0] - avg[1]) * 3.0, 0, 1))
            res.append((sp, (0.0, py), red))
    return res


def shrub_sprites():
    """Each: (RGBA, pivot at the stem end (bottom), yellowness). Leaf tip points to -y (up)."""
    col = Image.open(os.path.join(acg, "LeafSet022", "LeafSet022_1K-JPG_Color.jpg"))
    opa = Image.open(os.path.join(acg, "LeafSet022", "LeafSet022_1K-JPG_Opacity.jpg"))
    res = []
    for r in range(2):
        for c in range(3):
            x0, x1 = round(c * 1024 / 3), round((c + 1) * 1024 / 3)
            sp = cut_sprite(col, opa, (x0, r * 512, x1, (r + 1) * 512))
            if sp is None:
                continue
            a = np.asarray(sp.split()[3], dtype=np.float32) / 255.0
            cols = np.nonzero(a[-4:, :].sum(axis=0) > .3)[0]
            px = float(cols.mean()) if len(cols) else sp.size[0] / 2
            res.append((sp, (px, float(sp.size[1] - 1)), 0.0))
    return res


def grade(sp, mul=(1, 1, 1), sat=1.0, mix=None, mix_k=0.0):
    """Colour-grade a sprite: multiply, change saturation, mix towards a colour (keeps the leaf's own shading)."""
    a = np.asarray(sp, dtype=np.float32) / 255.0
    rgb = a[..., :3] * np.array(mul, np.float32)
    lum = (rgb * np.array([.299, .587, .114], np.float32)).sum(-1, keepdims=True)
    rgb = lum + (rgb - lum) * sat
    if mix is not None:
        rgb = rgb * (1 - mix_k) + lum / max(float(lum.mean()), 1e-3) * np.array(mix, np.float32) * mix_k
    a = np.concatenate([np.clip(rgb, 0, 1), a[..., 3:]], axis=-1)
    return Image.fromarray((a * 255 + .5).astype(np.uint8), "RGBA")


def paste(canvas, sp, pivot, at, angle_deg, length_px, base_len):
    """Paste sprite so that its pivot lands on `at`, scaled so base_len px of sprite = length_px, rotated by angle (ccw)."""
    k = length_px / base_len
    w, h = max(1, round(sp.size[0] * k)), max(1, round(sp.size[1] * k))
    s = sp.resize((w, h), Image.LANCZOS)
    px, py = pivot[0] * k, pivot[1] * k
    r = int(math.ceil(max(math.hypot(px, py), math.hypot(w - px, py), math.hypot(px, h - py), math.hypot(w - px, h - py)))) + 2
    pad = Image.new("RGBA", (2 * r, 2 * r), (0, 0, 0, 0))
    pad.paste(s, (round(r - px), round(r - py)))
    pad = pad.rotate(angle_deg, Image.BICUBIC)
    canvas.alpha_composite(pad, (round(at[0] - r), round(at[1] - r))) if 0 <= at[0] - r and 0 <= at[1] - r and at[0] + r <= canvas.size[0] and at[1] + r <= canvas.size[1] else safe_composite(canvas, pad, (round(at[0] - r), round(at[1] - r)))


def safe_composite(canvas, im, pos):
    """alpha_composite that clips to the canvas."""
    x, y = pos
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(canvas.size[0], x + im.size[0]), min(canvas.size[1], y + im.size[1])
    if x1 <= x0 or y1 <= y0:
        return
    canvas.alpha_composite(im.crop((x0 - x, y0 - y, x1 - x, y1 - y)), (x0, y0))


def twig_path(p0, ang, length, bend, steps=14):
    """Polyline from p0 heading `ang` (radians, 0 = up, + = to the right), bending by `bend` radians in total."""
    pts, a, p = [p0], ang, p0
    for i in range(steps):
        a += bend / steps + RND.uniform(-.05, .05)
        p = (p[0] + math.sin(a) * length / steps, p[1] - math.cos(a) * length / steps)
        pts.append(p)
    return pts


def draw_twig(d, pts, w0, w1, col):
    n = len(pts) - 1
    for i in range(n):
        w = w0 + (w1 - w0) * i / n
        k = .85 + .3 * RND.random()
        c = tuple(int(min(255, v * k)) for v in col) + (255,)
        d.line([pts[i], pts[i + 1]], fill=c, width=max(1, round(w)))
        d.ellipse([pts[i][0] - w / 2, pts[i][1] - w / 2, pts[i][0] + w / 2, pts[i][1] + w / 2], fill=c)


def direction(pts, i):
    a, b = pts[max(0, i - 1)], pts[min(len(pts) - 1, i + 1)]
    return math.atan2(b[0] - a[0], -(b[1] - a[1]))


def finish(cells, path, fill):
    """Downsample the supersampled cells, lay them out 2 x 2, bleed the average colour under transparent pixels."""
    atlas = Image.new("RGBA", (CELL * 2, CELL * 2), (0, 0, 0, 0))
    for i, c in enumerate(cells):
        small = c.resize((CELL, CELL), Image.LANCZOS)
        # keep a clear 6 px margin so neighbouring cells never bleed into a card
        m = Image.new("L", (CELL, CELL), 0)
        ImageDraw.Draw(m).rectangle([6, 6, CELL - 7, CELL - 7], fill=255)
        small.putalpha(Image.composite(small.split()[3], Image.new("L", (CELL, CELL), 0), m))
        atlas.paste(small, ((i % 2) * CELL, (i // 2) * CELL))
    a = np.asarray(atlas, dtype=np.float32) / 255.0
    al = a[..., 3:]
    blur = np.asarray(atlas.filter(ImageFilter.GaussianBlur(6)), dtype=np.float32) / 255.0
    under = np.where(blur[..., 3:] > .02, blur[..., :3] / np.maximum(blur[..., 3:], .02), np.array(fill, np.float32))
    rgb = a[..., :3] * al + under * (1 - al)
    res = np.concatenate([np.clip(rgb, 0, 1), al], axis=-1)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray((res * 255 + .5).astype(np.uint8), "RGBA").save(path, optimize=True)
    w = al
    avg = (a[..., :3] * w).sum((0, 1)) / max(float(w.sum()), 1e-3)
    lin = np.where(avg <= .04045, avg / 12.92, ((avg + .055) / 1.055) ** 2.4)
    print(f"{os.path.basename(path):28s} coverage {float(al.mean()):.2f}  avgLin {lin[0]:.4f}f, {lin[1]:.4f}f, {lin[2]:.4f}f")


# ----------------------------------------------------------------------------- maple
def maple_atlas():
    sprites = maple_sprites()
    reds = sorted(sprites, key=lambda s: -s[2])
    twig_col = (58, 30, 26)

    def pick(dead=0.0):
        # mostly the red / dark-red leaves, some orange, now and then a yellow one
        r = RND.random()
        pool = reds[:5] if r < .62 else reds[5:10] if r < .9 else reds[10:]
        sp, pv, red = RND.choice(pool or reds)
        k = RND.uniform(.72, 1.0)
        if red > .45:
            g = grade(sp, (k * 1.0, k * .50, k * .55), 1.1) if RND.random() < .8 else grade(sp, (k, k * .78, k * .7), 1.05)
        else:
            g = grade(sp, (k, k * .60, k * .58), 1.05) if RND.random() < .85 else grade(sp, (k, k * .9, k * .8), 1.0, (.55, .30, .10), .25)
        if RND.random() < dead:
            g = grade(g, (.8, .7, .6), .5, (.36, .22, .12), .5)
        return g, pv

    def sprig(seed_lean):
        im = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        main = twig_path((BIG / 2, BIG - 12), seed_lean, BIG * .62, RND.uniform(-.5, .5))
        twigs = [(main, 9, 4)]
        for t in (.25, .35, .45, .55, .65, .75, .88):
            i = int(t * (len(main) - 1))
            side = 1 if RND.random() < .5 else -1
            a = direction(main, i) + side * RND.uniform(.55, 1.0)
            twigs.append((twig_path(main[i], a, BIG * RND.uniform(.2, .36), -side * RND.uniform(.1, .5), 9), 5, 2))
        for pts, w0, w1 in twigs:
            draw_twig(d, pts, w0, w1, twig_col)
        nodes = []
        for pts, _, _ in twigs:
            for i in range(2, len(pts)):
                for _ in range(2):
                    if RND.random() < .8:
                        nodes.append((pts[i], direction(pts, i)))
            nodes.append((pts[-1], direction(pts, len(pts) - 1)))
        RND.shuffle(nodes)
        for p, a in nodes:
            for side in (-1, 1):                               # maples carry their leaves in opposite pairs
                if RND.random() < .12:
                    continue
                sp, pv = pick(.08)
                ang = a + side * RND.uniform(.5, 1.25)
                ln = BIG * RND.uniform(.12, .18)
                # sprite tip points +x; rotate so it points along `ang` (0 = up): ccw angle = 90deg - ang
                paste(im, sp, pv, p, 90 - math.degrees(ang), ln, sp.size[0])
        return im

    cells = [sprig(-.25), sprig(.2), sprig(0.0)]
    # cell 3: 4 x 4 single leaves, stems down (fallen leaves and loose leaves)
    im = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
    q = BIG // 4
    order = reds[:9] + reds[5:12]
    for i in range(16):
        sp, pv, red = order[i % len(order)]
        k = RND.uniform(.6, .95)
        g = grade(sp, (k, k * .58, k * .6), .95, (.40, .20, .10), .15 + .35 * RND.random())
        cx, cy = (i % 4) * q + q / 2, (i // 4) * q + q - 14
        paste(im, g, pv, (cx, cy), 90, q * .86, sp.size[0])
    cells.append(im)
    finish(cells, os.path.join(out, "Foliage", "maple_sprig", "maple_sprig_diff_a.png"), (.33, .09, .06))


# ----------------------------------------------------------------------------- shrub
def shrub_atlas():
    sprites = shrub_sprites()
    twig_col = (64, 50, 38)

    def leaf(dead):
        sp, pv, _ = RND.choice(sprites)
        k = RND.uniform(.55, .9)
        r = RND.random()
        if r < dead:
            g = grade(sp, (k, k * .8, k * .5), .6, (.42, .30, .14), .75)        # brown, dried
        elif r < dead + .12:
            g = grade(sp, (k * 1.05, k * .95, k * .45), 1.0, (.55, .50, .16), .35)  # yellowing
        else:
            g = grade(sp, (k * .44, k * .62, k * .36), .85)                    # deep evergreen
        return g, pv

    def shoot(lean, dead, keep):
        im = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        main = twig_path((BIG / 2, BIG - 12), lean, BIG * .5, RND.uniform(-.4, .4), 10)
        twigs = [(main, 10, 5)]
        ends = []
        for t in (.35, .55, .7, .85, 1.0):
            i = min(len(main) - 1, int(t * (len(main) - 1)))
            for side in (-1, 1):
                if RND.random() < .2:
                    continue
                a = direction(main, i) + side * RND.uniform(.35, 1.05)
                pts = twig_path(main[i], a, BIG * RND.uniform(.2, .36), -side * RND.uniform(0, .5), 8)
                twigs.append((pts, 5, 2))
                ends.append(pts)
        ends.append(main)
        for pts, w0, w1 in twigs:
            draw_twig(d, pts, w0, w1, twig_col)
        items = []
        for pts in ends:
            # azalea-like: leaves crowd in whorls at the shoot tips, a few further down
            for i in range(len(pts) - 1, 1, -1):
                t = i / (len(pts) - 1)
                n = 5 if t > .85 else 3 if t > .55 else 1
                for _ in range(n):
                    if RND.random() > keep:
                        continue
                    a = direction(pts, i) + RND.uniform(-1.25, 1.25)
                    items.append((pts[i], a, BIG * RND.uniform(.12, .19)))
        RND.shuffle(items)
        for p, a, ln in items:
            sp, pv = leaf(dead)
            paste(im, sp, pv, p, -math.degrees(a), ln, sp.size[1])
        return im

    cells = [shoot(-.2, .04, 1.0), shoot(.25, .06, 1.0), shoot(0.0, .1, .9), shoot(.1, .7, .38)]
    finish(cells, os.path.join(out, "Foliage", "shrub_sprig", "shrub_sprig_diff_a.png"), (.10, .16, .07))


# ----------------------------------------------------------------------------- pine
def pine_atlas():
    d0 = os.path.join(ph, "pine_sapling_small")
    diff = np.asarray(Image.open(os.path.join(d0, "pine_sapling_small_twig_diff_1k.jpg")).convert("RGB"), dtype=np.float32) / 255.0
    alpha = np.asarray(Image.open(os.path.join(d0, "pine_sapling_small_twig_alpha_1k.png")).convert("L"), dtype=np.float32) / 255.0
    # needle strips live in the lower-right of the unwrap, where the alpha map is mostly black with thin white needles
    ys, xs = np.nonzero((alpha[580:1010, 650:1015] > .85))
    pal = diff[580:1010, 650:1015][ys, xs]
    lum = pal.sum(1)
    pal = pal[np.argsort(lum)][int(len(pal) * .15):int(len(pal) * .9)]      # drop the darkest / brightest outliers
    pal = pal[:: max(1, len(pal) // 400)]
    bark = diff[640:980, 60:560].reshape(-1, 3)
    bark = bark[:: max(1, len(bark) // 200)]
    print(f"pine: {len(pal)} needle colours, mean {pal.mean(0).round(3)}; twig mean {bark.mean(0).round(3)}")

    def needle_col(dead, t):
        c = pal[RND.randrange(len(pal))] * np.array([.62, .95, .70], np.float32) * RND.uniform(.7, 1.05) * (.75 + .4 * t)   # darker at the base
        if RND.random() < dead:
            l = float(c.sum()) / 3
            c = np.array([.50, .33, .14], np.float32) * (l / .25) * RND.uniform(.7, 1.1)
        return tuple(int(min(255, max(0, v * 255))) for v in c) + (255,)

    def tuft(lean, dead, count):
        im = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        stem = twig_path((BIG / 2, BIG - 12), lean, BIG * .44, RND.uniform(-.25, .25), 10)
        tw = bark[RND.randrange(len(bark))]
        draw_twig(d, stem, 16, 9, tuple(int(v * 255 * .8) for v in tw))
        needles = []
        for _ in range(count):
            t = RND.uniform(.28, 1.0) ** .8
            i = t * (len(stem) - 1)
            i0 = int(i)
            p = stem[min(i0, len(stem) - 1)]
            base = direction(stem, i0)
            spread = 1.25 - .55 * t                                           # wider near the base, a brush at the tip
            a = base + RND.uniform(-spread, spread)
            ln = BIG * RND.uniform(.30, .47) * (.8 + .2 * t)
            needles.append((p, a, ln, RND.uniform(-.25, .25)))
        needles.sort(key=lambda n: -abs(n[1]))                               # outer needles first, centre on top
        for p, a, ln, curve in needles:
            pts, q, ang = [p], p, a
            for s in range(6):
                ang += curve / 6
                q = (q[0] + math.sin(ang) * ln / 6, q[1] - math.cos(ang) * ln / 6)
                pts.append(q)
            for s in range(6):
                w = 7.0 * (1 - s / 8.0)
                d.line([pts[s], pts[s + 1]], fill=needle_col(dead, s / 5.0), width=max(3, round(w)))
        bud = stem[-1]
        d.ellipse([bud[0] - 9, bud[1] - 16, bud[0] + 9, bud[1] + 10], fill=(118, 84, 52, 255))
        return im

    cells = [tuft(-.12, .03, 300), tuft(.15, .05, 270), tuft(0.0, .08, 320), tuft(.08, .7, 170)]
    finish(cells, os.path.join(out, "Foliage", "pine_tuft", "pine_tuft_diff_a.png"), (.10, .13, .07))


maple_atlas()
shrub_atlas()
pine_atlas()
