using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 房间中央的家具组：和室的座卓 + 坐垫 + 火钵、仏间面向仏坛的坐垫和经机、座敷下了一半的围棋、
    /// 居间摊开的针线活、儿童房的小桌和玩具、杂物间倒扣着椅子的旧桌、台所的备菜台、玄関土间的縁台、
    /// 土蔵的米俵堆、寝室的衣桁和卓袱台、客房铺好的被褥。（座敷、居间原来就有座卓 / 围炉桌，不再加桌子。）
    /// 和第三遍沿墙补家具一样按真实尺寸书写、统一放大 FurnitureScale；桌子这类会挡路的放 Blocker_*，坐垫、被褥不挡。
    /// 位置：离墙面至少 0.7 m，四周和别的家具之间留 0.65 m 走道（玩家胶囊半径 0.22 m），
    /// 避开门口、暗门地毯（地毯本身能走，挨着地毯外 25 cm 的留空区摆）、交互站位；桌子、火钵这类挡路的离步行路线 0.35 m 以上（坐垫、被褥可以踩）；
    /// 审图机位不能落进家具里，高的家具不挡在镜头前。在这些条件下尽量靠房间中心；放不下就跳过。
    /// 依然不做箱子、抽屉、带盖的东西。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        sealed class CenterSet
        {
            public string name;
            /// <summary>占地（真实尺寸，含坐垫）：w 沿局部 x，d 沿局部 z；h 最高点。</summary>
            public float w, d, h;
            /// <summary>固定朝向（世界 yaw）；NaN = 0° / 90° 都试。</summary>
            public float yaw = float.NaN;
            /// <summary>局部原点 = 占地中心，y = 0 为地面，+z 为正面。</summary>
            public System.Action<Kit, System.Random> draw;
            /// <summary>挡块：底面中心、尺寸（局部，真实尺寸）。</summary>
            public readonly List<(Vector3 c, Vector3 size)> blocks = new List<(Vector3 c, Vector3 size)>();
        }

        const float CenterWallGap = .7f;
        const float CenterWalk = .65f;

        static List<CenterSet> Sets(params CenterSet[] s) => new List<CenterSet>(s);

        static List<CenterSet> CenterSets(Room room)
        {
            switch (room.id)
            {
                case "Genkan": return Sets(CEndai());
                case "Daidokoro": return Sets(CPrepTable());
                case "Tsugi": return Sets(CZataku(4, true));
                case "Butsuma": return Sets(CPrayerRows());
                case "Zashiki": return Sets(CGoBoard());
                case "Ima": return Sets(CSewing());
                case "Nando": return Sets(CStoredPile(true));
                case "Oku": return Sets(CZataku(2, true));
                case "Kura": return Sets(CRiceBalePile());
                case "Shinshitsu": return Sets(CChabudai(2, false), CIkou());
                case "Washitsu2F": return Sets(CZataku(4, true), CLaidFuton());
                case "Monooki": return Sets(CStoredPile(false));
                case "Kodomo": return Sets(CKidsPlay());
                default: return new List<CenterSet>();
            }
        }

        static bool TryPlaceCenter(Kit k, FillCtx c, CenterSet set, System.Random rng)
        {
            float S = FurnitureScale;
            var mid = c.inner.center;
            float[] yaws = !float.IsNaN(set.yaw) ? new[] { set.yaw } : rng.Next(2) == 0 ? new[] { 0f, 90f } : new[] { 90f, 0f };
            bool found = false;
            float best = float.MaxValue, bx = 0f, bz = 0f, byaw = 0f;
            var why = new int[CenterWhy.Length];
            foreach (float yaw in yaws)
            {
                Extents(set, yaw, out float ex, out float ez);
                float x0 = c.inner.xMin + CenterWallGap + ex, x1 = c.inner.xMax - CenterWallGap - ex;
                float z0 = c.inner.yMin + CenterWallGap + ez, z1 = c.inner.yMax - CenterWallGap - ez;
                float penalty = yaw == yaws[0] ? 0f : .01f;
                for (float x = x0; x <= x1 + 1e-4f; x += .1f)
                    for (float z = z0; z <= z1 + 1e-4f; z += .1f)
                    {
                        float score = (x - mid.x) * (x - mid.x) + (z - mid.y) * (z - mid.y) + penalty;
                        if (score >= best) continue;
                        int fail = CenterFits(c, set, CenterBox(c, set, x, z, ex, ez), new Vector3(x, c.fl, z), yaw);
                        if (fail != 0) { why[fail]++; continue; }
                        best = score; bx = x; bz = z; byaw = yaw; found = true;
                    }
            }
            if (!found)
            {
                var parts = new List<string>();
                for (int i = 1; i < why.Length; i++) if (why[i] > 0) parts.Add($"{CenterWhy[i]} {why[i]}");
                Debug.Log($"[Hako Art] 中央家具组 {c.room.id}/{set.name} 放不下：" + (parts.Count > 0 ? string.Join("，", parts) : "房间不够大"));
                return false;
            }

            var origin = new Vector3(bx, c.fl, bz);
            k.PushS(origin, new Vector3(0f, byaw, 0f), Vector3.one * S);
            set.draw(k, rng);
            k.Pop();
            foreach (var (p, bs) in CenterBlocks(set, origin, byaw))
                Blocker(c.room.id + "_" + set.name, p, byaw, bs);
            Extents(set, byaw, out float fx, out float fz);
            c.placed.Add(CenterBox(c, set, bx, bz, fx, fz));
            return true;
        }

        static void Extents(CenterSet set, float yaw, out float ex, out float ez)
        {
            bool turned = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > .5f;
            ex = FurnitureScale * (turned ? set.d : set.w) / 2;
            ez = FurnitureScale * (turned ? set.w : set.d) / 2;
        }

        static Bounds CenterBox(FillCtx c, CenterSet set, float x, float z, float ex, float ez)
        {
            float h = FurnitureScale * set.h;
            return new Bounds(new Vector3(x, c.fl + h / 2, z), new Vector3(2 * ex, h, 2 * ez));
        }

        /// <summary>挡块的世界位置（底面中心）和尺寸（已放大）。</summary>
        static IEnumerable<(Vector3 p, Vector3 size)> CenterBlocks(CenterSet set, Vector3 origin, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            foreach (var (bc, bs) in set.blocks)
                yield return (origin + rot * (bc * FurnitureScale), bs * FurnitureScale);
        }

        static readonly string[] CenterWhy = { "", "门口/窗/站位禁区", "离家具不到走道宽", "离别的中央组太近", "挡步行路线", "审图机位" };

        /// <summary>0 = 放得下；否则是 CenterWhy 里的原因编号。</summary>
        static int CenterFits(FillCtx c, CenterSet set, Bounds box, Vector3 origin, float yaw)
        {
            for (int i = 0; i < c.hardFixed; i++) if (box.Intersects(c.hard[i])) return 1;
            foreach (var z in c.keep) if (box.Intersects(z)) return 1;
            var pad = box;
            pad.Expand(new Vector3(2 * CenterWalk, 0f, 2 * CenterWalk));
            for (int i = 0; i < c.occ.Count; i++)
                if ((c.occSoft[i] ? box : pad).Intersects(c.occ[i])) return 2;
            foreach (var o in c.placed) if (pad.Intersects(o)) return 3;
            // 步行路线只管会挡路的桌子、火钵（坐垫、被褥踩得过去）
            foreach (var (p, bs) in CenterBlocks(set, origin, yaw))
                if (LaneClearance(BlockerBounds(p, yaw, bs)) < .35f) return 4;
            // 审图机位不能落进家具里；高的（衣桁、杂物堆）不挡在镜头前 1.5 m
            var rect = Rect.MinMaxRect(box.min.x, box.min.z, box.max.x, box.max.z);
            bool tall = box.size.y > 1.1f;
            for (int i = 0; i + 1 < c.views.Count; i += 2)
            {
                var a = c.views[i];
                if (PtRectDist(a, rect) < 1f) return 5;
                if (!tall) continue;
                var ahead = c.views[i + 1] - a;
                var e = ahead.sqrMagnitude > 1e-6f ? a + ahead.normalized * Mathf.Min(1.5f, ahead.magnitude) : a;
                if (SegRectDist(a, e, rect) < .4f) return 5;
            }
            return 0;
        }

        // =====================================================================
        // 各组（局部：原点在占地中心，+z 为正面，真实尺寸）
        // =====================================================================
        static Material CushionCloth(System.Random r) => r.Next(3) switch { 0 => D.ClothPurple, 1 => D.ClothIndigo, _ => D.ClothRed };

        /// <summary>矮桌：桌面 + 两条幕板 + 四条腿。</summary>
        static void LowTable(Kit k, float w, float d, float h)
        {
            k.Bb(D.TrimMid, 0, h - .04f, 0, w, .04f, d);
            foreach (float sz in new[] { -1f, 1f }) k.Bb(D.TrimMid, 0, h - .085f, sz * (d / 2 - .045f), w - .14f, .045f, .025f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    k.Bb(D.TrimMid, sx * (w / 2 - .07f), 0, sz * (d / 2 - .07f), .055f, h - .04f, .055f);
        }

        /// <summary>茶盆 + 急须 + 茶杯（y 为桌面高度）。</summary>
        static void TeaSet(Kit k, float x, float y, float z, int cups)
        {
            k.Bb(D.Lacquer, x, y, z, .38f, .018f, .26f);
            k.C(D.CeramicBrown, x - .09f, y + .018f, z, .062f, .055f, .1f, 14);
            k.C(D.CeramicBrown, x - .09f, y + .118f, z, .035f, .02f, .02f, 10);
            k.Cr(D.CeramicBrown, new Vector3(x - .035f, y + .07f, z), .012f, .007f, .07f, Quaternion.Euler(0, 0, -60f), 6);
            for (int i = 0; i < cups; i++)
                k.C(D.CeramicWhite, x + .05f + (i % 2) * .08f, y + .018f, z - .06f + (i / 2) * .1f, .028f, .034f, .062f, 12);
        }

        /// <summary>座卓 + 两侧坐垫（seats = 2 / 4 / 6），可选在一头放火钵。</summary>
        static CenterSet CZataku(int seats, bool hibachi)
        {
            float W = seats > 4 ? 1.8f : seats > 2 ? 1.5f : 1.1f, Dp = seats > 2 ? .75f : .65f;
            const float H = .33f, Zh = .275f;
            float side = Dp / 2 + .1f + Zh;
            float tx = hibachi ? -.33f : 0f, hx = tx + W / 2 + .42f;
            var set = new CenterSet { name = "Zataku", w = W + (hibachi ? .68f : .1f), d = 2 * (side + Zh), h = hibachi ? .62f : .48f };
            set.draw = (k, r) =>
            {
                var cloth = CushionCloth(r);
                k.Push(new Vector3(tx, 0, 0));
                LowTable(k, W, Dp, H);
                TeaSet(k, Rf(r, -.15f, .15f) * W, H, Rf(r, -.06f, .06f), 2 + r.Next(3));
                if (seats >= 4)
                {
                    // 摊开的报纸 + 烟灰缸
                    k.Br(D.Paper, -W / 2 + .3f, H + .003f, .1f, .4f, .004f, .3f, 0, Rf(r, -15f, 15f), 0);
                    k.C(D.Iron, W / 2 - .22f, H, -.15f, .055f, .05f, .03f, 12);
                }
                int perSide = seats / 2;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    for (int i = 0; i < perSide; i++)
                    {
                        float x = perSide == 1 ? 0f : (i - (perSide - 1) / 2f) * (W / perSide);
                        Zabuton(k, x + Rf(r, -.03f, .03f), 0, sgn * side + Rf(r, -.03f, .03f), Rf(r, -6f, 6f), cloth);
                    }
                k.Pop();
                if (hibachi) { k.Push(new Vector3(hx, 0, -.27f)); DrawHibachi(k); k.Pop(); }
            };
            set.blocks.Add((new Vector3(tx, 0, 0), new Vector3(W, H, Dp)));
            if (hibachi) set.blocks.Add((new Vector3(hx, 0, 0), new Vector3(.44f, .5f, .44f)));
            return set;
        }

        /// <summary>卓袱台 + 坐垫。meal = 摆着吃了一半的饭菜，否则一套茶具。</summary>
        static CenterSet CChabudai(int seats, bool meal)
        {
            float R = seats > 2 ? .45f : .38f;
            const float H = .32f, Zh = .275f;
            float rz = R + .1f + Zh;
            int n = seats > 2 ? 4 : 2;
            var set = new CenterSet { name = "Chabudai", w = n == 4 ? 2 * (rz + Zh) : 2 * R + .1f, d = 2 * (rz + Zh), h = .45f };
            set.draw = (k, r) =>
            {
                var cloth = CushionCloth(r);
                k.C(D.TrimMid, 0, H - .03f, 0, R, R, .03f, 28);
                k.C(D.TrimMid, 0, H - .07f, 0, R - .06f, R - .06f, .04f, 24);
                for (int i = 0; i < 4; i++)
                {
                    float a = (45f + 90f * i) * Mathf.Deg2Rad;
                    k.Bb(D.TrimMid, Mathf.Cos(a) * R * .62f, 0, Mathf.Sin(a) * R * .62f, .045f, H - .07f, .045f);
                }
                for (int i = 0; i < n; i++)
                {
                    float deg = n == 4 ? 90f * i : 180f * i, a = deg * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    var side = new Vector3(dir.z, 0, -dir.x);
                    var zp = dir * rz;
                    Zabuton(k, zp.x + Rf(r, -.03f, .03f), 0, zp.z + Rf(r, -.03f, .03f), deg + Rf(r, -8f, 8f), cloth);
                    if (!meal) continue;
                    var place = dir * (R - .15f);
                    var rice = place - side * .06f; var soup = place + side * .06f; var hashi = place + dir * .08f;
                    k.C(D.CeramicWhite, rice.x, H, rice.z, .035f, .055f, .06f, 12);
                    k.S(D.ClothWhite, rice.x, H + .055f, rice.z, .045f, .015f, .045f, 8);
                    k.C(D.Lacquer, soup.x, H, soup.z, .035f, .055f, .055f, 12);
                    k.Br(D.TrimLight, hashi.x, H + .005f, hashi.z, .21f, .008f, .008f, 0, deg + Rf(r, -6f, 6f), 0);
                }
                if (meal)
                {
                    k.C(D.CeramicBlue, 0, H, 0, .1f, .14f, .045f, 16);
                    k.S(D.Leaf, -.03f, H + .05f, .02f, .06f, .025f, .05f, 8);
                    k.S(D.ClothRed, .05f, H + .05f, -.03f, .035f, .02f, .03f, 7);
                }
                else TeaSet(k, 0, H, 0, 2);
            };
            set.blocks.Add((Vector3.zero, new Vector3(R * 1.45f, H, R * 1.45f)));
            return set;
        }

        /// <summary>仏间：面向仏坛（北墙）两排坐垫，最前面是经机（香炉、木鱼、经本）。</summary>
        static CenterSet CPrayerRows()
        {
            var set = new CenterSet { name = "PrayerRows", w = 2.2f, d = 2.0f, h = .45f, yaw = 0f };
            set.draw = (k, r) =>
            {
                var cloth = r.Next(2) == 0 ? D.ClothPurple : D.ClothIndigo;
                foreach (float z in new[] { -.75f, .05f })
                    foreach (float x in new[] { -.78f, 0f, .78f })
                        Zabuton(k, x + Rf(r, -.03f, .03f), 0, z + Rf(r, -.03f, .03f), Rf(r, -4f, 4f), cloth);
                const float z0 = .78f, H = .29f;
                k.Bb(D.Lacquer, 0, H - .03f, z0, .62f, .03f, .32f);
                foreach (float sx in new[] { -.27f, .27f }) k.Bb(D.Lacquer, sx, 0, z0, .04f, H - .03f, .28f);
                k.C(D.Brass, -.14f, H, z0, .05f, .06f, .05f, 12);
                k.C(D.Ash, -.14f, H + .045f, z0, .052f, .052f, .003f, 12);
                for (int i = 0; i < 3; i++)
                    k.Cr(D.Bark, new Vector3(-.155f + i * .015f, H + .048f, z0), .002f, .002f, .13f,
                        Quaternion.Euler(Rf(r, -8f, 8f), 0, Rf(r, -8f, 8f)), 4);
                k.S(D.TrimMid, .13f, H + .045f, z0, .075f, .055f, .065f, 10);
                k.Bb(D.Paper, .02f, H, z0 - .1f, .14f, .015f, .2f);
            };
            return set;
        }

        /// <summary>座敷：足付きの碁盤上下了一半的棋，两边的碁笥（盖子翻过来放在旁边，装着提掉的子），对坐的两块坐垫。</summary>
        static CenterSet CGoBoard()
        {
            const float BW = .455f, BD = .424f, BT = .15f, LegH = .11f;
            var set = new CenterSet { name = "GoBoard", w = .95f, d = 1.75f, h = .3f };
            set.blocks.Add((Vector3.zero, new Vector3(BW, LegH + BT, BD)));
            set.draw = (k, r) =>
            {
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        k.C(D.TrimMid, sx * (BW / 2 - .07f), 0, sz * (BD / 2 - .07f), .035f, .045f, LegH, 10);
                k.Bb(D.TrimLight, 0, LegH, 0, BW, BT, BD);
                float top = LegH + BT, gx = BW * .9f / 18f, gz = BD * .9f / 18f;
                for (int i = 0; i < 19; i++)
                {
                    k.B(D.Ink, (i - 9) * gx, top + .0006f, 0, .0022f, .0012f, 18 * gz);
                    k.B(D.Ink, 0, top + .0006f, (i - 9) * gz, 18 * gx, .0012f, .0022f);
                }
                for (int i = 0; i < 16; i++)
                {
                    int ix = r.Next(3, 16), iz = r.Next(3, 16);
                    k.S(i % 2 == 0 ? D.Black : D.CeramicWhite, (ix - 9) * gx, top + .004f, (iz - 9) * gz, .011f, .0045f, .011f, 8);
                }
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    float bx = sg * .33f, bz = sg * .1f;
                    var stone = sg > 0 ? D.Black : D.CeramicWhite;
                    k.C(D.TrimMid, bx, 0, bz, .055f, .066f, .075f, 14);
                    k.S(stone, bx, .074f, bz, .055f, .014f, .055f, 10);
                    k.C(D.TrimMid, bx, 0, bz + sg * .17f, .062f, .066f, .028f, 14); // 翻过来的盖子
                    for (int j = 0; j < 3; j++)
                        k.S(sg > 0 ? D.CeramicWhite : D.Black, bx + Rf(r, -.025f, .025f), .03f, bz + sg * .17f + Rf(r, -.025f, .025f), .011f, .0045f, .011f, 8);
                    Zabuton(k, Rf(r, -.03f, .03f), 0, sg * (BD / 2 + .1f + .275f), Rf(r, -5f, 5f), D.ClothPurple);
                }
            };
            return set;
        }

        /// <summary>居间：地上摊开的裁ち板（布料、竹尺、剪刀、针插、线轴），一卷反物，叠好的衣服，坐垫在长边前。没有针线盒（不放带盖的东西）。</summary>
        static CenterSet CSewing()
        {
            const float L = 1.0f, Wd = .4f, H = .045f;
            var set = new CenterSet { name = "Sewing", w = 1.2f, d = 1.0f, h = .2f };
            set.draw = (k, r) =>
            {
                var cloth = r.Next(2) == 0 ? D.ClothIndigo : D.ClothPurple;
                k.Push(new Vector3(-.05f, 0, .27f));
                foreach (float sx in new[] { -.38f, .38f }) k.Bb(D.TrimMid, sx, 0, 0, .05f, .02f, Wd - .04f);
                k.Bb(D.TrimLight, 0, .02f, 0, L, .025f, Wd);
                k.Br(cloth, -.08f, H + .002f, 0, .7f, .004f, .3f, 0, Rf(r, -3f, 3f), 0);
                k.Cr(cloth, new Vector3(.33f, H + .05f, -.16f), .05f, .05f, .32f, Quaternion.Euler(90f, 0, 0), 12);
                k.Br(D.TrimLight, -.05f, H + .007f, -.15f, .75f, .006f, .03f, 0, Rf(r, -4f, 4f), 0);
                foreach (float a in new[] { 18f, -18f }) k.Br(D.Iron, .08f, H + .007f, .08f, .15f, .005f, .011f, 0, a, 0);
                k.Bb(D.Black, .16f, H, .08f, .05f, .008f, .04f);
                k.S(D.ClothRed, -.4f, H + .025f, .1f, .045f, .03f, .045f, 10);
                for (int i = 0; i < 4; i++)
                    k.Cr(D.Brass, new Vector3(-.4f + Rf(r, -.02f, .02f), H + .045f, .1f + Rf(r, -.02f, .02f)), .002f, .002f, .03f, Quaternion.Euler(Rf(r, -25f, 25f), 0, Rf(r, -25f, 25f)), 4);
                for (int i = 0; i < 2; i++)
                {
                    k.C(D.TrimLight, .18f + i * .05f, H, -.1f, .017f, .017f, .045f, 8);
                    k.C(i == 0 ? D.ClothRed : D.ClothWhite, .18f + i * .05f, H + .006f, -.1f, .021f, .021f, .033f, 8);
                }
                k.Pop();
                Zabuton(k, -.15f + Rf(r, -.04f, .04f), 0, -.24f, Rf(r, -6f, 6f), D.ClothRed);
                k.Bb(D.ClothWhite, .37f, 0, -.27f, .36f, .035f, .28f);
                k.Bb(cloth, .37f, .035f, -.27f, .34f, .03f, .27f);
                k.Bb(D.ClothWhite, .37f, .065f, -.27f, .33f, .025f, .26f);
            };
            return set;
        }

        /// <summary>儿童房：小圆桌（画纸、蜡笔、纸鹤）、两块坐垫、散在地上的皮球、不倒翁、积木、陀螺。</summary>
        static CenterSet CKidsPlay()
        {
            const float R = .3f, H = .3f;
            var set = new CenterSet { name = "KidsPlay", w = 1.6f, d = 1.55f, h = .42f };
            set.draw = (k, r) =>
            {
                k.Push(new Vector3(-.075f, 0, .13f));
                k.C(D.TrimLight, 0, H - .03f, 0, R, R, .03f, 24);
                for (int i = 0; i < 3; i++)
                {
                    float a = (90f + 120f * i) * Mathf.Deg2Rad;
                    k.Bb(D.TrimLight, Mathf.Cos(a) * R * .6f, 0, Mathf.Sin(a) * R * .6f, .035f, H - .03f, .035f);
                }
                k.Bb(D.Paper, .02f, H, -.03f, .3f, .003f, .22f);
                Material[] crayon = { D.PlasticRed, D.ClothIndigo, D.Leaf, D.Gold };
                for (int i = 0; i < crayon.Length; i++)
                    k.Br(crayon[i], -.14f + i * .03f, H + .008f, .14f, .07f, .01f, .01f, 0, Rf(r, -30f, 30f), 0);
                k.Br(D.Paper, -.05f, H + .03f, .1f, .08f, .002f, .05f, 0, 30f, 25f);
                k.Br(D.Paper, -.05f, H + .03f, .1f, .08f, .002f, .05f, 0, 30f, -25f);
                Zabuton(k, 0, 0, -.62f, Rf(r, -8f, 8f), D.ClothRed);
                Zabuton(k, .6f, 0, .1f, 90f + Rf(r, -8f, 8f), D.ClothIndigo);
                // 皮球、不倒翁
                k.S(D.PlasticRed, -.62f, .09f, .35f, .09f, .09f, .09f, 12);
                k.S(D.ClothRed, -.55f, .1f, -.25f, .1f, .105f, .095f, 12);
                k.S(D.ClothWhite, -.55f, .125f, -.18f, .055f, .05f, .03f, 10);
                foreach (float ex in new[] { -.02f, .02f }) k.S(D.Black, -.55f + ex, .13f, -.155f, .008f, .008f, .005f, 6);
                // 积木
                Material[] block = { D.TrimLight, D.PlasticRed, D.ClothIndigo, D.CeramicWhite };
                for (int i = 0; i < 3; i++)
                    k.Br(block[i], .25f + Rf(r, -.01f, .01f), .04f + i * .08f, .5f + Rf(r, -.01f, .01f), .08f, .08f, .08f, 0, Rf(r, -20f, 20f), 0);
                k.Br(block[3], .4f, .04f, .42f, .08f, .08f, .08f, 0, Rf(r, 0f, 90f), 0);
                // 陀螺
                k.C(D.PlasticRed, -.2f, 0, .55f, .006f, .05f, .05f, 12);
                k.C(D.TrimMid, -.2f, .05f, .55f, .05f, .02f, .015f, 12);
                k.C(D.TrimMid, -.2f, .065f, .55f, .008f, .008f, .04f, 6);
                k.Pop();
            };
            set.blocks.Add((new Vector3(-.075f, 0, .13f), new Vector3(.5f, H, .5f)));
            return set;
        }

        /// <summary>收着的旧方桌：两把椅子倒扣在上面，靠背垂到桌边外；地上两卷地毯 / 草席，一台昭和电风扇。</summary>
        static CenterSet CStoredPile(bool compact)
        {
            if (compact) return CStoredPileSmall();
            const float W = 1.2f, Dp = .7f, H = .72f;
            var set = new CenterSet { name = "StoredPile", w = 2.05f, d = 1.15f, h = 1.2f };
            set.draw = (k, r) =>
            {
                k.Push(new Vector3(-.16f, 0, .15f));
                k.Bb(D.TrimMid, 0, H - .035f, 0, W, .035f, Dp);
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        k.Bb(D.Post, sx * (W / 2 - .06f), 0, sz * (Dp / 2 - .06f), .05f, H - .035f, .05f);
                foreach (float sx in new[] { -.3f, .3f })
                {
                    k.Push(new Vector3(sx, H, .19f), Rf(r, -4f, 4f));
                    k.Bb(D.TrimLight, 0, 0, 0, .42f, .035f, .4f);
                    foreach (float lx in new[] { -.18f, .18f })
                        foreach (float lz in new[] { -.17f, .17f })
                            k.Bb(D.TrimLight, lx, .035f, lz, .03f, .42f, .03f);
                    k.Bb(D.TrimLight, 0, -.4f, .215f, .4f, .4f, .025f);
                    k.Pop();
                }
                k.Cr(D.ClothRed, new Vector3(-.85f, .11f, -.6f), .11f, .11f, 1.5f, AlongX, 12);
                k.Cr(D.Straw, new Vector3(-.75f, .09f, -.4f), .09f, .09f, 1.3f, AlongX, 10);
                float fx = W / 2 + .38f;
                k.C(D.Iron, fx, 0, 0, .14f, .15f, .035f, 16);
                k.C(D.Iron, fx, .035f, 0, .016f, .016f, .62f, 8);
                k.S(D.CeramicWhite, fx, .7f, -.04f, .09f, .09f, .11f, 12);
                Ring(k, D.Iron, new Vector3(fx, .7f, .08f), .19f, .01f);
                Ring(k, D.Iron, new Vector3(fx, .7f, .1f), .11f, .008f);
                for (int i = 0; i < 3; i++)
                {
                    float a = (i * 120f + Rf(r, 0f, 30f)) * Mathf.Deg2Rad;
                    k.Br(D.ClothIndigo, fx + Mathf.Cos(a) * .09f, .7f + Mathf.Sin(a) * .09f, .075f, .15f, .08f, .005f, 0, 0, a * Mathf.Rad2Deg);
                }
                k.Pop();
            };
            set.blocks.Add((new Vector3(-.16f, 0, .15f), new Vector3(W, 1.15f, Dp)));
            return set;
        }

        /// <summary>小一号的：旧方桌上倒扣两把椅子，桌下横放一卷地毯（纳戸四面墙中间都有门，只剩角落能放）。</summary>
        static CenterSet CStoredPileSmall()
        {
            const float W = 1.0f, Dp = .6f, H = .7f;
            var set = new CenterSet { name = "StoredPile", w = 1.1f, d = .8f, h = 1.15f };
            set.draw = (k, r) =>
            {
                k.Push(new Vector3(0, 0, -.03f));
                k.Bb(D.TrimMid, 0, H - .035f, 0, W, .035f, Dp);
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        k.Bb(D.Post, sx * (W / 2 - .06f), 0, sz * (Dp / 2 - .06f), .05f, H - .035f, .05f);
                foreach (float sx in new[] { -.25f, .25f })
                {
                    k.Push(new Vector3(sx, H, .14f), Rf(r, -4f, 4f));
                    k.Bb(D.TrimLight, 0, 0, 0, .42f, .035f, .4f);
                    foreach (float lx in new[] { -.18f, .18f })
                        foreach (float lz in new[] { -.17f, .17f })
                            k.Bb(D.TrimLight, lx, .035f, lz, .03f, .42f, .03f);
                    k.Bb(D.TrimLight, 0, -.4f, .215f, .4f, .4f, .025f);
                    k.Pop();
                }
                k.Cr(D.ClothRed, new Vector3(-.45f, .11f, -.08f), .11f, .11f, .9f, AlongX, 12);
                k.Pop();
            };
            set.blocks.Add((new Vector3(0, 0, -.03f), new Vector3(W, 1.15f, Dp)));
            return set;
        }

        /// <summary>土间的备菜台：砧板、萝卜、菜刀、碗、锅，下层木桶和一摞碗；前面两个圆凳。</summary>
        static CenterSet CPrepTable()
        {
            const float W = 1.4f, Dp = .7f, H = .85f;
            var set = new CenterSet { name = "PrepTable", w = 1.5f, d = 1.45f, h = 1.0f };
            set.draw = (k, r) =>
            {
                k.Push(new Vector3(0, 0, .3f));
                k.Bb(D.TrimLight, 0, H - .05f, 0, W, .05f, Dp);
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        k.Bb(D.Post, sx * (W / 2 - .06f), 0, sz * (Dp / 2 - .06f), .065f, H - .05f, .065f);
                k.Bb(D.TrimMid, 0, .14f, 0, W - .14f, .03f, Dp - .1f);
                Oke(k, -.35f, .17f, 0, .15f, .2f);
                for (int i = 0; i < 3; i++) k.C(D.CeramicWhite, .3f, .17f + i * .04f, 0, .06f, .09f, .04f, 12);
                k.Bb(D.TrimLight, -.25f, H, -.05f, .45f, .035f, .28f);
                k.Cr(D.ClothWhite, new Vector3(-.42f, H + .07f, -.05f), .04f, .03f, .32f, AlongX, 10);
                k.S(D.Leaf, -.47f, H + .08f, -.05f, .07f, .03f, .05f, 7);
                k.B(D.Iron, -.1f, H + .04f, .06f, .2f, .004f, .045f);
                k.B(D.Post, .05f, H + .045f, .06f, .1f, .018f, .025f);
                k.C(D.CeramicBlue, .3f, H, -.12f, .06f, .1f, .07f, 12);
                k.C(D.Iron, .42f, H, .12f, .12f, .11f, .14f, 14);
                k.C(D.Iron, .42f, H + .14f, .12f, .07f, .03f, .03f, 12);
                k.Pop();
                foreach (float sx in new[] { -.35f, .35f })
                {
                    k.C(D.TrimMid, sx + Rf(r, -.04f, .04f), 0, -.5f, .15f, .13f, .42f, 14);
                    k.C(D.TrimLight, sx, .42f, -.5f, .17f, .17f, .035f, 16);
                }
            };
            set.blocks.Add((new Vector3(0, 0, .3f), new Vector3(W, H, Dp)));
            return set;
        }

        /// <summary>玄関土间的縁台：铺着緋毛氈，上面一套茶具和一把团扇。</summary>
        static CenterSet CEndai()
        {
            const float W = 1.8f, Dp = .9f, H = .42f;
            var set = new CenterSet { name = "Endai", w = 1.9f, d = 1.0f, h = .6f };
            set.draw = (k, r) =>
            {
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        k.Bb(D.Post, sx * (W / 2 - .08f), 0, sz * (Dp / 2 - .08f), .07f, H - .04f, .07f);
                foreach (float sz in new[] { -1f, 1f }) k.Bb(D.Post, 0, H - .1f, sz * (Dp / 2 - .08f), W - .1f, .06f, .05f);
                for (int i = 0; i < 8; i++)
                    k.Bb(D.TrimLight, 0, H - .04f, -Dp / 2 + .06f + i * (Dp - .12f) / 7f, W, .04f, .095f);
                // 铺一张草席（茣蓙），不用红毛毡
                k.Bb(D.Straw, -.2f, H, 0, 1.1f, .006f, Dp - .04f);
                foreach (float sx in new[] { -1f, 1f }) k.Bb(D.ClothIndigo, -.2f + sx * .545f, H, 0, .02f, .007f, Dp - .04f);
                TeaSet(k, .45f, H + .006f, .05f, 2);
                k.Br(D.Paper, -.45f, H + .012f, .15f, .2f, .004f, .18f, 0, 25f, 0);
                k.Br(D.TrimMid, -.33f, H + .01f, .2f, .14f, .01f, .012f, 0, 25f, 0);
            };
            set.blocks.Add((Vector3.zero, new Vector3(W, H, Dp)));
            return set;
        }

        /// <summary>土蔵里码成一堆的米俵：下三上二。</summary>
        static CenterSet CRiceBalePile()
        {
            var set = new CenterSet { name = "RiceBalePile", w = .9f, d = 1.3f, h = .78f };
            set.draw = (k, r) =>
            {
                void Bale(Vector3 c, float yaw)
                {
                    k.Push(c, yaw);
                    k.Cr(D.Straw, new Vector3(-.36f, 0, 0), .2f, .2f, .72f, AlongX, 12);
                    foreach (float sx in new[] { -.39f, .35f }) k.Cr(D.Straw, new Vector3(sx, 0, 0), .17f, .17f, .04f, AlongX, 12);
                    foreach (float sx in new[] { -.22f, 0f, .22f }) k.Cr(D.TrimMid, new Vector3(sx - .012f, 0, 0), .205f, .205f, .024f, AlongX, 12);
                    k.Pop();
                }
                foreach (float z in new[] { -.42f, 0f, .42f }) Bale(new Vector3(Rf(r, -.03f, .03f), .2f, z), Rf(r, -3f, 3f));
                foreach (float z in new[] { -.21f, .21f }) Bale(new Vector3(Rf(r, -.04f, .04f), .54f, z), Rf(r, -4f, 4f));
            };
            set.blocks.Add((Vector3.zero, new Vector3(.85f, .74f, 1.3f)));
            return set;
        }

        /// <summary>衣桁：漆黑的架子上搭着一件和服，两只袖子张开。</summary>
        static CenterSet CIkou()
        {
            const float W = 1.5f, H = 1.65f;
            var set = new CenterSet { name = "Ikou", w = 1.85f, d = .55f, h = 1.7f };
            set.draw = (k, r) =>
            {
                foreach (float sx in new[] { -W / 2, W / 2 })
                {
                    k.Bb(D.Lacquer, sx, 0, 0, .06f, .05f, .5f);
                    k.Bb(D.Lacquer, sx, .05f, 0, .04f, H - .05f, .04f);
                }
                k.Bb(D.Lacquer, 0, H - .04f, 0, W + .3f, .04f, .045f);
                k.Bb(D.Lacquer, 0, H - .5f, 0, W, .03f, .035f);
                var m = r.Next(3) switch { 0 => D.ClothIndigo, 1 => D.ClothPurple, _ => D.ClothRed };
                foreach (float sz in new[] { -.03f, .03f }) k.Bb(m, 0, H - 1.25f, sz, 1.25f, 1.2f, .012f);
                foreach (float sx in new[] { -.78f, .78f }) k.Bb(m, sx, H - .62f, 0, .3f, .56f, .075f);
                k.Bb(D.Gold, 0, H - .78f, 0, 1.26f, .1f, .076f);
            };
            set.blocks.Add((Vector3.zero, new Vector3(W, 1.6f, .45f)));
            return set;
        }

        /// <summary>铺好的被褥：敷布団、掛布団、枕，枕边一盏行灯。</summary>
        static CenterSet CLaidFuton()
        {
            var set = new CenterSet { name = "LaidFuton", w = 1.45f, d = 2.05f, h = .9f };
            set.draw = (k, r) =>
            {
                k.Push(new Vector3(-.2f, 0, 0), Rf(r, -3f, 3f));
                k.Bb(D.ClothWhite, 0, 0, 0, 1.0f, .09f, 2.0f);
                var m = r.Next(2) == 0 ? D.ClothIndigo : D.ClothRed;
                k.Bb(m, 0, .09f, -.22f, 1.04f, .07f, 1.5f);
                k.Cr(m, new Vector3(-.52f, .125f, .53f), .035f, .035f, 1.04f, AlongX, 8);
                k.Bb(D.ClothWhite, 0, .155f, .5f, 1.0f, .012f, .1f);
                k.Bb(D.ClothWhite, 0, .09f, .78f, .5f, .11f, .3f);
                k.Pop();
                k.Push(new Vector3(.52f, 0, .75f - .18f));
                DrawAndon(k);
                k.Pop();
            };
            set.blocks.Add((new Vector3(.52f, 0, .75f), new Vector3(.3f, .82f, .3f)));
            return set;
        }
    }
}
