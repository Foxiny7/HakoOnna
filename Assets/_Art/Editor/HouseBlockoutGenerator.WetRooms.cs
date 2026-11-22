using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第三遍补家具之前，按新尺寸直接搭的房间内隔断和固定设备（第三轮重做）：
    /// <list type="bullet">
    /// <item>便所：一道到顶的隔墙分成前间（洗手器、扫除用具、架子）和里间（朝颜小便器、东北角隔间里的和式蹲便）。</item>
    /// <item>風呂：北边是浴室（隔墙不动）；南边再用一道隔墙分出西边的洗面所（昭和瓷砖洗脸台），
    /// 剩下的是脱衣所兼洗衣处（洗衣机、洗衣盆、毛巾架，长凳在第二遍）。</item>
    /// </list>
    /// 新隔墙的门洞不装门、挂暖帘（不会出现“打不开的门”）；门洞净宽约 1 m。
    /// 隔墙和设备按真实尺寸的设计再统一放大 FurnitureScale（和其他家具一致），挡块离墙、离别的挡块至少 1 cm。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        static void RoomFixtures(Kit k, FillCtx c)
        {
            var rng = new System.Random(StableHash(c.room.id) ^ 0x5A17);
            if (c.room.id == "Benjo") BenjoRooms(k, c, rng);
            else if (c.room.id == "Furo") FuroRooms(k, c, rng);
        }

        static bool HasFixtures(Room room) => room.id == "Benjo" || room.id == "Furo";

        // =====================================================================
        // 通用：隔墙、暖帘、固定设备
        // =====================================================================
        const float PartHalf = .05f; // 隔墙半厚

        /// <summary>
        /// 到顶的隔墙：alongX = 墙沿 x 方向（在 z = line 处），否则沿 z 方向（在 x = line 处）；[a, b] 是墙的范围，
        /// [d0, d1] 是门洞（d1 &lt;= d0 = 没有门洞）。两面 1.2 m 瓷砖墙裙（和房间的墙一致），门洞有门框、门槛，挂暖帘。
        /// 挡块分两段，伸进门框 6 cm（门框不会被人穿过去），两头缩回 blockA / blockB。
        /// </summary>
        static void Partition(Kit k, FillCtx c, System.Random rng, string name, bool alongX, float line, float a, float b,
            float d0, float d1, Material curtain, float blockA, float blockB)
        {
            const float t = PartHalf;
            float fl = c.fl, ceil = c.ceil, head = fl + DoorTop;
            var wall = WallMatFor(c.room, P);
            bool door = d1 > d0;
            void Box(Material m, float u0, float u1, float y0, float y1, float w0, float w1)
            {
                float u = (u0 + u1) / 2, y = (y0 + y1) / 2, w = (w0 + w1) / 2;
                if (alongX) k.B(m, u, y, line + w, u1 - u0, y1 - y0, w1 - w0);
                else k.B(m, line + w, y, u, w1 - w0, y1 - y0, u1 - u0);
            }
            k.Push(Vector3.zero);
            var segs = door ? new[] { (a, d0), (d1, b) } : new[] { (a, b) };
            foreach (var (s0, s1) in segs)
            {
                if (s1 - s0 < .02f) continue;
                Box(wall, s0, s1, fl, ceil, -t, t);
                Box(D.BathTile, s0, s1, fl, fl + 1.2f, t, t + .01f);
                Box(D.BathTile, s0, s1, fl, fl + 1.2f, -t - .01f, -t);
                Box(D.TrimLight, s0, s1, fl + 1.2f, fl + 1.225f, t, t + .016f);
                Box(D.TrimLight, s0, s1, fl + 1.2f, fl + 1.225f, -t - .016f, -t);
            }
            if (door)
            {
                Box(wall, d0, d1, head, ceil, -t, t);
                Box(D.TrimMid, d0, d0 + .06f, fl, head, -t - .015f, t + .015f);
                Box(D.TrimMid, d1 - .06f, d1, fl, head, -t - .015f, t + .015f);
                Box(D.TrimMid, d0, d1, head - .06f, head, -t - .015f, t + .015f);
                Box(D.TrimLight, d0 + .06f, d1 - .06f, fl, fl + .012f, -t - .01f, t + .01f);
            }
            k.Pop();
            if (door && curtain != null) Noren(k, rng, alongX, line, d0 + .06f, d1 - .06f, head - .06f, curtain);

            // 挡块
            float h = ceil - fl;
            var blocks = door ? new[] { (blockA, d0 + .06f), (d1 - .06f, blockB) } : new[] { (blockA, blockB) };
            int n = 0;
            foreach (var (s0, s1) in blocks)
            {
                if (s1 - s0 < .02f) continue;
                float u = (s0 + s1) / 2;
                var foot = alongX ? new Vector3(u, fl, line) : new Vector3(line, fl, u);
                var size = alongX ? new Vector3(s1 - s0, h, 2 * t + .02f) : new Vector3(2 * t + .02f, h, s1 - s0);
                Blocker($"{c.room.id}_{name}{(door ? (n == 0 ? "A" : "B") : "")}", foot, 0, size);
                c.placed.Add(new Bounds(foot + Vector3.up * h / 2, size));
                n++;
            }
        }

        /// <summary>门洞上挂的半暖簾：竹竿 + 两三幅布（一幅撕短了），中间一幅上有白色圆纹。[d0, d1] 是净宽，top = 竹竿高度。</summary>
        static void Noren(Kit k, System.Random rng, bool alongX, float line, float d0, float d1, float top, Material cloth)
        {
            float w = d1 - d0, cx = (d0 + d1) / 2;
            int n = w > .9f ? 3 : 2;
            const float gap = .015f, len = .48f;
            float pw = (w - gap * (n - 1)) / n;
            Vector3 At(float u, float y, float off = 0f) => alongX ? new Vector3(u, y, line + off) : new Vector3(line + off, y, u);
            k.Push(Vector3.zero);
            k.Cr(D.Bamboo, At(d0 - .02f, top), .012f, .012f, w + .04f, alongX ? AlongX : AlongZ, 8);
            int torn = rng.Next(n);
            for (int i = 0; i < n; i++)
            {
                float u = d0 + pw / 2 + i * (pw + gap);
                float l = i == torn ? len * Rf(rng, .45f, .65f) : len * Rf(rng, .95f, 1f);
                // 第三轮：布分上下两段，下段多摆一点，看起来是软的；下沿一圈手摸出来的灰
                float s1 = Rf(rng, -5f, 5f), s2 = s1 + Rf(rng, -8f, 8f), l1 = l * .62f, l2 = l - l1;
                Quaternion R1 = alongX ? Quaternion.Euler(s1, 0, 0) : Quaternion.Euler(0, 0, s1);
                Quaternion R2 = alongX ? Quaternion.Euler(s2, 0, 0) : Quaternion.Euler(0, 0, s2);
                var t0 = At(u, top - .015f);
                var c1 = t0 + R1 * new Vector3(0, -l1 / 2, 0);
                var t1 = t0 + R1 * new Vector3(0, -l1, 0);
                var c2 = t1 + R2 * new Vector3(0, -l2 / 2, 0);
                var cb = t1 + R2 * new Vector3(0, -l2 + .03f, 0);
                if (alongX)
                {
                    k.Br(cloth, c1.x, c1.y, c1.z, pw, l1 + .004f, .006f, s1, 0, 0);
                    k.Br(cloth, c2.x, c2.y, c2.z, pw, l2, .006f, s2, 0, 0);
                    k.Br(D.Ash, cb.x, cb.y, cb.z, pw + .001f, .06f, .0075f, s2, 0, 0);
                }
                else
                {
                    k.Br(cloth, c1.x, c1.y, c1.z, .006f, l1 + .004f, pw, 0, 0, s1);
                    k.Br(cloth, c2.x, c2.y, c2.z, .006f, l2, pw, 0, 0, s2);
                    k.Br(D.Ash, cb.x, cb.y, cb.z, .0075f, .06f, pw + .001f, 0, 0, s2);
                }
                // 竿上的布袢
                k.B(cloth, At(u, top).x, top, At(u, top).z, alongX ? pw * .9f : .03f, .035f, alongX ? .03f : pw * .9f);
                if (i == n / 2)
                {
                    // 白色家纹贴在上段两面（跟着上段的摆角）
                    var normal = R1 * (alongX ? Vector3.forward : Vector3.right);
                    var cc = t0 + R1 * new Vector3(0, -l1 * .62f, 0);
                    var axis = R1 * (alongX ? AlongZ : AlongX);
                    k.Cr(D.ClothWhite, cc + normal * .0031f, .065f, .065f, .0015f, axis, 16);
                    k.Cr(D.ClothWhite, cc - normal * .0046f, .065f, .065f, .0015f, axis, 16);
                }
            }
            k.Pop();
        }

        /// <summary>按真实尺寸设计、放大 FurnitureScale 摆的固定设备。foot = 墙脚点（局部原点），yaw 让局部 +z 朝房间。</summary>
        static readonly List<string> FixtureWarnings = new List<string>();

        static void FixtureAt(Kit k, FillCtx c, string name, Vector3 foot, float yaw, Vector3 block, System.Action draw)
        {
            float S = FurnitureScale;
            k.PushS(foot, new Vector3(0, yaw, 0), Vector3.one * S);
            draw();
            k.Pop();
            // 第四轮审图：洗衣机、毛巾、洗衣筐、洗手钵、洗脸台（正面偏 20° 看）
            float lookY = name switch { "Washer" => .75f, "TowelBar" => 1.05f, "Basket" => .3f, "HandBasin" => .8f, "Washstand" => .85f, "Urinal" => .6f, _ => -1f };
            if (lookY > 0f)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                ShotDir($"{c.room.id}_{name}", foot + q * new Vector3(0f, lookY * S, .3f * S), q * Quaternion.Euler(0f, 20f, 0f) * Vector3.forward,
                    name == "Basket" ? 1.5f : 1.8f, name == "Basket" ? .9f : .45f);
            }
            if (block.y <= 0f) return;
            var rot = Quaternion.Euler(0, yaw, 0);
            var size = block * S;
            var center = foot + rot * new Vector3(0, 0, .01f + size.z / 2); // 离墙 1 cm（测试要求家具碰撞体不碰墙）
            Blocker($"{c.room.id}_{name}", center, yaw, size);
            var world = new Bounds(center + Vector3.up * size.y / 2, Vector3.zero);
            foreach (var sx in new[] { -.5f, .5f })
                foreach (var sz in new[] { -.5f, .5f })
                    world.Encapsulate(center + rot * new Vector3(sx * size.x, 0, sz * size.z) + Vector3.up * size.y);
            world.Encapsulate(center);
            c.placed.Add(world);
            var shrunk = world;
            shrunk.Expand(-.02f);
            foreach (var o in c.occ)
                if (shrunk.Intersects(o)) { FixtureWarnings.Add($"{c.room.id}/{name} 和旧陈设 {o.center:F2} 重叠"); break; }
            for (int i = 0; i < c.hardFixed; i++)
                if (shrunk.Intersects(c.hard[i])) { FixtureWarnings.Add($"{c.room.id}/{name} 进了门口/容器正面留空区 {c.hard[i].center:F2}"); break; }
        }

        /// <summary>旧镜子（局部 xy 平面朝 +z，c = 镜子中心，贴墙）：木框、边上发黑的脱银斑、一层灰雾、一处放射状的裂纹。</summary>
        static void OldMirror(Kit k, System.Random rng, Vector3 c, float w, float h, Material frame)
        {
            const float fd = .02f, bw = .035f;
            k.B(frame, c.x, c.y + h / 2 - bw / 2, c.z + fd / 2, w, bw, fd);
            k.B(frame, c.x, c.y - h / 2 + bw / 2, c.z + fd / 2, w, bw, fd);
            k.B(frame, c.x - w / 2 + bw / 2, c.y, c.z + fd / 2, bw, h - 2 * bw, fd);
            k.B(frame, c.x + w / 2 - bw / 2, c.y, c.z + fd / 2, bw, h - 2 * bw, fd);
            float iw = w - 2 * bw, ih = h - 2 * bw, mz = c.z + .008f, sz = mz + .0022f;
            k.B(D.Mirror, c.x, c.y, mz, iw + .01f, ih + .01f, .004f);
            for (int i = 0; i < 9; i++)
            {
                float ex = Rf(rng, -1f, 1f), ey = Rf(rng, -1f, 1f);
                if (Mathf.Abs(ex) > Mathf.Abs(ey)) ex = Mathf.Sign(ex); else ey = Mathf.Sign(ey);
                float r = Rf(rng, .02f, .055f);
                k.Cr(i % 3 == 0 ? D.Ash : D.Black, new Vector3(c.x + ex * (iw / 2 - r * .3f), c.y + ey * (ih / 2 - r * .3f), sz), r, r, .0008f, AlongZ, 10);
            }
            PictureQuad(k, D.MirrorFog, new Vector3(c.x, c.y, sz + .0016f), iw, ih, new Rect(0, 0, 1, 1));
            var o = new Vector2(c.x + Rf(rng, -.25f, .25f) * iw, c.y + Rf(rng, -.25f, .25f) * ih);
            float a0 = Rf(rng, 0f, 60f);
            for (int i = 0; i < 6; i++)
            {
                float a = a0 + i * 60f + Rf(rng, -18f, 18f);
                var d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                float tx = Mathf.Abs(d.x) > 1e-3f ? ((d.x > 0 ? c.x + iw / 2 : c.x - iw / 2) - o.x) / d.x : 99f;
                float ty = Mathf.Abs(d.y) > 1e-3f ? ((d.y > 0 ? c.y + ih / 2 : c.y - ih / 2) - o.y) / d.y : 99f;
                float len = Mathf.Min(Rf(rng, .35f, 1.1f) * Mathf.Min(iw, ih) * .5f, Mathf.Min(tx, ty) - .005f);
                if (len < .02f) continue;
                var mid = o + d * len / 2;
                k.Br(D.Black, mid.x, mid.y, sz + .0026f, len, .0022f, .0006f, 0, 0, a);
            }
        }

        // =====================================================================
        // 便所
        // =====================================================================
        static void BenjoRooms(Kit k, FillCtx c, System.Random rng)
        {
            var r = c.room.r;
            float fl = c.fl, x0 = r.xMin + T, x1 = r.xMax - T;
            // 隔墙：前间 / 里间，门洞对着走廊的门（门在南墙正中）
            float zx = r.yMin + 3.98f, dc = r.center.x;
            Partition(k, c, rng, "CrossWall", true, zx, x0, x1, dc - .55f, dc + .55f, D.ClothWhite, c.inner.xMin, c.inner.xMax);
            BenjoStall(k, c);

            // ---- 前间 ----
            FixtureAt(k, c, "HandBasin", new Vector3(x0, fl, r.yMin + 2.38f), 90f, new Vector3(.5f, .9f, .36f), () => DrawHandBasin(k, rng));
            FixtureAt(k, c, "Cleaning", new Vector3(x1 - FillGap, fl, r.yMin + .73f), -90f, new Vector3(.75f, .45f, .45f), () => DrawCleaning(k, rng, true));
            FixtureAt(k, c, "Shelf", new Vector3(x1 - FillGap, fl, r.yMin + 2.63f), -90f, new Vector3(.6f, 1.2f, .38f), () => DrawShelf(k, rng, .6f, 1.2f, 3, 5));
            // 隔墙南面挂着一本旧日历（昭和的）
            k.Push(new Vector3(x0 + .75f, fl + 1.55f, zx - PartHalf - .012f), 180f);
            k.B(D.Iron, 0, .24f, .01f, .01f, .01f, .03f);
            k.B(D.Paper, 0, 0, .004f, .3f, .45f, .004f);
            k.B(D.ClothRed, 0, .19f, .0065f, .22f, .04f, .001f);
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 7; j++)
                    k.B(j == 0 ? D.ClothRed : D.Ink, -.12f + j * .04f, .1f - i * .055f, .0065f, .018f, .018f, .001f);
            k.Br(D.Paper, .1f, -.24f, .02f, .2f, .12f, .003f, -35f, 0, 6f); // 翻起来的一页
            k.Pop();
            BareBulb(k, new Vector3(dc, c.ceil, r.yMin + 2.0f));

            // ---- 里间 ----
            FixtureAt(k, c, "Urinal", new Vector3(x0, fl, r.yMin + 5.4f), 90f, new Vector3(.94f, 1.4f, .55f), () => DrawAsagao(k, rng));
            // 拖鞋：一只在门口，一只踢到了墙边
            k.PushS(new Vector3(dc - .2f, fl, zx + .45f), new Vector3(0, 8f, 0), Vector3.one * FurnitureScale);
            k.Bb(D.PlasticRed, -.065f, 0, 0, .1f, .02f, .25f);
            k.Br(D.PlasticRed, -.065f, .035f, .06f, .1f, .03f, .1f, -12f, 0, 0);
            k.Pop();
            k.PushS(new Vector3(dc + .55f, fl, zx + 1.05f), new Vector3(0, 0, 180f), Vector3.one * FurnitureScale);
            k.Bb(D.PlasticRed, 0, -.02f, 0, .1f, .02f, .25f);
            k.Pop();
            // 隔间里的和式蹲便：局部原点在隔间北墙脚中点，朝南
            float sw = r.xMax - 1.31f; // 隔间西板中线（BenjoStall）
            float sx = (sw + .03f + c.inner.xMax) / 2;
            float sideX = (sx - (sw + .03f)) / FurnitureScale;
            FixtureAt(k, c, "Washiki", new Vector3(sx, fl, r.yMax - T), 180f, Vector3.zero, () => DrawWashiki(k, rng, sideX));
            BareBulb(k, new Vector3(sx, c.ceil, r.yMax - .95f));
        }

        /// <summary>便所隔间的门（2026-11-19 用户定：做成 E 开关的门）。BenjoStall 记下位置，门扇在 BuildDoors 里做（BuildStallDoor）。</summary>
        sealed class StallDoorSpec
        {
            public Vector3 hinge;       // 门轴：门扇关着时外面（南）一面靠西门框的那条竖线，y = 地板
            public float width, y0, y1, thick;
            public Bounds opening;      // 门洞（世界坐标）
        }
        static StallDoorSpec StallDoor;

        /// <summary>
        /// 便所里间：东北角的隔间把蹲便围起来，门洞在隔间南板的西头。
        /// 门扇是能用 E 开关的铰链门（向外开 90°，默认开着），在 BuildDoors 里做；这里只做门框、门框上的半边合页。
        /// </summary>
        static void BenjoStall(Kit k, FillCtx c)
        {
            var r = c.room.r;
            float fl = c.fl, H = 2.05f, xe = c.inner.xMax, zn = c.inner.yMax;
            float xw = r.xMax - 1.31f, zs = r.yMax - 1.87f;   // 西板、南板的中线（避开北墙的小窗）
            float d0 = xw + .05f, d1 = d0 + .68f;           // 门洞
            k.Push(Vector3.zero);
            BoardPartition(k, xw - .02f, xw + .02f, zs - .02f, zn, fl, H, D.TrimMid);
            BoardPartition(k, d1 + .05f, xe, zs - .02f, zs + .02f, fl, H, D.TrimMid);
            k.Bb(D.TrimMid, xw + .025f, fl, zs, .05f, H, .05f);
            k.Bb(D.TrimMid, d1 + .025f, fl, zs, .05f, H, .05f);
            k.Bb(D.TrimMid, (d0 + d1) / 2, fl + 1.86f, zs, d1 - d0, .05f, .05f);
            k.Bb(D.Paper, (d0 + d1) / 2, fl + 1.91f, zs, d1 - d0, H - 1.91f, .008f);
            for (float x = d0 + .06f; x < d1 - .03f; x += .06f) k.Bb(D.TrimMid, x, fl + 1.91f, zs, .012f, H - 1.91f, .02f);
            k.Bb(D.TrimLight, (d0 + d1) / 2, fl, zs, d1 - d0, .012f, .06f);
            // 西门框南面的半边合页（另一半在门扇上，跟着门扇转）
            foreach (float hy in new[] { .28f, 1.53f })
                k.Bb(D.Iron, d0 - .012f, fl + hy, zs - .027f, .024f, .09f, .004f);
            k.Pop();
            Blocker("Benjo_StallWallW", new Vector3(xw, fl, (zs - .02f + zn) / 2), 0, new Vector3(.06f, H + .02f, zn - zs + .02f));
            Blocker("Benjo_StallWallS", new Vector3((d1 + xe) / 2, fl, zs), 0, new Vector3(xe - d1, H, .06f));
            // 门扇：关着时在门框里（南北居中，厚 3.2 cm），门轴在外面一侧，向外（南）转开
            StallDoor = new StallDoorSpec
            {
                hinge = new Vector3(d0 + .006f, fl, zs - .016f),
                width = d1 - d0 - .012f,
                y0 = fl + .025f,
                y1 = fl + 1.835f,
                thick = .032f,
                opening = new Bounds(new Vector3((d0 + d1) / 2, fl + .93f, zs), new Vector3(d1 - d0, 1.86f, 2 * T)),
            };
            c.placed.Add(new Bounds(new Vector3((xw + xe) / 2, fl + 1.1f, (zs + zn) / 2), new Vector3(xe - xw + .1f, 2.2f, zn - zs + .1f)));
            c.hard.Add(new Bounds(new Vector3((xw + d1) / 2, fl + 1f, zs - .55f), new Vector3(d1 - xw + .5f, 2.4f, 1.1f)));
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

        /// <summary>前间的洗手器：木背板、陶瓷小手盆（里面一圈污垢）、铜水龙头；上面一面小旧镜子，旁边木毛巾杆上搭着发黄的毛巾。</summary>
        static void DrawHandBasin(Kit k, System.Random rng)
        {
            k.Bb(D.TrimMid, 0, .42f, .01f, .5f, .52f, .02f);
            foreach (float sx in new[] { -.13f, .13f }) k.Br(D.TrimMid, sx, .52f, .09f, .022f, .18f, .022f, -42f, 0, 0);
            k.C(D.CeramicWhite, 0, .57f, .17f, .09f, .155f, .11f, 18);
            k.C(D.Ash, 0, .675f, .17f, .13f, .13f, .006f, 16);
            k.C(D.Iron, 0, .677f, .17f, .02f, .02f, .006f, 8);
            k.C(D.Iron, 0, .3f, .15f, .016f, .016f, .28f, 8);
            k.Cr(D.Iron, new Vector3(0, .31f, .15f), .016f, .016f, .15f, Quaternion.Euler(-90f, 0, 0), 8);
            k.Cr(D.Brass, new Vector3(0, .86f, .02f), .011f, .011f, .13f, AlongZ, 8);
            k.Cr(D.Brass, new Vector3(0, .86f, .15f), .01f, .008f, .06f, Quaternion.Euler(180f, 0, 0), 8);
            k.C(D.Brass, 0, .87f, .09f, .005f, .005f, .03f, 6);
            k.B(D.Brass, 0, .905f, .09f, .07f, .01f, .012f);
            OldMirror(k, rng, new Vector3(0, 1.24f, 0), .34f, .42f, D.TrimLight);
            k.Cr(D.TrimLight, new Vector3(.3f, .98f, .075f), .01f, .01f, .3f, AlongX, 6);
            foreach (float sx in new[] { .31f, .59f }) k.B(D.TrimMid, sx, .98f, .04f, .02f, .03f, .08f);
            // 第四轮：毛巾搭在杆上（前后两片、起褶；Soft.cs TowelOnBar），原来是两块薄板 + 一块黄渍
            TowelOnBar(k.mb, D.Towel, D.Towel, new Vector3(.34f, .98f, .075f), Quaternion.identity, .22f, .27f, .2f, .01f, 1.7f);
        }

        // =====================================================================
        // 風呂
        // =====================================================================
        static void FuroRooms(Kit k, FillCtx c, System.Random rng)
        {
            var r = c.room.r;
            float fl = c.fl, x0 = r.xMin + T, x1 = r.xMax - T, z0 = r.yMin + T;
            float zc = r.yMax - 2.52f;
            FuroPartition(k, c, zc);

            // ---- 西边的洗面所：隔墙 A，门洞正对洗脸台 ----
            float xa = r.xMin + 2.39f, dz = r.yMin + 1.83f;
            Partition(k, c, rng, "WallA", false, xa, z0, zc - PartHalf, dz - .55f, dz + .55f, D.ClothIndigo, c.inner.yMin, zc - .17f);
            FixtureAt(k, c, "Washstand", new Vector3(x0, fl, dz), 90f, new Vector3(1.0f, .72f, .55f), () => DrawShowaWashstand(k, rng));
            // 隔墙 A 西面的毛巾杆
            FixtureAt(k, c, "TowelBar", new Vector3(xa - PartHalf - .012f, fl, r.yMin + 4.5f), -90f, Vector3.zero, () => DrawTowelBar(k, rng));
            // 地上掉下来的瓷砖、翻倒的小凳
            for (int i = 0; i < 7; i++)
            {
                k.Push(new Vector3(x0 + Rf(rng, .75f, 1.25f), fl, dz + Rf(rng, -.8f, .8f)), Rf(rng, 0f, 90f), Rf(rng, -4f, 4f), Rf(rng, -4f, 4f));
                k.Bb(D.BathTile, 0, 0, 0, Rf(rng, .05f, .1f), .008f, Rf(rng, .05f, .1f));
                k.Pop();
            }
            k.Model("wooden_stool_02", new Vector3(x0 + 1.35f, fl, dz + 1.25f), Rf(rng, 0f, 360f), 1.75f, null, 0f, 96f);
            BareBulb(k, new Vector3((x0 + xa) / 2, c.ceil, r.yMin + 3.3f));

            // ---- 脱衣所兼洗衣处 ----
            FixtureAt(k, c, "Washer", new Vector3(x1 - FillGap, fl, zc - .95f), -90f, new Vector3(.8f, 1.1f, .62f), () => DrawWasher(k));
            // 洗衣盆靠浴室隔墙、挨着洗衣机（东墙那段让给原来的体重秤）
            float zp = zc - PartHalf - FillGap;
            FixtureAt(k, c, "WashTubs", new Vector3(x1 - 2.05f, fl, zp), 180f, new Vector3(.85f, .5f, .75f), () => DrawWashTubs(k, rng));
            FixtureAt(k, c, "Shelf", new Vector3(r.xMin + 7.04f, fl, zp), 180f, new Vector3(.9f, 1.4f, .38f),
                () => DrawShelf(k, rng, .9f, 1.4f, 3, 5));

            // ---- 第三轮：脱衣所太空，做成废弃后堆杂物的洗衣间 ----
            // 天花板吊下来的晾衣竿，衣服一直没收（衣服下沿离地 1.8 m 左右，人走得过去）；下面一筐旧衣服
            float lx = r.xMin + 7.1f, lz = zc - 3.0f;
            LaundryLine(k, rng, new Vector3(lx, fl + 2.42f, lz), 2.1f, c.ceil);
            ShotToward("laundry_line", new Vector3(lx, fl + 1.95f, lz), 2.3f, -.35f, 15f);
            FixtureAt(k, c, "Basket", new Vector3(lx - .35f, fl, lz - .05f), 0f, new Vector3(.52f, .42f, .42f), () => DrawLaundryBasket(k, rng));
            // 南墙（门东边）：两个捆死的苹果木箱叠着 + 一个侧倒、空着的
            FixtureAt(k, c, "Crates", new Vector3(r.xMin + 8.0f, fl, z0 + FillGap), 0f, new Vector3(1.1f, .62f, .5f), () => DrawCrates(k, rng));
            // 门西边的旧胶靴、隔墙 A 上靠着的梯子、东南角的老鼠夹
            FixtureAt(k, c, "Boots", new Vector3(r.xMin + 4.0f, fl, z0 + FillGap), Rf(rng, -10f, 10f), Vector3.zero, () => DrawBoots(k, rng));
            FixtureAt(k, c, "Ladder", new Vector3(xa + PartHalf + FillGap, fl, r.yMin + 4.6f), 90f, new Vector3(1.0f, 1.7f, .64f), () => DrawLadder(k, rng));
            FixtureAt(k, c, "MouseTrap", new Vector3(x1 - .3f, fl, z0 + FillGap), 0f, Vector3.zero,
                () => k.Model("mousetrap", new Vector3(0, 0, .08f), Rf(rng, -25f, 25f)));
            // 地上：散开的旧报纸、掉下来的衣夹和衣架
            for (int i = 0; i < 6; i++)
            {
                k.Push(new Vector3(lx + Rf(rng, -1.6f, 1.4f), fl + .002f + i * .0007f, lz + Rf(rng, -1.6f, .9f)), Rf(rng, 0f, 180f));
                k.B(D.Paper, 0, 0, 0, Rf(rng, .32f, .42f), .0012f, Rf(rng, .22f, .3f));
                k.Pop();
            }
            for (int i = 0; i < 9; i++)
            {
                k.Push(new Vector3(lx + Rf(rng, -1.0f, 1.0f), fl + .006f, lz + Rf(rng, -.4f, .4f)), Rf(rng, 0f, 180f));
                k.B(i % 2 == 0 ? D.TrimLight : D.PlasticRed, 0, 0, 0, .075f, .012f, .014f);
                k.Pop();
            }
            k.Push(new Vector3(lx + .8f, fl + .004f, lz + .55f), Rf(rng, 0f, 180f));
            Hanger(k, Vector3.zero, 0f, true);
            k.Pop();
        }

        /// <summary>
        /// 天花板吊下来的竹晾衣竿（world 坐标，不放大）：两根草绳、竿上一排没收的衣服——两条毛巾、
        /// 衣架上的汗衫、一件小孩的红裙子、一双袜子，都带点霉斑。center = 竿子中点。
        /// </summary>
        static void LaundryLine(Kit k, System.Random rng, Vector3 center, float len, float ceil)
        {
            k.Push(center, Rf(rng, -4f, 4f));
            k.Cr(D.BambooDry, new Vector3(-len / 2, 0, 0), .017f, .017f, len, AlongX, 10);
            foreach (float sx in new[] { -len / 2 + .18f, len / 2 - .18f })
                k.Cr(D.Straw, new Vector3(sx, 0, 0), .006f, .006f, ceil - center.y, Quaternion.identity, 6);
            float x = -len / 2 + .3f;
            // 第四轮：衣服都换成有褶的软布（Soft.cs）。随机数照旧（每件的长短、歪的角度不变；原来霉斑的那个随机数空耗掉）
            // 毛巾：搭在竿上，前后两片
            for (int i = 0; i < 2; i++, x += .42f)
            {
                var m = i == 0 ? D.Towel : D.TowelBlue;
                float l = Rf(rng, .32f, .42f), sw = Rf(rng, -4f, 4f);
                TowelOnBar(k.mb, m, m, new Vector3(x - .15f, 0f, 0f), Quaternion.Euler(sw, 0f, 0f), .3f, l, l * .8f, .017f, i * 2.3f + .7f, foldAmp: .01f);
                Rf(rng, -.08f, .08f);
            }
            // 衣架上的汗衫：身子一片、两只短袖斜着
            x += .05f;
            Hanger(k, new Vector3(x, 0, 0), Rf(rng, -8f, 8f), false);
            k.Push(new Vector3(x, -.07f, 0), 0f, Rf(rng, -3f, 3f), Rf(rng, -4f, 4f));
            HangPanel(k, D.ClothWhite, new Vector3(0f, -.32f, 0f), .4f, .56f, 0f, new HangSpec { thick = .004f, foldAmp = .008f, foldLen = .1f, flare = .03f, hem = .006f, seed = 1.3f });
            foreach (float s in new[] { -1f, 1f })
                HangPanel(k, D.ClothWhite, new Vector3(s * .24f, -.13f, -.004f), .16f, .09f, s * -28f, new HangSpec { thick = .004f, foldAmp = .004f, foldLen = .06f, hem = .004f, seed = 2f + s });
            k.Pop();
            // 小孩的红格子裙（短一点，下摆张开）
            x += .5f;
            Hanger(k, new Vector3(x, 0, 0), Rf(rng, -10f, 10f), false);
            k.Push(new Vector3(x, -.07f, 0), 0f, Rf(rng, -4f, 4f), Rf(rng, -6f, 6f));
            HangPanel(k, D.GinghamRed, new Vector3(0f, -.14f, 0f), .24f, .2f, 0f, new HangSpec { thick = .004f, foldAmp = .004f, foldLen = .07f, hem = .003f, seed = 3.1f });
            HangPanel(k, D.GinghamRed, new Vector3(0f, -.35f, .003f), .3f, .23f, 0f, new HangSpec { thick = .004f, foldAmp = .012f, foldLen = .07f, flare = .35f, hem = .01f, seed = 4.2f });
            k.Br(D.ClothWhite, 0, -.05f, .0072f, .1f, .03f, .002f, 0, 0, 0); // 白领子
            k.Pop();
            // 用衣夹夹着的一双袜子
            x += .32f;
            foreach (float s in new[] { -.05f, .05f })
            {
                HangPanel(k, D.ClothWhite, new Vector3(x + s, -.11f, 0f), .05f, .2f, Rf(rng, -5f, 5f), new HangSpec { thick = .006f, foldAmp = .003f, foldLen = .05f, hem = .004f, twist = 8f, seed = 5f + s * 10f });
                k.B(D.TrimLight, x + s, -.005f, 0, .014f, .06f, .03f);
            }
            k.Pop();
        }

        /// <summary>一片垂布（Soft.cs HangCloth）：c = 布的中心，w × h，绕 z 歪 roll 度（顶边跟着歪）。</summary>
        static void HangPanel(Kit k, Material m, Vector3 c, float w, float h, float roll, HangSpec s)
        {
            var q = Quaternion.Euler(0f, 0f, roll);
            s.width = w;
            s.length = h;
            HangCloth(k.mb, m, m, c + q * new Vector3(-w / 2f, h / 2f, 0f), q, s);
        }

        /// <summary>铁丝衣架（三角 + 钩子）。p = 挂钩挂在竿上的点；onFloor = 掉在地上平躺着。</summary>
        static void Hanger(Kit k, Vector3 p, float roll, bool onFloor)
        {
            k.Push(p, 0f, onFloor ? -90f : 0f, roll);
            k.Cr(D.Iron, new Vector3(0, .0f, 0), .003f, .003f, .05f, Quaternion.Euler(0, 0, 180f), 6);   // 钩子柄
            k.Cr(D.Iron, new Vector3(0, .012f, 0), .003f, .003f, .03f, Quaternion.Euler(0, 0, -150f), 6); // 钩尖
            foreach (float s in new[] { -1f, 1f })
                k.Cr(D.Iron, new Vector3(0, -.05f, 0), .003f, .003f, .23f, Quaternion.Euler(0, 0, s * 116f), 6);
            k.Cr(D.Iron, new Vector3(-.205f, -.15f, 0), .003f, .003f, .41f, AlongX, 6);
            k.Pop();
        }

        /// <summary>竹编的洗衣筐，里面一堆旧衣服，一件搭在筐沿上。局部：正面 +z，背面贴 z=0。</summary>
        static void DrawLaundryBasket(Kit k, System.Random rng)
        {
            const float z = .21f;
            const string basket = "wicker_basket_01";
            var cloth = new[] { D.ClothWhite, D.ClothIndigo, D.Towel, D.ClothRed, D.ClothPurple };
            if (!ExternalModels.Has(basket))
            {
                k.C(D.BambooDry, 0, 0, z, .19f, .22f, .34f, 16);
                k.C(D.Black, 0, .335f, z, .205f, .205f, .004f, 16);
                k.Cr(D.Bamboo, new Vector3(0, .34f, z), .225f, .225f, .02f, Quaternion.identity, 16); // 筐沿
                for (int i = 0; i < 6; i++)
                    k.S(cloth[i % cloth.Length], Rf(rng, -.09f, .09f), .33f + Rf(rng, 0f, .06f), z + Rf(rng, -.09f, .09f),
                        Rf(rng, .08f, .13f), Rf(rng, .035f, .06f), Rf(rng, .07f, .12f), 8);
                k.Br(D.ClothIndigo, .2f, .26f, z + .1f, .03f, .2f, .16f, 0, 0, -8f);
                return;
            }
            // 第四轮：竹筐换成 CC0 的椭圆藤编筐（wicker_basket_01），筐里一堆软的旧衣服（随机数照旧），一件搭在筐沿上垂到外面
            const float sc = 1.3f;
            var size = Kit.ModelSize(basket, sc);
            k.Model(basket, new Vector3(0f, 0f, z), 0f, sc);
            for (int i = 0; i < 6; i++)
            {
                float px = Rf(rng, -.09f, .09f), py = Rf(rng, 0f, .06f), pz = Rf(rng, -.09f, .09f);
                float rx = Rf(rng, .08f, .13f), ry = Rf(rng, .035f, .06f), rz = Rf(rng, .07f, .12f);
                var s = new PuffSpec
                {
                    w = 1.8f * rx, d = 1.7f * rz, hTop = ry, hBot = .012f, corner = Mathf.Min(rx, rz) * .7f, rampTop = ry * 1.6f, rampBot = .012f,
                    dome = .15f, wrinkle = .012f, pucker = .003f, crumple = .011f, sinkX = ry * .2f, seed = i * 1.9f + .4f, nx = 18, nz = 18,
                };
                Puff(k.mb, cloth[i % cloth.Length], cloth[i % cloth.Length], new Vector3(px, .03f + py * 1.3f + i * .012f, z + pz * .6f), Quaternion.Euler(0f, i * 37f, 0f), s);
            }
            TowelOnBar(k.mb, D.ClothIndigo, D.ClothIndigo, new Vector3(size.x / 2f - .012f, size.y - .012f, z - .07f), Quaternion.Euler(0f, -90f, 0f),
                .14f, .07f, Mathf.Min(.14f, size.y - .02f), .012f, 3.3f, foldAmp: .01f);
        }

        /// <summary>
        /// 苹果木箱：两个叠着、盖板钉死、草绳十字捆住（一看就是封着的）；旁边一个侧倒的空箱，敞口朝外，里面只有灰。
        /// 局部：正面 +z，背面贴 z=0。
        /// </summary>
        static void DrawCrates(Kit k, System.Random rng)
        {
            Crate(k, rng, new Vector3(-.26f, 0, .2f), Rf(rng, -4f, 4f), 0f, true);
            Crate(k, rng, new Vector3(-.24f, .305f, .21f), Rf(rng, -9f, 9f), 0f, true);
            // 侧倒的空箱：开口朝 +z
            Crate(k, rng, new Vector3(.29f, .16f, .25f), Rf(rng, -10f, 10f), 90f, false);
        }

        /// <summary>一个木箱：两端实心板、前后各三条横木条（中间有缝）、底板。sealed = 盖板钉死 + 草绳十字捆；否则敞口、里面一层灰。</summary>
        static void Crate(Kit k, System.Random rng, Vector3 p, float yaw, float pitch, bool sealedBox)
        {
            const float W = .5f, H = .3f, Dp = .32f;
            k.Push(p, yaw, pitch);
            if (pitch != 0f) k.Push(new Vector3(0, -H / 2, 0)); // 侧倒的绕中心转
            else k.Push(Vector3.zero);
            foreach (float sz in new[] { -1f, 1f })
                for (int i = 0; i < 3; i++)
                    k.Bb(i == 1 ? D.TrimMid : D.TrimLight, 0, .015f + i * .095f, sz * (Dp / 2 - .008f), W - .04f, .08f, .016f);
            foreach (float sx in new[] { -1f, 1f }) k.Bb(D.TrimMid, sx * (W / 2 - .01f), 0, 0, .02f, H, Dp);
            k.Bb(D.TrimLight, 0, 0, 0, W - .04f, .015f, Dp - .03f);
            if (sealedBox)
            {
                for (int i = 0; i < 3; i++)
                {
                    float z = (i - 1) * .105f;
                    k.Bb(D.TrimLight, 0, H, z, W, .014f, .1f);
                    foreach (float sx in new[] { -1f, 1f }) k.C(D.Iron, sx * (W / 2 - .01f), H + .014f, z, .006f, .006f, .003f, 6); // 钉头
                }
                // 草绳十字捆
                k.Bb(D.Straw, 0, H + .014f, 0, W + .01f, .008f, .02f);
                k.Bb(D.Straw, 0, H + .014f, 0, .02f, .008f, Dp + .01f);
                foreach (float sx in new[] { -1f, 1f }) k.Bb(D.Straw, sx * (W / 2 + .004f), 0, 0, .008f, H + .02f, .02f);
                foreach (float sz in new[] { -1f, 1f }) k.Bb(D.Straw, 0, 0, sz * (Dp / 2 + .004f), .02f, H + .02f, .008f);
                // 褪色的纸签
                k.B(D.Paper, Rf(rng, -.1f, .1f), H * .55f, Dp / 2 + .0015f, .12f, .08f, .0015f);
            }
            else
            {
                // 敞口、空的：里面一层灰
                k.Bb(D.Ash, 0, .015f, 0, W - .06f, .002f, Dp - .05f);
            }
            k.Pop();
            k.Pop();
        }

        /// <summary>
        /// 風呂北边的浴室：东西向一道到顶的隔墙（两面瓷砖墙裙），门洞上有门框，两扇磨砂玻璃拉门收在门洞西边；
        /// 浴室里另挂一盏灯。浴室不再补家具。
        /// </summary>
        static void FuroPartition(Kit k, FillCtx c, float zc)
        {
            var r = c.room.r;
            float fl = c.fl, ceil = c.ceil, t = PartHalf;
            float x0 = c.inner.xMin, x1 = c.inner.xMax;
            float d0 = r.xMin + 3.44f, d1 = d0 + 1.6f, head = fl + DoorTop;
            var wall = WallMatFor(c.room, P);
            k.Push(Vector3.zero);
            foreach (var (a, b) in new[] { (x0, d0), (d1, x1) })
            {
                k.Bb(wall, (a + b) / 2, fl, zc, b - a, ceil - fl, 2 * t);
                foreach (float s in new[] { -1f, 1f })
                {
                    k.Bb(D.BathTile, (a + b) / 2, fl, zc + s * (t + .005f), b - a, 1.2f, .01f);
                    k.Bb(D.TrimLight, (a + b) / 2, fl + 1.2f, zc + s * (t + .008f), b - a, .025f, .016f);
                }
            }
            k.Bb(wall, (d0 + d1) / 2, head, zc, d1 - d0, ceil - head, 2 * t);
            k.Bb(D.TrimMid, d0 + .03f, fl, zc, .06f, head - fl, 2 * t + .03f);
            k.Bb(D.TrimMid, d1 - .03f, fl, zc, .06f, head - fl, 2 * t + .03f);
            k.Bb(D.TrimMid, (d0 + d1) / 2, head - .06f, zc, d1 - d0, .06f, 2 * t + .03f);
            k.Bb(D.TrimLight, (d0 + d1) / 2, fl, zc, d1 - d0 - .12f, .015f, 2 * t + .02f);
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
            c.hard.Add(new Bounds(new Vector3(r.center.x, (fl + ceil) / 2, (zc + r.yMax) / 2), new Vector3(r.width + .2f, ceil - fl + .4f, r.yMax - zc + .2f)));
            c.hard.Add(new Bounds(new Vector3((d0 + d1) / 2, fl + 1f, zc - .8f), new Vector3(d1 - d0 + .6f, 2.4f, 1.6f)));
        }

        /// <summary>
        /// 昭和的瓷砖洗脸台：两条瓷砖腿托着瓷砖台面，嵌一个白瓷洗脸盆，下面露出铁的存水弯；后面瓷砖墙裙到 1.3 m，
        /// 有几块掉了露出灰浆；两个铜水龙头；一条木搁板（杯子、牙刷、干裂的肥皂、倒了的瓶子）；上面一面发黑、起雾、带裂纹的旧镜子。
        /// 设计尺寸 = 世界尺寸 / FurnitureScale（台面 0.72 → 0.9 m）。
        /// </summary>
        /// <summary>洗脸盆剖面：盆底外面在 y = 0，台面在 y = .16；口沿的平边半径 .29，盖住台面方洞（半宽 .2）的四个角。</summary>
        static readonly Vector2[] WashBasinProfile =
        {
            new Vector2(0f, 0f), new Vector2(.1f, 0f), new Vector2(.155f, .025f), new Vector2(.19f, .075f), new Vector2(.203f, .155f),
            new Vector2(.288f, .157f), new Vector2(.293f, .163f), new Vector2(.288f, .169f), new Vector2(.215f, .17f), new Vector2(.205f, .166f),
            new Vector2(.188f, .1f), new Vector2(.15f, .045f), new Vector2(.09f, .021f), new Vector2(0f, .018f),
        };

        static void DrawShowaWashstand(Kit k, System.Random rng)
        {
            const float W = 1.0f, Dp = .55f, H = .72f, slab = .09f, bz = .3f;
            foreach (float sx in new[] { -1f, 1f })
                k.Bb(D.BathTile, sx * (W / 2 - .06f), 0, Dp / 2, .12f, H - slab, Dp - .04f);
            // 第四轮：台面开一个方洞，洗脸盆是车削出来的凹盆（口沿的平边盖住方洞四个角），原来是台面上平放的两片圆片
            const float hole = .2f;
            k.Bb(D.BathTile, 0, H - slab, (bz - hole) / 2, W, slab, bz - hole);
            k.Bb(D.BathTile, 0, H - slab, (bz + hole + Dp) / 2, W, slab, Dp - bz - hole);
            foreach (float sx in new[] { -1f, 1f })
                k.Bb(D.BathTile, sx * (W / 2 + hole) / 2, H - slab, bz, W / 2 - hole, slab, 2 * hole);
            k.Bb(D.CeramicWhite, 0, H - .02f, Dp - .012f, W + .012f, .03f, .028f);
            // 洗脸盆：盆底一层干掉的灰泥、铁漏水口
            Lathe(k.mb, D.CeramicWhite, new Vector3(0f, H - .16f, bz), Quaternion.identity, WashBasinProfile, 32);
            k.C(D.Ash, 0, H - .142f, bz, .1f, .1f, .002f, 20);
            k.C(D.Iron, 0, H - .141f, bz, .028f, .028f, .003f, 12);
            // 存水弯
            k.C(D.Iron, 0, .3f, bz, .021f, .021f, H - .155f - .3f, 8);
            k.S(D.Iron, 0, .3f, bz, .03f, .03f, .03f, 8);
            k.Cr(D.Iron, new Vector3(0, .3f, bz), .021f, .021f, bz, Quaternion.Euler(-90f, 0, 0), 8);
            // 瓷砖墙裙 + 掉了瓷砖的地方
            k.Bb(D.BathTile, 0, H, .012f, W + .1f, .58f, .024f);
            for (int i = 0; i < 4; i++)
                k.B(D.Ash, Rf(rng, -.45f, .45f), H + Rf(rng, .08f, .5f), .0245f, Rf(rng, .06f, .12f), Rf(rng, .06f, .1f), .002f);
            // 铜水龙头 ×2
            foreach (float sx in new[] { -.17f, .17f })
            {
                k.Cr(D.Brass, new Vector3(sx, H + .19f, .02f), .013f, .013f, .12f, AlongZ, 8);
                k.Cr(D.Brass, new Vector3(sx, H + .19f, .14f), .012f, .009f, .07f, Quaternion.Euler(180f, 0, 0), 8);
                k.C(D.Brass, sx, H + .2f, .09f, .006f, .006f, .05f, 6);
                k.B(D.Brass, sx, H + .255f, .09f, .09f, .012f, .014f);
                k.B(D.Brass, sx, H + .255f, .09f, .014f, .012f, .09f);
            }
            // 搁板 + 小东西
            float sy = H + .6f;
            k.Bb(D.TrimLight, 0, sy, .07f, .7f, .018f, .13f);
            foreach (float sx in new[] { -.3f, .3f }) k.Br(D.TrimMid, sx, sy - .05f, .05f, .02f, .1f, .02f, -40f, 0, 0);
            k.C(D.CeramicWhite, -.22f, sy + .018f, .075f, .03f, .035f, .09f, 10);
            k.Br(D.PlasticRed, -.23f, sy + .1f, .07f, .012f, .16f, .008f, 0, 0, 12f);
            k.Br(D.ClothWhite, -.21f, sy + .1f, .08f, .012f, .15f, .008f, 0, 0, -9f);
            k.Bb(D.Straw, .03f, sy + .018f, .075f, .07f, .02f, .045f);
            k.Cr(D.Glass, new Vector3(.17f, sy + .043f, .075f), .025f, .025f, .13f, AlongX, 10);
            k.Br(D.Black, .3f, sy + .03f, .08f, .015f, .02f, .07f, 0, 30f, 0);
            // 旧镜子
            OldMirror(k, rng, new Vector3(0, H + .97f, 0), .56f, .46f, D.TrimLight);
        }

        /// <summary>墙上的木毛巾杆，搭着两条发黄发黑的旧毛巾（一条快掉下来了）。</summary>
        static void DrawTowelBar(Kit k, System.Random rng)
        {
            const float y = 1.25f;
            k.Cr(D.TrimLight, new Vector3(-.32f, y, .07f), .012f, .012f, .64f, AlongX, 6);
            foreach (float sx in new[] { -.3f, .3f }) k.B(D.TrimMid, sx, y, .04f, .025f, .035f, .08f);
            // 第四轮：两条毛巾换成搭在杆上的软布（Soft.cs TowelOnBar）；蓝的那条前长后短、往右滑，快掉下来了
            TowelOnBar(k.mb, D.Towel, D.Towel, new Vector3(-.27f, y, .07f), Quaternion.identity, .26f, .37f, .24f, .012f, 2.2f);
            TowelOnBar(k.mb, D.TowelBlue, D.TowelBlue, new Vector3(.06f, y, .07f), Quaternion.identity, .22f, .58f, .07f, .012f, 4.6f, skew: .22f);
        }
    }
}
