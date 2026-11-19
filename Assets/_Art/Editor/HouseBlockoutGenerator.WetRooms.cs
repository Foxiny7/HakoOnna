using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第三遍补家具之前，按新尺寸直接搭的房间内隔断（真实尺寸，不放大）：
    /// 便所东北角的厕所隔间（门向外开着，马桶在里面）；
    /// 風呂用一道隔墙分成南边的脱衣处和北边的浴室（磨砂玻璃拉门收在门洞旁）。
    /// 隔断用 Blocker 挡住，并登记到 c.placed / c.hard，后面补的家具会避开。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        static void RoomFixtures(Kit k, FillCtx c)
        {
            if (c.room.id == "Benjo") BenjoStall(k, c);
            else if (c.room.id == "Furo") FuroPartition(k, c);
        }

        /// <summary>木板隔断：芯板 + 两面板缝 + 踢脚 + 顶框。</summary>
        static void BoardPartition(Kit k, float x0, float x1, float z0, float z1, float y0, float h, Material face)
        {
            bool alongZ = z1 - z0 > x1 - x0;
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, len = alongZ ? z1 - z0 : x1 - x0, th = alongZ ? x1 - x0 : z1 - z0;
            k.Bb(face, cx, y0, cz, x1 - x0, h, z1 - z0);
            for (float s = .15f; s < len - .05f; s += .15f)
                foreach (float f in new[] { -1f, 1f })
                {
                    if (alongZ) k.Bb(D.BeamDark, cx + f * (th / 2 + .001f), y0 + .1f, z0 + s, .002f, h - .15f, .008f);
                    else k.Bb(D.BeamDark, x0 + s, y0 + .1f, cz + f * (th / 2 + .001f), .008f, h - .15f, .002f);
                }
            float ex = alongZ ? .012f : 0f, ez = alongZ ? 0f : .012f;
            k.Bb(D.BeamDark, cx, y0, cz, x1 - x0 + ex, .1f, z1 - z0 + ez);
            k.Bb(D.TrimMid, cx, y0 + h - .045f, cz, x1 - x0 + 2 * ex, .045f, z1 - z0 + 2 * ez);
        }

        /// <summary>便所：东北角的隔间把马桶围起来，门洞在隔间南板的西头，门扇向外开到 90°。</summary>
        static void BenjoStall(Kit k, FillCtx c)
        {
            var r = c.room.r;
            float fl = c.fl, H = 2.05f, xe = c.inner.xMax, zn = c.inner.yMax;
            float xw = r.xMax - 1.31f, zs = r.yMax - 1.87f;   // 西板、南板的中线（避开北墙的小窗）
            float d0 = xw + .05f, d1 = d0 + .68f;           // 门洞
            k.Push(Vector3.zero);
            BoardPartition(k, xw - .02f, xw + .02f, zs - .02f, zn, fl, H, D.TrimMid);
            BoardPartition(k, d1 + .05f, xe, zs - .02f, zs + .02f, fl, H, D.TrimMid);
            // 门框；门楣上面一小段纸格子
            k.Bb(D.TrimMid, xw + .025f, fl, zs, .05f, H, .05f);
            k.Bb(D.TrimMid, d1 + .025f, fl, zs, .05f, H, .05f);
            k.Bb(D.TrimMid, (d0 + d1) / 2, fl + 1.86f, zs, d1 - d0, .05f, .05f);
            k.Bb(D.Paper, (d0 + d1) / 2, fl + 1.91f, zs, d1 - d0, H - 1.91f, .008f);
            for (float x = d0 + .06f; x < d1 - .03f; x += .06f) k.Bb(D.TrimMid, x, fl + 1.91f, zs, .012f, H - 1.91f, .02f);
            k.Bb(D.TrimLight, (d0 + d1) / 2, fl, zs, d1 - d0, .012f, .06f);
            // 门扇：门轴在西框，向外开到 90°
            float lx = d0 + .018f, lz1 = zs - .05f, lz0 = lz1 - .68f, lc = (lz0 + lz1) / 2;
            k.Bb(D.TrimLight, lx, fl + .03f, lc, .03f, 1.8f, .68f);
            foreach (float f in new[] { -1f, 1f })
            {
                float fx = lx + f * .017f;
                k.Bb(D.TrimMid, fx, fl + .03f, lz0 + .025f, .005f, 1.8f, .05f);
                k.Bb(D.TrimMid, fx, fl + .03f, lz1 - .025f, .005f, 1.8f, .05f);
                k.Bb(D.TrimMid, fx, fl + .03f, lc, .005f, .07f, .68f);
                k.Bb(D.TrimMid, fx, fl + 1.76f, lc, .005f, .07f, .68f);
                k.Bb(D.Paper, fx, fl + 1.4f, lc, .004f, .26f, .42f);
                for (float z = lc - .18f; z < lc + .2f; z += .06f) k.Bb(D.TrimMid, fx + f * .002f, fl + 1.4f, z, .004f, .26f, .01f);
                k.Bb(D.Brass, lx + f * .03f, fl + .9f, lz0 + .08f, .025f, .04f, .045f);
            }
            k.Pop();
            Blocker("Benjo_StallWallW", new Vector3(xw, fl, (zs - .02f + zn) / 2), 0, new Vector3(.06f, H + .02f, zn - zs + .02f));
            Blocker("Benjo_StallWallS", new Vector3((d1 + xe) / 2, fl, zs), 0, new Vector3(xe - d1, H, .06f));
            Blocker("Benjo_StallDoor", new Vector3(lx, fl, lc), 0, new Vector3(.05f, 1.9f, lz1 - lz0));
            c.placed.Add(new Bounds(new Vector3((xw + xe) / 2, fl + 1.1f, (zs + zn) / 2), new Vector3(xe - xw + .1f, 2.2f, zn - zs + .1f)));
            // 门扇和门洞前面留空
            c.hard.Add(new Bounds(new Vector3((xw + d1) / 2, fl + 1f, zs - .55f), new Vector3(d1 - xw + .5f, 2.4f, 1.1f)));
        }

        /// <summary>
        /// 風呂：东西向一道到顶的隔墙，北边是浴室（1.2 m 瓷砖墙裙），南边是脱衣处（木墙裙）；
        /// 门洞上有门框，两扇磨砂玻璃拉门收在门洞西边；浴室里另挂一盏灯。浴室不再补家具。
        /// </summary>
        static void FuroPartition(Kit k, FillCtx c)
        {
            var r = c.room.r;
            float fl = c.fl, ceil = c.ceil, zc = r.yMax - 2.52f, t = .05f;
            float x0 = c.inner.xMin, x1 = c.inner.xMax;
            float d0 = r.xMin + 3.44f, d1 = d0 + 1.6f, head = fl + DoorTop;
            k.Push(Vector3.zero);
            foreach (var (a, b) in new[] { (x0, d0), (d1, x1) })
            {
                k.Bb(P.Plaster, (a + b) / 2, fl, zc, b - a, ceil - fl, 2 * t);
                k.Bb(D.BathTile, (a + b) / 2, fl, zc + t + .005f, b - a, 1.2f, .01f);
                k.Bb(D.TrimLight, (a + b) / 2, fl + 1.2f, zc + t + .008f, b - a, .025f, .016f);
                k.Bb(D.Wainscot, (a + b) / 2, fl, zc - t - .006f, b - a, .9f, .012f);
                k.Bb(D.TrimMid, (a + b) / 2, fl + .87f, zc - t - .012f, b - a, .03f, .024f);
            }
            k.Bb(P.Plaster, (d0 + d1) / 2, head, zc, d1 - d0, ceil - head, 2 * t);
            k.Bb(D.TrimMid, d0 + .03f, fl, zc, .06f, head - fl, 2 * t + .03f);
            k.Bb(D.TrimMid, d1 - .03f, fl, zc, .06f, head - fl, 2 * t + .03f);
            k.Bb(D.TrimMid, (d0 + d1) / 2, head - .06f, zc, d1 - d0, .06f, 2 * t + .03f);
            k.Bb(D.TrimLight, (d0 + d1) / 2, fl, zc, d1 - d0 - .12f, .015f, 2 * t + .02f);
            // 两扇磨砂玻璃拉门，叠着收在门洞西边
            for (int i = 0; i < 2; i++)
            {
                float lz = zc - t - .045f - i * .035f, la = d0 - .84f + i * .03f, lb = la + .8f, lw = .8f, lh = head - fl - .08f, y0 = fl + .02f;
                float lcx = (la + lb) / 2;
                k.Bb(D.TrimMid, la + .022f, y0, lz, .045f, lh, .03f);
                k.Bb(D.TrimMid, lb - .022f, y0, lz, .045f, lh, .03f);
                k.Bb(D.TrimMid, lcx, y0, lz, lw, .1f, .03f);
                k.Bb(D.TrimMid, lcx, y0 + lh - .05f, lz, lw, .05f, .03f);
                k.Bb(D.LampGlass, lcx, y0 + .1f, lz, lw - .09f, lh - .15f, .006f);
                foreach (float y in new[] { .8f, 1.4f }) k.Bb(D.TrimMid, lcx, y0 + y, lz, lw - .09f, .02f, .026f);
                k.Bb(D.Iron, lb - .07f, y0 + .95f, lz - .016f, .025f, .1f, .004f);
            }
            k.Pop();
            PendantLamp(k, (x0 + x1) / 2 + 1.0f, (zc + r.yMax) / 2, ceil, .5f, 0);
            Blocker("Furo_PartitionW", new Vector3((x0 + d0) / 2, fl, zc - .05f), 0, new Vector3(d0 - x0, ceil - fl, 2 * t + .1f));
            Blocker("Furo_PartitionE", new Vector3((d1 + x1) / 2, fl, zc), 0, new Vector3(x1 - d1, ceil - fl, 2 * t));
            c.placed.Add(new Bounds(new Vector3((x0 + x1) / 2, (fl + ceil) / 2, zc - .05f), new Vector3(x1 - x0, ceil - fl, 2 * t + .1f)));
            // 浴室整块不补家具；门洞前留空
            c.hard.Add(new Bounds(new Vector3(r.center.x, (fl + ceil) / 2, (zc + r.yMax) / 2), new Vector3(r.width + .2f, ceil - fl + .4f, r.yMax - zc + .2f)));
            c.hard.Add(new Bounds(new Vector3((d0 + d1) / 2, fl + 1f, zc - .8f), new Vector3(d1 - d0 + .6f, 2.4f, 1.6f)));
        }
    }
}
