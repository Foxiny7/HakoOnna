using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第三轮：重度废弃感（2026-11-19 定的“重”）。按房间补家具的最后一步、每个房间 Build 之前调用：
    /// <list type="bullet">
    /// <item>地上：墙脚掉下来的墙皮和土块、碎纸和发黄的旧报纸、摔碎的瓷器、门口和縁側吹进来的枯叶和红叶、
    ///   土蔵 / 台所 / 物置的稻草、便所和風呂的瓷砖碎块、墙角的灰团；玄関和台所的土间从墙脚长出枯草；
    ///   縁側、玄関倒着一个花盆，台所倒着一只木桶。</item>
    /// <item>天花四角的蜘蛛网（半透明的网面 + 丝 + 挂着灰的垂丝）。</item>
    /// <item>物置的天花塌了一块：洞、垂下来的天花板、断梁、漏下的稻草和土，地上一堆土和木板（挡块不让人踩上去）。</item>
    /// <item>走廊里掉在地上的一张遗影（玻璃碎了）；子供部屋地上是蜡笔画和蜡笔。</item>
    /// </list>
    /// 规则：都贴地、很薄，没有碰撞体（塌顶那堆除外），不挡路；不放在门口、门扇停靠处、容器正面 1 m、暗门地毯、
    /// 交互站位和已有家具的脚下。没有盒子、袋子、包袱——看起来都不能像能搜的东西。
    /// 用自己的随机数（不影响前面家具的摆法）。想多想少：改 RuinFor 里的数。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>每个房间放几处（墙皮、灰团、碎瓷、落叶、稻草、瓷砖是“堆”，纸是张数）；webs = 每个天花角挂网的概率。</summary>
        struct Ruin
        {
            public int paper, shards, plaster, leaves, straw, tiles, dust, weeds;
            public float webs;
        }

        static Ruin RuinFor(Room room)
        {
            switch (room.id)
            {
                case "Genkan": return new Ruin { paper = 5, shards = 1, plaster = 3, leaves = 6, straw = 1, dust = 3, weeds = 5, webs = .75f };
                case "Daidokoro": return new Ruin { paper = 5, shards = 3, plaster = 3, leaves = 3, straw = 3, dust = 3, weeds = 3, webs = 1f };
                case "Tsugi": return new Ruin { paper = 7, shards = 1, plaster = 4, dust = 3, webs = .5f };
                case "Butsuma": return new Ruin { paper = 6, shards = 2, plaster = 3, dust = 3, webs = .75f };
                case "Zashiki": return new Ruin { paper = 8, shards = 2, plaster = 5, leaves = 1, dust = 4, webs = .5f };
                case "Ima": return new Ruin { paper = 9, shards = 2, plaster = 4, dust = 3, webs = .5f };
                case "Nando": return new Ruin { paper = 7, shards = 1, plaster = 5, straw = 2, dust = 4, webs = 1f };
                case "Oku": return new Ruin { paper = 6, shards = 1, plaster = 4, dust = 3, webs = .5f };
                case "Engawa": return new Ruin { paper = 3, plaster = 2, leaves = 7, dust = 2, webs = .5f };
                case "EngawaE": return new Ruin { paper = 2, plaster = 2, leaves = 5, dust = 2, webs = .5f };
                case "Rouka": return new Ruin { paper = 4, shards = 1, plaster = 3, leaves = 1, dust = 3, webs = .5f };
                case "Benjo": return new Ruin { paper = 3, plaster = 1, tiles = 3, dust = 2, webs = 1f };
                case "Furo": return new Ruin { paper = 4, shards = 1, plaster = 2, leaves = 1, tiles = 4, dust = 3, webs = 1f };
                case "Kura": return new Ruin { paper = 4, shards = 1, plaster = 6, straw = 5, dust = 5, webs = 1f };
                case "Monooki": return new Ruin { paper = 5, plaster = 5, straw = 3, dust = 4, webs = 1f };
                case "Kodomo": return new Ruin { paper = 10, plaster = 3, dust = 3, webs = .5f };
                case "Rouka2F":
                case "Rouka2FN": return new Ruin { paper = 3, plaster = 2, dust = 2, webs = .5f };
                default: return new Ruin { paper = 6, shards = 1, plaster = 4, dust = 3, webs = .5f };
            }
        }

        /// <summary>碎屑数量的总倍数（房间按平面放大过两倍，再重一点）。</summary>
        const float RuinMore = 2f;

        /// <summary>这一屋已经被塌顶、遗影这类大件占掉的地面（碎屑不再叠上去）。</summary>
        static readonly List<Bounds> RuinUsed = new List<Bounds>();
        static readonly List<string> RuinReport = new List<string>();
        /// <summary>审图机位（ArtSnapshot 生成完接着拍）：名字、相机位置、看的点。只记几个房间的第一处。</summary>
        internal static readonly List<(string name, Vector3 pos, Vector3 look)> RuinShots = new List<(string, Vector3, Vector3)>();
        static readonly List<string> webSizes = new List<string>();

        /// <summary>从屋子中间那一侧、斜上方看地上的一点。</summary>
        static void ShotAt(string name, FillCtx c, Vector3 p, float back, float up)
        {
            var mid = new Vector3(c.room.r.center.x, p.y, c.room.r.center.y);
            var dir = mid - p;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            RuinShots.Add((name, p + dir * back + Vector3.up * up, p));
        }
        static int ruinLayer;

        /// <summary>贴地薄片的高度：轮流抬 0.6 mm，叠在一起的纸、叶子不闪。</summary>
        static float Ly() => .0012f + (ruinLayer++ % 9) * .0006f;

        static void Abandon(Kit k, FillCtx c)
        {
            var room = c.room;
            var rng = new System.Random(StableHash(room.id) ^ 0x5A17E);
            var ruin = RuinFor(room);
            var wallMat = WallMatFor(room, P) ?? D.PlasterWhite;
            RuinUsed.Clear();
            ruinLayer = 0;
            int n = 0, webs = 0;
            // 房间按平面放大过两倍，表里的数是“正常大小的房间”，这里统一乘 RuinMore
            ruin.paper = Mathf.RoundToInt(ruin.paper * RuinMore); ruin.shards = Mathf.RoundToInt(ruin.shards * RuinMore);
            ruin.plaster = Mathf.RoundToInt(ruin.plaster * RuinMore); ruin.leaves = Mathf.RoundToInt(ruin.leaves * RuinMore);
            ruin.straw = Mathf.RoundToInt(ruin.straw * RuinMore); ruin.tiles = Mathf.RoundToInt(ruin.tiles * RuinMore);
            ruin.dust = Mathf.RoundToInt(ruin.dust * RuinMore);
            // 整屋的碎屑算一件，下面都用世界坐标（y 以 c.fl 为地面）；整体抬到地面的实际表面上：
            // 榻榻米面在 fl + 6.5 mm（縁 7 mm），木地板、土间、瓷砖在 fl + 2 mm。之前没抬，薄的纸、水渍大多被席子盖住了
            k.Push(new Vector3(0f, IsTatami(room) ? .0072f : .002f, 0f));
            try
            {
                if (room.id == "Monooki" && CollapsedCeiling(k, c, rng)) n++;
                // 地上的水渍、霉斑（半透明的深色，叠几层；土间、浴室不画）
                if (!IsDoma(room) && room.id != "Furo" && room.id != "Benjo")
                {
                    int stains = IsCorridor(room) ? 1 + rng.Next(2) : 2 + rng.Next(2);
                    for (int i = 0; i < stains; i++)
                        if (FloorSpot(c, rng, .45f, 1.6f, out var p))
                        {
                            FloorStain(k, rng, p);
                            if (i == 0 && (room.id == "Zashiki" || room.id == "Ima")) ShotAt("stain_" + room.id, c, p, 1.6f, 1.5f);
                            n++;
                        }
                }
                // 和室：一扇断了的障子（上半截断掉，纸破了一半）靠在墙上
                if (IsTatami(room) && rng.NextDouble() < .7)
                    for (int t = 0; t < 8; t++)
                        if (FloorSpotE(c, rng, .4f, .05f, out var sp, out var se) && ShojiScrap(k, rng, c, se, sp))
                        {
                            if (room.id == "Zashiki" || room.id == "Shinshitsu")
                                RuinShots.Add(("shoji_" + room.id, sp + Inward(se) * 1.9f + Vector3.up * 1.55f + (se.alongX ? Vector3.right : Vector3.forward) * .5f, sp + Vector3.up * .55f));
                            n++;
                            break;
                        }
                if (room.id == "Rouka" && FloorSpot(c, rng, .3f, .3f, out var pp)) { FallenPortrait(k, rng, pp); n++; }
                for (int i = 0; i < ruin.plaster; i++)
                    if (FloorSpot(c, rng, .22f, .12f, out var p)) { PlasterFall(k, rng, p, wallMat); n++; }
                for (int i = 0; i < ruin.dust; i++)
                    if (CornerSpot(c, rng, .2f, out var p) || FloorSpot(c, rng, .2f, .1f, out p)) { DustPile(k, rng, p); n++; }
                for (int i = 0; i < ruin.paper; i++)
                    if (FloorSpot(c, rng, .28f, i % 3 == 0 ? 1.8f : .7f, out var p))
                    {
                        if (room.id == "Kodomo" && i % 2 == 0) { Drawing(k, rng, p); Crayons(k, rng, p); }
                        else Paper(k, rng, p);
                        n++;
                    }
                for (int i = 0; i < ruin.shards; i++)
                    if (FloorSpot(c, rng, .25f, 1.2f, out var p)) { Shards(k, rng, p); n++; }
                for (int i = 0; i < ruin.leaves; i++)
                    if (FloorSpot(c, rng, .35f, .5f, out var p)) { Leaves(k, rng, p, 8 + rng.Next(9)); n++; }
                for (int i = 0; i < ruin.straw; i++)
                    if (FloorSpot(c, rng, .3f, .6f, out var p)) { Sticks(k, rng, p, 14 + rng.Next(12), D.Straw, .08f, .28f, .0035f, .3f); n++; }
                for (int i = 0; i < ruin.tiles; i++)
                    if (FloorSpot(c, rng, .22f, .15f, out var p)) { TileChips(k, rng, p); n++; }
                for (int i = 0; i < ruin.weeds; i++)
                    if (FloorSpot(c, rng, .18f, .05f, out var p)) { Weed(k, rng, p); n++; }
                if (room.id == "Butsuma") // 散落的线香
                    for (int i = 0; i < 2; i++)
                        if (FloorSpot(c, rng, .2f, .6f, out var p)) { Sticks(k, rng, p, 9 + rng.Next(6), D.Bark, .1f, .14f, .0022f, .14f); n++; }
                if ((room.id == "Engawa" || room.id == "Genkan") && FloorSpot(c, rng, .35f, .3f, out var pot)) { ToppledModel(k, rng, pot, "planter_pot_clay", true); n++; }
                if (room.id == "Daidokoro" && FloorSpot(c, rng, .35f, .4f, out var pail)) { ToppledModel(k, rng, pail, "wooden_bucket_02", false); n++; }
                webSizes.Clear();
                webs = Cobwebs(k, c, rng, ruin.webs);
            }
            finally
            {
                k.Pop();
            }
            RuinReport.Add($"{room.id} {n}+网{webs}" + (webSizes.Count > 0 ? "[" + string.Join(" ", webSizes) + "]" : ""));
        }

        // =====================================================================
        // 找地方
        // =====================================================================
        static float Gauss(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
        }

        static bool Overlap2D(Bounds a, Bounds b, float pad) =>
            a.min.x - pad < b.max.x && a.max.x + pad > b.min.x && a.min.z - pad < b.max.z && a.max.z + pad > b.min.z;

        /// <summary>地上半径 rad 的一块是空的：不出房间，不碰门口 / 窗 / 门扇停靠处 / 容器正面 / 暗门地毯 / 交互站位，不在落地家具脚下。</summary>
        static bool FloorFree(FillCtx c, Vector3 p, float rad)
        {
            var r = c.inner;
            if (p.x - rad < r.xMin || p.x + rad > r.xMax || p.z - rad < r.yMin || p.z + rad > r.yMax) return false;
            var b = new Bounds(new Vector3(p.x, c.fl + .15f, p.z), new Vector3(2 * rad, .3f, 2 * rad));
            foreach (var h in c.hard) if (h.Intersects(b)) return false;
            foreach (var o in c.occ) if (o.min.y < c.fl + .4f && Overlap2D(o, b, .03f)) return false;
            foreach (var o in c.placed) if (o.min.y < c.fl + .4f && Overlap2D(o, b, .03f)) return false;
            foreach (var o in RuinUsed) if (Overlap2D(o, b, .02f)) return false;
            return true;
        }

        /// <summary>靠墙找一块地：离墙的距离 = 半径 + |正态| × spread（spread 小就贴着墙脚，大就散到屋子中间）。</summary>
        static bool FloorSpot(FillCtx c, System.Random rng, float rad, float spread, out Vector3 p) => FloorSpotE(c, rng, rad, spread, out p, out _);

        static bool FloorSpotE(FillCtx c, System.Random rng, float rad, float spread, out Vector3 p, out Edge edge)
        {
            var edges = new List<Edge>();
            foreach (var e in EdgesOf(c.room)) if (HasWall(c.room, e)) edges.Add(e);
            for (int t = 0; t < 18 && edges.Count > 0; t++)
            {
                var e = edges[rng.Next(edges.Count)];
                InnerSpan(c.room, e, out float lo, out float hi);
                if (hi - lo < 2 * rad + .05f) continue;
                float s = Rf(rng, lo + rad + .02f, hi - rad - .02f);
                float d = T + .03f + rad + Mathf.Abs(Gauss(rng)) * spread;
                p = OnWall(e, s, c.fl, d);
                edge = e;
                if (FloorFree(c, p, rad)) return true;
            }
            p = default;
            edge = default;
            return false;
        }

        static bool CornerSpot(FillCtx c, System.Random rng, float rad, out Vector3 p)
        {
            var r = c.inner;
            for (int t = 0; t < 10; t++)
            {
                bool east = rng.Next(2) == 0, north = rng.Next(2) == 0;
                float x = east ? r.xMax - rad - Rf(rng, .02f, .14f) : r.xMin + rad + Rf(rng, .02f, .14f);
                float z = north ? r.yMax - rad - Rf(rng, .02f, .14f) : r.yMin + rad + Rf(rng, .02f, .14f);
                p = new Vector3(x, c.fl, z);
                if (FloorFree(c, p, rad)) return true;
            }
            p = default;
            return false;
        }

        // =====================================================================
        // 地上的碎屑（p = 地面上的一点；Kit 当前是世界坐标）
        // =====================================================================
        /// <summary>墙皮：一片白粉，几块墙皮（和这间屋的墙同材质）和墙里的土块。</summary>
        static void PlasterFall(Kit k, System.Random rng, Vector3 p, Material wall)
        {
            k.PushS(p, new Vector3(0, Rf(rng, 0f, 360f), 0), Vector3.one * FurnitureScale);
            k.S(D.Ash, 0, Ly(), 0, Rf(rng, .1f, .19f), .003f, Rf(rng, .07f, .12f), 8);
            int n = 5 + rng.Next(7);
            for (int i = 0; i < n; i++)
            {
                float s = Rf(rng, .015f, .07f), r = Rf(rng, 0f, .17f), a = Rf(rng, 0f, 6.283f), h = s * Rf(rng, .3f, .55f);
                var m = rng.NextDouble() < .35 ? D.Dirt : wall;
                k.Br(m, Mathf.Cos(a) * r, h / 2 + .002f, Mathf.Sin(a) * r, s, h, s * Rf(rng, .6f, 1f),
                    Rf(rng, -18f, 18f), Rf(rng, 0f, 360f), Rf(rng, -18f, 18f));
            }
            k.Pop();
        }

        /// <summary>墙角的灰团：一片薄灰 + 几团绒。</summary>
        static void DustPile(Kit k, System.Random rng, Vector3 p)
        {
            k.PushS(p, new Vector3(0, Rf(rng, 0f, 360f), 0), Vector3.one * FurnitureScale);
            k.S(D.Ash, 0, Ly(), 0, Rf(rng, .1f, .19f), .006f, Rf(rng, .07f, .13f), 9);
            int n = 3 + rng.Next(3);
            for (int i = 0; i < n; i++)
                k.S(D.Ash, Rf(rng, -.09f, .09f), .006f, Rf(rng, -.07f, .07f), Rf(rng, .014f, .032f), Rf(rng, .008f, .014f), Rf(rng, .014f, .028f), 6);
            k.Pop();
        }

        /// <summary>旧报纸（字块 + 标题）、揉成团的纸，或者一张远端翘起来的纸（有时写着几行字）。</summary>
        static void Paper(Kit k, System.Random rng, Vector3 p)
        {
            k.PushS(p + new Vector3(0, Ly(), 0), new Vector3(0, Rf(rng, 0f, 360f), 0), Vector3.one * FurnitureScale);
            int kind = rng.Next(6);
            if (kind < 2) Newspaper(k, rng);
            else if (kind == 2)
                k.S(rng.Next(2) == 0 ? D.Paper : D.PaperOld, 0, .022f, 0, Rf(rng, .028f, .04f), Rf(rng, .02f, .03f), Rf(rng, .026f, .036f), 7);
            else
            {
                float w = Rf(rng, .18f, .23f), d = Rf(rng, .25f, .31f);
                var m = rng.Next(3) == 0 ? D.Paper : D.PaperOld;
                k.B(m, 0, 0, -d / 4, w, .0012f, d / 2);
                k.Push(Vector3.zero, 0f, -Rf(rng, 3f, 12f));
                k.B(m, 0, 0, d / 4, w, .0012f, d / 2);
                k.Pop();
                if (rng.NextDouble() < .4)
                    for (int i = 0; i < 4; i++) k.B(D.Ink, -w / 2 + .04f + i * .035f, .0008f, -d / 4, .004f, .0004f, d * .4f);
            }
            k.Pop();
        }

        /// <summary>
        /// 旧报纸（局部坐标，+z = 版面上方）：纸上分四五段，每段是一排竖排的小字（细线，长短不齐），
        /// 右上角一两条粗的竖标题，有的版面上还有一块照片；有的中间一道折痕。字都是贴在纸面上的平面（不加厚度）。
        /// </summary>
        static void Newspaper(Kit k, System.Random rng)
        {
            float w = Rf(rng, .3f, .38f), d = Rf(rng, .42f, .52f);
            k.B(D.PaperOld, 0, 0, 0, w, .0012f, d);
            const float y = .0008f, m = .018f;
            int bands = 4 + rng.Next(2);
            float gap = .01f, bh = (d - 2 * m - (bands - 1) * gap) / bands;
            // 标题（右上角，竖着）和照片：先定好位置，小字绕开它们
            var holes = new List<Rect>();
            float hx = w / 2 - m - .014f, hl = bh * Rf(rng, 1.2f, 1.9f);
            Flat(k, D.Ink, hx, y, d / 2 - m - hl / 2, .024f, hl);
            holes.Add(Rect.MinMaxRect(hx - .02f, d / 2 - m - hl - .01f, w / 2, d / 2));
            if (rng.NextDouble() < .6)
            {
                float hl2 = hl * Rf(rng, .45f, .75f);
                Flat(k, D.Ink, hx - .032f, y, d / 2 - m - hl2 / 2, .016f, hl2);
                holes.Add(Rect.MinMaxRect(hx - .048f, d / 2 - m - hl2 - .01f, hx - .02f, d / 2));
            }
            if (rng.NextDouble() < .55)
            {
                float pw = w * Rf(rng, .3f, .42f), ph = bh * Rf(rng, 1.1f, 1.6f);
                float px = Rf(rng, -w / 2 + m + pw / 2, 0f), pz = Rf(rng, -d / 2 + m + ph / 2, d / 2 - m - ph / 2 - bh);
                Flat(k, D.Ash, px, y, pz, pw, ph);
                Flat(k, D.Ink, px, y + .0001f, pz + ph * .1f, pw * .55f, ph * .45f); // 照片里的人影
                holes.Add(Rect.MinMaxRect(px - pw / 2 - .008f, pz - ph / 2 - .008f, px + pw / 2 + .008f, pz + ph / 2 + .008f));
            }
            for (int b = 0; b < bands; b++)
            {
                float top = d / 2 - m - b * (bh + gap);
                for (float x = w / 2 - m - .003f; x > -w / 2 + m; x -= .011f) // 竖排从右往左
                {
                    float len = rng.NextDouble() < .2 ? bh * Rf(rng, .3f, .8f) : bh;
                    float cz = top - len / 2;
                    bool blocked = false;
                    foreach (var h in holes) blocked |= h.Overlaps(Rect.MinMaxRect(x - .003f, top - len, x + .003f, top));
                    if (!blocked) Flat(k, D.PaperText, x, y, cz, .0045f, len);
                }
            }
            if (rng.NextDouble() < .5) Flat(k, D.PaperText, 0f, y + .0001f, 0f, w, .0035f); // 折痕
        }

        /// <summary>贴在面上的一个平的矩形（朝上，只有一面）。</summary>
        static void Flat(Kit k, Material m, float x, float y, float z, float w, float d)
        {
            var pts = new[]
            {
                new Vector3(x - w / 2, y, z - d / 2), new Vector3(x - w / 2, y, z + d / 2),
                new Vector3(x + w / 2, y, z + d / 2), new Vector3(x + w / 2, y, z - d / 2),
            };
            k.mb.Poly(m, pts, new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }, Vector3.up);
        }

        /// <summary>子供部屋的蜡笔画：乱涂几笔，三张里有一张画的是一个箱子，里面有个小人。</summary>
        static void Drawing(Kit k, System.Random rng, Vector3 p)
        {
            k.PushS(p + new Vector3(0, Ly(), 0), new Vector3(0, Rf(rng, 0f, 360f), 0), Vector3.one * FurnitureScale);
            k.B(D.Paper, 0, 0, 0, .27f, .0012f, .38f);
            if (rng.Next(3) == 0)
            {
                var m = rng.Next(2) == 0 ? D.ClothRed : D.Ink;
                const float bw = .13f, bd = .11f, cz = .03f;
                k.B(m, 0, .0009f, cz - bd / 2, bw, .0005f, .006f);
                k.B(m, 0, .0009f, cz + bd / 2, bw, .0005f, .006f);
                k.B(m, -bw / 2, .0009f, cz, .006f, .0005f, bd);
                k.B(m, bw / 2, .0009f, cz, .006f, .0005f, bd);
                k.Cr(D.Ink, new Vector3(0, .0007f, cz + .02f), .012f, .012f, .0005f, Quaternion.identity, 10);
                k.B(D.Ink, 0, .0009f, cz - .01f, .004f, .0005f, .035f);
            }
            else
            {
                Material[] crayon = { D.ClothRed, D.ClothIndigo, D.Leaf, D.Gold, D.Ink };
                for (int i = 0; i < 5; i++)
                    k.Br(crayon[rng.Next(crayon.Length)], Rf(rng, -.09f, .09f), .0009f, Rf(rng, -.14f, .14f),
                        Rf(rng, .04f, .11f), .0005f, .005f, 0, Rf(rng, 0f, 180f), 0);
            }
            k.Pop();
        }

        static void Crayons(Kit k, System.Random rng, Vector3 p)
        {
            Material[] cols = { D.ClothRed, D.ClothIndigo, D.Leaf, D.Gold, D.PlasticRed, D.Ink };
            int n = 1 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                var q = Quaternion.Euler(0, Rf(rng, 0f, 360f), 0) * AlongX;
                var b = p + new Vector3(Rf(rng, -.2f, .2f), .0045f, Rf(rng, -.2f, .2f));
                k.Cr(cols[rng.Next(cols.Length)], b, .0045f, .0045f, Rf(rng, .04f, .075f), q, 6);
            }
        }

        /// <summary>摔碎的瓷器：一圈碎片、半截碗壁斜立着，有时旁边还倒着一只没碎的茶碗（口朝外，里面黑的）。</summary>
        static void Shards(Kit k, System.Random rng, Vector3 p)
        {
            var m = rng.Next(3) == 0 ? D.CeramicBlue : D.CeramicWhite;
            k.PushS(p, new Vector3(0, Rf(rng, 0f, 360f), 0), Vector3.one * FurnitureScale);
            int n = 5 + rng.Next(6);
            for (int i = 0; i < n; i++)
            {
                float a = Rf(rng, 0f, 6.283f), r = Rf(rng, .01f, .19f);
                k.Br(m, Mathf.Cos(a) * r, .004f, Mathf.Sin(a) * r, Rf(rng, .012f, .045f), .004f, Rf(rng, .01f, .032f),
                    Rf(rng, -10f, 10f), Rf(rng, 0f, 360f), Rf(rng, -10f, 10f));
            }
            if (rng.NextDouble() < .6)
                k.Br(m, Rf(rng, -.05f, .05f), .016f, Rf(rng, -.05f, .05f), .065f, .03f, .004f, Rf(rng, -15f, 15f), Rf(rng, 0f, 360f), Rf(rng, 15f, 35f));
            if (rng.NextDouble() < .45)
            {
                var q = Quaternion.Euler(0, Rf(rng, 0f, 360f), 0) * AlongX;
                var b = new Vector3(Rf(rng, -.14f, .14f), .034f, Rf(rng, -.14f, .14f));
                k.Cr(m, b, .03f, .036f, .068f, q, 12);
                k.Cr(D.Black, b + q * Vector3.up * .0685f, .031f, .031f, .001f, q, 12);
            }
            k.Pop();
        }

        /// <summary>一堆枯叶（偶尔夹着红叶），每片带叶柄，微微翘着。</summary>
        static void Leaves(Kit k, System.Random rng, Vector3 p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Rf(rng, 0f, 6.283f), r = .32f * Mathf.Sqrt((float)rng.NextDouble());
                var m = rng.Next(4) == 0 ? D.MapleLeaf : D.LeafDead;
                k.Push(p + new Vector3(Mathf.Cos(a) * r, Ly() + .001f, Mathf.Sin(a) * r), Rf(rng, 0f, 360f), Rf(rng, -10f, 10f), Rf(rng, -10f, 10f));
                float L = Rf(rng, .022f, .036f);
                k.S(m, 0, 0, 0, L, .0015f, L * Rf(rng, .45f, .65f), 6);
                k.B(D.Bark, L * .95f, 0, 0, L * .5f, .0012f, .0012f);
                k.Pop();
            }
        }

        /// <summary>散在地上的细棍：稻草、线香。</summary>
        static void Sticks(Kit k, System.Random rng, Vector3 p, int n, Material m, float l0, float l1, float t, float spread)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Rf(rng, 0f, 6.283f), r = spread * Mathf.Sqrt((float)rng.NextDouble());
                k.Br(m, p.x + Mathf.Cos(a) * r, p.y + Ly() + t / 2, p.z + Mathf.Sin(a) * r, Rf(rng, l0, l1), t, t,
                    0, Rf(rng, 0f, 360f), Rf(rng, -2f, 2f));
            }
        }

        /// <summary>便所、風呂：一小片灰、几块掉下来的马赛克瓷砖。</summary>
        static void TileChips(Kit k, System.Random rng, Vector3 p)
        {
            k.S(D.Ash, p.x, p.y + Ly(), p.z, Rf(rng, .08f, .14f), .003f, Rf(rng, .06f, .1f), 8);
            int n = 4 + rng.Next(6);
            for (int i = 0; i < n; i++)
            {
                float a = Rf(rng, 0f, 6.283f), r = Rf(rng, 0f, .17f), s = Rf(rng, .015f, .03f);
                var m = rng.Next(4) == 0 ? D.CeramicWhite : D.CeramicBlue;
                k.Br(m, p.x + Mathf.Cos(a) * r, p.y + .004f, p.z + Mathf.Sin(a) * r, s, .005f, s * Rf(rng, .6f, 1f),
                    Rf(rng, -8f, 8f), Rf(rng, 0f, 360f), Rf(rng, -8f, 8f));
            }
        }

        /// <summary>土间墙脚长出来的枯草（Poly Haven 的杂草 / 枯荨麻）。</summary>
        static void Weed(Kit k, System.Random rng, Vector3 p)
        {
            // 两个模型里都是一排好几株（子物体），只取其中一株
            bool nettle = rng.Next(3) == 0;
            string id = nettle ? "nettle_plant" : "weed_plant_02";
            string only = nettle ? new[] { "small_a", "small_b", "medium_a", "medium_b" }[rng.Next(4)] : "02_" + (char)('a' + rng.Next(5));
            var size = Kit.ModelSize(id, 1f, only);
            if (size.y <= 0f) return;
            k.Model(id, p, Rf(rng, 0f, 360f), Rf(rng, .2f, .45f) / size.y, only);
        }

        /// <summary>倒在地上的模型（花盆洒出一摊土；木桶口朝外）。</summary>
        static void ToppledModel(Kit k, System.Random rng, Vector3 p, string id, bool spill)
        {
            var size = Kit.ModelSize(id, FurnitureScale);
            if (size.y <= 0f) return;
            float yaw = Rf(rng, 0f, 360f);
            k.Model(id, p, yaw, FurnitureScale, null, 90f);
            if (!spill) return;
            var dir = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var c = p + dir * (size.y * .5f + .08f);
            k.S(D.Dirt, c.x, p.y + .006f, c.z, Rf(rng, .1f, .16f), .02f, Rf(rng, .08f, .12f), 9);
            for (int i = 0; i < 5; i++)
                k.S(D.Dirt, c.x + Rf(rng, -.12f, .12f), p.y + .008f, c.z + Rf(rng, -.1f, .1f), .02f, .012f, .018f, 6);
            RuinUsed.Add(new Bounds(p, new Vector3(.7f, .4f, .7f)));
        }

        /// <summary>地上的水渍 / 霉斑：两三层不规则的半透明深色斑叠在一起，中间深、边上浅。</summary>
        static void FloorStain(Kit k, System.Random rng, Vector3 p)
        {
            if (D.Stain == null) return;
            float R = Rf(rng, .22f, .45f);
            int layers = 2 + rng.Next(2);
            for (int l = 0; l < layers; l++)
            {
                float rr = l == 0 ? R : R * Rf(rng, .4f, .7f);
                var off = l == 0 ? Vector3.zero : new Vector3(Rf(rng, -.3f, .3f) * R, 0f, Rf(rng, -.3f, .3f) * R);
                Blob(k, D.Stain, p + off + new Vector3(0f, Ly(), 0f), rr, Rf(rng, .55f, 1f), rng);
            }
            RuinUsed.Add(new Bounds(p, new Vector3(R, .1f, R))); // 中间那一块不再压别的碎屑（边上可以）
        }

        /// <summary>地上一块不规则的平面（中心点 + 一圈三角扇）：rr = 平均半径，squash = 横向压扁。</summary>
        static void Blob(Kit k, Material m, Vector3 c, float rr, float squash, System.Random rng)
        {
            const int N = 18;
            float yaw = Rf(rng, 0f, 6.283f), p1 = Rf(rng, 0f, 6.283f), p2 = Rf(rng, 0f, 6.283f);
            float a1 = Rf(rng, .1f, .2f), a2 = Rf(rng, .05f, .12f);
            var pts = new Vector3[N + 2];
            var uv = new Vector2[N + 2];
            pts[0] = c;
            uv[0] = new Vector2(.5f, .5f);
            for (int i = 0; i < N; i++)
            {
                float a = i * 6.283f / N;
                float r = rr * (1f + a1 * Mathf.Sin(2 * a + p1) + a2 * Mathf.Sin(5 * a + p2) + Rf(rng, -.07f, .07f));
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r * squash;
                pts[i + 1] = c + new Vector3(x * Mathf.Cos(yaw) - z * Mathf.Sin(yaw), 0f, x * Mathf.Sin(yaw) + z * Mathf.Cos(yaw));
                uv[i + 1] = new Vector2(.5f + .5f * Mathf.Cos(a), .5f + .5f * Mathf.Sin(a));
            }
            pts[N + 1] = pts[1];
            uv[N + 1] = uv[1];
            k.mb.Poly(m, pts, uv, Vector3.up);
        }

        /// <summary>
        /// 和室：一扇断了的障子靠在墙上——上半截断掉了，两根竖框一高一低、组子的断茬朝上，一根横组子耷拉着，纸破了一半；
        /// 脚下几片碎纸和一截掉下来的组子。从地到顶不碰门、窗、门扇停靠处、容器正面、家具和挂件，离步行路线 0.45 m 以上；
        /// 放一个薄挡块（人不会从它身上穿过去）。
        /// </summary>
        static bool ShojiScrap(Kit k, System.Random rng, FillCtx c, Edge e, Vector3 p)
        {
            float w = Rf(rng, .74f, .84f), h = Rf(rng, 1.05f, 1.3f), lean = Rf(rng, 12f, 18f);
            float s = e.alongX ? p.x : p.z;
            float sin = Mathf.Sin(lean * Mathf.Deg2Rad), cos = Mathf.Cos(lean * Mathf.Deg2Rad);
            const float sd = .034f, sw = .032f, rail = .07f; // 框子的厚、宽，下框高
            float dBase = T + .006f + h * sin + sd / 2 * cos;  // 底边离墙线：顶上的背面刚好靠在墙面上
            var vol = BoxOnWall(e, s - w / 2 - .05f, s + w / 2 + .05f, c.fl + .02f, c.fl + h + .08f, T + .002f, dBase + sd + .12f);
            foreach (var b in c.hard) if (b.Intersects(vol)) return false;
            foreach (var b in c.occ) if (b.Intersects(vol)) return false;
            foreach (var b in c.placed) if (b.Intersects(vol)) return false;
            foreach (var b in RuinUsed) if (Overlap2D(b, vol, .02f)) return false;
            var foot = BoxOnWall(e, s - w / 2, s + w / 2, c.fl, c.fl + 1.1f, T, dBase + sd / 2);
            if (LaneClearance(foot) < .45f) return false;

            var basePos = OnWall(e, s, c.fl + .006f, dBase);
            var wood = D.TrimLight;
            bool leftShort = rng.Next(2) == 0;
            float hL = leftShort ? h - Rf(rng, .15f, .35f) : h, hR = leftShort ? h : h - Rf(rng, .15f, .35f);
            float hTop = Mathf.Min(hL, hR);
            k.Push(basePos, WallYaw(e), -lean); // 局部：x 沿墙，y 顺着门扇往上，+z 朝屋里
            // 竖框（断口上一根劈开的木刺）、下框、下框上的灰
            foreach (var (x, hh) in new[] { (-w / 2 + sw / 2, hL), (w / 2 - sw / 2, hR) })
            {
                k.Bb(wood, x, 0f, 0f, sw, hh, sd);
                k.Br(wood, x + Rf(rng, -.006f, .006f), hh + .022f, Rf(rng, -.004f, .004f), sw * .45f, .055f, sd * .6f, Rf(rng, -10f, 10f), 0f, Rf(rng, -22f, 22f));
            }
            k.Bb(wood, 0f, 0f, 0f, w - 2 * sw + .002f, rail, sd);
            k.B(D.Ash, 0f, rail + .0015f, .004f, w - 2 * sw - .01f, .003f, sd * .7f);
            // 组子：竖 3 根、横的每 0.2 m 一根（到断的地方为止）；竖组子的断茬长短不一，顶上一小截歪着
            const float kw = .011f, kd = .018f, kz = -.007f;
            float ix0 = -w / 2 + sw, ix1 = w / 2 - sw, cw = (ix1 - ix0) / 4;
            var rows = new List<float> { rail };
            for (float y = rail + .2f; y < hTop - .06f; y += .2f) rows.Add(y);
            float lastRow = rows[rows.Count - 1];
            for (int j = 1; j <= 3; j++)
            {
                float top = lastRow + Rf(rng, .03f, .19f), x = ix0 + j * cw;
                k.Bb(wood, x, rail, kz, kw, top - rail, kd);
                k.Br(wood, x + Rf(rng, -.004f, .004f), top + .014f, kz, kw * .6f, .032f, kd * .7f, 0f, 0f, Rf(rng, -25f, 25f));
            }
            for (int i = 1; i < rows.Count; i++) k.Bb(wood, 0f, rows[i] - kw / 2, kz, ix1 - ix0, kw, kd);
            // 长的那根竖框上还挂着半截横组子，耷拉下来
            float yd = lastRow + .2f, hLong = Mathf.Max(hL, hR);
            if (yd < hLong - .03f)
            {
                float sx = leftShort ? 1f : -1f, len = cw * Rf(rng, 1.2f, 1.8f), droop = Rf(rng, 18f, 32f), ang = droop * Mathf.Deg2Rad;
                k.Br(wood, sx * (ix1 - len / 2 * Mathf.Cos(ang)), yd - len / 2 * Mathf.Sin(ang), kz, len, kw, kd, 0f, 0f, sx * droop);
            }
            // 纸（贴在背面）：一半还在，一些只剩上半截、破口斜着，其余的格子是空的
            float pz = -sd / 2 + .0006f;
            for (int i = 0; i + 1 < rows.Count; i++)
                for (int j = 0; j < 4; j++)
                {
                    float x0 = ix0 + j * cw + (j > 0 ? kw / 2 : 0f), x1 = ix0 + (j + 1) * cw - (j < 3 ? kw / 2 : 0f);
                    float y0 = rows[i] + (i > 0 ? kw / 2 : 0f), y1 = rows[i + 1] - kw / 2;
                    var pm = rng.Next(4) == 0 ? D.Paper : D.PaperOld;
                    double roll = rng.NextDouble();
                    if (roll < .5) k.B(pm, (x0 + x1) / 2, (y0 + y1) / 2, pz, x1 - x0, y1 - y0, .0012f);
                    else if (roll < .75)
                    {
                        float ph = (y1 - y0) * Rf(rng, .3f, .65f);
                        k.B(pm, (x0 + x1) / 2, y1 - ph / 2, pz, x1 - x0, ph, .0012f);
                        float tw = (x1 - x0) * Rf(rng, .4f, .7f), tx = Rf(rng, x0 + tw / 2, x1 - tw / 2);
                        k.Br(pm, tx, y1 - ph - .012f, pz, tw, .03f, .0012f, 0f, 0f, Rf(rng, -28f, 28f)); // 破口
                    }
                }
            k.Pop();
            // 脚下：几片碎纸、一截掉下来的组子
            var ax = e.alongX ? Vector3.right : Vector3.forward;
            var inw = Inward(e);
            int scraps = 3 + rng.Next(3);
            for (int i = 0; i < scraps; i++)
            {
                var q = basePos + ax * Rf(rng, -w / 2, w / 2) + inw * Rf(rng, .04f, .32f);
                k.Br(rng.Next(3) == 0 ? D.Paper : D.PaperOld, q.x, c.fl + Ly(), q.z, Rf(rng, .04f, .1f), .0012f, Rf(rng, .03f, .08f), 0f, Rf(rng, 0f, 360f), 0f);
            }
            {
                var q = basePos + ax * Rf(rng, -w / 3, w / 3) + inw * Rf(rng, .12f, .3f);
                k.Br(wood, q.x, c.fl + kd / 2, q.z, Rf(rng, .15f, .3f), kd, kw, 0f, Rf(rng, 0f, 360f), 0f);
            }
            Blocker(c.room.id + "_ShojiScrap", OnWall(e, s, c.fl, (T + dBase + sd / 2) / 2), WallYaw(e), new Vector3(w, 1.1f, dBase + sd / 2 - T));
            RuinUsed.Add(vol);
            return true;
        }

        /// <summary>走廊地上一张遗影，正面朝上，玻璃从一点裂开，旁边几粒玻璃渣。</summary>
        static void FallenPortrait(Kit k, System.Random rng, Vector3 p)
        {
            const float w = .3f, h = .42f, iw = w * .78f / 2, ih = h * .82f / 2;
            k.Push(p + new Vector3(0, .0155f, 0), Rf(rng, 0f, 360f), -90f);
            PortraitFrame(k, w, h);
            var hit = new Vector3(Rf(rng, -.05f, .05f), Rf(rng, -.07f, .07f), .0166f);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 51f + Rf(rng, -15f, 15f);
                var dv = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f);
                float mx = Mathf.Abs(dv.x) > 1e-3f ? ((dv.x > 0 ? iw : -iw) - hit.x) / dv.x : 99f;
                float my = Mathf.Abs(dv.y) > 1e-3f ? ((dv.y > 0 ? ih : -ih) - hit.y) / dv.y : 99f;
                float len = Mathf.Min(Rf(rng, .06f, .17f), Mathf.Min(mx, my) - .004f);
                if (len < .02f) continue;
                k.Br(D.CeramicWhite, hit.x + dv.x * len / 2, hit.y + dv.y * len / 2, hit.z, .0012f, len, .0004f, 0, 0, a - 90f);
            }
            k.Pop();
            for (int i = 0; i < 6; i++)
                k.Br(D.Glass, p.x + Rf(rng, -.28f, .28f), p.y + .002f, p.z + Rf(rng, -.28f, .28f), Rf(rng, .01f, .03f), .002f, Rf(rng, .008f, .02f),
                    0, Rf(rng, 0f, 360f), 0);
            RuinUsed.Add(new Bounds(p, new Vector3(.6f, .2f, .6f)));
        }

        // =====================================================================
        // 蜘蛛网
        // =====================================================================
        /// <summary>
        /// 天花四个角：按概率挂一张角网（回り縁下面）。网的范围碰到门、窗、门扇停靠处、容器正面、家具或挂件就缩小再试，
        /// 缩到很小还碰就不挂。返回挂了几张。
        /// </summary>
        static int Cobwebs(Kit k, FillCtx c, System.Random rng, float prob)
        {
            if (D.Cobweb == null || prob <= 0f) return 0;
            var r = c.room.r;
            float y = c.ceil - .04f;
            float x0 = r.xMin + T + .03f, x1 = r.xMax - T - .03f, z0 = r.yMin + T + .03f, z1 = r.yMax - T - .03f;
            float maxU = (x1 - x0) * .42f, maxV = (z1 - z0) * .42f, maxH = (c.ceil - c.fl) * .24f;
            var corners = new[]
            {
                (new Vector3(x0, y, z0), Vector3.right, Vector3.forward), (new Vector3(x1, y, z0), Vector3.left, Vector3.forward),
                (new Vector3(x0, y, z1), Vector3.right, Vector3.back), (new Vector3(x1, y, z1), Vector3.left, Vector3.back),
            };
            int n = 0;
            foreach (var (o, u, v) in corners)
            {
                if (rng.NextDouble() >= prob) continue;
                float size = Rf(rng, .45f, .8f);
                for (int t = 0; t < 4; t++, size *= .72f)
                {
                    float a = Mathf.Min(size * Rf(rng, .8f, 1.2f), maxU), b = Mathf.Min(size * Rf(rng, .8f, 1.2f), maxV);
                    float h = Mathf.Min(size * Rf(rng, .5f, .85f), maxH), sag = h * Rf(rng, .45f, .75f);
                    var bb = new Bounds(o, Vector3.zero);
                    bb.Encapsulate(o + u * a);
                    bb.Encapsulate(o + v * b);
                    bb.Encapsulate(o + Vector3.down * Mathf.Max(h * 1.5f, sag + .5f)); // 第二层网 / 垂丝的最低点
                    // 门、门扇停靠处只管到门楣上面一点：门头上（欄間前）的墙角可以挂网，门扇推过去碰不到
                    bool hit = false;
                    float clip = c.fl + DoorTop + .1f;
                    for (int i = 0; i < c.hardFixed && !hit; i++)
                    {
                        var hb = c.hard[i];
                        if (hb.min.y < clip && hb.max.y > clip) hb.SetMinMax(hb.min, new Vector3(hb.max.x, clip, hb.max.z));
                        hit = hb.Intersects(bb);
                    }
                    foreach (var q in c.occ) hit |= q.Intersects(bb);
                    foreach (var q in c.placed) hit |= q.Intersects(bb);
                    if (hit) continue;
                    Cobweb(k, rng, o, u, v, a, b, h, sag);
                    webSizes.Add($"{Mathf.Max(a, b):0.00}");
                    if (n == 0 && (c.room.id == "Zashiki" || c.room.id == "Kura" || c.room.id == "Daidokoro"))
                        RuinShots.Add(("web_" + c.room.id, o + (u + v).normalized * 2.0f + Vector3.down * (c.ceil - c.fl - 1.6f), o + Vector3.down * h * .5f));
                    n++;
                    break;
                }
            }
            return n;
        }

        /// <summary>
        /// 一张角网：o = 墙角在天花下的一点，u、v = 沿两面墙离开墙角的方向；网面是 A（墙 1 顶上）、B（墙 2 顶上）、C（墙角往下 h）
        /// 围成的三角，两条边贴墙，A–B 这条边悬空往下垂（中间垂 sag，还往屋里鼓一点）。
        /// 网面细分成小三角，随机破几个洞、垂边更破；三成的网下面再挂一层小的。上面是放射丝、三圈横丝、几粒灰，
        /// 垂边上挂下来两三根带灰团的丝。
        /// </summary>
        static void Cobweb(Kit k, System.Random rng, Vector3 o, Vector3 u, Vector3 v, float a, float b, float h, float sag)
        {
            var outward = (u + v).normalized;
            var nrm = (outward + Vector3.down * .8f).normalized;
            Vector3 A = o + u * a, B = o + v * b, C = o + Vector3.down * h;
            float p1 = Rf(rng, 0f, 6.283f), p2 = Rf(rng, 0f, 6.283f);
            Vector3 Pt(float wa, float wb) => WebPt(A, B, C, sag, outward, p1, p2, wa, wb);
            WebSheet(k, rng, A, B, C, sag, outward, nrm, p1, p2, .28f);
            if (rng.NextDouble() < .65) // 第二层：小一点、挂得更低
            {
                var A2 = o + u * a * Rf(rng, .5f, .8f) + Vector3.down * h * Rf(rng, .1f, .3f);
                var B2 = o + v * b * Rf(rng, .5f, .8f) + Vector3.down * h * Rf(rng, .1f, .3f);
                var C2 = o + Vector3.down * h * Rf(rng, 1.15f, 1.45f);
                WebSheet(k, rng, A2, B2, C2, sag * 1.3f, outward, nrm, Rf(rng, 0f, 6.283f), Rf(rng, 0f, 6.283f), .36f);
            }
            // 放射丝：从网中间（偏墙角）到一圈边上的 8 个点，顺着网面弯下去
            var hub = new Vector2(.3f, .3f);
            var rim = new[]
            {
                new Vector2(0f, 0f), new Vector2(.5f, 0f), new Vector2(1f, 0f), new Vector2(.75f, .25f), new Vector2(.5f, .5f),
                new Vector2(.25f, .75f), new Vector2(0f, 1f), new Vector2(0f, .5f),
            };
            const int seg = 4;
            for (int i = 0; i < rim.Length; i++)
                for (int s = 0; s < seg; s++)
                {
                    var w0 = Vector2.Lerp(hub, rim[i], s / (float)seg);
                    var w1 = Vector2.Lerp(hub, rim[i], (s + 1) / (float)seg);
                    Strand(k, Pt(w0.x, w0.y), Pt(w1.x, w1.y), .004f);
                }
            // 网的三条边（贴墙的两条 + 垂边）
            for (int s = 0; s < 6; s++)
            {
                float t0 = s / 6f, t1 = (s + 1) / 6f;
                Strand(k, Pt(t0, 0f), Pt(t1, 0f), .0035f);
                Strand(k, Pt(0f, t0), Pt(0f, t1), .0035f);
                Strand(k, Pt(1f - t0, t0), Pt(1f - t1, t1), .0035f);
            }
            // 三圈横丝
            foreach (float f in new[] { .3f, .55f, .8f })
                for (int i = 0; i < rim.Length; i++)
                {
                    var w0 = Vector2.Lerp(hub, rim[i], f);
                    var w1 = Vector2.Lerp(hub, rim[(i + 1) % rim.Length], f);
                    if (rng.NextDouble() < .15) continue; // 断了几根
                    Strand(k, Pt(w0.x, w0.y), Pt(w1.x, w1.y), .0028f);
                }
            // 粘在网上的灰
            int specks = 4 + rng.Next(5);
            for (int i = 0; i < specks; i++)
            {
                float wa = Rf(rng, .1f, .6f), wb = Rf(rng, .1f, .9f - wa);
                k.mb.Sphere(D.Ash, Pt(wa, wb), new Vector3(Rf(rng, .006f, .012f), Rf(rng, .004f, .008f), Rf(rng, .006f, .012f)), 6);
            }
            // 垂边上挂下来的丝，下面坠着灰团
            int hang = 2 + rng.Next(2);
            for (int i = 0; i < hang; i++)
            {
                float t = Rf(rng, .2f, .8f);
                var top = Pt(t, 1f - t);
                var kink = top + new Vector3(Rf(rng, -.03f, .03f), -Rf(rng, .08f, .2f), Rf(rng, -.03f, .03f));
                var end = kink + new Vector3(Rf(rng, -.02f, .02f), -Rf(rng, .07f, .25f), Rf(rng, -.02f, .02f));
                Strand(k, top, kink, .003f);
                Strand(k, kink, end, .003f);
                if (rng.NextDouble() < .75)
                    k.mb.Sphere(D.Ash, end, new Vector3(Rf(rng, .008f, .014f), Rf(rng, .01f, .02f), Rf(rng, .008f, .014f)), 7);
            }
        }

        /// <summary>网面上的一点：wa、wb = A、B 的重心坐标（C 的是 1 − wa − wb）。贴墙的两条边不动，越靠垂边中间垂得越多。</summary>
        static Vector3 WebPt(Vector3 A, Vector3 B, Vector3 C, float sag, Vector3 outward, float p1, float p2, float wa, float wb)
        {
            float f = 4f * wa * wb * (1f + .2f * Mathf.Sin(7f * wa + p1) * Mathf.Sin(5f * wb + p2));
            return A * wa + B * wb + C * (1f - wa - wb) + Vector3.down * sag * f + outward * sag * .22f * f;
        }

        /// <summary>细分成 6 × 6 的小三角，随机破洞（垂边一带更多）。</summary>
        static void WebSheet(Kit k, System.Random rng, Vector3 A, Vector3 B, Vector3 C, float sag, Vector3 outward, Vector3 nrm, float p1, float p2, float holes)
        {
            const int N = 6;
            var uv3 = new[] { Vector2.zero, Vector2.right, Vector2.up };
            Vector3 P(int i, int j) => WebPt(A, B, C, sag, outward, p1, p2, i / (float)N, j / (float)N);
            for (int i = 0; i < N; i++)
                for (int j = 0; i + j < N; j++)
                {
                    float edge = i + j + 1 >= N ? .18f : 0f;
                    if (rng.NextDouble() >= holes + edge) k.mb.Poly(D.Cobweb, new[] { P(i, j), P(i + 1, j), P(i, j + 1) }, uv3, nrm);
                    if (i + j + 2 <= N && rng.NextDouble() >= holes + (i + j + 2 >= N ? .18f : 0f))
                        k.mb.Poly(D.Cobweb, new[] { P(i + 1, j), P(i + 1, j + 1), P(i, j + 1) }, uv3, nrm);
                }
        }

        static void Strand(Kit k, Vector3 a, Vector3 b, float t)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            k.mb.Box(D.WebStrand, (a + b) / 2, new Vector3(t, len, t), Quaternion.FromToRotation(Vector3.up, d / len));
        }

        /// <summary>两点之间的一根方料（宽 w 水平、厚 t）。</summary>
        static void Beam(Kit k, Material m, Vector3 a, Vector3 b, float w, float t)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            var dir = d / len;
            var up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > .99f ? Vector3.forward : Vector3.up;
            k.mb.Box(m, (a + b) / 2, new Vector3(w, t, len), Quaternion.LookRotation(dir, up));
        }

        // =====================================================================
        // 物置的塌顶
        // =====================================================================
        /// <summary>
        /// 物置的天花塌了一块：找一块从地到天花都空着、离步行路线 0.5 m 以上的地方（尽量靠东侧、南门进来右手边看得见），
        /// 天花上一个不规则的黑洞，洞边一圈断茬、三块垂下来的天花板、漏下来的稻草和土；一根断梁一头还卡在洞边、斜着垂下来
        /// （断口劈开，下端离地 2.3 m 以上）；地上一个尖土堆，斜插着几块天花板、横着一截断梁，土块、墙皮、稻草落在堆上和四周；
        /// 挡块 1.5 m 高（不让人踩上去）。
        /// </summary>
        static bool CollapsedCeiling(Kit k, FillCtx c, System.Random rng)
        {
            const float fw = 1.05f, fd = .75f;
            var pref = new Vector2(15.45f, 22.25f);
            var r = c.inner;
            bool found = false, rot = false;
            float bestScore = float.MaxValue;
            var q = Vector3.zero;
            for (int o = 0; o < 2; o++)
            {
                float hx = (o == 0 ? fw : fd) / 2, hz = (o == 0 ? fd : fw) / 2;
                for (float x = r.xMin + hx + .05f; x <= r.xMax - hx - .05f; x += .05f)
                    for (float z = r.yMin + hz + .05f; z <= r.yMax - hz - .05f; z += .05f)
                    {
                        float score = Vector2.Distance(new Vector2(x, z), pref);
                        if (score >= bestScore) continue;
                        var foot = new Bounds(new Vector3(x, c.fl + .2f, z), new Vector3(2 * hx, .4f, 2 * hz));
                        var col = new Bounds(new Vector3(x, (c.fl + c.ceil) / 2, z), new Vector3(2 * hx + .24f, c.ceil - c.fl - .1f, 2 * hz + .24f));
                        if (!ColumnFree(c, foot, col)) continue;
                        bestScore = score;
                        q = new Vector3(x, c.fl, z);
                        rot = o == 1;
                        found = true;
                    }
            }
            if (!found)
            {
                Debug.LogWarning("[Hako Art] 物置塌顶：找不到从地到天花都空着的地方，跳过");
                return false;
            }
            float W = rot ? fd : fw, Dd = rot ? fw : fd, cy = c.ceil;
            RuinUsed.Add(new Bounds(new Vector3(q.x, c.fl + .2f, q.z), new Vector3(W + .3f, .4f, Dd + .3f)));

            // 天花上的洞
            var hc = new Vector3(q.x + Rf(rng, -.08f, .08f), cy - .003f, q.z + Rf(rng, -.06f, .06f));
            float hw = W * .85f, hd = Dd * .8f;
            k.B(D.Black, hc.x, hc.y, hc.z, hw, .002f, hd);
            k.Br(D.Black, hc.x + hw * .2f, hc.y - .0004f, hc.z - hd * .15f, hw * .7f, .002f, hd * .9f, 0, 28f, 0);
            k.Br(D.Black, hc.x - hw * .25f, hc.y - .0008f, hc.z + hd * .2f, hw * .55f, .002f, hd * .7f, 0, -33f, 0);
            // 洞边一圈断茬
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * 6.283f + Rf(rng, -.2f, .2f);
                var edge = hc + new Vector3(Mathf.Cos(a) * hw * .5f, -.004f, Mathf.Sin(a) * hd * .5f);
                var dir = new Vector3(-Mathf.Cos(a), 0, -Mathf.Sin(a));
                float len = Rf(rng, .08f, .22f), drop = Rf(rng, 15f, 45f) * Mathf.Deg2Rad;
                Beam(k, i % 3 == 0 ? D.CeilingDark : D.Ceiling, edge, edge + dir * len * Mathf.Cos(drop) + Vector3.down * len * Mathf.Sin(drop), Rf(rng, .06f, .12f), .01f);
            }
            // 垂下来的天花板
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + Rf(rng, -.5f, .5f);
                var hinge = hc + new Vector3(Mathf.Cos(a) * hw * .45f, -.006f, Mathf.Sin(a) * hd * .45f);
                var dir = new Vector3(-Mathf.Cos(a), 0, -Mathf.Sin(a));
                float len = Rf(rng, .55f, 1.0f), drop = Rf(rng, 58f, 80f) * Mathf.Deg2Rad;
                Beam(k, i == 1 ? D.CeilingDark : D.Ceiling, hinge, hinge + dir * len * Mathf.Cos(drop) + Vector3.down * len * Mathf.Sin(drop), Rf(rng, .16f, .24f), .012f);
            }
            // 漏下来的稻草和土
            for (int i = 0; i < 10; i++)
            {
                float a = Rf(rng, 0f, 6.283f);
                var top = hc + new Vector3(Mathf.Cos(a) * hw * Rf(rng, .3f, .5f), -.004f, Mathf.Sin(a) * hd * Rf(rng, .3f, .5f));
                Beam(k, D.Straw, top, top + new Vector3(Rf(rng, -.04f, .04f), -Rf(rng, .12f, .45f), Rf(rng, -.04f, .04f)), .004f, .004f);
            }
            for (int i = 0; i < 4; i++)
                k.S(D.Dirt, hc.x + Rf(rng, -hw * .4f, hw * .4f), hc.y - .03f, hc.z + Rf(rng, -hd * .4f, hd * .4f),
                    Rf(rng, .05f, .1f), Rf(rng, .02f, .04f), Rf(rng, .05f, .09f), 7);
            bool alongX = W >= Dd;
            var ax = alongX ? Vector3.right : Vector3.forward;   // 塌口的长边
            var ay = alongX ? Vector3.forward : Vector3.right;
            float L = Mathf.Max(W, Dd), S = Mathf.Min(W, Dd);
            // 断梁：一头还卡在洞边，斜着垂下来，断口劈开（下端离地 2.3 m 以上，人碰不到）
            {
                var top = hc - ax * hw * .45f * (alongX ? 1f : hd / hw) + ay * Rf(rng, -.1f, .1f) + Vector3.down * .02f;
                float run = L * .8f, drop = Mathf.Clamp(cy - (c.fl + 2.35f), .25f, .75f);
                var dir = (ax + ay * Rf(rng, -.15f, .15f)).normalized;
                var bot = top + dir * run + Vector3.down * drop;
                Beam(k, D.BeamDark, top, bot, .1f, .09f);
                var along = (bot - top).normalized;
                for (int i = 0; i < 4; i++)
                {
                    var sp = bot + new Vector3(Rf(rng, -.03f, .03f), Rf(rng, -.03f, .03f), Rf(rng, -.03f, .03f));
                    var d = (along + new Vector3(Rf(rng, -.35f, .35f), Rf(rng, -.35f, .35f), Rf(rng, -.35f, .35f))).normalized;
                    Beam(k, D.BeamDark, sp - along * .02f, sp + d * Rf(rng, .06f, .16f), Rf(rng, .012f, .03f), Rf(rng, .01f, .025f));
                }
            }
            // 地上：一层灰；土堆（下面三团大的矮的，上面三团小的高的，堆出一个尖）
            k.S(D.Ash, q.x, c.fl + .004f, q.z, W * .62f, .006f, Dd * .62f, 12);
            var mounds = new List<(Vector3 c, Vector3 r)>();
            for (int i = 0; i < 3; i++)
                mounds.Add((q + ax * Rf(rng, -L * .18f, L * .18f) + ay * Rf(rng, -S * .15f, S * .15f),
                    ax * Rf(rng, L * .26f, L * .34f) + ay * Rf(rng, S * .3f, S * .38f) + Vector3.up * Rf(rng, .1f, .16f)));
            for (int i = 0; i < 3; i++)
                mounds.Add((q + ax * Rf(rng, -L * .1f, L * .1f) + ay * Rf(rng, -S * .08f, S * .08f),
                    ax * Rf(rng, .15f, .22f) + ay * Rf(rng, .13f, .19f) + Vector3.up * Rf(rng, .2f, .3f)));
            for (int i = 0; i < mounds.Count; i++)
            {
                var (mc, mr) = mounds[i];
                k.S(i % 2 == 0 ? D.Dirt : D.Ash, mc.x, c.fl, mc.z, Mathf.Abs(mr.x), mr.y, Mathf.Abs(mr.z), 12);
            }
            float HeapY(Vector3 at)
            {
                float best = 0f;
                foreach (var (mc, mr) in mounds)
                {
                    float dx = (at.x - mc.x) / Mathf.Abs(mr.x), dz = (at.z - mc.z) / Mathf.Abs(mr.z), s2 = 1f - dx * dx - dz * dz;
                    if (s2 > 0f) best = Mathf.Max(best, mr.y * Mathf.Sqrt(s2));
                }
                return c.fl + best;
            }
            // 掉下来的天花板：大致顺着同一个方向（天花板是一排木板掉下来的），一块挨一块斜搭在土堆上；两根断了的竿縁横在上面
            float boardYaw = alongX ? 0f : 90f;
            for (int i = 0; i < 6; i++)
            {
                var at = q + ax * Rf(rng, -L * .2f, L * .2f) + ay * ((i - 2.5f) * S * .13f + Rf(rng, -.03f, .03f));
                k.Br(i % 3 == 1 ? D.CeilingDark : D.Ceiling, at.x, HeapY(at) + .012f, at.z, Rf(rng, .55f, .9f), .012f, Rf(rng, .15f, .22f),
                    Rf(rng, -8f, 8f), boardYaw + Rf(rng, -22f, 22f), Rf(rng, -24f, 24f));
            }
            for (int i = 0; i < 2; i++)
            {
                var at = q + ax * Rf(rng, -L * .15f, L * .15f) + ay * Rf(rng, -S * .2f, S * .2f);
                k.Br(D.TrimMid, at.x, HeapY(at) + .03f, at.z, .032f, .03f, Rf(rng, .7f, 1.05f), Rf(rng, -12f, 12f), boardYaw + Rf(rng, -30f, 30f), 0f);
            }
            // 一截断梁横在土堆上：一头着地，一头搭在堆尖上
            {
                var lo = q - ax * L * .42f + ay * Rf(rng, -S * .2f, S * .2f);
                var hi = q + ax * L * Rf(rng, .05f, .2f) + ay * Rf(rng, -S * .15f, S * .15f);
                Beam(k, D.BeamDark, lo + Vector3.up * (c.fl - lo.y + .045f), hi + Vector3.up * (HeapY(hi) - hi.y + .03f), .09f, .08f);
            }
            // 土块、墙皮（落在土堆表面）
            for (int i = 0; i < 18; i++)
            {
                float s = Rf(rng, .02f, .08f), a = Rf(rng, 0f, 6.283f), rr = Rf(rng, .1f, 1f);
                var at = q + new Vector3(Mathf.Cos(a) * rr * (W / 2 + .06f), 0f, Mathf.Sin(a) * rr * (Dd / 2 + .06f));
                k.Br(rng.Next(3) == 0 ? D.PlasterWhite : D.Dirt, at.x, HeapY(at) + s * .2f, at.z,
                    s, s * .5f, s * .8f, Rf(rng, -25f, 25f), Rf(rng, 0f, 360f), Rf(rng, -25f, 25f));
            }
            // 稻草：堆上和四周
            for (int i = 0; i < 40; i++)
            {
                float a = Rf(rng, 0f, 6.283f), rr = L * .55f * Mathf.Sqrt((float)rng.NextDouble());
                var at = q + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr * S / L);
                k.Br(D.Straw, at.x, HeapY(at) + .004f, at.z, Rf(rng, .1f, .3f), .0035f, .0035f, 0f, Rf(rng, 0f, 360f), Rf(rng, -20f, 20f));
            }
            Blocker(c.room.id + "_CeilingCollapse", q, 0f, new Vector3(W * .9f, 1.5f, Dd * .9f));
            Debug.Log($"[Hako Art] 物置塌顶：({q.x:0.00}, {q.z:0.00})，{W:0.00} x {Dd:0.00} m");
            return true;
        }

        /// <summary>塌顶那一块：地面不碰门口 / 容器正面 / 暗门，从地到天花不碰任何家具、灯、挂件，离步行路线 0.5 m 以上。</summary>
        static bool ColumnFree(FillCtx c, Bounds foot, Bounds col)
        {
            foreach (var h in c.hard) if (h.Intersects(foot)) return false;
            foreach (var o in c.occ) if (o.Intersects(col)) return false;
            foreach (var o in c.placed) if (o.Intersects(col)) return false;
            return LaneClearance(foot) >= .5f;
        }
    }
}
