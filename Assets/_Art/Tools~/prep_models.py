"""Round 3: turn the downloaded CC0 models / paintings into project-ready files (staged, not yet imported).
usage: prep_models.py <poly haven model dl dir> <met dl dir> <out dir>

PolyHaven/<id>/<id>.fbx                     the 1K FBX as downloaded
PolyHaven/<id>/<set>_diff_1k.jpg            copied; sets with an alpha / opacity map -> <set>_diff_a.png (RGBA)
PolyHaven/<id>/<set>_nor_gl_1k.jpg          copied
PolyHaven/<id>/<set>_mask.png (<=512)       R = metal (arm.B), G = AO (arm.R), B = 0, A = smoothness (1 - arm.G)
Met/<objectID>.jpg (+ .json)                longest side <= 2048
"""
import json, os, shutil, sys
from PIL import Image, ImageChops

ph_dir, met_dir, out = sys.argv[1], sys.argv[2], sys.argv[3]
MASK_MAX, MET_MAX = 512, 2048
SUFFIXES = ("_nor_gl", "_diff", "_arm", "_alpha", "_opacity")


def split(fname):
    stem = fname.rsplit(".", 1)[0]
    if stem.endswith("_1k"):
        stem = stem[:-3]
    for s in SUFFIXES:
        if stem.endswith(s):
            return stem[: -len(s)], s[1:]
    return stem, None


def mask(arm_path, dst):
    ao, rough, metal = Image.open(arm_path).convert("RGB").split()
    w, h = rough.size
    if max(w, h) > MASK_MAX:
        k = MASK_MAX / max(w, h)
        size = (max(1, round(w * k)), max(1, round(h * k)))
        ao, rough, metal = (im.resize(size, Image.LANCZOS) for im in (ao, rough, metal))
    Image.merge("RGBA", (metal, ao, Image.new("L", rough.size, 0), ImageChops.invert(rough))).save(dst, optimize=True)


for aid in sorted(os.listdir(ph_dir)):
    src = os.path.join(ph_dir, aid)
    if not os.path.isdir(src):
        continue
    dst = os.path.join(out, "PolyHaven", aid)
    os.makedirs(dst, exist_ok=True)
    maps = {}
    for f in os.listdir(src):
        if f.endswith(".fbx"):
            shutil.copyfile(os.path.join(src, f), os.path.join(dst, aid + ".fbx"))
            continue
        s, kind = split(f)
        maps.setdefault(s, {})[kind] = os.path.join(src, f)
    for s, m in maps.items():
        alpha = m.get("alpha") or m.get("opacity")
        if alpha is None and "_" in s:  # e.g. nettle_plant_dry_diff uses nettle_plant_alpha
            base = maps.get(s.rsplit("_", 1)[0], {})
            alpha = base.get("alpha") or base.get("opacity") if "diff" in m else None
        if "diff" in m:
            if alpha:
                rgb = Image.open(m["diff"]).convert("RGB")
                a = Image.open(alpha).convert("L").resize(rgb.size)
                Image.merge("RGBA", (*rgb.split(), a)).save(os.path.join(dst, f"{s}_diff_a.png"), optimize=True)
            else:
                shutil.copyfile(m["diff"], os.path.join(dst, f"{s}_diff_1k.jpg"))
        if "nor_gl" in m:
            shutil.copyfile(m["nor_gl"], os.path.join(dst, f"{s}_nor_gl_1k.jpg"))
        if "arm" in m:
            mask(m["arm"], os.path.join(dst, f"{s}_mask.png"))
    print("ph", aid, sorted(os.listdir(dst)))

md = os.path.join(out, "Met")
os.makedirs(md, exist_ok=True)
for f in sorted(os.listdir(met_dir)):
    if not f.endswith(".jpg"):
        continue
    oid = f.split("_", 1)[0]
    im = Image.open(os.path.join(met_dir, f)).convert("RGB")
    if max(im.size) > MET_MAX:
        k = MET_MAX / max(im.size)
        im = im.resize((round(im.size[0] * k), round(im.size[1] * k)), Image.LANCZOS)
    im.save(os.path.join(md, oid + ".jpg"), quality=90)
    shutil.copyfile(os.path.join(met_dir, f[:-4] + ".json"), os.path.join(md, oid + ".json"))
    print("met", oid, im.size)
