"""Round 6b (outskirts, after the first review): draw foliage card atlases for the sugi hill, the bamboo over the west
wall and the dry grass on the terrace edges, and write a greyer copy of the concrete map.
usage: prep_tex_r6b.py <Assets/_Art/Import~ dir> <Assets/_Art/Textures/External dir>

Sources (all CC0, downloaded earlier for rounds 5 and 6; only colours are sampled, nothing is cut out):
  Poly Haven pine_sapling_small twig diffuse + alpha  (Import~/r5/ph_maps)  needle colours for the sugi sprays
  ambientCG LeafSet022 colour + opacity               (Import~/r5/acg)      leaf colours for the bamboo
  Poly Haven withered_grass diffuse                   (Import~/r6/ph_maps)  straw colours for the grass
  Poly Haven concrete_moss diffuse                    (Import~/r6/ph_maps)  -> concrete_moss_grey_diff_1k.jpg (22 % saturation)

Outputs (1024 x 1024 RGBA, alpha = coverage, four 512 px cells; a sprig grows up from the bottom centre of its cell):
  Foliage/cedar_spray/cedar_spray_diff_a.png    cells 0-2 sugi sprays (rope-like shoots), cell 3 = bronzed spray
  Foliage/bamboo_spray/bamboo_spray_diff_a.png  cells 0-2 bamboo branchlets with leaf fans, cell 3 = yellowing branchlet
  Foliage/susuki_tuft/susuki_tuft_diff_a.png    cells 0-2 dry susuki (pampas grass) clumps with plumes, cell 3 = low dry grass
Cell index: 0 = top-left, 1 = top-right, 2 = bottom-left, 3 = bottom-right (image space). Prints the alpha coverage of
every cell and the linear-space mean colour of the grey concrete (ExternalMats.PH needs it).
"""
import math, os, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

imp, out = sys.argv[1], sys.argv[2]
RND = random.Random(20261124)
CELL, SS = 512, 2
BIG = CELL * SS


# ----------------------------------------------------------------------------- helpers (same as prep_tex_r5.py)
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


def walk(pts, step):
    """Points every `step` px along a polyline: (point, heading, fraction along)."""
    seg = [math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]) for i in range(len(pts) - 1)]
    total = sum(seg) or 1.0
    res, acc, i, s = [], 0.0, 0, 0.0
    while s <= total and i < len(seg):
        while i < len(seg) - 1 and acc + seg[i] < s:
            acc += seg[i]
            i += 1
        k = 0.0 if seg[i] == 0 else (s - acc) / seg[i]
        p = (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * k, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * k)
        res.append((p, math.atan2(pts[i + 1][0] - pts[i][0], -(pts[i + 1][1] - pts[i][1])), s / total))
        s += step
    return res


def col8(c, a=255):
    return tuple(int(min(255, max(0, v * 255))) for v in c) + (a,)


