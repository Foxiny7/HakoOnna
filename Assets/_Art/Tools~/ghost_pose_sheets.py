"""Contact sheets for ghost_pose_lib.py renders.

python -I ghost_pose_sheets.py <render_dir> <out_prefix>
  <out_prefix>_hiding.jpg   per container: player view | front | side, with the safe-zone report
  <out_prefix>_lunge.jpg    per container: hiding pose (player view) | lunge (player view) |
                            lunge with the camera pulled in to 0.2 m (art bible 4.3) | lunge side view
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

KINDS = ["Oshiire", "YofukuTansu", "ChaTansu", "GetaBako", "Tansu", "Nagamochi", "Tsuzura", "Komebitsu"]


def font():
    try:
        return ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 13)
    except Exception:
        return ImageFont.load_default()


def sheet(d, kinds, cols, caption, out, sc):
    kinds = [k for k in kinds if all(os.path.exists(os.path.join(d, p % k)) for p, _ in cols)]
    if not kinds:
        return
    W, H = int(sum(w for _, w in cols) * sc), int(540 * sc) + 20
    im = Image.new("RGB", (W, H * len(kinds)), (20, 20, 22))
    dr, f = ImageDraw.Draw(im), font()
    for i, k in enumerate(kinds):
        x = 0
        for pat, w in cols:
            src = Image.open(os.path.join(d, pat % k)).convert("RGB").resize((int(w * sc), int(540 * sc)), Image.LANCZOS)
            im.paste(src, (x, i * H + 20))
            x += int(w * sc)
        dr.text((6, i * H + 3), caption(k), fill=(230, 230, 230), font=f)
    im.save(out, quality=88)
    print("sheet", out, im.size)


def main():
    d, prefix = sys.argv[1], sys.argv[2]
    rep = json.load(open(os.path.join(d, "report.json"), encoding="utf-8"))

    def hide_cap(k):
        r = rep.get(k, {})
        worst = {a: v for a, v in r.get("worst_cm", {}).items() if v}
        return "%s | hiding | vertices outside the safe zone %s/%s %s" % (k, r.get("outside"), r.get("of"), worst or "")

    def lunge_cap(k):
        r = rep.get("Lunge_" + k, {})
        return "%s | lunge | eyes to camera %.2f m | hips in box %s | in boards %s (%.1f cm) %s" % (
            k, r.get("eye_cam_m", 0), r.get("hips_in_box"), r.get("in_boards"), r.get("in_boards_worst_cm", 0),
            [b for b, _ in r.get("in_boards_by_bone", [])[:3]])

    sheet(d, KINDS, (("%s_player.png", 960), ("%s_front.png", 540), ("%s_side.png", 540)), hide_cap,
          prefix + "_hiding.jpg", 0.6)
    sheet(d, KINDS, (("%s_player.png", 960), ("%s_lunge_player.png", 960), ("%s_lunge_climax.png", 960),
                     ("%s_lunge_side.png", 540)), lunge_cap, prefix + "_lunge.jpg", 0.5)


if __name__ == "__main__":
    main()
