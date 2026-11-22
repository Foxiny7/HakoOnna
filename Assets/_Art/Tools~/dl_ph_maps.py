"""Download only the 1K maps of one material set of a Poly Haven model (no FBX), md5-checked.
usage: dl_ph_maps.py <catalog dir with files_<id>.json> <out dir> id:set ...

Example: pine_sapling_small:twig fetches twig_diff / twig_nor_gl / twig_arm as JPG and twig_alpha as PNG.
"""
import hashlib, json, os, sys, urllib.request

cat, out = sys.argv[1], sys.argv[2]
UA = {"User-Agent": "model-catalog-script/1.0"}
total = 0
for spec in sys.argv[3:]:
    aid, mset = spec.split(":")
    data = json.load(open(os.path.join(cat, f"files_{aid}.json"), encoding="utf-8"))
    d = os.path.join(out, aid)
    os.makedirs(d, exist_ok=True)
    for kind, fmt in (("diff", "jpg"), ("nor_gl", "jpg"), ("arm", "jpg"), ("alpha", "png")):
        key = f"{mset}_{kind}"
        if key not in data:
            continue
        m = data[key]["1k"][fmt]
        url = m["url"]
        if not url.startswith("https://dl.polyhaven.org/"):
            raise SystemExit(f"unexpected host: {url}")
        dst = os.path.join(d, os.path.basename(url))
        if not (os.path.exists(dst) and hashlib.md5(open(dst, "rb").read()).hexdigest() == m["md5"]):
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180) as r:
                blob = r.read()
            if hashlib.md5(blob).hexdigest() != m["md5"]:
                raise SystemExit(f"md5 mismatch: {url}")
            open(dst, "wb").write(blob)
        total += os.path.getsize(dst)
        print(f"ok {os.path.basename(dst)} {os.path.getsize(dst) / 1e6:.2f} MB")
print(f"TOTAL {total / 1e6:.2f} MB")
