using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 恐怖细节用的材质：旧纸、水渍、锈水、风化木板、符纸、血手印、抓痕、注连绳。
    /// 贴图是程序生成的占位（第二轮换 CC0 贴图时直接替换同名 PNG 即可），只在缺失时生成；材质也只在缺失时创建。
    /// 贴花（符纸、血手印、抓痕）用 Alpha Clip，UV 0..1 铺满一张四边形；水渍是半透明（Alpha 混合）。
    /// 想重新生成：删掉 Assets/_Art/Textures/Generated 里对应的 PNG 和 Assets/_Art/Materials/Detail 里的 MH_*.mat。
    /// </summary>
    internal static class DM
    {
        public static Material PaperOld, ShojiOld, Stain, Rust, Weathered, Ofuda, Blood, Scratch, Rope;

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
        static float Sat(float x) => Mathf.Clamp01(x);

        public static void Load()
        {
            ArtKit.EnsureFolder(DetailMats.Dir);
            var paper = DetailTextures.Albedo("T_PaperOld", 512, 512, PaperOldA);
            var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(DetailTextures.Dir + "/T_WoodGrain.png");
            var rope = DetailTextures.Albedo("T_RopeTwist", 128, 128, RopeA);
            var stain = AlphaTex("T_StainBand", 256, 256, StainA, TextureWrapMode.Repeat, TextureWrapMode.Clamp);
            var ofuda = AlphaTex("T_Ofuda", 128, 384, OfudaF(), TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            var hand = AlphaTex("T_BloodHand", 256, 256, HandF(), TextureWrapMode.Clamp, TextureWrapMode.Clamp);
            var scratch = AlphaTex("T_Scratches", 256, 512, ScratchF(), TextureWrapMode.Clamp, TextureWrapMode.Clamp);

            PaperOld = Lit("MH_FusumaPaperOld", paper, C(.86f, .82f, .76f), .05f, new Vector2(1.2f, 1.2f));
            ShojiOld = Lit("MH_ShojiPaperOld", paper, C(1f, .97f, .9f), .04f, new Vector2(1.6f, 1.6f));
            Stain = Fade("MH_WaterStain", stain, new Vector2(.7f, 1f));
            Rust = Lit("MH_RustStreak", null, C(.19f, .085f, .045f), .12f, Vector2.one);
            // 后来钉上去的木板：比老门板浅得多，一眼能看出是封门
            Weathered = Lit("MH_BoardWeathered", grain, C(.62f, .54f, .43f), .05f, new Vector2(.8f, 2.5f));
            Ofuda = Cutout("MH_Ofuda", ofuda, .5f, .06f);
            Blood = Cutout("MH_BloodHand", hand, .5f, .28f);
            Scratch = Cutout("MH_Scratches", scratch, .5f, .12f);
            Rope = Lit("MH_StrawRope", rope, Color.white, .04f, new Vector2(7f, 7f));
            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // 材质
        // =====================================================================
        static Material Existing(string name, out string path)
        {
            path = $"{DetailMats.Dir}/{name}.mat";
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static Material NewLit(string name, Texture2D tex, Color tint, float smooth, Vector2 tiling)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", tint);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling);
            }
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        static Material Lit(string name, Texture2D tex, Color tint, float smooth, Vector2 tiling)
        {
            var m = Existing(name, out string path);
            if (m != null) return m;
            m = NewLit(name, tex, tint, smooth, tiling);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>贴花：Alpha Clip，不投影（贴在面上，投影只会造成痤疮）。</summary>
        static Material Cutout(string name, Texture2D tex, float cutoff, float smooth)
        {
            var m = Existing(name, out string path);
            if (m != null) return m;
            m = NewLit(name, tex, Color.white, smooth, Vector2.one);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetFloat("_CastShadows", 0f);
            m.SetShaderPassEnabled("ShadowCaster", false);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>半透明（Alpha 混合）：水渍这种要透出底下纹理的。不写深度、不投影。</summary>
        static Material Fade(string name, Texture2D tex, Vector2 tiling)
        {
            var m = Existing(name, out string path);
            if (m != null) return m;
            m = NewLit(name, tex, Color.white, .08f, tiling);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_CastShadows", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent - 10; // 比玻璃早画
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // =====================================================================
        // 带 Alpha 的贴图（DetailTextures.Albedo 会把 alpha 写成 1）
        // =====================================================================
        static Texture2D AlphaTex(string name, int w, int h, Func<float, float, Color> f, TextureWrapMode wrapU, TextureWrapMode wrapV)
        {
            string path = $"{DetailTextures.Dir}/{name}.png";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null) return t;
            ArtKit.EnsureFolder(DetailTextures.Dir);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + .5f) / w, (y + .5f) / h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true; // 远处 Alpha Clip 不会越来越稀
            imp.alphaTestReferenceValue = .5f;
            imp.wrapModeU = wrapU;
            imp.wrapModeV = wrapV;
            imp.anisoLevel = 4;
            imp.filterMode = FilterMode.Trilinear;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =====================================================================
        // 贴图内容
        // =====================================================================

        /// <summary>泛黄的旧纸：大块发黄、细纤维、霉点（无缝）。</summary>
        static Color PaperOldA(float u, float v)
        {
            float n = Noise.Fbm(u, v, 4, 4, 5, 501), fine = Noise.Fbm(u, v, 48, 48, 2, 502), blot = Noise.Fbm(u, v, 3, 3, 4, 503);
            var c = C(.74f, .67f, .52f) * (.84f + .22f * n + .07f * fine);
            c = Color.Lerp(c, C(.52f, .40f, .23f), Sat((blot - .56f) * 4f) * .55f);
            c = Color.Lerp(c, C(.40f, .27f, .15f), Foxing(u, v, 16, 504) * .75f);
            c.a = 1f;
            return c;
        }

        /// <summary>霉点：每格以一定概率有一个软边圆点（无缝）。</summary>
        static float Foxing(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells, best = 0f;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = xi + dx, cy = yi + dy, mx = Noise.Mod(cx, cells), my = Noise.Mod(cy, cells);
                    if (Noise.Hash(mx, my, seed) > .2f) continue;
                    float px = cx + .2f + .6f * Noise.Hash(mx, my, seed + 1), py = cy + .2f + .6f * Noise.Hash(mx, my, seed + 2);
                    float r = .06f + .22f * Noise.Hash(mx, my, seed + 3);
                    float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) / r;
                    best = Mathf.Max(best, Sat(1f - d));
                }
            return Mathf.Pow(best, .6f);
        }

        /// <summary>水渍带：u 沿墙平铺，v = 0 底边 → 1 顶边；顶上一道深色的水线，往下渐淡。</summary>
        static Color StainA(float u, float v)
        {
            float n = Noise.Fbm(u, v, 6, 3, 5, 611);
            float tide = Mathf.Exp(-Mathf.Pow((v - .92f) / .035f, 2f));
            float body = Mathf.Lerp(.36f, .1f, v) * (.55f + .9f * n);
            float a = Sat(body + tide * (.4f + .35f * n)) * Sat((1f - v) / .025f);
            var c = Color.Lerp(C(.30f, .20f, .09f), C(.20f, .12f, .05f), tide);
            c.a = a;
            return c;
        }

        /// <summary>草绳：斜着拧的三股（无缝）。</summary>
        static Color RopeA(float u, float v)
        {
            float s = u * 3f + v * 3f;
            s -= Mathf.Floor(s);
            float shade = .45f + .55f * Mathf.Pow(Mathf.Sin(Mathf.PI * s), .6f);
            float fib = Noise.Fbm(u, v, 24, 24, 3, 801);
            var c = C(.60f, .50f, .30f) * shade * (.82f + .3f * fib);
            c.a = 1f;
            return c;
        }

        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude - r;
        }

        /// <summary>符纸：撕得不齐的旧纸条、红框、顶上红印、中间一列墨字（假字，笔画随机）。</summary>
        static Func<float, float, Color> OfudaF()
        {
            var rnd = new System.Random(7101);
            var strokes = new List<(Vector2 a, Vector2 b, float r)>();
            // 竖写一列 5 个“字”，每个 3~6 笔；坐标里 v 按纸条长宽比放大到和 u 同尺度（宽 1 : 高 3）
            for (int g = 0; g < 5; g++)
            {
                float y0 = .32f + g * .4f, y1 = y0 + .32f;
                int count = 3 + rnd.Next(4);
                for (int k = 0; k < count; k++)
                {
                    float kind = (float)rnd.NextDouble();
                    float ax = .28f + .44f * (float)rnd.NextDouble(), ay = Mathf.Lerp(y0, y1, (float)rnd.NextDouble());
                    Vector2 a = new Vector2(ax, ay), b;
                    if (kind < .4f) b = new Vector2(Mathf.Clamp(ax + (rnd.Next(2) * 2 - 1) * (.15f + .2f * (float)rnd.NextDouble()), .22f, .78f), ay + .03f * ((float)rnd.NextDouble() - .5f));
                    else if (kind < .75f) b = new Vector2(ax + .03f * ((float)rnd.NextDouble() - .5f), Mathf.Clamp(ay - (.12f + .2f * (float)rnd.NextDouble()), y0, y1));
                    else b = new Vector2(Mathf.Clamp(ax + .25f * ((float)rnd.NextDouble() - .5f), .22f, .78f), Mathf.Clamp(ay - (.1f + .15f * (float)rnd.NextDouble()), y0, y1));
                    strokes.Add((a, b, .028f + .016f * (float)rnd.NextDouble()));
                }
            }
            strokes.Add((new Vector2(.5f, .38f), new Vector2(.49f, .12f), .03f)); // 收尾的长竖
            return (u, v) =>
            {
                var p = new Vector2(u, v * 3f);
                float edgeN = Noise.Fbm(u, v, 5, 15, 3, 702);
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v) * 3f);
                float a = Sat((edge - (.035f + .06f * edgeN)) * 90f);
                float n = Noise.Fbm(u, v, 3, 9, 4, 703);
                var c = C(.80f, .74f, .58f) * (.82f + .25f * n);
                c = Color.Lerp(c, C(.55f, .43f, .26f), Sat((Noise.Fbm(u, v, 2, 6, 3, 704) - .55f) * 3f) * .6f);
                // 红框
                float frame = Mathf.Abs(edge - .1f);
                if (frame < .012f && Noise.Fbm(u, v, 8, 24, 2, 705) > .3f) c = C(.50f, .07f, .05f);
                // 顶上的红印（方形，中间留白的“字”）
                var sp = p - new Vector2(.5f, 2.6f);
                if (Mathf.Abs(sp.x) < .22f && Mathf.Abs(sp.y) < .2f)
                {
                    bool hole = (Mathf.Abs(sp.x) < .15f && Mathf.Abs(sp.y) < .13f) &&
                                (Mathf.Abs(sp.x) < .025f || Mathf.Abs(sp.y + .04f) < .022f || Mathf.Abs(sp.y - .07f) < .02f);
                    if (!hole && Noise.Fbm(u, v, 16, 48, 2, 706) > .28f) c = C(.58f, .08f, .06f);
                }
                // 墨字（干笔：笔画里有空白）
                float ink = 1e3f;
                foreach (var s in strokes) ink = Mathf.Min(ink, Capsule(p, s.a, s.b, s.r));
                if (ink < 0f && Noise.Fbm(u, v, 12, 36, 2, 707) > .24f) c = C(.04f, .03f, .03f);
                c.a = a;
                return c;
            };
        }

        /// <summary>血手印：手掌 + 五指（指尖朝上），掌根往下拖出几道血迹；印得不均匀，中间有空白。</summary>
        static Func<float, float, Color> HandF()
        {
            var parts = new (Vector2 a, Vector2 b, float r)[]
            {
                (new Vector2(.40f, .53f), new Vector2(.36f, .80f), .043f), // 食指
                (new Vector2(.48f, .55f), new Vector2(.47f, .87f), .045f), // 中指
                (new Vector2(.56f, .54f), new Vector2(.585f, .83f), .042f), // 无名指
                (new Vector2(.625f, .50f), new Vector2(.70f, .71f), .036f), // 小指
                (new Vector2(.35f, .38f), new Vector2(.21f, .55f), .047f), // 拇指
            };
            var drips = new (float x, float y, float r)[] { (.40f, .08f, .011f), (.47f, .02f, .014f), (.535f, .14f, .01f), (.60f, .2f, .009f) };
            const float px = 1f / 256f;
            return (u, v) =>
            {
                var p = new Vector2(u, v);
                // 手掌：椭圆的近似距离
                var q = new Vector2((u - .5f) / .175f, (v - .41f) / .15f);
                float d = (q.magnitude - 1f) * .16f;
                foreach (var f in parts) d = Mathf.Min(d, Capsule(p, f.a, f.b, f.r));
                // 往下拖的掌根（竖条纹）
                float drag = Mathf.Max(Mathf.Abs(u - .5f) - .13f, Mathf.Max(.26f - v, v - .36f));
                float streak = Noise.Fbm(u, v * .1f, 32, 2, 2, 902);
                if (streak > .45f) d = Mathf.Min(d, drag);
                foreach (var k in drips)
                {
                    d = Mathf.Min(d, Capsule(p, new Vector2(k.x, .3f), new Vector2(k.x + .004f, k.y), k.r * (.7f + .3f * v)));
                    d = Mathf.Min(d, (p - new Vector2(k.x + .004f, k.y)).magnitude - k.r * 1.5f);
                }
                d += (Noise.Fbm(u, v, 12, 12, 3, 903) - .5f) * .02f; // 边缘不齐
                float a = Sat(.5f - d / (1.5f * px));
                // 皮肤纹路：印上去的血不均匀，里面有小空白
                float skin = Noise.Fbm(u, v, 40, 40, 2, 904);
                if (skin > .68f && d < -.01f) a *= Sat((.74f - skin) * 20f);
                float shade = Noise.Fbm(u, v, 6, 6, 3, 905);
                var c = Color.Lerp(C(.30f, .025f, .02f), C(.12f, .012f, .01f), Sat(shade * 1.4f - .2f));
                c.a = a;
                return c;
            };
        }

        /// <summary>抓痕：四道指甲划痕，从上往下，两头细中间宽，划痕里有发黑的血。</summary>
        static Func<float, float, Color> ScratchF()
        {
            var rnd = new System.Random(9301);
            var lines = new List<(float x, float bend, float v0, float v1, float w)>();
            for (int k = 0; k < 4; k++)
                lines.Add((.2f + .19f * k + .04f * ((float)rnd.NextDouble() - .5f), .05f * ((float)rnd.NextDouble() - .5f),
                    .04f + .1f * (float)rnd.NextDouble(), .9f - .12f * (float)rnd.NextDouble(), .018f + .008f * (float)rnd.NextDouble()));
            return (u, v) =>
            {
                float a = 0f, core = 0f;
                foreach (var l in lines)
                {
                    if (v < l.v0 || v > l.v1) continue;
                    float t = (v - l.v0) / (l.v1 - l.v0);
                    float x = l.x + l.bend * Mathf.Sin(t * Mathf.PI) + .025f * (1f - t);
                    float w = l.w * Mathf.Pow(Mathf.Sin(t * Mathf.PI), .45f) * (.75f + .5f * Noise.Fbm(u, v, 8, 16, 2, 931));
                    float dist = Mathf.Abs(u - x);
                    a = Mathf.Max(a, Sat((w - dist) * 256f * .7f));
                    core = Mathf.Max(core, Sat(1f - dist / Mathf.Max(w * .5f, 1e-4f)));
                }
                var c = Color.Lerp(C(.24f, .14f, .10f), C(.16f, .03f, .025f), core);
                c.a = a;
                return c;
            };
        }
    }
}
