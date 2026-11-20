"""Download the approved Poly Haven models: the 1K FBX plus the 1K maps we use, md5-checked.
usage: dl_ph_models.py <catalog dir with files_<id>.json> <out dir> id ...

Maps: diffuse / nor_gl / arm as JPG, alpha / opacity as PNG, for every material set
(keys "Diffuse", "nor_gl", "arm", "Alpha" and the prefixed "<set>_diff", "<set>_nor_gl", ...).
The FBX's own texture bundle (EXR normals etc.) is skipped: prep_models.py builds the
URP textures from these maps instead.
"""
import hashlib, json, os, sys, urllib.request

cat, out = sys.argv[1], sys.argv[2]
UA = {"User-Agent": "model-catalog-script/1.0"}
KEEP = ("Diffuse", "nor_gl", "arm", "Alpha")
SUFFIX = ("_diff", "_nor_gl", "_arm", "_alpha", "_opacity")
total = 0


def fetch(url, md5, dst):
    global total
    if not url.startswith("https://dl.polyhaven.org/"):
        raise SystemExit(f"unexpected host: {url}")
    if not (os.path.exists(dst) and hashlib.md5(open(dst, "rb").read()).hexdigest() == md5):
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180) as r:
            blob = r.read()
        if hashlib.md5(blob).hexdigest() != md5:
            raise SystemExit(f"md5 mismatch: {url}")
        open(dst, "wb").write(blob)
    total += os.path.getsize(dst)
    print(f"ok {os.path.basename(dst)} {os.path.getsize(dst) / 1e6:.2f} MB")


for aid in sys.argv[3:]:
    data = json.load(open(os.path.join(cat, f"files_{aid}.json"), encoding="utf-8"))
    d = os.path.join(out, aid)
    os.makedirs(d, exist_ok=True)
    fbx = data["fbx"]["1k"]["fbx"]
    fetch(fbx["url"], fbx["md5"], os.path.join(d, os.path.basename(fbx["url"])))
    for key, by_res in data.items():
        if key in ("blend", "gltf", "usd", "fbx"):
            continue
        if not (key in KEEP or key.endswith(SUFFIX)):
            continue
        fmt = "png" if key.lower().endswith(("alpha", "opacity")) else "jpg"
        m = by_res["1k"][fmt]
        fetch(m["url"], m["md5"], os.path.join(d, os.path.basename(m["url"])))
print(f"TOTAL {total / 1e6:.1f} MB")
