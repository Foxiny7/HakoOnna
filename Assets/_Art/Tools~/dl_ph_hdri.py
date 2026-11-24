"""Download Poly Haven HDRIs (Radiance .hdr) at one resolution and verify md5.
usage: dl_ph_hdri.py <catalog dir with files_<id>.json> <out dir> id:res ...   (res = 1k / 2k / 4k / 8k)
"""
import hashlib, json, os, sys, urllib.request

cat, out = sys.argv[1], sys.argv[2]
UA = {"User-Agent": "hdri-catalog-script/1.0"}
total = 0
os.makedirs(out, exist_ok=True)
for spec in sys.argv[3:]:
    aid, res = spec.split(":")
    data = json.load(open(os.path.join(cat, f"files_{aid}.json"), encoding="utf-8"))
    m = data["hdri"][res]["hdr"]
    url = m["url"]
    if not url.startswith("https://dl.polyhaven.org/"):
        raise SystemExit(f"unexpected host: {url}")
    dst = os.path.join(out, os.path.basename(url))
    if not (os.path.exists(dst) and hashlib.md5(open(dst, "rb").read()).hexdigest() == m["md5"]):
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=300) as r:
            blob = r.read()
        if hashlib.md5(blob).hexdigest() != m["md5"]:
            raise SystemExit(f"md5 mismatch: {url}")
        open(dst, "wb").write(blob)
    total += os.path.getsize(dst)
    print(f"ok {os.path.basename(dst)} {os.path.getsize(dst) / 1e6:.2f} MB")
print(f"TOTAL {total / 1e6:.1f} MB")
