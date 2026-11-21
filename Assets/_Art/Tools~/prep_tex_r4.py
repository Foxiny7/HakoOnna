"""Round 4: build the project textures from the approved CC0 downloads (Poly Haven + ambientCG).
usage: prep_tex_r4.py <ph_textures dir> <acg dir> <out External dir>

Every output tiles seamlessly. All aging (stains, mildew, foxing, crazing) uses periodic noise.
Fabrics are composites of n x n source tiles so baked stains don't repeat every 27 cm:
  <name>_diff.jpg      albedo (plain fabrics are desaturated and tinted in Unity via _BaseColor)
  <name>_nor_gl.jpg    OpenGL normal, tiled the same way
  <name>_mask.png      R metal, G AO, B 0, A smoothness (<= 512 px)
Prints the linear average colour of every albedo (ExternalMats tints from it) and the tile size in metres.
"""
import os, sys
import numpy as np
from PIL import Image

ph, acg, out = sys.argv[1], sys.argv[2], sys.argv[3]
RNG = np.random.default_rng(20261120)
MASK_MAX = 512
report = []


# ----------------------------------------------------------------------------- helpers
def load(path, mode="RGB"):
    return np.asarray(Image.open(path).convert(mode), dtype=np.float32) / 255.0


def save_rgb(a, path, quality=92):
    Image.fromarray(np.clip(a * 255.0 + .5, 0, 255).astype(np.uint8), "RGB").save(path, quality=quality)


def resize(a, px):
    if a.shape[0] == px and a.shape[1] == px:
        return a
    mode = "RGB" if a.ndim == 3 else "L"
    im = Image.fromarray(np.clip(a * 255.0 + .5, 0, 255).astype(np.uint8), mode)
    return np.asarray(im.resize((px, px), Image.LANCZOS), dtype=np.float32) / 255.0


def tile(a, n, px):
    reps = (n, n, 1) if a.ndim == 3 else (n, n)
    return resize(np.tile(a, reps), px)


