"""Copy the approved CC0 maps into the Unity project and build the derived textures.
usage: prep_tex.py <polyhaven dl dir> <ambientcg extract root> <project External dir>

PolyHaven:  <id>_diff_*.jpg, <id>_nor_gl_*.jpg copied as-is;
            <id>_mask.png (<=1K): R = metal (arm.B), G = AO (arm.R), B = 0, A = smoothness (1 - arm.G)
ambientCG:  Color / NormalGL copied; mask from Roughness (+AO);
            Leaking decals -> one RGBA png (RGB = Color, A = Opacity)
"""
import os, shutil, sys
from PIL import Image, ImageChops

ph_dir, acg_dir, out_root = sys.argv[1], sys.argv[2], sys.argv[3]
MASK_MAX = 512


def save_mask(metal, ao, rough, dst):
    w, h = rough.size
    if max(w, h) > MASK_MAX:
        k = MASK_MAX / max(w, h)
        size = (max(1, round(w * k)), max(1, round(h * k)))
        metal, ao, rough = (im.resize(size, Image.LANCZOS) for im in (metal, ao, rough))
    smooth = ImageChops.invert(rough)
    blue = Image.new("L", rough.size, 0)
    Image.merge("RGBA", (metal, ao, blue, smooth)).save(dst, optimize=True)


def ph(aid):
    src = os.path.join(ph_dir, aid)
    dst = os.path.join(out_root, "PolyHaven", aid)
    os.makedirs(dst, exist_ok=True)
    arm = None
    for f in sorted(os.listdir(src)):
        p = os.path.join(src, f)
        if "_arm_" in f:
            arm = p
        elif "_diff_" in f or "_nor_gl_" in f:
            shutil.copyfile(p, os.path.join(dst, f))
    a = Image.open(arm).convert("RGB")
    ao, rough, metal = a.split()
    save_mask(metal, ao, rough, os.path.join(dst, f"{aid}_mask.png"))
    print("ph", aid, sorted(os.listdir(dst)))


def acg_surface(aid):
    src = os.path.join(acg_dir, f"x_{aid}")
    dst = os.path.join(out_root, "ambientCG", aid)
    os.makedirs(dst, exist_ok=True)
    files = os.listdir(src)
    pick = lambda key: next(os.path.join(src, f) for f in files if f.endswith(f"_{key}.jpg"))
    for key in ("Color", "NormalGL"):
        shutil.copyfile(pick(key), os.path.join(dst, f"{aid}_{key}.jpg"))
    rough = Image.open(pick("Roughness")).convert("L")
    try:
        ao = Image.open(pick("AmbientOcclusion")).convert("L").resize(rough.size)
    except StopIteration:
        ao = Image.new("L", rough.size, 255)
    save_mask(Image.new("L", rough.size, 0), ao, rough, os.path.join(dst, f"{aid}_mask.png"))
    print("acg", aid, sorted(os.listdir(dst)))


def acg_decal(aid):
    src = os.path.join(acg_dir, f"x_{aid}")
    dst = os.path.join(out_root, "ambientCG", aid)
    os.makedirs(dst, exist_ok=True)
    files = os.listdir(src)
    pick = lambda key: next(os.path.join(src, f) for f in files if f.endswith(f"_{key}.jpg"))
    col = Image.open(pick("Color")).convert("RGB")
    op = Image.open(pick("Opacity")).convert("L").resize(col.size)
    r, g, b = col.split()
    Image.merge("RGBA", (r, g, b, op)).save(os.path.join(dst, f"{aid}_decal.png"), optimize=True)
    print("decal", aid, col.size, sorted(os.listdir(dst)))


for aid in sorted(os.listdir(ph_dir)):
    if os.path.isdir(os.path.join(ph_dir, aid)):
        ph(aid)
for aid in ("Tiles019", "Wicker010B"):
    acg_surface(aid)
for aid in ("Leaking001", "Leaking003", "Leaking005"):
    acg_decal(aid)
