"""Round 5 (interior): two drawn decals for props that have no CC0 source.
usage: make_props_r5.py <Assets/_Art/Textures/Decals dir>

T_ClockDial.png   512 x 512, a yellowed paper clock face (Roman numerals, minute track, two winding holes); alpha = the round face
T_GobanGrid.png   1024 x 1024, the 19 x 19 lines and nine star points of a go board on transparent; lines run from 5 % to 95 %
"""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

out = sys.argv[1]
RNG = np.random.default_rng(20261122)


def smooth_noise(px, cells, octaves=3):
    acc = np.zeros((px, px), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        n = max(2, cells * 2 ** o)
        g = RNG.random((n, n)).astype(np.float32)
        im = Image.fromarray((g * 255).astype(np.uint8), "L").resize((px, px), Image.BICUBIC)
        acc += amp * np.asarray(im, dtype=np.float32) / 255.0
        tot += amp
        amp *= .5
    return acc / tot


def numeral(text, h):
    """Roman numeral drawn with strokes and serifs; returns an L image (white = ink), tight around the glyphs."""
    w_i, w_v, gap, t = h * .2, h * .52, h * .07, max(3, round(h * .12))
    thin = max(2, round(h * .05))
    widths = [w_i if c == "I" else w_v for c in text]
    total = sum(widths) + gap * (len(text) - 1)
    im = Image.new("L", (int(total + h * .3), int(h * 1.2)), 0)
    d = ImageDraw.Draw(im)
    x, y0, y1 = h * .15, h * .1, h * 1.1
    for c, w in zip(text, widths):
        if c == "I":
            d.line([(x + w / 2, y0), (x + w / 2, y1)], fill=255, width=t)
        elif c == "V":
            d.line([(x, y0), (x + w / 2, y1)], fill=255, width=t)
            d.line([(x + w, y0), (x + w / 2, y1)], fill=255, width=thin)
        else:
            d.line([(x, y0), (x + w, y1)], fill=255, width=t)
            d.line([(x + w, y0), (x, y1)], fill=255, width=thin)
        x += w + gap
    # one long serif bar top and bottom ties the strokes together, the way old dials are drawn
    d.line([(h * .08, y0), (total + h * .22, y0)], fill=255, width=thin + 1)
    d.line([(h * .08, y1), (total + h * .22, y1)], fill=255, width=thin + 1)
    return im


def clock_dial():
    S, px = 4, 512
    big = px * S
    c = big / 2
    ink = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(ink)
    R = big * .49
    for r, w in ((.93, 5), (.80, 3)):
        d.ellipse([c - R * r, c - R * r, c + R * r, c + R * r], outline=255, width=w * S // 2)
    for i in range(60):
        a = math.radians(i * 6)
        r0, r1 = (.80, .93)
        w = (7 if i % 5 == 0 else 2) * S // 2
        d.line([(c + math.sin(a) * R * r0, c - math.cos(a) * R * r0), (c + math.sin(a) * R * r1, c - math.cos(a) * R * r1)], fill=255, width=w)
    names = ["XII", "I", "II", "III", "IIII", "V", "VI", "VII", "VIII", "IX", "X", "XI"]
    for k, t in enumerate(names):
        g = numeral(t, big * .105)
        ang = k * 30
        g = g.rotate(-ang, Image.BICUBIC, expand=True)
        a = math.radians(ang)
        x, y = c + math.sin(a) * R * .62, c - math.cos(a) * R * .62
        ink.paste(Image.new("L", g.size, 255), (round(x - g.size[0] / 2), round(y - g.size[1] / 2)), g)
    # centre boss ring, two winding holes, a small maker's cartouche
    d.ellipse([c - R * .05, c - R * .05, c + R * .05, c + R * .05], outline=255, width=2 * S)
    for sx in (-1, 1):
        x, y = c + sx * R * .27, c + R * .2
        d.ellipse([x - R * .035, y - R * .035, x + R * .035, y + R * .035], fill=255)
    d.rounded_rectangle([c - R * .16, c - R * .36, c + R * .16, c - R * .25], radius=R * .03, outline=255, width=S)
    for i in range(5):
        x = c - R * .11 + i * R * .055
        d.line([(x, c - R * .33), (x + R * .02, c - R * .28)], fill=255, width=S)
    ink = ink.resize((px, px), Image.LANCZOS)
    k = np.asarray(ink, dtype=np.float32) / 255.0

    paper = np.array([.80, .74, .60], np.float32)[None, None, :] * (.88 + .24 * smooth_noise(px, 5)[..., None])
    stain = np.clip((smooth_noise(px, 3) - .52) * 5, 0, 1)[..., None]
    paper = paper * (1 - stain * .28) * np.array([1.0, .96, .86], np.float32) ** stain
    yy, xx = np.mgrid[0:px, 0:px].astype(np.float32)
    rr = np.hypot(xx - px / 2 + .5, yy - px / 2 + .5) / (px / 2)
    paper *= (1 - .25 * np.clip((rr - .55) / .45, 0, 1) ** 2)[..., None]       # browner toward the rim
    fox = (RNG.random((px, px)) > .9985).astype(np.float32)
    fox = np.asarray(Image.fromarray((fox * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.6)), dtype=np.float32) / 255.0
    paper *= (1 - np.clip(fox * 6, 0, 1) * .45)[..., None]
    worn = .78 + .22 * smooth_noise(px, 9)                                       # the ink has rubbed thin in places
    rgb = paper * (1 - k[..., None] * worn[..., None] * .93)
    alpha = np.clip((.985 - rr) * px / 4, 0, 1)
    img = np.concatenate([np.clip(rgb, 0, 1), alpha[..., None]], axis=-1)
    Image.fromarray((img * 255 + .5).astype(np.uint8), "RGBA").save(os.path.join(out, "T_ClockDial.png"), optimize=True)


def goban_grid():
    px = 1024
    a = Image.new("L", (px, px), 0)
    d = ImageDraw.Draw(a)
    lo, hi = px * .05, px * .95
    step = (hi - lo) / 18
    for i in range(19):
        v = lo + i * step
        d.line([(lo, v), (hi, v)], fill=235, width=3)
        d.line([(v, lo), (v, hi)], fill=235, width=3)
    for i in (3, 9, 15):
        for j in (3, 9, 15):
            x, y = lo + i * step, lo + j * step
            d.ellipse([x - 7, y - 7, x + 7, y + 7], fill=245)
    al = np.asarray(a, dtype=np.float32) / 255.0
    al *= .72 + .28 * smooth_noise(px, 12)                                       # lacquer lines worn by decades of stones
    img = np.concatenate([np.full((px, px, 3), .035, np.float32), al[..., None]], axis=-1)
    Image.fromarray((img * 255 + .5).astype(np.uint8), "RGBA").save(os.path.join(out, "T_GobanGrid.png"), optimize=True)


clock_dial()
goban_grid()
print("wrote T_ClockDial.png, T_GobanGrid.png")
