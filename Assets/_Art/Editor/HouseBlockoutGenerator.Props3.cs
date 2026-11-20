using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第三轮：家具与陈设。
    /// <list type="bullet">
    /// <item>容器正面前 1 m 不放陈设（第二遍搬家后的旧陈设撤掉，第三遍补家具当禁区）。</item>
    /// <item>陈设互相穿插的诊断（日志里列出来，逐个修）。</item>
    /// <item>外部 CC0 模型做的件（ExternalModels）：胶靴、扫除用具、梯子、凳子、油罐、提灯、倒下的书架……</item>
    /// <item>Met 的 CC0 古画：屏风、挂轴。</item>
    /// </list>
    /// 依然不做看起来能搜的东西：箱子只做敞开的空箱或明显封死的（绳子、封条、积灰）。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        // =====================================================================
        // 容器前方 1 m
        // =====================================================================
        /// <summary>每个容器正面前 1 m 的禁区（宽 = 容器宽 + 两边各 10 cm，6 cm 以上到 1.8 m；平的地毯、纸不算）。</summary>
        static List<Bounds> ContainerFronts = new List<Bounds>();

        const float FrontDepth = 1f;

        static List<Bounds> ComputeContainerFronts(Transform root)
        {
            var list = new List<Bounds>();
            var cont = root.Find("Containers");
            if (cont == null) return list;
            foreach (Transform t in cont)
            {
                var wb = WorldBounds(t);
                if (wb == null) continue;
                var b = wb.Value;
                var ip = t.Find("InteractPoint");
                var dir = ip != null ? ip.position - b.center : t.forward;
                dir.y = 0f;
                dir = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? new Vector3(Mathf.Sign(dir.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(dir.z));
                var along = new Vector3(Mathf.Abs(dir.x), 0f, Mathf.Abs(dir.z));
                var side = new Vector3(along.z, 0f, along.x);
                float half = Vector3.Dot(b.extents, along), width = Vector3.Dot(b.size, side) + .2f;
                float y0 = b.min.y + .06f, y1 = b.min.y + 1.8f;
                var c = b.center + dir * (half + .02f + FrontDepth / 2);
                c.y = (y0 + y1) / 2;
                list.Add(new Bounds(c, side * width + along * FrontDepth + Vector3.up * (y1 - y0)));
            }
            return list;
        }

        static Bounds MapBounds(Matrix4x4 m, Bounds b)
        {
            var r = new Bounds(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, ExternalModels.Corner(0))), Vector3.zero);
            for (int i = 1; i < 8; i++) r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, ExternalModels.Corner(i))));
            return r;
        }

        static float Vol(Bounds b) => b.size.x * b.size.y * b.size.z;

        static float OverlapVol(Bounds a, Bounds b)
        {
            var mn = Vector3.Max(a.min, b.min);
            var mx = Vector3.Min(a.max, b.max);
            var d = mx - mn;
            return d.x > 0 && d.y > 0 && d.z > 0 ? d.x * d.y * d.z : 0f;
        }

        /// <summary>第二遍之前：搬家后落在容器正面 1 m 内的旧陈设（和它们的挡块）撤掉。</summary>
        static void ClearContainerFronts()
        {
            var xs = Kit.ItemXform;
            var ib = Kit.ItemBounds;
            var solid = Kit.ItemSolid;
            if (xs == null || ContainerFronts.Count == 0) return;
            int n = Mathf.Min(xs.Count, ib.Count);
            var dropped = new List<Bounds>();
            var names = new List<string>();
            for (int i = 0; i < n; i++)
            {
                if (Kit.IsDropped(xs[i]) || (i < solid.Count && solid[i]) || ib[i].size == Vector3.zero) continue;
                var b = MapBounds(xs[i], ib[i]);
                if (b.size.y < .06f) continue; // 平的（地毯、纸）不挡
                foreach (var f in ContainerFronts)
                {
                    if (!b.Intersects(f) || OverlapVol(b, f) < 1e-5f) continue;
                    xs[i] = Matrix4x4.zero;
                    dropped.Add(b);
                    names.Add($"#{i}({b.center.x:0.0},{b.center.y:0.0},{b.center.z:0.0})");
                    break;
                }
            }
            int blockers = 0;
            for (int i = 0; i < n; i++)
            {
                if (!(i < solid.Count && solid[i]) || Kit.IsDropped(xs[i])) continue;
                var b = MapBounds(xs[i], ib[i]);
                bool hit = ContainerFronts.Any(f => OverlapVol(b, f) > 1e-5f) || dropped.Any(d => OverlapVol(b, d) > .5f * Vol(b));
                if (!hit) continue;
                xs[i] = Matrix4x4.zero;
                blockers++;
            }
            Debug.Log($"[Hako Art] 容器正面 1 m 内的旧陈设撤掉 {dropped.Count} 件、挡块 {blockers} 个" +
                      (names.Count > 0 ? "：" + string.Join(" ", names) : ""));
        }

        /// <summary>第三轮换掉的场景物体（BuildProps 里的洋式马桶、旧洗脸台）：搬家求解算完再删，求解的输入不变。</summary>
        static readonly string[] ReplacedProps = { "Benjo_Toilet", "Furo_Washstand" };

        static void RemoveReplacedProps(Transform root)
        {
            var props = root.Find("Props");
            if (props == null) return;
            foreach (var n in ReplacedProps)
            {
                var t = props.Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
        }

        /// <summary>陈设之间互相穿插（旧陈设 × 场景家具、场景家具 × 场景家具）：按重叠比例列出最严重的。</summary>
        static void ReportOverlaps(Transform root, List<Bounds> kitBounds, List<Matrix4x4> xform, List<bool> kitSolid)
        {
            var props = new List<(string name, Bounds b)>();
            var pr = root.Find("Props");
            if (pr != null)
                foreach (Transform t in pr)
                {
                    if (t.name == "Dressing_Blockers") continue;
                    var wb = WorldBounds(t);
                    if (wb != null && wb.Value.size.y > .05f) props.Add((t.name, wb.Value));
                }
            var kit = new List<(string name, Bounds b)>();
            if (xform != null)
                for (int i = 0; i < Mathf.Min(kitBounds.Count, xform.Count); i++)
                {
                    if (Kit.IsDropped(xform[i]) || (i < kitSolid.Count && kitSolid[i]) || kitBounds[i].size == Vector3.zero) continue;
                    var b = MapBounds(xform[i], kitBounds[i]);
                    if (b.size.y > .03f) kit.Add(($"陈设#{i}", b));
                }
            var hits = new List<(float ratio, string text)>();
            void Check((string name, Bounds b) a, (string name, Bounds b) c)
            {
                float ov = OverlapVol(a.b, c.b);
                if (ov < 4e-4f) return;
                float ratio = ov / Mathf.Max(1e-6f, Mathf.Min(Vol(a.b), Vol(c.b)));
                if (ratio < .25f) return;
                hits.Add((ratio, $"{a.name}@({a.b.center.x:0.00},{a.b.center.y:0.00},{a.b.center.z:0.00}) 尺寸{a.b.size.x:0.00}x{a.b.size.y:0.00}x{a.b.size.z:0.00} × " +
                                 $"{c.name}@({c.b.center.x:0.00},{c.b.center.y:0.00},{c.b.center.z:0.00}) 重叠{ratio:P0}"));
            }
            foreach (var k in kit) foreach (var p in props) Check(k, p);
            for (int i = 0; i < props.Count; i++)
                for (int j = i + 1; j < props.Count; j++) Check(props[i], props[j]);
            hits.Sort((a, b) => b.ratio.CompareTo(a.ratio));
            Debug.Log($"[Hako Art] 陈设穿插检查：{hits.Count} 处" + (hits.Count > 0 ? "（最严重的 25 处）\n" + string.Join("\n", hits.Take(25).Select(h => h.text)) : ""));
        }

        // =====================================================================
        // 外部模型件（局部坐标同其他件：正面朝 +z，背面贴 z=0，原点在墙脚中点；真实尺寸，外面再放大）
        // =====================================================================
        static Piece PBoots() => new Piece { name = "Boots", w = .5f, d = .36f, h = .4f, draw = DrawBoots };
        static Piece PCleaning() => new Piece { name = "CleaningTools", w = .75f, d = .45f, h = 1.5f, blockH = .45f, corner = true, draw = (k, r) => DrawCleaning(k, r, false) };
        static Piece PLadder() => new Piece { name = "Ladder", w = 1.0f, d = .64f, h = 1.75f, blockH = 1.7f, draw = DrawLadder };
        static Piece PStool(bool fallen) => new Piece { name = fallen ? "StoolFallen" : "Stool", w = .5f, d = .5f, h = .46f, blockH = fallen ? 0f : .44f, draw = (k, r) => DrawStool(k, r, fallen) };
        static Piece POilTins() => new Piece { name = "OilTins", w = .55f, d = .32f, h = .32f, corner = true, draw = DrawOilTins };
        static Piece PMouseTrap() => new Piece { name = "MouseTrap", w = .14f, d = .16f, h = .05f, draw = (k, r) => k.Model("mousetrap", new Vector3(0, 0, .08f), Rf(r, -25f, 25f)) };
        static Piece PWateringCan() => new Piece { name = "WateringCan", w = .5f, d = .26f, h = .26f, draw = (k, r) => k.Model("watering_can_metal_01", new Vector3(0, 0, .14f), Rf(r, 75f, 105f)) };
        static Piece PSpade() => new Piece { name = "Spade", w = .3f, d = .22f, h = 1.12f, draw = (k, r) => k.Model("rusted_spade_01", new Vector3(Rf(r, -.03f, .03f), 0, .1f), Rf(r, -10f, 10f), 1f, null, -11f) };
        static Piece PFallenShelf() => new Piece { name = "FallenShelf", w = 1.15f, d = 1.75f, h = .55f, blockH = .38f, draw = DrawFallenShelf };
        static Piece PBookshelfWorn() => new Piece { name = "BookshelfWorn", w = 1.15f, d = .5f, h = 1.7f, blockH = 1.65f, draw = DrawBookshelfWorn };
        static Piece PLanternHook() => new Piece { name = "LanternHook", w = .2f, d = .14f, h = .36f, lift = 1.35f, wall = true, draw = DrawLanternHook };

        static void DrawBoots(Kit k, System.Random rng)
        {
            k.Model("rubber_boots", new Vector3(-.1f, 0, .15f), Rf(rng, -15f, 15f), 1f, "dirt_r");
            if (rng.Next(3) > 0) k.Model("rubber_boots", new Vector3(.13f, 0, .2f), Rf(rng, 55f, 115f), 1f, "dirty_l", 0f, 90f); // 倒在地上
            else k.Model("rubber_boots", new Vector3(.11f, 0, .15f), Rf(rng, -12f, 12f), 1f, "dirty_l");
        }

        /// <summary>靠墙的长柄刷、木桶、簸箕（便所里再加一个马桶搋子）。</summary>
        static void DrawCleaning(Kit k, System.Random rng, bool toilet)
        {
            k.Model("wooden_broom", new Vector3(-.2f, 0, .12f), Rf(rng, -6f, 6f), 1f, null, -13f);
            k.Model("wooden_bucket_01", new Vector3(.17f, 0, .2f), Rf(rng, 0f, 360f));
            k.Model("dustpan", new Vector3(-.02f, 0, .34f), Rf(rng, -35f, 35f));
            if (toilet) k.Model("plunger", new Vector3(.32f, 0, .1f), Rf(rng, 0f, 360f), 1f, null, -8f);
        }

        static void DrawLadder(Kit k, System.Random rng)
        {
            k.Model("wooden_ladder_02", new Vector3(Rf(rng, -.04f, .04f), 0, .33f), Rf(rng, -4f, 4f));
            // 横档上搭一块破布
            k.Push(new Vector3(.1f, .62f, .33f), 0f, 0f, 4f);
            k.Bb(D.ClothWhite, 0, 0, 0, .36f, .006f, .5f);
            k.Bb(D.ClothWhite, .18f, -.2f, 0, .006f, .2f, .48f);
            k.Pop();
        }

        static void DrawStool(Kit k, System.Random rng, bool fallen)
        {
            string id = rng.Next(3) == 0 ? "wooden_stool_02" : "wooden_stool_01";
            float s = id == "wooden_stool_02" ? 1.4f : 1f;
            if (fallen) k.Model(id, new Vector3(Rf(rng, -.05f, .05f), 0, .27f), Rf(rng, 0f, 360f), s, null, 0f, Rf(rng, 95f, 105f));
            else k.Model(id, new Vector3(0, 0, .26f), Rf(rng, -20f, 20f), s);
        }

        static void DrawOilTins(Kit k, System.Random rng)
        {
            k.Model("oil_tin", new Vector3(-.16f, 0, .12f), Rf(rng, -20f, 20f));
            k.Model("oil_tin", new Vector3(.02f, 0, .15f), Rf(rng, 60f, 120f), 1f, null, 0f, 90f); // 倒了的
            k.Model("Lantern_01", new Vector3(.2f, 0, .17f), Rf(rng, 0f, 360f));
            k.C(D.Water, .03f, .001f, .21f, .12f, .12f, .002f, 16); // 漏出来的油渍
        }

        /// <summary>书架整个往前扑倒（正面朝下、顶朝屋里），书从下面和顶头散出来。</summary>
        static void DrawFallenShelf(Kit k, System.Random rng)
        {
            const float s = .8f; // 外面再放大 1.25 = 模型真实尺寸
            k.Model("wooden_bookshelf_worn", new Vector3(0, 0, .86f), Rf(rng, -5f, 5f), s, null, 90f);
            for (int i = 0; i < 14; i++)
            {
                float x, z;
                if (i < 6) { x = Rf(rng, -.55f, .55f); z = Rf(rng, 1.68f, 1.95f); }       // 顶头
                else if (i < 10) { x = (i % 2 == 0 ? -1f : 1f) * Rf(rng, .5f, .62f); z = Rf(rng, .3f, 1.6f); } // 两边
                else { x = Rf(rng, -.45f, .45f); z = Rf(rng, .05f, .2f); }                  // 墙脚
                LooseBook(k, rng, x, z);
            }
        }

        static void LooseBook(Kit k, System.Random rng, float x, float z)
        {
            Material[] covers = { D.ClothRed, D.ClothIndigo, D.ClothPurple, D.TrimMid, D.Black, D.ClothWhite };
            var cover = covers[rng.Next(covers.Length)];
            float w = Rf(rng, .1f, .13f), d = Rf(rng, .15f, .19f), t = Rf(rng, .012f, .03f);
            bool open = rng.Next(4) == 0;
            k.Push(new Vector3(x, 0, z), Rf(rng, 0f, 360f));
            if (open)
            {
                // 摊开、扣在地上的书
                k.Br(cover, -w / 2, .012f, 0, w, .004f, d, 0, 0, 8f);
                k.Br(cover, w / 2, .012f, 0, w, .004f, d, 0, 0, -8f);
                k.Bb(D.Paper, 0, .004f, 0, 2 * w - .01f, .012f, d - .01f);
            }
            else
            {
                k.Bb(cover, 0, 0, 0, w, t, d);
                k.Bb(D.Paper, .004f, .002f, 0, w - .006f, t - .004f, d - .008f);
            }
            k.Pop();
        }

        /// <summary>立着的旧书架：书东倒西歪，有几格空了，书掉在下面。</summary>
        static void DrawBookshelfWorn(Kit k, System.Random rng)
        {
            const float s = .8f;
            k.Model("wooden_bookshelf_worn", new Vector3(0, 0, .24f), 0f, s);
            // 模型 1.374 x 2.063 x 0.581（真实）→ 局部 1.1 x 1.65 x .465；层板高度按模型目测
            float[] shelves = { .09f, .41f, .73f, 1.05f, 1.37f };
            foreach (float y in shelves)
            {
                if (rng.Next(3) == 0) continue; // 空的一格
                float x = -.46f;
                while (x < .44f)
                {
                    float bw = Rf(rng, .025f, .05f), bh = Rf(rng, .17f, .24f);
                    if (rng.Next(6) == 0) { x += Rf(rng, .05f, .14f); continue; }
                    var m = rng.Next(4) switch { 0 => D.ClothRed, 1 => D.ClothIndigo, 2 => D.TrimMid, _ => D.ClothPurple };
                    bool lean = rng.Next(5) == 0;
                    k.Br(m, x + bw / 2, y + bh / 2 + .005f, .24f, bw, bh, .15f, 0, 0, lean ? 18f : 0f);
                    x += bw + .003f;
                }
            }
            for (int i = 0; i < 4; i++) LooseBook(k, rng, Rf(rng, -.45f, .45f), Rf(rng, .55f, .8f));
        }

        static void DrawLanternHook(Kit k, System.Random rng)
        {
            k.B(D.Iron, 0, .33f, .02f, .012f, .012f, .05f);           // 墙上的钉子
            k.Model("Lantern_01", new Vector3(0, .33f - .294f + .005f, .065f), Rf(rng, -15f, 15f));
        }

        /// <summary>
        /// 倒在地上的红书包（ランドセル）：背面着地、开口朝上，里面是空的（黑），翻盖整个掀开摊在地上；
        /// 背带压在两边；本子、铅笔盒、铅笔、竖笛撒在一旁——一看就是已经被翻过、空了。p = 原来书包的位置（旧坐标）。
        /// </summary>
        static void EmptiedRandoseru(Kit k, Vector3 p, float yaw, System.Random rng)
        {
            k.Push(p, yaw);
            Debug.Log($"[Hako Art] 红书包（第二遍）在 {k.mb.M.MultiplyPoint3x4(Vector3.zero):F2}");
            k.Bb(D.PlasticRed, 0, 0, 0, .25f, .17f, .3f);
            k.Bb(D.Black, 0, .168f, 0, .215f, .004f, .262f);
            k.Bb(D.PlasticRed, 0, .145f, -.14f, .25f, .03f, .025f);
            k.B(D.Brass, 0, .09f, -.152f, .045f, .03f, .006f);
            // 翻盖：后沿铰链处弯下去，平摊在地上
            k.Br(D.PlasticRed, 0, .095f, .175f, .25f, .17f, .012f, -18f, 0, 0);
            k.Bb(D.PlasticRed, 0, 0, .38f, .25f, .012f, .38f);
            k.Bb(D.Black, 0, .012f, .38f, .2f, .002f, .3f); // 翻盖内侧的旧污
            // 背带
            foreach (float s in new[] { -1f, 1f })
                k.Br(D.PlasticRed, s * .19f, .004f, .02f, .035f, .007f, .34f, 0, s * 14f, 0);
            // 撒出来的东西（都在书包西边，远离书桌）
            Material[] covers = { D.ClothIndigo, D.Paper, D.ClothRed };
            for (int i = 0; i < 3; i++)
            {
                k.Push(new Vector3(-.3f - i * .07f + Rf(rng, -.03f, .03f), i * .007f, -.12f + i * .12f), Rf(rng, -40f, 40f));
                k.Bb(covers[i], 0, 0, 0, .18f, .005f, .25f);
                k.Bb(D.Paper, .002f, .005f, 0, .17f, .002f, .24f);
                k.Pop();
            }
            k.Push(new Vector3(-.22f, 0, -.33f), Rf(rng, 50f, 80f));
            k.Bb(D.ClothIndigo, 0, 0, 0, .21f, .03f, .07f);
            k.Br(D.ClothIndigo, 0, .045f, .05f, .21f, .005f, .07f, -60f, 0, 0); // 打开的盖
            k.Pop();
            for (int i = 0; i < 3; i++)
                k.Cr(D.TrimLight, new Vector3(-.42f + i * .05f, .004f, -.4f + Rf(rng, -.04f, .04f)), .004f, .004f, .17f,
                    Quaternion.Euler(0, Rf(rng, 0f, 180f), 90f), 6);
            k.Cr(D.CeramicWhite, new Vector3(-.12f, .012f, -.48f), .012f, .01f, .32f, Quaternion.Euler(0, Rf(rng, 60f, 110f), 90f), 10);
            k.Pop();
        }

        /// <summary>吊在天花上的瓷灯座 + 拉链 + 灯泡（不亮）。p = 天花上的点（局部）。</summary>
        static void BareBulb(Kit k, Vector3 p, float yaw = 0f)
        {
            k.Push(p, yaw);
            k.mb.Model("pull_chain_light_socket", ExternalModels.Place("pull_chain_light_socket", null, new Vector3(0, -.165f, 0), Quaternion.identity, 1f));
            // 灯泡倒挂（螺口朝上），拧在灯座下面
            k.mb.Model("lightbulb_01", ExternalModels.Place("lightbulb_01", null, new Vector3(0, -.165f - .1f + .012f, 0), Quaternion.Euler(180f, 0f, 0f), 1f));
            k.Pop();
        }

        // =====================================================================
        // Met 的古画（CC0，来源见 Textures/External/README_来源.md）
        // =====================================================================
        const string MetMatDir = "Assets/_Art/Materials/External/Met";

        static readonly Dictionary<string, Material> metMats = new Dictionary<string, Material>();

        /// <summary>古画材质：发黄、哑光（只建一次；手调不覆盖）。</summary>
        static Material MetMat(string oid)
        {
            if (metMats.TryGetValue(oid, out var cached) && cached != null) return cached;
            string path = $"{MetMatDir}/M_Met_{oid}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ExternalMats.MetDir}/{oid}.jpg");
                if (tex == null) { Debug.LogWarning($"[Hako Art] 缺少古画贴图：{oid}"); return null; }
                ArtKit.EnsureFolder(MetMatDir);
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Met_" + oid };
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", new Color(.74f, .68f, .56f, 1f)); // 旧纸发黄、积灰
                m.SetFloat("_Smoothness", .08f);
                m.SetFloat("_Metallic", 0f);
                AssetDatabase.CreateAsset(m, path);
            }
            metMats[oid] = m;
            return m;
        }

        /// <summary>
        /// 每张古画照片里真正的画面范围（UV，v 从下往上；去掉照片背景、屏风边框、挂轴的绫子），
        /// 按 5% 网格目测（Tools~ 外的 met_grid 联系表）。
        /// </summary>
        static Rect MetContent(string oid)
        {
            static Rect R(float u0, float v0, float u1, float v1) => Rect.MinMaxRect(u0, v0, u1, v1);
            switch (oid)
            {
                case "45072": return R(.03f, .12f, .97f, .93f);   // 雪村 山水図屏風
                case "45373": return R(.02f, .10f, .97f, .94f);   // 長谷川左近 仙人図屏風
                case "65670": return R(.065f, .13f, .935f, .855f); // 桜・梅・柳図屏風（金地）
                case "49070": return R(.11f, .02f, .89f, .98f);   // 梅に叭々鳥
                case "816190": return R(.11f, .03f, .89f, .97f);  // 寂室元光 墨跡
                case "44855": return R(.025f, .02f, .97f, .98f);  // 芦雁図
                case "45739": return R(.14f, .04f, .85f, .95f);   // 寿老人
                case "845126": return R(.12f, .06f, .875f, .92f); // 不動明王の印仏
                default: return new Rect(0, 0, 1, 1);
            }
        }

        /// <summary>画面范围的宽高比（按贴图像素）。</summary>
        static float MetAspect(Material m, Rect uv)
        {
            var tex = m != null ? m.GetTexture("_BaseMap") : null;
            return tex == null ? 1f : tex.width * uv.width / (tex.height * uv.height);
        }

        /// <summary>竖着的画面（局部 xy 平面，朝 +z）：中心 c，尺寸 w x h，贴 uv 那一块。</summary>
        static void PictureQuad(Kit k, Material m, Vector3 c, float w, float h, Rect uv)
        {
            if (m == null) return;
            var pts = new[] { c + new Vector3(-w / 2, -h / 2, 0), c + new Vector3(w / 2, -h / 2, 0), c + new Vector3(w / 2, h / 2, 0), c + new Vector3(-w / 2, h / 2, 0) };
            var uvs = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
            k.mb.Poly(m, pts, uvs, Vector3.forward);
        }

        /// <summary>
        /// 屏风用哪幅画、从六扇里的第几扇开始取四扇：寝室 45373（仙人，人物在右边四扇），
        /// 金屏风 65670（樱、梅、柳，取中间），其余素屏风 45072（雪村山水，湖和主峰在右边四扇）。
        /// </summary>
        static (string oid, int first) ByobuPainting(Room room, bool gold) =>
            room != null && room.id == "Shinshitsu" ? ("45373", 2) : gold ? ("65670", 1) : ("45072", 2);

        /// <summary>挂轴用哪幅画；null = 保留原来的手绘（幽灵画）。</summary>
        static string ScrollPainting(Room room, int kind)
        {
            if (room != null && room.id == "Butsuma") return "845126"; // 不動明王の印仏
            switch (kind)
            {
                case 0: return "816190"; // 寂室元光 墨迹
                case 1: return "44855";  // 鉄舟徳済 芦雁図
                case 2: return room != null && room.id == "Washitsu2F" ? "45739" : "49070"; // 寿老人 / 梅に叭々鳥
                default: return null;
            }
        }
    }
}