def srgb_to_lin(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def avg_lin(a):
    return srgb_to_lin(a).reshape(-1, 3).mean(0)


def periodic_noise(px, scale_px, seed_shift=0, octaves=1):
    """Seamless smooth noise in [0,1]: white noise low-passed in Fourier space (periodic by construction)."""
    acc = np.zeros((px, px), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        w = RNG.standard_normal((px, px)).astype(np.float32)
        f = np.fft.fft2(w)
        fy = np.fft.fftfreq(px)[:, None]
        fx = np.fft.fftfreq(px)[None, :]
        r = np.sqrt(fx * fx + fy * fy) * (scale_px / 2.0 ** o)
        f *= np.exp(-(r * r) * 4.0)
        n = np.real(np.fft.ifft2(f))
        n = (n - n.mean()) / (n.std() + 1e-8)
        acc += amp * n
        tot += amp
        amp *= .5
    acc /= tot
    return np.clip(.5 + .18 * acc, 0, 1)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def neutral(a, mean=.80, contrast=1.6):
    """Desaturate to luminance, keep the weave variation (a bit stronger), set the mean."""
    lum = a[..., 0] * .299 + a[..., 1] * .587 + a[..., 2] * .114
    lum = (lum - lum.mean()) * contrast + mean
    return np.repeat(np.clip(lum, 0, 1)[..., None], 3, axis=2)


def warp(n, amp_px, scale_px):
    """Domain warp (periodic, bilinear): makes stain outlines ragged instead of round blobs."""
    px = n.shape[0]
    wx, wy = periodic_noise(px, scale_px, octaves=2), periodic_noise(px, scale_px, octaves=2)
    yy, xx = np.mgrid[0:px, 0:px].astype(np.float32)
    x = (xx + (wx - .5) * 2 * amp_px) % px
    y = (yy + (wy - .5) * 2 * amp_px) % px
    x0, y0 = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - x0, y - y0
    x1, y1 = (x0 + 1) % px, (y0 + 1) % px
    x0, y0 = x0 % px, y0 % px
    return (n[y0, x0] * (1 - fx) + n[y0, x1] * fx) * (1 - fy) + (n[y1, x0] * (1 - fx) + n[y1, x1] * fx) * fy


def water_stains(a, px, count_scale, strength=1.0, tint=(.955, .915, .82), rim=(.80, .68, .50), coverage=.08):
    """
    Old water stains: barely yellowed inside, getting darker toward the edge (water carries the dirt outward),
    a thin brown tide line on the edge and a fainter second one just inside it. Outlines are domain-warped.
    """
    n = periodic_noise(px, scale_px=px / (3.0 * count_scale), octaves=3)
    n = warp(n, px / 70.0, px / 18.0)
    th = np.quantile(n, 1 - coverage)
    d = n - th
    inside = smooth(0, .003, d)
    edge = np.exp(-np.maximum(d, 0) / .025)
    ring = np.exp(-(d / .0032) ** 2) + .45 * np.exp(-((d - .02) / .0025) ** 2)
    ring *= .45 + .55 * periodic_noise(px, scale_px=px / 30.0)
    k_in = inside * (.35 + .65 * edge) * strength
    k_rim = ring * strength
    out = a.copy()
    for c in range(3):
        out[..., c] *= 1 - k_in * (1 - tint[c])
        out[..., c] *= 1 - k_rim * (1 - rim[c])
    return out


def mildew(a, px, strength=1.0, col=(.50, .52, .46)):
    """A few loose clusters of small grey-green speckles."""
    cluster = smooth(.66, .76, warp(periodic_noise(px, scale_px=px / 5.0, octaves=2), px / 40.0, px / 12.0))
    fine = periodic_noise(px, scale_px=px / 300.0)
    dots = smooth(.72, .78, fine) * cluster * strength * (.5 + .5 * periodic_noise(px, scale_px=px / 60.0))
    out = a.copy()
    for c in range(3):
        out[..., c] *= 1 - np.clip(dots, 0, 1) * (1 - col[c])
    return out


def mottle(a, px, amount=.07):
    m = periodic_noise(px, scale_px=px / 6.0, octaves=3)
    return a * (1 + (m[..., None] - .5) * 2 * amount)


def mask_from(rough, ao=None, metal=None, smooth_scale=1.0, size=None):
    size = size or min(MASK_MAX, rough.shape[0])
    rough = resize(rough, size)
    ao = np.ones_like(rough) if ao is None else resize(ao, size)
    metal = np.zeros_like(rough) if metal is None else resize(metal, size)
    sm = np.clip((1 - rough) * smooth_scale, 0, 1)
    rgba = np.stack([metal, ao, np.zeros_like(rough), sm], axis=2)
    return Image.fromarray(np.clip(rgba * 255 + .5, 0, 255).astype(np.uint8), "RGBA")


def ph_maps(aid, diff_name=None):
    d = os.path.join(ph, aid)
    diff = load(os.path.join(d, diff_name or f"{aid}_diff_1k.jpg"))
    nor = load(os.path.join(d, f"{aid}_nor_gl_1k.jpg"))
    arm = load(os.path.join(d, f"{aid}_arm_1k.jpg"))
    return diff, nor, arm


def acg_map(aid, key, mode="RGB", px=1024):
    """Some ambientCG sets are not square (Smear004 is 1024 x 779): everything is resampled to px x px."""
    p = os.path.join(acg, aid, f"{aid}_1K-JPG_{key}.jpg")
    if not os.path.exists(p):
        return None
    im = Image.open(p).convert(mode)
    if im.size != (px, px):
        im = im.resize((px, px), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def write_set(name, sub, albedo, normal, mask_img, metres, note):
    d = os.path.join(out, sub, name)
    os.makedirs(d, exist_ok=True)
    save_rgb(albedo, os.path.join(d, f"{name}_diff.jpg"))
    save_rgb(normal, os.path.join(d, f"{name}_nor_gl.jpg"), quality=95)
    mask_img.save(os.path.join(d, f"{name}_mask.png"), optimize=True)
    a = avg_lin(albedo)
    report.append(f"{name:18s} {metres:5.2f} m  avgLin {a[0]:.4f}f, {a[1]:.4f}f, {a[2]:.4f}f   {note}")


# ----------------------------------------------------------------------------- fabrics
def fabric(name, src, n, px, metres_src, colour=False, diff_name=None, stain=1.0, mold=1.0, mean=.80, note=""):
    diff, nor, arm = ph_maps(src, diff_name)
    alb = tile(diff, n, px)
    if not colour:
        alb = neutral(alb, mean)
    alb = mottle(alb, px, .06)
    alb = water_stains(alb, px, count_scale=n / 3.0, strength=stain)
    alb = mildew(alb, px, strength=mold)
    nrm = tile(nor, n, px)
    ao, rough, metal = arm[..., 0], arm[..., 1], arm[..., 2]
    mask = mask_from(tile(rough, n, px), tile(ao, n, px), None, 1.0)
    write_set(name, "PolyHaven", np.clip(alb, 0, 1), nrm, mask, metres_src * n, note)


def ticking():
    """Mattress ticking: aged linen with narrow blue-grey stripes along v, big stains (one tile = 1.9 m)."""
    n, px = 7, 2048
    diff, nor, arm = ph_maps("rough_linen")
    alb = neutral(tile(diff, n, px), .78, 1.3)
    x = (np.arange(px) + .5) / px
    period = 1.0 / 72.0                      # 72 stripe repeats per tile = 2.6 cm
    f = (x / period) % 1.0
    stripe = (smooth(.00, .02, f) - smooth(.16, .18, f)) * .85 + (smooth(.30, .31, f) - smooth(.36, .37, f)) * .6
    wob = periodic_noise(px, scale_px=px / 90.0)
    stripe = stripe[None, :] * (.85 + .3 * wob)
    col = np.array([.40, .46, .56], np.float32)
    alb = alb * (1 - stripe[..., None] * (1 - col))
    alb = mottle(alb, px, .08)
    # 床垫：一大片发黄的汗渍 / 尿渍（边上一道深色），再加几块小的水渍和霉点
    alb = water_stains(alb, px, count_scale=.9, strength=1.0, tint=(.86, .76, .55), rim=(.66, .52, .32), coverage=.14)
    alb = water_stains(alb, px, count_scale=2.2, strength=.9, coverage=.06)
    alb = mildew(alb, px, 1.3)
    mask = mask_from(tile(arm[..., 1], n, px), tile(arm[..., 0], n, px))
    write_set("ticking_aged", "PolyHaven", np.clip(alb, 0, 1), tile(nor, n, px), mask, .2707 * n, "mattress ticking")


# ----------------------------------------------------------------------------- hard surfaces
def ceramic():
    """Old glazed ceramic: white porcelain + faint grime + crazing lines (tile 0.5 m)."""
    px = 1024
    col = acg_map("Porcelain001", "Color")
    nor = acg_map("Porcelain001", "NormalGL")
    rough = acg_map("Porcelain001", "Roughness", "L")
    alb = np.clip(col * 0 + .86, 0, 1)
    alb = mottle(alb, px, .05)
    n1 = periodic_noise(px, scale_px=px / 24.0, octaves=2)
    n2 = periodic_noise(px, scale_px=px / 40.0, octaves=2)
    craze = np.maximum(np.exp(-((n1 - .5) / .004) ** 2), .7 * np.exp(-((n2 - .5) / .003) ** 2))
    grime = smooth(.55, .75, periodic_noise(px, scale_px=px / 4.0, octaves=3))
    dirt = np.array([.62, .55, .44], np.float32)
    alb = alb * (1 - (craze * .45 + grime * .25)[..., None] * (1 - dirt))
    r = np.clip(rough + grime * .25 + craze * .2, 0, 1)
    write_set("ceramic_aged", "ambientCG", alb, nor, mask_from(r), .5, "porcelain + crazing")


def mirror():
    """Old mirror: silvered (metal 1), smudges and fingerprints lower smoothness, a few desilvered spots."""
    px = 1024
    sm = acg_map("Smear004", "Color", "L")
    fp = acg_map("Fingerprints002", "Opacity", "L")
    if fp is None:
        fp = acg_map("Fingerprints002", "Color", "L")
    nor = acg_map("Smear004", "NormalGL")
    sp = warp(periodic_noise(px, scale_px=px / 16.0, octaves=3), px / 50.0, px / 10.0)
    spots = smooth(.77, .80, sp) * .9 + smooth(.72, .80, sp) * .25
    dirt = np.clip(sm * 1.1 + fp * .6, 0, 1)
    alb = np.repeat((.78 - .22 * dirt - .5 * spots)[..., None], 3, axis=2)
    alb[..., 2] *= .97
    metal = np.clip(1 - spots * .9 - dirt * .3, 0, 1)
    smooth_ = np.clip(.97 - .62 * dirt - .7 * spots, 0, 1)
    rgba = np.stack([resize(metal, 512), np.ones((512, 512), np.float32), np.zeros((512, 512), np.float32), resize(smooth_, 512)], 2)
    mask = Image.fromarray(np.clip(rgba * 255 + .5, 0, 255).astype(np.uint8), "RGBA")
    write_set("mirror_old", "ambientCG", alb, nor, mask, .6, "mirror smudges")


def smudge(name, base_col, smooth_hi, note):
    """Glossy dark / plastic surfaces: flat albedo, smears + fingerprints break up the gloss (tile 0.6 m)."""
    px = 1024
    sm = acg_map("Smear004", "Color", "L")
    fp = acg_map("Fingerprints002", "Opacity", "L")
    nor = acg_map("Smear004", "NormalGL")
    dirt = np.clip(sm * 1.3 + fp * .7, 0, 1)
    alb = np.full((px, px, 3), base_col, np.float32) * (1 + .25 * dirt[..., None])
    alb = mottle(alb, px, .05)
    s = np.clip(smooth_hi - .5 * dirt, 0, 1)
    write_set(name, "ambientCG", np.clip(alb, 0, 1), nor, mask_from(1 - s), .6, note)


def metal(name, aid, metres, note, rough_add=0.0):
    col = acg_map(aid, "Color")
    nor = acg_map(aid, "NormalGL")
    rough = np.clip(acg_map(aid, "Roughness", "L") + rough_add, 0, 1)
    met = acg_map(aid, "Metalness", "L")
    ao = acg_map(aid, "AmbientOcclusion", "L")
    write_set(name, "ambientCG", col, nor, mask_from(rough, ao, met), metres, note)


def paper(name, aid, metres, yellow, foxing, note):
    px = 1024
    col = acg_map(aid, "Color")
    nor = acg_map(aid, "NormalGL")
    rough = acg_map(aid, "Roughness", "L")
    alb = neutral(col, .82, 1.0) if yellow else col.copy()
    alb = mottle(alb, px, .06)
    if foxing:
        alb = water_stains(alb, px, count_scale=1.5, strength=.9, tint=(.93, .86, .70), rim=(.72, .58, .40), coverage=.07)
        alb = mildew(alb, px, .9, col=(.62, .48, .34))
    write_set(name, "ambientCG", np.clip(alb, 0, 1), nor, mask_from(rough), metres, note)


def ph_plain(aid, metres, note):
    diff, nor, arm = ph_maps(aid)
    mask = mask_from(arm[..., 1], arm[..., 0], arm[..., 2])
    write_set(aid, "PolyHaven", diff, nor, mask, metres, note)


# ----------------------------------------------------------------------------- run
fabric("linen_aged", "rough_linen", 5, 2048, .2707, note="sheets, dust sheets, white cotton")
fabric("crepe_aged", "crepe_satin", 3, 2048, .2632, stain=.8, mold=.6, note="chirimen silk (red, purple)")
fabric("velour_aged", "velour_velvet", 3, 2048, .2842, stain=.7, note="zabuton velvet")
fabric("terry_aged", "terry_cloth", 3, 2048, .3671, mean=.82, stain=1.1, mold=1.3, note="towels")
fabric("check_aged", "fabric_pattern_05", 2, 2048, .5, colour=True, diff_name="fabric_pattern_05_col_01_1k.jpg", note="window-pane check (green/blue)")
fabric("gingham_red_aged", "fabric_pattern_07", 2, 2048, .4, colour=True, diff_name="fabric_pattern_07_col_1_1k.jpg", note="red gingham")
fabric("gingham_green_aged", "fabric_pattern_07", 2, 2048, .4, colour=True, diff_name="fabric_pattern_07_col_03_1k.jpg", note="green gingham")
ticking()
ceramic()
mirror()
smudge("black_smudged", (.06, .06, .06), .86, "black gloss with smears")
smudge("plastic_smudged", (.80, .80, .80), .72, "plastic / milk glass (tinted)")
metal("enamel_chipped", "PaintedMetal012", .6, "white enamel, chips, rust")
metal("gold_dirty", "Metal048B", .5, "gold / brass")
metal("bronze_old", "Metal017", .6, "dark tarnished brass / bronze", .1)
paper("paper_white", "Paper003", .5, False, False, "creased white paper")
paper("paper_aged", "Paper006", .5, True, True, "yellowed old paper with foxing")
ph_plain("reed_roof_04", 2.5, "straw")
ph_plain("green_metal_rust", 1.0, "green painted metal")
ph_plain("rusty_metal_04", 2.0, "rusty sheet metal")
print("\n".join(report))
