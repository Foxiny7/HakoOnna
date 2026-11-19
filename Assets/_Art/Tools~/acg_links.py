"""Print the 1K-JPG zip download link for each ambientCG asset id found in the given catalog files.
usage: acg_links.py <ids comma separated> <json> ..."""
import json, sys

ids = set(sys.argv[1].split(","))
for path in sys.argv[2:]:
    for a in json.load(open(path, encoding="utf-8")).get("foundAssets", []):
        if a.get("assetId") not in ids:
            continue
        for cat in (a.get("downloadFolders") or {}).values():
            for fc in (cat.get("downloadFiletypeCategories") or {}).values():
                for d in fc.get("downloads", []):
                    if d.get("attribute") == "1K-JPG":
                        print(a["assetId"], d.get("size"), d.get("downloadLink") or d.get("rawLink"), d.get("fileName"))
