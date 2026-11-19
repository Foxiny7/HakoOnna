"""Make the 'aged' diffuse maps: the Poly Haven originals are too orange for this house.
usage: python -I age_wood.py <PolyHaven dir>
Reads <id>/<id>_diff_1k.jpg, writes <id>/<id>_diff_aged.jpg (run before make_textures.py, which rotates the cedar map).
These are the values used on 2026-11-19; the 1k originals were deleted afterwards."""
import os, sys
from PIL import Image, ImageEnhance

root = sys.argv[1]
for aid, sat, bri in (("japanese_cedar_planks", .42, .85),
                      ("wood_cabinet_worn_long", .5, 1.15),
                      ("wood_table_worn", .7, 1.05)):
    src = os.path.join(root, aid, f"{aid}_diff_1k.jpg")
    dst = os.path.join(root, aid, f"{aid}_diff_aged.jpg")
    im = Image.open(src).convert("RGB")
    im = ImageEnhance.Brightness(ImageEnhance.Color(im).enhance(sat)).enhance(bri)
    im.save(dst, quality=92)
    print(dst, im.size)
