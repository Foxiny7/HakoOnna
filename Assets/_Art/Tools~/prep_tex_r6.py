"""Round 6 (outskirts): stage the four Poly Haven maps + the sky HDRI under a mirror of the project tree.
usage: prep_tex_r6.py <Import~/r6 dir> <stage root (mirrors the project root)>
  maps:  <id>_diff_1k.jpg, <id>_nor_gl_1k.jpg copied; <id>_mask.png from arm (R metal, G AO, B 0, A 1-rough), <=512
  sky:   kloppenheim_07_puresky_4k.hdr copied as-is
prints the linear-space average colour of each diffuse (ExternalMats.PH needs it)
"""
import os, shutil, sys
import numpy as np
from PIL import Image, ImageChops

src, stage = sys.argv[1], sys.argv[2]
ext = os.path.join(stage, "Assets", "_Art", "Textures", "External", "PolyHaven")
MASK_MAX = 512


def lin(c):
    c = c / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


for aid in ("asphalt_02", "withered_grass", "rusty_corrugated_iron", "concrete_moss"):
    s = os.path.join(src, "ph_maps", aid)
    d = os.path.join(ext, aid)
    os.makedirs(d, exist_ok=True)
    for f in (f"{aid}_diff_1k.jpg", f"{aid}_nor_gl_1k.jpg"):
        shutil.copyfile(os.path.join(s, f), os.path.join(d, f))
    a = Image.open(os.path.join(s, f"{aid}_arm_1k.jpg")).convert("RGB")
    ao, rough, metal = a.split()
    if max(a.size) > MASK_MAX:
        size = (MASK_MAX, round(a.size[1] * MASK_MAX / a.size[0]))
        ao, rough, metal = (im.resize(size, Image.LANCZOS) for im in (ao, rough, metal))
    Image.merge("RGBA", (metal, ao, Image.new("L", rough.size, 0), ImageChops.invert(rough))).save(os.path.join(d, f"{aid}_mask.png"), optimize=True)
    px = np.asarray(Image.open(os.path.join(s, f"{aid}_diff_1k.jpg")).convert("RGB"), dtype=np.float64)
    m = lin(px).reshape(-1, 3).mean(axis=0)
    r = np.asarray(rough, dtype=np.float64).mean() / 255
    mt = np.asarray(metal, dtype=np.float64).mean() / 255
    print(f"{aid}: avgLin ({m[0]:.4f}f, {m[1]:.4f}f, {m[2]:.4f}f)  rough {r:.2f} metal {mt:.2f}  files {sorted(os.listdir(d))}")

sky = os.path.join(ext, "kloppenheim_07_puresky")
os.makedirs(sky, exist_ok=True)
shutil.copyfile(os.path.join(src, "hdri", "kloppenheim_07_puresky_4k.hdr"), os.path.join(sky, "kloppenheim_07_puresky_4k.hdr"))
print("sky", sorted(os.listdir(sky)))
