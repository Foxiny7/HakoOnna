using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第五轮（室内，2026-11-22）：第四轮剩下的、还是方块 / 圆柱拼的东西——糸車、木马、柱时计、手压泵（这个文件），
    /// 铠甲、雏人形、人台、缝纫机（Props5b），小便器、蹲便、碁盤、玩具、小陶瓷（Props5c）。
    /// 都找不到合适的 CC0 模型，用车削（Lathe / LatheArc）、弯管（Limb / Tube）、倒角木件（BevelBox）和已有的 CC0 贴图重做。
    /// <list type="bullet">
    /// <item>这些都是第三遍（按房间补家具）或固定设备里画的，搬家已经算完，只要不超出原来声明的占地就不影响别的东西。</item>
    /// <item>随机数：原来在画的过程中取几次随机数，现在还取几次、顺序不变（后面的摆放靠同一条序列）。</item>
    /// <item>局部坐标：x 沿墙、z 从墙面往屋里、y 朝上，单位米（外面统一放大 1.25）。</item>
    /// </list>
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        // =====================================================================
        // 工具
        // =====================================================================
        /// <summary>回转体的一段弧（a0…a1，度；点 = (cos a · r, y, sin a · r)，+z 是 90°）。剖面写法同 Lathe。</summary>
        static void LatheArc(MeshBatch mb, Material m, Vector3 at, Quaternion rot, IList<Vector2> prof, int seg, float a0, float a1)
        {
            int n = prof.Count;
            if (n < 2 || m == null || mb.Mute) return;
            var pn = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var acc = Vector2.zero;
                if (i > 0) { var d = prof[i] - prof[i - 1]; if (d.sqrMagnitude > 1e-12f) acc += new Vector2(d.y, -d.x).normalized; }
                if (i < n - 1) { var d = prof[i + 1] - prof[i]; if (d.sqrMagnitude > 1e-12f) acc += new Vector2(d.y, -d.x).normalized; }
                pn[i] = acc.sqrMagnitude > 1e-12f ? acc.normalized : Vector2.up;
            }
            var G = new Vector3[seg + 1, n];
            var GN = new Vector3[seg + 1, n];
            var GU = new Vector2[seg + 1, n];
            for (int s = 0; s <= seg; s++)
            {
                float a = Mathf.Lerp(a0, a1, s / (float)seg) * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float arc = 0f;
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) arc += (prof[i] - prof[i - 1]).magnitude;
                    var p = prof[i];
                    G[s, i] = at + rot * new Vector3(ca * p.x, p.y, sa * p.x);
                    GN[s, i] = rot * new Vector3(ca * pn[i].x, pn[i].y, sa * pn[i].x);
                    GU[s, i] = new Vector2(a * Mathf.Max(p.x, .02f), arc);
                }
            }
            EmitGrid(mb, m, G, GN, GU);
        }

        /// <summary>一圈闭合的细管（箍、绳圈、轮辋）：圆在 axis 垂直的平面里。</summary>
        static void Hoop(MeshBatch mb, Material m, Vector3 c, Vector3 axis, float radius, float r, int n = 20, int seg = 5, float squashZ = 1f)
        {
            axis.Normalize();
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.right : Vector3.up).normalized;
            var v = Vector3.Cross(axis, u);
            var pts = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                var p = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
                p.z *= squashZ;
                pts.Add(c + p);
            }
            Tube(mb, m, pts, r, seg, true);
        }

        static Material propDial, propGoban;
        static bool propDecalsLoaded;

        /// <summary>Tools~/make_props_r5.py 画的两张贴花：钟面（裁切）、碁盤的线（半透明）。缺贴图时为 null。</summary>
        static void PropDecals()
        {
            if (propDecalsLoaded && propDial != null) return;
            propDecalsLoaded = true;
            ExternalMats.EnsureImportSettings();
            propDial = PropDecal("MD_ClockDial", "T_ClockDial", false, .12f);
            propGoban = PropDecal("MD_GobanGrid", "T_GobanGrid", true, .2f);
        }

        static Material PropDecal(string name, string tex, bool blend, float smooth)
        {
            string path = $"{DetailMats.Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ExternalMats.DecalDir}/{tex}.png");
            if (t == null)
            {
                Debug.LogWarning($"[Hako Art] 缺少贴花 {tex}.png");
                return null;
            }
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_CastShadows", 0f);
            m.SetShaderPassEnabled("ShadowCaster", false);
            if (blend)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_BlendModePreserveSpecular", 0f);
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent - 10;
                m.SetShaderPassEnabled("DepthOnly", false);
            }
            else
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", .5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Vector2 P2(float r, float y) => new Vector2(r, y);

        /// <summary>
        /// 审图用：沿墙补家具时这一版布局里可能放不下的件，临时摆在院墙外的一块空地上（只拍照、不存进场景）。
        /// 机位记成 r5_gal_*（白天的光）。生成完紧接着调用（要用到这次生成的材质）。
        /// </summary>
        internal static GameObject BuildReviewGallery()
        {
            if (D == null) return null;
            var root = new GameObject("ReviewGallery_Temp");
            var items = new (string name, System.Action<Kit> draw)[]
            {
                ("itoguruma", DrawSpinningWheel), ("rockinghorse", DrawRockingHorse), ("pump", DrawPump), ("sewing", DrawSewing),
                ("ichimatsu", kk => IchimatsuDoll(kk, new Vector3(0f, 0f, .25f), .3f, D.ClothRed, D.ClothPurple)),
            };
            Kit.ItemXform = null;
            Kit.BeginPass(false);
            var k = new Kit();
            var floor = new MeshBatch();
            for (int i = 0; i < items.Length; i++)
            {
                var o = new Vector3(70f + i * 4f, 0f, -40f);
                floor.Box(D.Tataki, o + new Vector3(0f, -.05f, .5f), new Vector3(3.6f, .1f, 3.6f));
                floor.Box(D.PlasterWhite, o + new Vector3(0f, 1.1f, -.06f), new Vector3(3.6f, 2.2f, .1f));
                k.PushS(o, Vector3.zero, Vector3.one * FurnitureScale);
                items[i].draw(k);
                k.Pop();
                bool small = items[i].name == "ichimatsu";
                RuinShots.Add(("r5_gal_" + items[i].name, o + (small ? new Vector3(.35f, .45f, 1.0f) : new Vector3(1.1f, 1.15f, 2.3f)), o + new Vector3(0f, small ? .2f : .6f, .3f)));
            }
            floor.Build(root.transform, "GalleryFloor", false);
            k.mb.Build(root.transform, "GalleryProps");
            return root;
        }

        // =====================================================================
        // 糸車（用户定：不用欧式纺车的 CC0 模型，做日本的）
        // =====================================================================
        /// <summary>
        /// 糸車：一根长木做台、两头横脚；一头两根车柱架着大轮（两扇细竹辐条，辐条梢用绳之字形连起来当轮辋）和摇把，
        /// 另一头两根小柱架着铁锭子，锭子上一团纺了一半的棉线，调绳从大轮绕到锭子上。占地 1.0 × 0.42，高 0.85。
        /// </summary>
        static void DrawSpinningWheel(Kit k)
        {
            var mb = k.mb;
            const float z = .21f, R = .29f;
            var fwd = Vector3.forward;
            var wood = D.TrimMid;
            BevelBox(mb, wood, new Vector3(-.04f, .075f, z), new Vector3(.84f, .05f, .075f), .008f);
            foreach (float sx in new[] { -.42f, .3f }) BevelBox(mb, wood, new Vector3(sx, .025f, z), new Vector3(.07f, .05f, .36f), .008f);
            var c = new Vector3(.2f, .52f, z);
            foreach (float s in new[] { -.09f, .09f })
            {
                BevelBox(mb, wood, new Vector3(c.x, .33f, z + s), new Vector3(.045f, .5f, .032f), .006f);
                // 车柱脚下的斜撑
                mb.Box(wood, new Vector3(c.x - .09f, .17f, z + s), new Vector3(.022f, .24f, .022f), Quaternion.Euler(0f, 0f, -38f));
            }
            mb.Cyl(D.Iron, c - fwd * .14f, .008f, .008f, .32f, 6, true, AlongZ);
            mb.Cyl(wood, c - fwd * .055f, .034f, .034f, .11f, 12, true, AlongZ);
            // 摇把
            Tube(mb, D.Iron, new[] { c + fwd * .15f, c + fwd * .18f, c + fwd * .18f + new Vector3(.075f, -.065f, 0f), c + fwd * .215f + new Vector3(.075f, -.065f, 0f) }, .006f, 5);
            mb.Cyl(D.BambooDry, c + fwd * .2f + new Vector3(.075f, -.065f, 0f), .012f, .012f, .075f, 8, true, AlongZ);
            // 两扇辐条（各 8 根，错开半格）
            var tipA = new List<Vector3>();
            var tipB = new List<Vector3>();
            for (int s = 0; s < 2; s++)
            {
                float zz = s == 0 ? -.045f : .045f;
                for (int i = 0; i < 8; i++)
                {
                    float a = (i * 45f + s * 22.5f + 7f) * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    var tip = c + d * R + fwd * zz;
                    Limb(mb, D.BambooDry, new[] { c + d * .03f + fwd * (zz * .6f), tip }, new[] { .0065f, .0045f }, 5);
                    (s == 0 ? tipA : tipB).Add(tip);
                }
            }
            var zig = new List<Vector3>();
            for (int i = 0; i < 8; i++) { zig.Add(tipA[i]); zig.Add(tipB[i]); }
            Tube(mb, D.Straw, zig, .0028f, 4, true);
            // 锭子那一头
            var sp = new Vector3(-.36f, .3f, z);
            foreach (float s in new[] { -.055f, .055f }) BevelBox(mb, wood, new Vector3(sp.x, .2f, z + s), new Vector3(.03f, .23f, .02f), .004f);
            mb.Cyl(D.Iron, sp - fwd * .09f, .0035f, .0018f, .34f, 5, true, AlongZ);
            mb.Cyl(wood, sp - fwd * .016f, .015f, .015f, .032f, 8, true, AlongZ);
            Lathe(mb, D.ClothWhite, sp + fwd * .07f, AlongZ, new[] { P2(.004f, 0f), P2(.02f, .02f), P2(.025f, .05f), P2(.014f, .1f), P2(.004f, .125f) }, 10);
            // 调绳：从大轮上下两边到锭子
            Tube(mb, D.Straw, new[] { c + Vector3.up * (R - .01f), sp + Vector3.up * .016f }, .002f, 4);
            Tube(mb, D.Straw, new[] { c - Vector3.up * (R - .01f), new Vector3(-.1f, .25f, z), sp - Vector3.up * .016f }, .002f, 4);
            // 从锭尖垂到地上的一截断线、地上一小团棉条
            Tube(mb, D.ClothWhite, new[] { sp + fwd * .24f, sp + fwd * .25f + Vector3.down * .12f, new Vector3(-.3f, .012f, z + .2f), new Vector3(-.18f, .008f, z + .17f) }, .0016f, 4);
            mb.Sphere(D.ClothWhite, new Vector3(-.14f, .02f, z + .16f), new Vector3(.05f, .02f, .03f), 8, Quaternion.Euler(0f, 25f, 0f));
            KitShot(k, "r5i_itoguruma", new Vector3(0f, .4f, z), 1.7f, .35f);
        }

        // =====================================================================
        // 木马
        // =====================================================================
        /// <summary>摇马：两条弧形摇板、四条外撇的腿、圆桶身、弯脖子、长脸，黑鬃黑尾、红鞍垫，头上横一根握把。占地 1.1 × 0.45，高 0.95。</summary>
        static void DrawRockingHorse(Kit k)
        {
            var mb = k.mb;
            const float z = .22f, R = .8f;
            var body = D.TrimLight;
            Vector3 Arc(float deg, float zz) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad) * R, R - Mathf.Cos(deg * Mathf.Deg2Rad) * R + .02f, zz);
            foreach (float sz in new[] { -.13f, .13f })
                for (int i = 0; i < 12; i++)
                {
                    float a = -36f + (i + .5f) * 6f;
                    BevelBox(mb, D.TrimMid, Arc(a, z + sz), new Vector3(R * 6f * Mathf.Deg2Rad * 1.06f, .036f, .032f), .006f, Quaternion.Euler(0f, 0f, a));
                }
            foreach (float a in new[] { -27f, 27f })
                BevelBox(mb, D.TrimMid, Arc(a, z) + Vector3.up * .028f, new Vector3(.05f, .022f, .3f), .005f, Quaternion.Euler(0f, 0f, a));
            // 腿
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var top = new Vector3(sx * .19f, .5f, z + sz * .06f);
                    var foot = Arc(sx * 22f, z + sz * .13f) + Vector3.up * .02f;
                    var mid = Vector3.Lerp(top, foot, .5f) + new Vector3(sx * .025f, 0f, 0f);
                    Limb(mb, body, new[] { top, mid, foot }, new[] { .036f, .026f, .021f }, 7);
                    mb.Cyl(D.Black, foot - Vector3.up * .012f, .024f, .022f, .03f, 7);
                }
            // 身子（沿 x 的回转体）
            Lathe(mb, body, new Vector3(-.3f, .57f, z), AlongX, new[]
            {
                P2(0f, 0f), P2(.07f, .008f), P2(.108f, .06f), P2(.118f, .2f), P2(.108f, .36f), P2(.1f, .5f), P2(.08f, .57f), P2(0f, .6f),
            }, 16);
            // 脖子、头
            Limb(mb, body, new[] { new Vector3(.2f, .6f, z), new Vector3(.27f, .72f, z), new Vector3(.33f, .86f, z), new Vector3(.36f, .9f, z) },
                new[] { .095f, .078f, .062f, .05f }, 10);
            Limb(mb, body, new[] { new Vector3(.32f, .9f, z), new Vector3(.4f, .86f, z), new Vector3(.48f, .8f, z), new Vector3(.52f, .77f, z), new Vector3(.535f, .758f, z) },
                new[] { .05f, .056f, .045f, .036f, .014f }, 10);
            foreach (float s in new[] { -1f, 1f })
            {
                mb.Cyl(body, new Vector3(.335f, .93f, z + s * .03f), .017f, .002f, .055f, 6, true, Quaternion.Euler(s * 14f, 0f, 12f));
                mb.Sphere(D.Black, new Vector3(.415f, .875f, z + s * .048f), new Vector3(.012f, .012f, .006f), 6);
                mb.Sphere(D.Black, new Vector3(.527f, .775f, z + s * .018f), new Vector3(.006f, .005f, .004f), 5);
            }
            // 握把：一根横穿过头的圆木
            mb.Cyl(D.TrimMid, new Vector3(.37f, .85f, z - .15f), .012f, .012f, .3f, 8, true, AlongZ);
            // 鬃、尾（黑）
            Limb(mb, D.Black, new[] { new Vector3(.345f, .955f, z), new Vector3(.3f, .9f, z), new Vector3(.24f, .78f, z), new Vector3(.19f, .7f, z) },
                new[] { .012f, .022f, .024f, .012f }, 6);
            Limb(mb, D.Black, new[] { new Vector3(-.3f, .62f, z), new Vector3(-.37f, .6f, z), new Vector3(-.41f, .47f, z), new Vector3(-.4f, .36f, z) },
                new[] { .016f, .028f, .024f, .006f }, 6);
            // 鞍垫（红）+ 肚带、缰绳
            LatheArc(mb, D.ClothRed, new Vector3(-.1f, .57f, z), AlongX, new[] { P2(.123f, 0f), P2(.127f, .01f), P2(.127f, .19f), P2(.123f, .2f) }, 12, 110f, 250f); // AlongX 把局部 -x 转到朝上，所以弧取 180° 两边
            Hoop(mb, D.Black, new Vector3(0f, .57f, z), Vector3.right, .122f, .005f, 18);
            Tube(mb, D.Black, new[] { new Vector3(.5f, .79f, z + .03f), new Vector3(.36f, .78f, z + .075f), new Vector3(.12f, .7f, z + .05f) }, .0035f, 4);
            Tube(mb, D.Black, new[] { new Vector3(.5f, .79f, z - .03f), new Vector3(.36f, .78f, z - .075f), new Vector3(.12f, .7f, z - .05f) }, .0035f, 4);
            KitShot(k, "r5i_rockinghorse", new Vector3(0f, .5f, z), 1.7f, .35f);
        }

        // =====================================================================
        // 柱时计（八角挂钟）
        // =====================================================================
        /// <summary>
        /// 八角时计：上面八角的钟头（发黄的纸钟面、罗马数字、黄铜压圈、玻璃），下面开着玻璃窗的摆箱，里面一根停住的钟摆。
        /// 局部原点 = 钟的底边中点、贴墙；宽 0.37，高 0.62，厚 0.11。
        /// </summary>
        static void DrawClock(Kit k)
        {
            PropDecals();
            var mb = k.mb;
            var wood = D.Post;
            // 摆箱：背板、两侧、底、前面的门框
            mb.Box(D.BeamDark, new Vector3(0f, .17f, .008f), new Vector3(.2f, .3f, .012f));
            foreach (float sx in new[] { -.094f, .094f }) BevelBox(mb, wood, new Vector3(sx, .17f, .046f), new Vector3(.014f, .3f, .09f), .003f);
            BevelBox(mb, wood, new Vector3(0f, .024f, .046f), new Vector3(.2f, .014f, .09f), .003f);
            foreach (float sx in new[] { -.078f, .078f }) BevelBox(mb, wood, new Vector3(sx, .17f, .087f), new Vector3(.03f, .29f, .01f), .003f);
            BevelBox(mb, wood, new Vector3(0f, .04f, .087f), new Vector3(.19f, .03f, .01f), .003f);
            mb.Box(D.WindowGlass, new Vector3(0f, .175f, .084f), new Vector3(.13f, .25f, .003f)); // 透明玻璃（MD_Glass 是发暗的旧玻璃，会把钟摆盖住）
            // 箱底的垂饰
            HexRings(mb, wood, new Vector3(0f, 0f, .046f), 0f, new[] { P2(.045f, -.012f), P2(.105f, .017f) }, 4, true, false);
            // 钟摆：停在偏一点的地方
            Tube(mb, D.Brass, new[] { new Vector3(0f, .31f, .045f), new Vector3(.028f, .1f, .045f) }, .0025f, 5);
            Lathe(mb, D.Brass, new Vector3(.03f, .085f, .036f), AlongZ, new[] { P2(0f, 0f), P2(.03f, .003f), P2(.035f, .008f), P2(.03f, .013f), P2(0f, .016f) }, 16);
            // 钟头：转到“y 轴朝屋里”的坐标系里，八角框 + 钟面 + 压圈 + 玻璃
            k.Push(new Vector3(0f, .45f, .01f), 0f, 90f);
            HexRings(mb, wood, Vector3.zero, 22.5f, new[] { P2(.185f, 0f), P2(.185f, .052f), P2(.172f, .066f), P2(.136f, .066f), P2(.128f, .05f) }, 8, false, true);
            const float fr = .124f, fy = .0508f;
            if (propDial != null)
                mb.Poly(propDial, new[] { new Vector3(fr, fy, fr), new Vector3(-fr, fy, fr), new Vector3(-fr, fy, -fr), new Vector3(fr, fy, -fr) },
                    new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, Vector3.up);
            else mb.Cyl(D.CeramicWhite, new Vector3(0f, fy - .002f, 0f), fr, fr, .002f, 20);
            // 指针（时针、分针）、轴
            foreach (var (ang, len, w) in new[] { (125f, .06f, .009f), (-28f, .092f, .006f) })
            {
                var q = Quaternion.Euler(0f, ang, 0f);
                mb.Box(D.Black, new Vector3(0f, fy + .003f, 0f) + q * new Vector3(0f, 0f, -len / 2f + .01f), new Vector3(w, .0015f, len), q);
            }
            mb.Cyl(D.Brass, new Vector3(0f, fy, 0f), .008f, .006f, .006f, 8);
            Hoop(mb, D.Brass, new Vector3(0f, .062f, 0f), Vector3.up, .128f, .0055f, 28, 6);
            mb.Cyl(D.WindowGlass, new Vector3(0f, .064f, 0f), .125f, .125f, .002f, 24);
            k.Pop();
            KitShot(k, "r5i_clock", new Vector3(0f, .35f, .05f), 1.1f, 0f);
        }

        // =====================================================================
        // 手压泵
        // =====================================================================
        /// <summary>
        /// 手压井泵：靠墙一根木柱，绿漆铸铁泵身（喇叭口、弯出水嘴、长压柄）立在铁管上，底下一口石流し（浅水槽，积着一点脏水），
        /// 旁边一只木桶（CC0）和柄杓。占地 1.25 × 0.7，高 1.2。
        /// </summary>
        static void DrawPump(Kit k)
        {
            var mb = k.mb;
            const float sx = -.15f, z = .36f, pz = .17f;
            // 石流し
            BevelBox(mb, D.Stone, new Vector3(sx, .15f, z), new Vector3(.9f, .3f, .66f), .02f);
            foreach (float s in new[] { -1f, 1f })
            {
                BevelBox(mb, D.Stone, new Vector3(sx, .36f, z + s * .29f), new Vector3(.9f, .12f, .08f), .015f);
                BevelBox(mb, D.Stone, new Vector3(sx + s * .41f, .36f, z), new Vector3(.08f, .12f, .5f), .015f);
            }
            mb.Box(D.Water, new Vector3(sx, .306f, z), new Vector3(.74f, .012f, .5f));
            mb.Cyl(D.Black, new Vector3(sx + .28f, .312f, z + .16f), .03f, .03f, .002f, 10);
            mb.Sphere(D.Moss, new Vector3(sx - .25f, .314f, z - .15f), new Vector3(.12f, .006f, .08f), 8);
            // 木柱 + 两道铁箍把泵固定在柱上
            BevelBox(mb, D.Post, new Vector3(sx, .52f, .05f), new Vector3(.13f, 1.04f, .09f), .01f);
            // 进水管、泵身
            mb.Cyl(D.Iron, new Vector3(sx, .3f, pz), .022f, .022f, .32f, 8);
            var at = new Vector3(sx, .6f, pz);
            Lathe(mb, D.PaintGreen, at, Quaternion.identity, new[]
            {
                P2(.024f, 0f), P2(.062f, .004f), P2(.062f, .024f), P2(.046f, .034f), P2(.047f, .22f), P2(.052f, .26f), P2(.072f, .31f), P2(.082f, .345f),
                P2(.08f, .355f), P2(.071f, .352f), P2(.064f, .32f), P2(.046f, .27f), P2(.044f, .2f),
            }, 18);
            mb.Cyl(D.Black, at + Vector3.up * .21f, .044f, .044f, .002f, 12);
            foreach (float y in new[] { .68f, .84f })
            {
                Hoop(mb, D.Iron, new Vector3(sx, y, pz), Vector3.up, .05f, .005f, 14);
                mb.Box(D.Iron, new Vector3(sx, y, .11f), new Vector3(.03f, .012f, .05f));
            }
            // 出水嘴：从泵身前面弯下来
            Limb(mb, D.PaintGreen, new[] { at + new Vector3(0f, .2f, .04f), at + new Vector3(0f, .21f, .1f), at + new Vector3(0f, .185f, .16f), at + new Vector3(0f, .13f, .185f) },
                new[] { .03f, .028f, .026f, .025f }, 9);
            // 压柄：支在喇叭口后沿的耳朵上，往右伸出去、柄头下垂；活塞杆插进泵里
            var piv = at + new Vector3(.075f, .4f, 0f);
            mb.Box(D.PaintGreen, at + new Vector3(.07f, .365f, 0f), new Vector3(.03f, .08f, .02f));
            mb.Cyl(D.Iron, at + new Vector3(0f, .28f, 0f), .008f, .008f, .16f, 6);
            Limb(mb, D.Iron, new[] { at + new Vector3(-.01f, .44f, 0f), piv, at + new Vector3(.27f, .37f, 0f), at + new Vector3(.47f, .25f, 0f), at + new Vector3(.52f, .2f, 0f) },
                new[] { .011f, .014f, .012f, .012f, .016f }, 7);
            mb.Cyl(D.Iron, piv - Vector3.forward * .02f, .007f, .007f, .04f, 6, true, AlongZ);
            // 木桶 + 柄杓
            if (ExternalModels.Has("wooden_bucket_01"))
                mb.Model("wooden_bucket_01", ExternalModels.Place("wooden_bucket_01", null, new Vector3(.47f, 0f, .36f), Quaternion.Euler(0f, 25f, 0f),
                    ExternalModels.ScaleToHeight("wooden_bucket_01", .3f)));
            else Oke(k, .45f, 0f, .35f, .16f, .26f);
            Tube(mb, D.BambooDry, new[] { new Vector3(.4f, .22f, .36f), new Vector3(.57f, .55f, .3f) }, .008f, 6);
            Lathe(mb, D.BambooDry, new Vector3(.4f, .17f, .36f), Quaternion.Euler(0f, 0f, 22f), new[]
            {
                P2(0f, 0f), P2(.04f, 0f), P2(.042f, .07f), P2(.038f, .07f), P2(.037f, .006f), P2(0f, .006f),
            }, 12);
            KitShot(k, "r5i_pump", new Vector3(0f, .6f, .3f), 2.0f, .4f);
        }
    }
}
