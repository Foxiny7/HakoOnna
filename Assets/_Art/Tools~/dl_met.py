"""Download the approved public-domain (CC0) paintings from The Met's Open Access images.
usage: dl_met.py <out dir> <objectID>:<image file stem> ...

Only images.metmuseum.org originals; the object record must say isPublicDomain = true.
"""
import json, os, sys, urllib.request

out = sys.argv[1]
UA = {"User-Agent": "image-catalog-script/1.0"}
API = "https://collectionapi.metmuseum.org/public/collection/v1/objects/"
total = 0


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180) as r:
        return r.read()


os.makedirs(out, exist_ok=True)
for spec in sys.argv[2:]:
    oid, stem = spec.split(":")
    rec = json.loads(get(API + oid))
    if not rec.get("isPublicDomain"):
        raise SystemExit(f"{oid} is not public domain")
    url = f"https://images.metmuseum.org/CRDImages/as/original/{stem}.jpg"
    if url not in [rec.get("primaryImage")] + rec.get("additionalImages", []):
        raise SystemExit(f"{oid}: {stem} is not one of the object's images")
    blob = get(url)
    if blob[:3] != b"\xff\xd8\xff":
        raise SystemExit(f"{oid}: not a JPEG")
    dst = os.path.join(out, f"{oid}_{stem}.jpg")
    open(dst, "wb").write(blob)
    json.dump({k: rec.get(k) for k in ("objectID", "title", "artistDisplayName", "objectDate", "medium",
                                        "isPublicDomain", "objectURL", "primaryImage")},
              open(dst[:-4] + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    total += len(blob)
    print(f"ok {os.path.basename(dst)} {len(blob) / 1e6:.2f} MB  {rec.get('title')}")
print(f"TOTAL {total / 1e6:.1f} MB")
