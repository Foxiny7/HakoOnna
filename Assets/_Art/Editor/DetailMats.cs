using UnityEditor;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 场景细节用材质（URP/Lit）。材质和贴图都只在缺失时创建，手动调过的不会被覆盖；
    /// 想重新生成就用菜单「Hako/Art/2b 重建细节贴图和材质」。
    /// UV 以米为单位，材质 Tiling = 1 / 贴图对应的米数。
    /// </summary>
    internal sealed class DetailMats
    {
        public const string Dir = "Assets/_Art/Materials/Detail";

        public Material Tatami, TatamiSeam, Heri, FloorWood, KuraFloor, Ceiling, CeilingDark, Tataki, BathTile;
        public Material Post, BeamDark, TrimMid, TrimLight, Lacquer, Wainscot, WainscotDark, RanmaDark, Fusuma;
        public Material Kawara, RidgeTile, Soffit, Siding, PlasterWhite, Namako, Stone, Ground, Gravel, Bark;
        public Material Pine, Shrub, Bamboo, BambooLeaf, Leaf, Gold, GoldLeaf, Brass, Iron, Mirror, Glass, LampGlass;
        public Material CeramicWhite, CeramicBlue, CeramicBrown, ClothWhite, ClothRed, ClothIndigo, ClothPurple;
        public Material PaperLamp, Paper, Ink, Candle, Flame, Portrait, Ash, Straw, PlasticRed, Black, Water;
        public Material WindowGlass; // 透明（玻璃拉门）
        public Material BambooDry, MapleLeaf, Moss; // 庭院：晒干的竹篱笆、红叶、苔藓

        [MenuItem("Hako/Art/2b 重建细节贴图和材质", priority = 21)]
        static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("重建细节贴图和材质",
                    "会删除 " + Dir + " 和 " + DetailTextures.Dir + " 后重新生成（手动调整会丢失）。确定？", "重建", "取消")) return;
            AssetDatabase.DeleteAsset(Dir);
            AssetDatabase.DeleteAsset(DetailTextures.Dir);
            Load();
            AssetDatabase.SaveAssets();
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
        static Color K(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static int F(float x) => Mathf.FloorToInt(x);
        static float Fr(float x) => x - Mathf.Floor(x);
        static float Sat(float x) => Mathf.Clamp01(x);

        public static DetailMats Load()
        {
            ArtKit.EnsureFolder(Dir);
            var d = new DetailMats();
            var tatami = DetailTextures.Albedo("T_Tatami", 512, 512, TatamiA);
            var tatamiN = DetailTextures.Normal("T_Tatami_N", 512, 512, TatamiH, 6f);
            var floor = DetailTextures.Albedo("T_WoodFloor", 1024, 512, FloorA);
            var floorN = DetailTextures.Normal("T_WoodFloor_N", 1024, 512, FloorH, 4f);
            var ceil = DetailTextures.Albedo("T_WoodCeiling", 1024, 512, CeilA);
            var grain = DetailTextures.Albedo("T_WoodGrain", 512, 512, GrainA);
            var kawara = DetailTextures.Albedo("T_Kawara", 512, 512, KawaraA);
            var kawaraN = DetailTextures.Normal("T_Kawara_N", 512, 512, KawaraH, 7f);
            var siding = DetailTextures.Albedo("T_Siding", 512, 512, SidingA);
            var sidingN = DetailTextures.Normal("T_Siding_N", 512, 512, SidingH, 5f);
            var plaster = DetailTextures.Albedo("T_PlasterWhite", 512, 512, PlasterA);
            var namako = DetailTextures.Albedo("T_Namako", 512, 512, NamakoA);
            var namakoN = DetailTextures.Normal("T_Namako_N", 512, 512, NamakoH, 6f);
            var tataki = DetailTextures.Albedo("T_Tataki", 512, 512, TatakiA);
            var tile = DetailTextures.Albedo("T_BathTile", 512, 512, TileA);
            var tileN = DetailTextures.Normal("T_BathTile_N", 512, 512, TileH, 5f);
            var karakami = DetailTextures.Albedo("T_Karakami", 512, 512, KarakamiA);
            var stone = DetailTextures.Albedo("T_Stone", 512, 512, StoneA);
            var stoneN = DetailTextures.Normal("T_Stone_N", 512, 512, (u, v) => Noise.Fbm(u, v, 6, 6, 5, 141), 4f);
            var ground = DetailTextures.Albedo("T_Ground", 1024, 1024, GroundA);
            var gravel = DetailTextures.Albedo("T_Gravel", 512, 512, GravelA);
            var gravelN = DetailTextures.Normal("T_Gravel_N", 512, 512, GravelH, 5f);
            var bark = DetailTextures.Albedo("T_Bark", 256, 512, BarkA);
            var barkN = DetailTextures.Normal("T_Bark_N", 256, 512, BarkH, 4f);
            var portrait = DetailTextures.Albedo("T_Portrait", 256, 320, PortraitA, true);

            var one = Vector2.one;
            d.Tatami = Mat("MD_Tatami", tatami, tatamiN, Color.white, .12f, new Vector2(2, 2), bump: .6f);
            d.TatamiSeam = Mat("MD_TatamiSeam", null, null, C(.05f, .045f, .035f), .05f, one);
            d.Heri = Mat("MD_TatamiHeri", grain, null, C(.07f, .07f, .085f), .25f, new Vector2(8, 8));
            d.FloorWood = Mat("MD_FloorWood", floor, floorN, Color.white, .55f, new Vector2(.5f, 1f), bump: .5f);
            d.KuraFloor = Mat("MD_FloorKura", floor, floorN, C(.82f, .78f, .72f), .18f, new Vector2(.5f, 1f), bump: .6f);
            d.Ceiling = Mat("MD_Ceiling", ceil, null, Color.white, .15f, new Vector2(1 / 1.8f, 1 / .9f));
            d.CeilingDark = Mat("MD_CeilingSmoked", ceil, null, C(.32f, .27f, .24f), .1f, new Vector2(1 / 1.8f, 1 / .9f));
            d.Tataki = Mat("MD_Tataki", tataki, null, Color.white, .08f, new Vector2(.5f, .5f));
            d.BathTile = Mat("MD_BathTile", tile, tileN, Color.white, .65f, one, bump: .7f);
            d.Post = Mat("MD_WoodPost", grain, null, C(.30f, .20f, .13f), .35f, one);
            d.BeamDark = Mat("MD_WoodBeamSmoked", grain, null, C(.13f, .09f, .065f), .25f, new Vector2(.5f, .5f));
            d.TrimMid = Mat("MD_WoodTrimMid", grain, null, C(.47f, .34f, .22f), .35f, one);
            d.TrimLight = Mat("MD_WoodCedar", grain, null, C(.62f, .48f, .33f), .25f, one);
            d.Lacquer = Mat("MD_LacquerBlack", null, null, C(.03f, .025f, .025f), .75f, one);
            d.Wainscot = Mat("MD_Wainscot", floor, floorN, C(.85f, .8f, .75f), .3f, new Vector2(.5f, 1f), bump: .4f);
            d.WainscotDark = Mat("MD_WainscotDoma", floor, floorN, C(.45f, .4f, .36f), .2f, new Vector2(.5f, 1f), bump: .4f);
            d.RanmaDark = Mat("MD_RanmaShadow", null, null, C(.012f, .01f, .01f), 0f, one);
            d.Fusuma = Mat("MD_Fusuma_Karakami", karakami, null, Color.white, .1f, new Vector2(2, 4));
            d.Kawara = Mat("MD_Kawara", kawara, kawaraN, Color.white, .45f, new Vector2(1 / .6f, 1 / .5f), .15f, 1f);
            d.RidgeTile = Mat("MD_KawaraRidge", null, null, C(.13f, .135f, .15f), .45f, one, .15f);
            d.Soffit = Mat("MD_EaveSoffit", ceil, null, C(.38f, .3f, .24f), .1f, new Vector2(1 / 1.8f, 1 / .9f));
            d.Siding = Mat("MD_Yakisugi", siding, sidingN, Color.white, .12f, new Vector2(1f, 1 / .6f), bump: .8f);
            d.PlasterWhite = Mat("MD_PlasterWhite", plaster, null, Color.white, .05f, new Vector2(.5f, .5f));
            d.Namako = Mat("MD_Namako", namako, namakoN, Color.white, .35f, new Vector2(2, 2), bump: .8f);
            d.Stone = Mat("MD_Stone", stone, stoneN, Color.white, .2f, one, bump: .5f);
            d.Ground = Mat("MD_GardenGround", ground, null, Color.white, .08f, new Vector2(1 / 6f, 1 / 6f));
            d.Gravel = Mat("MD_GravelRaked", gravel, gravelN, Color.white, .1f, one, bump: .8f);
            d.Bark = Mat("MD_PineBark", bark, barkN, Color.white, .1f, new Vector2(1 / .6f, 1 / 1.2f), bump: .8f);
            d.Pine = Mat("MD_PineNeedles", null, null, C(.06f, .095f, .05f), .12f, one);
            d.Shrub = Mat("MD_Shrub", null, null, C(.07f, .11f, .055f), .15f, one);
            d.Bamboo = Mat("MD_Bamboo", null, null, C(.32f, .38f, .18f), .5f, one);
            d.BambooLeaf = Mat("MD_BambooLeaf", null, null, C(.16f, .24f, .09f), .2f, one);
            d.Leaf = Mat("MD_Leaf", null, null, C(.08f, .17f, .06f), .3f, one);
            d.Gold = Mat("MD_Gold", null, null, C(.80f, .60f, .26f), .65f, one, 1f);
            d.GoldLeaf = Mat("MD_GoldLeaf", null, null, C(.70f, .54f, .25f), .35f, one, .8f);
            d.Brass = Mat("MD_Brass", null, null, C(.52f, .40f, .20f), .5f, one, 1f);
            d.Iron = Mat("MD_Iron", null, null, C(.06f, .06f, .06f), .3f, one, .8f);
            d.Mirror = Mat("MD_MirrorOld", null, null, C(.35f, .36f, .36f), .95f, one, 1f);
            d.Glass = Mat("MD_GlassDark", null, null, C(.05f, .06f, .07f), .95f, one, .3f);
            d.LampGlass = Mat("MD_LampGlass", null, null, C(.82f, .80f, .74f), .8f, one);
            d.CeramicWhite = Mat("MD_CeramicWhite", null, null, C(.78f, .76f, .70f), .75f, one);
            d.CeramicBlue = Mat("MD_CeramicBlue", null, null, C(.16f, .22f, .36f), .75f, one);
            d.CeramicBrown = Mat("MD_CeramicBrown", null, null, C(.27f, .16f, .09f), .6f, one);
            d.ClothWhite = Mat("MD_ClothWhite", null, null, C(.74f, .73f, .69f), .05f, one);
            d.ClothRed = Mat("MD_ClothRed", null, null, C(.42f, .05f, .05f), .1f, one);
            d.ClothIndigo = Mat("MD_ClothIndigo", null, null, C(.06f, .09f, .20f), .08f, one);
            d.ClothPurple = Mat("MD_ClothPurple", null, null, C(.22f, .07f, .20f), .1f, one);
            d.PaperLamp = Mat("MD_PaperLamp", null, null, C(.86f, .82f, .70f), .05f, one);
            d.Paper = Mat("MD_PaperWhite", null, null, C(.84f, .82f, .76f), .05f, one);
            d.Ink = Mat("MD_Ink", null, null, C(.02f, .02f, .02f), .2f, one);
            d.Candle = Mat("MD_Candle", null, null, C(.88f, .86f, .80f), .3f, one);
            d.Flame = Mat("MD_CandleFlame", null, null, C(1f, .6f, .25f), 0f, one, 0f, 1f, new Color(1.6f, .75f, .25f));
            d.Portrait = Mat("MD_Portrait", portrait, null, Color.white, .6f, one);
            d.Ash = Mat("MD_Ash", null, null, C(.36f, .35f, .33f), .02f, one);
            d.Straw = Mat("MD_Straw", null, null, C(.55f, .46f, .27f), .05f, one);
            d.PlasticRed = Mat("MD_PlasticRed", null, null, C(.48f, .05f, .04f), .55f, one);
            d.Black = Mat("MD_BlackGloss", null, null, C(.015f, .015f, .015f), .8f, one);
            d.Water = Mat("MD_WaterDark", null, null, C(.01f, .012f, .015f), .97f, one);
            d.WindowGlass = GlassMat("MD_WindowGlass", new Color(.55f, .62f, .66f, .16f), .88f);
            d.BambooDry = Mat("MD_BambooDry", null, null, C(.50f, .43f, .27f), .38f, one);
            d.MapleLeaf = Mat("MD_MapleLeaf", null, null, C(.40f, .07f, .035f), .25f, one);
            d.Moss = Mat("MD_Moss", null, null, C(.10f, .16f, .055f), .08f, one);
            AssetDatabase.SaveAssets();
            return d;
        }

        static Material Mat(string name, Texture2D albedo, Texture2D normal, Color tint, float smooth, Vector2 tiling,
            float metallic = 0f, float bump = 1f, Color? emission = null)
        {
            string path = $"{Dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", tint);
            if (albedo != null)
            {
                m.SetTexture("_BaseMap", albedo);
                m.SetTextureScale("_BaseMap", tiling);
            }
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// 透明玻璃：URP Lit，Transparent + Alpha 混合（保留高光），不写深度、不投影，
        /// 月光能照进屋里。tint.a = 不透明度。
        /// </summary>
        static Material GlassMat(string name, Color tint, float smooth)
        {
            string path = $"{Dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Surface", 1f);                     // Transparent
            m.SetFloat("_Blend", 0f);                       // Alpha
            m.SetFloat("_BlendModePreserveSpecular", 1f);   // 漫反射乘 alpha，高光保留
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_CastShadows", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ===================== 贴图函数（u,v ∈ [0,1)） =====================
        // 榻榻米：一张贴图 = 0.5 m；灯芯草沿 u 横向排列成行（行沿 v 重复），经线沿 v。
        static Color TatamiA(float u, float v)
        {
            float s = v * 48f; int row = F(s); float fr = s - row;
            float reed = .80f + .20f * Mathf.Sin(Mathf.PI * fr);
            float rowVar = (Noise.Hash(row, 3, 11) - .5f) * .10f;
            float fiber = (Noise.Value(u * 96f, row, 96, 48, 5) - .5f) * .10f;
            float warp = Fr(u * 24f) < .07f ? .72f : 1f;
            var c = Color.Lerp(C(.60f, .55f, .34f), C(.50f, .45f, .27f), Noise.Fbm(u, v, 4, 4, 3, 7));
            c = Color.Lerp(c, C(.42f, .34f, .20f), Mathf.SmoothStep(0f, 1f, (Noise.Fbm(u, v, 3, 3, 4, 21) - .55f) / .2f));
            return K(c, (reed + rowVar + fiber) * warp);
        }

        static float TatamiH(float u, float v)
        {
            float fr = Fr(v * 48f);
            return Mathf.Sin(Mathf.PI * fr) * .6f + (Fr(u * 24f) < .07f ? -.5f : 0f);
        }

        // 地板：一张 = 2 m × 1 m，8 条 0.125 m 宽的板，沿 u 方向。
        static Color FloorA(float u, float v)
        {
            float pv = v * 8f; int p = F(pv); float fv = pv - p;
            float ju = Noise.Hash(p, 1, 31);
            int seg = Fr(u - ju) >= .5f ? 1 : 0;
            float tone = .80f + .40f * Noise.Hash(p, seg, 37);
            float g = Noise.Fbm(u, v, 4, 32, 4, 41 + p);
            float grainL = .5f + .5f * Mathf.Sin((fv * 5f + g * 4f + seg) * Mathf.PI * 2f);
            float t = Mathf.Repeat(u - ju, .5f); float du = Mathf.Min(t, .5f - t);
            float seam = Mathf.Min(fv, 1f - fv) < .025f ? .25f : 1f;
            float joint = du < .0015f ? .3f : 1f;
            var c = Color.Lerp(C(.31f, .20f, .12f), C(.37f, .26f, .17f), Noise.Fbm(u, v, 2, 2, 3, 51));
            return K(c, tone * (.82f + .18f * grainL) * seam * joint);
        }

        static float FloorH(float u, float v)
        {
            float fv = Fr(v * 8f); int p = F(v * 8f);
            float t = Mathf.Repeat(u - Noise.Hash(p, 1, 31), .5f);
            if (Mathf.Min(fv, 1f - fv) < .025f || Mathf.Min(t, .5f - t) < .0015f) return 0f;
            return 1f + .1f * Noise.Fbm(u, v, 4, 32, 3, 41 + p);
        }

        // 天花板：一张 = 1.8 m × 0.9 m，3 块 0.3 m 宽的杉木板。
        static Color CeilA(float u, float v)
        {
            float pv = v * 3f; int b = F(pv); float fv = pv - b;
            float g = Noise.Fbm(u, v, 3, 12, 4, 61 + b);
            float dd = Mathf.Abs(fv - .5f + (g - .5f) * .5f);
            float grainL = .5f + .5f * Mathf.Sin((dd * 14f + g * 3f) * Mathf.PI * 2f);
            float tone = .85f + .3f * Noise.Hash(b, 2, 67);
            float shadow = fv < .02f ? .35f : fv < .06f ? .75f : 1f;
            return K(C(.46f, .34f, .23f), tone * (.8f + .2f * grainL) * shadow);
        }

        // 通用木纹（灰度，材质上色）：一张 = 1 m，纹理沿 u。
        static Color GrainA(float u, float v)
        {
            float g = Noise.Fbm(u, v, 2, 16, 4, 71);
            float lines = .5f + .5f * Mathf.Sin((v * 60f + g * 6f) * Mathf.PI * 2f);
            float k = .78f + .14f * lines + .16f * (Noise.Fbm(u, v, 8, 2, 3, 73) - .5f);
            return K(Color.white, k);
        }

        // 瓦：一张 = 0.6 m(u, 沿檐) × 0.5 m(v, 沿坡向上)，2 列 × 2 行栈瓦。
        static float KawaraWave(float u) => .5f + .5f * Mathf.Cos((Fr(u * 2f) - .3f) * Mathf.PI * 2f);

        static float KawaraH(float u, float v) => KawaraWave(u) * .7f + (1f - Fr(v * 2f)) * .45f;

        static Color KawaraA(float u, float v)
        {
            int ti = F(u * 2f), ci = F(v * 2f);
            float fc = Fr(v * 2f), wave = KawaraWave(u);
            float tone = .85f + .3f * Noise.Hash(ti, ci, 81);
            float ao = (.62f + .38f * wave) * (fc > .9f ? Mathf.Lerp(1f, .45f, (fc - .9f) / .1f) : 1f) * (fc < .03f ? .8f : 1f);
            var c = Color.Lerp(C(.20f, .21f, .235f), C(.30f, .31f, .27f), Sat((Noise.Fbm(u, v, 4, 4, 4, 83) - .6f) * 4f));
            return K(c, tone * ao);
        }

        // 焼杉下见板：一张 = 1 m × 0.6 m，3 块 0.2 m 横板，下缘厚。
        static float SidingH(float u, float v) => 1f - Fr(v * 3f) * .8f;

        static Color SidingA(float u, float v)
        {
            float pv = v * 3f; int b = F(pv); float f = pv - b;
            float g = Noise.Fbm(u, v, 3, 18, 4, 91 + b);
            var c = Color.Lerp(C(.07f, .065f, .06f), C(.20f, .19f, .18f), Sat((g - .58f) * 5f) * .6f);
            float crack = Noise.Fbm(u, v, 24, 6, 2, 95 + b) > .7f ? .6f : 1f;
            float ao = f > .88f ? .45f : 1f;
            return K(c, (.85f + .3f * Noise.Fbm(u, v, 8, 24, 3, 97)) * ao * crack);
        }

        // 白漆喰：一张 = 2 m，带雨痕。
        static Color PlasterA(float u, float v)
        {
            float m = Noise.Fbm(u, v, 6, 6, 5, 101);
            float streak = Noise.Fbm(u, v, 24, 2, 3, 103);
            var c = Color.Lerp(C(.80f, .78f, .72f), C(.64f, .62f, .56f), Sat((streak - .55f) * 3f) * .7f);
            return K(c, .94f + .24f * (m - .5f));
        }

        // なまこ壁：一张 = 0.5 m，45° 斜铺 0.25 m 黑瓦，白色灰缝凸起。
        static float NamakoDist(float u, float v, out int ia, out int ib)
        {
            float a = (u + v) * 2f, b = (u - v) * 2f + 4f;
            ia = F(a); ib = F(b);
            float fa = a - ia, fb = b - ib;
            return Mathf.Min(Mathf.Min(fa, 1 - fa), Mathf.Min(fb, 1 - fb));
        }

        static float NamakoH(float u, float v)
        {
            float d = NamakoDist(u, v, out _, out _);
            return d < .08f ? .6f + .4f * Mathf.Cos(d / .08f * Mathf.PI * .5f) : .3f * Sat((d - .08f) / .05f);
        }

        static Color NamakoA(float u, float v)
        {
            float d = NamakoDist(u, v, out int ia, out int ib);
            if (d < .08f) return K(C(.82f, .80f, .75f), .88f + .12f * Noise.Fbm(u, v, 16, 16, 3, 191));
            return K(C(.085f, .085f, .095f), (.8f + .4f * Noise.Hash(Noise.Mod(ia, 2), Noise.Mod(ib, 2), 193)) * (.85f + .3f * Noise.Fbm(u, v, 8, 8, 3, 195)));
        }

        // 土间三和土：一张 = 2 m。
        static Color TatakiA(float u, float v)
        {
            float m = Noise.Fbm(u, v, 4, 4, 5, 111);
            float gr = Noise.Hash(F(u * 512f), F(v * 512f), 113);
            var c = Color.Lerp(C(.25f, .22f, .185f), C(.33f, .30f, .26f), m);
            return K(c, gr > .94f ? 1.35f : gr < .05f ? .6f : 1f);
        }

        // 浴室瓷砖：一张 = 1 m，10 × 10 块。
        static Color TileA(float u, float v)
        {
            float tu = u * 10f, tv = v * 10f; int ix = F(tu), iy = F(tv);
            float fu = tu - ix, fv = tv - iy;
            if (fu < .04f || fv < .04f) return K(C(.36f, .36f, .33f), .9f + .2f * Noise.Fbm(u, v, 16, 16, 2, 121));
            var c = Color.Lerp(C(.56f, .68f, .67f), C(.40f, .44f, .40f), Sat((Noise.Fbm(u, v, 5, 5, 4, 125) - .55f) * 2.5f));
            return K(c, .9f + .15f * Noise.Hash(ix, iy, 123));
        }

        static float TileH(float u, float v)
        {
            float fu = Fr(u * 10f), fv = Fr(v * 10f);
            if (fu < .04f || fv < .04f) return 0f;
            return Sat(Mathf.Min(Mathf.Min(fu - .04f, 1f - fu), Mathf.Min(fv - .04f, 1f - fv)) / .05f);
        }

        // 唐纸：七宝纹（云母色）。
        static float Ring(float x, float y, float off)
        {
            float cx = Mathf.Round(x - off) + off, cy = Mathf.Round(y - off) + off;
            return Mathf.Abs(new Vector2(x - cx, y - cy).magnitude - .5f);
        }

        static Color KarakamiA(float u, float v)
        {
            float x = u * 2f, y = v * 2f;
            float line = Mathf.Min(Ring(x, y, 0f), Ring(x, y, .5f));
            var paper = K(C(.80f, .76f, .64f), .95f + .08f * Noise.Fbm(u, v, 32, 32, 2, 131));
            paper = Color.Lerp(paper, C(.62f, .52f, .36f), Sat((Noise.Fbm(u, v, 2, 2, 4, 133) - .62f) * 3f));
            float mica = 1f - Mathf.SmoothStep(0f, 1f, (line - .015f) / .02f);
            return Color.Lerp(paper, C(.90f, .85f, .70f), mica * .7f);
        }

        // 花岗岩：一张 = 1 m。
        static Color StoneA(float u, float v)
        {
            float m = Noise.Fbm(u, v, 6, 6, 5, 141);
            float sp = Noise.Hash(F(u * 384f), F(v * 384f), 143);
            var c = K(C(.42f, .41f, .39f), .82f + .36f * m);
            if (sp > .92f) c = K(c, .45f); else if (sp < .05f) c = K(c, 1.4f);
            return Color.Lerp(c, C(.33f, .36f, .27f), Sat((Noise.Fbm(u, v, 3, 3, 4, 145) - .6f) * 3f));
        }

        // 庭院地面：一张 = 6 m，泥土 + 苔藓 + 碎石。
        static Color GroundA(float u, float v)
        {
            var soil = K(C(.15f, .13f, .10f), .8f + .4f * Noise.Fbm(u, v, 6, 6, 5, 151));
            float moss = Sat((Noise.Fbm(u, v, 3, 3, 5, 153) - .48f) * 5f);
            var c = Color.Lerp(soil, K(C(.085f, .12f, .055f), .75f + .5f * Noise.Fbm(u, v, 48, 48, 3, 155)), moss);
            int px = F(u * 220f), py = F(v * 220f);
            if (Noise.Hash(px, py, 157) > .965f)
            {
                float cx = (px + .5f) / 220f, cy = (py + .5f) / 220f;
                if (new Vector2(u - cx, v - cy).magnitude * 220f < .4f)
                    c = K(C(.32f, .31f, .29f), .8f + .4f * Noise.Hash(px, py, 159));
            }
            return c;
        }

        // 耙过的白砂：一张 = 1 m，沿 v 起伏的耙纹。
        static float GravelH(float u, float v) =>
            (.5f + .5f * Mathf.Cos(v * 16f * Mathf.PI * 2f)) * .6f + Noise.Value(u * 96f, v * 96f, 96, 96, 161) * .4f;

        static Color GravelA(float u, float v)
        {
            float g = Noise.Value(u * 96f, v * 96f, 96, 96, 161);
            float rk = .5f + .5f * Mathf.Cos(v * 16f * Mathf.PI * 2f);
            float g2 = Noise.Hash(F(u * 256f), F(v * 256f), 163);
            return K(C(.46f, .45f, .42f), (.7f + .5f * g) * (.75f + .25f * rk) * (g2 > .9f ? .7f : 1f));
        }

        // 松树皮：一张 = 0.6 × 1.2 m。
        static float BarkH(float u, float v) => 1f - Mathf.Abs(2f * Noise.Fbm(u, v, 5, 3, 4, 171) - 1f);

        static Color BarkA(float u, float v)
        {
            float crack = 1f - BarkH(u, v) < .14f ? .35f : 1f;
            return K(C(.22f, .16f, .12f), (.75f + .5f * Noise.Fbm(u, v, 10, 6, 3, 173)) * crack);
        }

        // 遗影：看不清脸的旧照片。
        static Color PortraitA(float u, float v)
        {
            var bg = Color.Lerp(C(.30f, .30f, .29f), C(.55f, .55f, .53f), v);
            float vig = 1f - .6f * Mathf.Pow(new Vector2(u - .5f, v - .55f).magnitude * 1.3f, 2f);
            float hx = (u - .5f) / .17f, hy = (v - .62f) / .21f;
            float sx = (u - .5f) / .42f, sy = (v - .02f) / .34f;
            float body = Mathf.Min(hx * hx + hy * hy, sx * sx + sy * sy);
            var c = Color.Lerp(bg, C(.07f, .07f, .07f), 1f - Mathf.SmoothStep(0f, 1f, (body - .85f) / .3f));
            float fx = (u - .5f) / .10f, fy = (v - .60f) / .13f;
            c = Color.Lerp(c, C(.20f, .19f, .18f), (1f - Mathf.SmoothStep(0f, 1f, (fx * fx + fy * fy - .4f) / .6f)) * .6f);
            float grainN = Noise.Hash(F(u * 256f), F(v * 320f), 181) - .5f;
            return K(c, Sat(vig) * (1f + grainN * .15f));
        }
    }
}
