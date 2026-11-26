"""审图用：把 MapSnapshot 记下的房间名 / 地点名叠到 3D 地图截图上，再加一行标题和颜色说明。

游戏里的字是玩法用 OnGUI 写的，相机拍不到，所以 MapSnapshot 每张图旁边存了一个同名 .json（字的位置照
NetworkHider.Views.cs 的写法算好）。这个脚本只给人看图用，不进游戏。

用法：python -I Assets/_Art/Tools~/map_labels.py [Docs/screenshots/detail]
每个 map_*.json 生成一张 map_*_labeled.png。
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

# 房间名、地点名是日文：游ゴシック；标题和颜色说明是简体中文：微软雅黑（游ゴシック没有简体字）
FONT = "C:/Windows/Fonts/YuGothM.ttc"
FONT_BOLD = "C:/Windows/Fonts/YuGothB.ttc"
FONT_CN = "C:/Windows/Fonts/msyh.ttc"
FONT_CN_BOLD = "C:/Windows/Fonts/msyhbd.ttc"

TEXT = {"room": (232, 232, 232), "here": (255, 132, 118), "landmark": (255, 255, 255)}


def rgb(r, g, b):
    return tuple(round(c * 255) for c in (r, g, b))


# 和 HouseMapProxy / HouseBlockoutGenerator.MapExplorer.cs / NetworkHider.Views.cs 里的颜色一致
LEGEND_EXPLORER = [
    (rgb(.95, .8, .25), "容器：没开过"),
    (rgb(.22, .62, .3), "容器：开过（规则待定）"),
    (rgb(.86, .24, .2), "你在这里"),
    (rgb(.95, .95, .9), "出口"),
    (rgb(.25, .8, .9), "金庫"),
    (rgb(.78, .5, .98), "仏壇"),
    (rgb(.6, .5, .3), "竹篱笆"),
    (rgb(.3, .62, 1), "楼梯"),
]
LEGEND_GHOST = [
    (rgb(1, 0, 0), "箱女所在"),
    (rgb(1, .92, .016), "同房间"),
    (rgb(0, 1, 0), "隔墙"),
    (rgb(0, 1, 1), "楼梯 / 后院"),
    (rgb(.5, .5, .5), "够不着"),
]


def draw_text(dr, xy, text, font, fill):
    x, y = xy
    dr.text((x + 1, y + 1), text, font=font, fill=(0, 0, 0))
    dr.text((x, y), text, font=font, fill=fill)


def main():
    folder = Path(sys.argv[1] if len(sys.argv) > 1 else "Docs/screenshots/detail")
    f_room = ImageFont.truetype(FONT, 17)
    f_mark = ImageFont.truetype(FONT_BOLD, 18)
    f_title = ImageFont.truetype(FONT_CN_BOLD, 21)
    f_legend = ImageFont.truetype(FONT_CN, 15)
    done = 0
    for js in sorted(folder.glob("map_*.json")):
        meta = json.loads(js.read_text(encoding="utf-8"))
        png = js.with_suffix(".png")
        if not png.exists():
            continue
        im = Image.open(png).convert("RGB")
        dr = ImageDraw.Draw(im)
        for lab in meta["labels"]:
            font = f_room if lab["kind"] == "room" else f_mark
            draw_text(dr, (lab["x"], lab["y"]), lab["text"], font, TEXT.get(lab["kind"], (255, 255, 255)))
        # 标题
        dr.rectangle([0, 0, im.width, 40], fill=(0, 0, 0))
        dr.text((14, 7), meta.get("title", js.stem), font=f_title, fill=(240, 240, 240))
        # 颜色说明（左下角）
        legend = LEGEND_GHOST if "ghost" in js.stem else LEGEND_EXPLORER
        x0, y0 = 14, im.height - 14 - 26 * len(legend)
        dr.rectangle([x0 - 8, y0 - 8, x0 + 230, im.height - 6], fill=(0, 0, 0))
        for i, (col, text) in enumerate(legend):
            y = y0 + 26 * i
            dr.rectangle([x0, y + 3, x0 + 16, y + 19], fill=col)
            dr.text((x0 + 26, y), text, font=f_legend, fill=(225, 225, 225))
        out = js.with_name(js.stem + "_labeled.png")
        im.save(out)
        done += 1
        print(out)
    print(f"{done} labeled")


if __name__ == "__main__":
    main()
