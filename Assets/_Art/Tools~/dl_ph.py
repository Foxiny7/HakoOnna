"""Download the approved Poly Haven maps (diff, nor_gl, arm as JPG) and verify md5.
usage: dl_ph.py <catalog dir with files_<id>.json> <out dir> id:res ..."""
import hashlib, json, os, sys, urllib.request

cat, out = sys.argv[1], sys.argv[2]
UA = {"User-Agent": "texture-catalog-script/1.0"}
total = 0
for spec in sys.argv[3:]:
    aid, res = spec.split(":")
    data = json.load(open(os.path.join(cat, f"files_{aid}.json"), encoding="utf-8"))
    d = os.path.join(out, aid)
    os.makedirs(d, exist_ok=True)
    for key in ("Diffuse", "nor_gl", "arm"):
        m = data[key][res]["jpg"]
        url = m["url"]
        if not url.startswith("https://dl.polyhaven.org/"):
            raise SystemExit(f"unexpected host: {url}")
        dst = os.path.join(d, os.path.basename(url))
        if not (os.path.exists(dst) and hashlib.md5(open(dst, "rb").read()).hexdigest() == m["md5"]):
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
                blob = r.read()
            if hashlib.md5(blob).hexdigest() != m["md5"]:
                raise SystemExit(f"md5 mismatch: {url}")
            open(dst, "wb").write(blob)
        total += os.path.getsize(dst)
        print(f"ok {os.path.basename(dst)} {os.path.getsize(dst) / 1e6:.2f} MB")
print(f"TOTAL {total / 1e6:.1f} MB")
