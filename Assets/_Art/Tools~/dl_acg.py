"""Download approved ambientCG 1K-JPG sets and extract only the texture maps.
The catalog JSONs come from ambientcg.com/api/v2/full_json (include=downloadData).
Checks: link host is ambientcg.com, final URL is https, byte size matches the catalog,
zip members are flat file names matching <id>_1K-JPG_<Map>.jpg (no paths, no traversal), each < 64 MB.
usage: dl_acg.py <zip dir> <out dir> <catalog.json> [catalog.json ...] -- id [id ...]"""
import json, os, re, sys, urllib.parse, urllib.request, zipfile

args = sys.argv[1:]
sep = args.index("--")
zdir, out, cats, ids = args[0], args[1], args[2:sep], args[sep + 1:]
UA = {"User-Agent": "texture-catalog-script/1.0"}
MAPS = ("Color", "NormalGL", "Roughness", "Metalness", "AmbientOcclusion", "Opacity", "Displacement")

links = {}
for c in cats:
    for lst in json.load(open(c, encoding="utf-8")).values():
        for a in lst:
            for folder in a.get("downloadFolders", {}).values():
                for dl in folder["downloadFiletypeCategories"].get("zip", {}).get("downloads", []):
                    if dl["attribute"] == "1K-JPG":
                        links.setdefault(a["assetId"], (dl["downloadLink"], dl["size"]))

os.makedirs(zdir, exist_ok=True)
total = 0
for aid in ids:
    if aid not in links:
        raise SystemExit(f"{aid}: not in catalog")
    url, size = links[aid]
    if urllib.parse.urlsplit(url).netloc != "ambientcg.com" or not url.startswith("https://"):
        raise SystemExit(f"unexpected link: {url}")
    zp = os.path.join(zdir, f"{aid}_1K-JPG.zip")
    if not (os.path.exists(zp) and os.path.getsize(zp) == size):
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180) as r:
            final = r.geturl()
            blob = r.read()
        if not final.startswith("https://"):
            raise SystemExit(f"non-https redirect: {final}")
        if len(blob) != size:
            raise SystemExit(f"{aid}: size {len(blob)} != catalog {size} (from {final})")
        open(zp, "wb").write(blob)
        print(f"got {aid} {size / 1e6:.2f} MB via {urllib.parse.urlsplit(final).netloc}")
    total += size
    pat = re.compile(rf"^{re.escape(aid)}_1K-JPG_({'|'.join(MAPS)})\.jpg$")
    d = os.path.join(out, aid)
    os.makedirs(d, exist_ok=True)
    with zipfile.ZipFile(zp) as z:
        for info in z.infolist():
            name = info.filename
            if info.is_dir() or "/" in name or "\\" in name or ".." in name or not pat.match(name):
                continue
            if info.file_size > 64 * 1024 * 1024:
                raise SystemExit(f"{aid}: member too large {name}")
            with z.open(info) as src, open(os.path.join(d, name), "wb") as dst:
                dst.write(src.read())
            print(f"  {name} {info.file_size / 1e6:.2f} MB")
print(f"TOTAL zips {total / 1e6:.1f} MB")