def finish(cells, path, fill):
    """Downsample the supersampled cells, lay them out 2 x 2, bleed the average colour under transparent pixels."""
    atlas = Image.new("RGBA", (CELL * 2, CELL * 2), (0, 0, 0, 0))
    for i, c in enumerate(cells):
        small = c.resize((CELL, CELL), Image.LANCZOS)
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
    cov = [float((al[(i // 2) * CELL:(i // 2 + 1) * CELL, (i % 2) * CELL:(i % 2 + 1) * CELL] > .38).mean()) for i in range(4)]
    print(f"{os.path.basename(path)}: coverage per cell " + " ".join(f"{c:.2f}" for c in cov))


def palette(rgb, mask, lo=.15, hi=.9, n=400):
    pal = rgb[mask]
    pal = pal[np.argsort(pal.sum(1))][int(len(pal) * lo):int(len(pal) * hi)]
    return pal[:: max(1, len(pal) // n)]


# ----------------------------------------------------------------------------- sugi (Cryptomeria) sprays
def cedar_atlas():
    d0 = os.path.join(imp, "r5", "ph_maps", "pine_sapling_small")
    diff = np.asarray(Image.open(os.path.join(d0, "pine_sapling_small_twig_diff_1k.jpg")).convert("RGB"), dtype=np.float32) / 255.0
    alpha = np.asarray(Image.open(os.path.join(d0, "pine_sapling_small_twig_alpha_1k.png")).convert("L"), dtype=np.float32) / 255.0
    sub = (slice(580, 1010), slice(650, 1015))
    pal = palette(diff[sub], alpha[sub] > .85)
    print(f"sugi: {len(pal)} needle colours, mean {pal.mean(0).round(3)}")

    def col(dead, t, shade):
        # sugi is darker and bluer than the black pine; shoot tips lighter
        c = pal[RND.randrange(len(pal))] * np.array([.50, .80, .64], np.float32) * RND.uniform(.72, 1.05) * (.6 + .5 * t) * shade
        if RND.random() < dead:
            l = float(c.sum()) / 3
            c = np.array([.56, .31, .15], np.float32) * (l / .2) * RND.uniform(.75, 1.1)   # winter bronze / dead red-brown
        return col8(c)

    def cord(d, pts, w, dead, shade):
        """A rope-like sugi shoot: a dark core with short awl needles pointing forward on both sides."""
        for i in range(len(pts) - 1):
            t = i / (len(pts) - 1)
            d.line([pts[i], pts[i + 1]], fill=col(dead * .5, .15, shade * .8), width=max(2, round(w * (1 - .45 * t))))
        for p, a, t in walk(pts, 4.0):
            ww = w * (1 - .4 * t)
            for side in (-1, 1):
                ang = a + side * RND.uniform(.4, .85)
                ln = ww * RND.uniform(1.1, 1.55)
                q = (p[0] + math.sin(ang) * ln, p[1] - math.cos(ang) * ln)
                d.line([p, q], fill=col(dead, t, shade), width=max(2, round(ww * .34)))

    def spray(lean, dead, shade=1.0):
        im = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        main = twig_path((BIG / 2, BIG - 14), lean, BIG * .78, RND.uniform(-.3, .3), 16)
        shoots = [(main, 27)]
        nside = 16
        for j in range(nside):
            t = .06 + .88 * j / (nside - 1)
            i = min(len(main) - 1, int(t * (len(main) - 1)))
            side = -1 if j % 2 else 1
            env = math.sin(math.pi * min(1.0, (t + .12) / 1.12)) ** .6            # ovate frond, widest a third of the way up
            ln = BIG * .42 * env * RND.uniform(.85, 1.1)
            a = direction(main, i) + side * RND.uniform(.7, 1.1)
            pts = twig_path(main[i], a, ln, -side * RND.uniform(.2, .6), 9)
            shoots.append((pts, 24))
            for k in range(1, 9):
                tk = k / 9
                ii = min(len(pts) - 1, int(tk * (len(pts) - 1)))
                s2 = side if k % 2 else -side
                a2 = direction(pts, ii) + s2 * RND.uniform(.55, .95)
                l2 = ln * .5 * (1 - .45 * tk) * RND.uniform(.8, 1.15)
                p2 = twig_path(pts[ii], a2, l2, -s2 * RND.uniform(.1, .4), 6)
                shoots.append((p2, 19))
                for m in (1, 2, 3):                                               # short tertiary shoots fill the gaps
                    im_ = min(len(p2) - 1, int(m / 4 * (len(p2) - 1)))
                    s3 = 1 if (m + k) % 2 else -1
                    a3 = direction(p2, im_) + s3 * RND.uniform(.5, .9)
                    shoots.append((twig_path(p2[im_], a3, l2 * RND.uniform(.35, .55), -s3 * .2, 4), 15))
        for pts, w in sorted(shoots, key=lambda s: s[1]):
            cord(d, pts, w, dead, shade)
        return im

    cells = [spray(-.10, .04), spray(.14, .05), spray(0.0, .08, .9), spray(.06, .75, .9)]
    finish(cells, os.path.join(out, "Foliage", "cedar_spray", "cedar_spray_diff_a.png"), (.06, .10, .07))


# ----------------------------------------------------------------------------- bamboo branchlets
def bamboo_atlas():
    d0 = os.path.join(imp, "r5", "acg", "LeafSet022")
    rgb = np.asarray(Image.open(os.path.join(d0, "LeafSet022_1K-JPG_Color.jpg")).convert("RGB"), dtype=np.float32) / 255.0
    opa = np.asarray(Image.open(os.path.join(d0, "LeafSet022_1K-JPG_Opacity.jpg")).convert("L"), dtype=np.float32) / 255.0
    pal = palette(rgb, opa > .85)
    print(f"bamboo: {len(pal)} leaf colours, mean {pal.mean(0).round(3)}")
    twig_col = (88, 92, 52)

    def leaf_col(yellow):
        c = pal[RND.randrange(len(pal))]
        l = float(c.sum()) / 3
        r = RND.random()
        if r < yellow:
            c = np.array([.52, .50, .26], np.float32) * (l / .25) * RND.uniform(.65, .95)   # yellowing
        elif r < yellow * 1.4:
            c = np.array([.46, .33, .17], np.float32) * (l / .25) * RND.uniform(.7, 1.0)    # dead, brown
        else:
            c = c * np.array([.78, .92, .46], np.float32) * RND.uniform(.75, 1.08)           # bamboo yellow-green
        return col8(c)

    def leaf(d, base, ang, ln, wd, c):
        """Lanceolate bamboo leaf: short stalk, widest at 28 %, long pointed tip, slightly curved."""
        curve, n = RND.uniform(-.3, .3), 14
        left, right, mid, p, a = [], [], [], base, ang
        for i in range(n + 1):
            s = i / n
            if i:
                a += curve / n
                p = (p[0] + math.sin(a) * ln / n, p[1] - math.cos(a) * ln / n)
            wv = .1 if s < .06 else (.1 + .9 * ((s - .06) / .22) ** .7 if s < .28 else (1 - (s - .28) / .72) ** 1.1)
            h = wd * .5 * wv
            nx, ny = math.cos(a), math.sin(a)
            left.append((p[0] - nx * h, p[1] - ny * h))
            right.append((p[0] + nx * h, p[1] + ny * h))
            mid.append(p)
        d.polygon(left + right[::-1], fill=c)
        d.line(mid[1:-2], fill=tuple(min(255, int(v * 1.2)) for v in c[:3]) + (255,), width=2)

    def branchlet(lean, yellow, keep=1.0):
        im = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        main = twig_path((BIG / 2, BIG - 14), lean, BIG * .58, RND.uniform(-.5, .5), 10)
        twigs = [main]
        for j, t in enumerate((.16, .28, .4, .52, .62, .72, .82, .9)):
            i = min(len(main) - 1, int(t * (len(main) - 1)))
            side = 1 if j % 2 else -1
            a = direction(main, i) + side * RND.uniform(.45, 1.0)
            twigs.append(twig_path(main[i], a, BIG * RND.uniform(.16, .28), -side * RND.uniform(0, .4), 6))
        for pts in twigs:
            draw_twig(d, pts, 7 if pts is main else 5, 3, twig_col)
        items = []
        for pts in twigs:
            # a fan at the tip and a smaller one two thirds along
            for frac, count in ((1.0, RND.randint(7, 11)), (.66, RND.randint(4, 6)), (.36, RND.randint(2, 4))):
                i = min(len(pts) - 1, int(frac * (len(pts) - 1)))
                da = direction(pts, i)
                for _ in range(count):
                    if RND.random() > keep:
                        continue
                    ln = BIG * RND.uniform(.2, .32)
                    items.append((pts[i], da + RND.uniform(-1.6, 1.6), ln, ln * RND.uniform(.11, .15)))
        RND.shuffle(items)
        for p, a, ln, wd in items:
            leaf(d, p, a, ln, wd, leaf_col(yellow))
        return im

    cells = [branchlet(-.25, .04), branchlet(.3, .05), branchlet(0.0, .06), branchlet(.1, .4, .8)]
    finish(cells, os.path.join(out, "Foliage", "bamboo_spray", "bamboo_spray_diff_a.png"), (.12, .15, .06))


# ----------------------------------------------------------------------------- susuki (Miscanthus) clumps
def susuki_atlas():
    d0 = os.path.join(imp, "r6", "ph_maps", "withered_grass")
    rgb = np.asarray(Image.open(os.path.join(d0, "withered_grass_diff_1k.jpg")).convert("RGB"), dtype=np.float32) / 255.0
    lum = rgb.sum(-1)
    pal = palette(rgb, lum > np.percentile(lum, 55), .2, .95)
    print(f"susuki: {len(pal)} straw colours, mean {pal.mean(0).round(3)}")

    def straw(green):
        c = pal[RND.randrange(len(pal))] * RND.uniform(.8, 1.15) * np.array([1.05, 1.0, .9], np.float32)
        if RND.random() < green:
            c = c * .55 + np.array([.20, .23, .13], np.float32)                             # a few still grey-green
        return col8(c)

    def blade(d, base, ang, ln, wd, bend, c):
        """A long thin leaf arching outwards: polygon tapering from wd to a point."""
        n, p, a, left, right = 12, base, ang, [], []
        for i in range(n + 1):
            s = i / n
            if i:
                a += bend / n
                p = (p[0] + math.sin(a) * ln / n, p[1] - math.cos(a) * ln / n)
            h = wd * .5 * (1 - s) ** .8 + .6
            nx, ny = math.cos(a), math.sin(a)
            left.append((p[0] - nx * h, p[1] - ny * h))
            right.append((p[0] + nx * h, p[1] + ny * h))
        d.polygon(left + right[::-1], fill=c)

    def plume(d, top, ang, ln):
        """Feathery flower head: fine drooping strands off a thin axis, pale silver-beige."""
        axis = twig_path(top, ang, ln, RND.uniform(.3, .7) * (1 if ang >= 0 else -1), 8)
        for i in range(len(axis) - 1):
            d.line([axis[i], axis[i + 1]], fill=(196, 182, 152, 255), width=3)
        for p, a, t in walk(axis, 5.0):
            for side in (-1, 1):
                if RND.random() < .25:
                    continue
                sa = a + side * RND.uniform(.25, .7) + .35                                    # strands lean down-wind
                sl = ln * RND.uniform(.18, .34) * (1 - .5 * t)
                q = (p[0] + math.sin(sa) * sl, p[1] - math.cos(sa) * sl)
                v = RND.uniform(.82, 1.0)
                d.line([p, q], fill=(int(214 * v), int(200 * v), int(172 * v), 255), width=2)

    def tuft(height, plumes, blades, green):
        im = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        items = []
        for _ in range(blades):
            ang = max(-1.35, min(1.35, RND.gauss(0, .5)))
            ln = BIG * RND.uniform(.35, .82) * height
            bend = (1 if ang >= 0 else -1) * RND.uniform(.3, 1.2) * (.4 + abs(ang))
            items.append((abs(ang), (BIG / 2 + RND.uniform(-46, 46), BIG - 12), ang, ln, RND.uniform(9, 16), bend))
        items.sort(key=lambda b: -b[0])                                                      # outer blades behind
        for _, base, ang, ln, wd, bend in items:
            blade(d, base, ang, ln, wd, bend, straw(green))
        for _ in range(plumes):
            ang = RND.uniform(-.32, .32)
            ln = BIG * RND.uniform(.74, .93) * height
            stem = twig_path((BIG / 2 + RND.uniform(-30, 30), BIG - 12), ang, ln, RND.uniform(-.15, .15), 10)
            for i in range(len(stem) - 1):
                d.line([stem[i], stem[i + 1]], fill=straw(0), width=4)
            plume(d, stem[-1], direction(stem, len(stem) - 1) + RND.uniform(-.2, .2), BIG * RND.uniform(.13, .2))
        return im

    cells = [tuft(1.0, 6, 115, .15), tuft(.95, 4, 125, .2), tuft(.9, 8, 105, .1), tuft(.55, 0, 150, .3)]
    finish(cells, os.path.join(out, "Foliage", "susuki_tuft", "susuki_tuft_diff_a.png"), (.30, .27, .20))


# ----------------------------------------------------------------------------- grey concrete
def concrete_grey():
    src = os.path.join(imp, "r6", "ph_maps", "concrete_moss", "concrete_moss_diff_1k.jpg")
    a = np.asarray(Image.open(src).convert("RGB"), dtype=np.float32) / 255.0
    lum = (a * np.array([.299, .587, .114], np.float32)).sum(-1, keepdims=True)
    g = np.clip(lum + (a - lum) * .22, 0, 1) * np.array([1.0, .99, .965], np.float32)
    dst = os.path.join(out, "PolyHaven", "concrete_moss", "concrete_moss_grey_diff_1k.jpg")
    Image.fromarray((g * 255 + .5).astype(np.uint8), "RGB").save(dst, quality=92)
    q = np.asarray(Image.open(dst).convert("RGB"), dtype=np.float32) / 255.0
    lin = np.where(q <= .04045, q / 12.92, ((q + .055) / 1.055) ** 2.4).reshape(-1, 3).mean(0)
    print(f"concrete_moss_grey_diff_1k.jpg: linear mean {lin[0]:.4f} {lin[1]:.4f} {lin[2]:.4f}")


cedar_atlas()
bamboo_atlas()
susuki_atlas()
concrete_grey()
