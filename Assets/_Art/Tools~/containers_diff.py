"""Container placement guard: dump or diff the 45 container positions of House_Blockout.unity.

The container placements are signed off by the designer; after every regeneration the scene must show 0 changed.

usage:
  python -I containers_diff.py <scene.unity>                       list positions
  python -I containers_diff.py <scene.unity> --save <baseline.json> write a baseline
  python -I containers_diff.py <baseline.json | before.unity> <after.unity>   diff (exit code 1 if anything moved)
"""
import json
import math
import re
import sys

NAME_RE = re.compile(r"^[A-Za-z0-9]+_[A-Za-z]+_\d+$")


def load(path):
    if path.endswith(".json"):
        return {k: tuple(v) for k, v in json.load(open(path, encoding="utf-8")).items()}
    text = open(path, encoding="utf-8").read()
    docs = text.split("\n--- !u!")
    out = {}
    for d in docs:
        if not d.startswith("1001 "):
            continue
        mods = {}
        cur = None
        for line in d.splitlines():
            s = line.strip()
            if s.startswith("propertyPath:"):
                cur = s.split(":", 1)[1].strip()
            elif s.startswith("value:") and cur is not None:
                mods.setdefault(cur, s.split(":", 1)[1].strip())
                cur = None
        name = mods.get("m_Name")
        if not name or not NAME_RE.match(name):
            continue
        try:
            x = float(mods.get("m_LocalPosition.x", "0"))
            y = float(mods.get("m_LocalPosition.y", "0"))
            z = float(mods.get("m_LocalPosition.z", "0"))
            qy = float(mods.get("m_LocalRotation.y", "0"))
            qw = float(mods.get("m_LocalRotation.w", "1"))
        except ValueError:
            continue
        yaw = math.degrees(2 * math.atan2(qy, qw))
        out[name] = (x, y, z, yaw)
    return out


def main():
    a = load(sys.argv[1])
    if len(sys.argv) == 4 and sys.argv[2] == "--save":
        json.dump({k: [round(v, 4) for v in a[k]] for k in sorted(a)}, open(sys.argv[3], "w", encoding="utf-8"), indent=1)
        print("saved", len(a), "containers to", sys.argv[3])
        return
    if len(sys.argv) == 2:
        for k in sorted(a):
            x, y, z, yaw = a[k]
            print(f"{k:28s} ({x:7.2f}, {y:5.2f}, {z:7.2f}) yaw {yaw:6.1f}")
        print(len(a), "containers")
        return
    b = load(sys.argv[2])
    moved = 0
    for k in sorted(set(a) | set(b)):
        if k not in a or k not in b:
            print(f"{k:28s} {'missing before' if k not in a else 'missing after'}")
            moved += 1
            continue
        (x0, y0, z0, r0), (x1, y1, z1, r1) = a[k], b[k]
        dist = math.dist((x0, y0, z0), (x1, y1, z1))
        dyaw = (r1 - r0 + 180) % 360 - 180
        if dist > .01 or abs(dyaw) > 1:
            moved += 1
            print(f"{k:28s} ({x0:6.2f},{z0:6.2f}) -> ({x1:6.2f},{z1:6.2f})  moved {dist:5.2f} m  yaw {dyaw:+.0f}")
    print(f"{len(a)} before, {len(b)} after, {moved} changed")
    sys.exit(1 if moved else 0)


if __name__ == "__main__":
    main()
