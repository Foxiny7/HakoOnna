using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第二轮：把 CC0 外部贴图（Poly Haven / ambientCG，来源见 Textures/External/README_来源.md）接到材质上。
    /// <list type="bullet">
    /// <item>导入设置：漫反射 sRGB；法线 NormalMap；*_mask（R 金属 / G AO / A 光滑度）线性；贴花带透明通道、Clamp。</item>
    /// <item>材质升级：已有的 Detail / Placeholder 材质只在没升级过时改一次（材质上记 HakoArtRev 标签），
    ///   之后在 Unity 里手调的参数不会被覆盖。想重来：删掉材质再生成。</item>
    /// <item>颜色：每张漫反射记下线性空间的平均色，按“这个表面平均看起来是什么颜色”（sRGB）反推 _BaseColor。</item>
    /// </list>
    /// </summary>
    internal static class ExternalMats
    {
        public const string Root = "Assets/_Art/Textures/External";
        public const string DecalDir = "Assets/_Art/Textures/Decals";
        const string RevTag = "HakoArtRev";
        const string Rev = "r2";

        internal sealed class Tex
        {
            public Texture2D albedo, normal, mask;
            public Vector2 metres;  // 一张贴图对应的实际尺寸（米）
            public Vector3 avgLin;  // 漫反射的线性平均色
            public bool Ok => albedo != null;
        }

        public static Tex Tatami, Clay, PaintedPlaster, WhitePlaster, DarkPlanks, Weathered, CedarU, Cabinet, Table;
        public static Tex Bamboo, StoneWall, MudLeaves, MudDry, GroundGrey, PineBark, Rust, Tiles, Wicker;
        // 第四轮：旧布（素色 + 格子）、毛巾、床垫布、瓷、搪瓷、旧镜子、带污渍的金属 / 塑料 / 纸（Tools~/prep_tex_r4.py 合成，全部无缝）
        public static Tex Linen, Crepe, Velour, Terry, Check, GinghamRed, GinghamGreen, Ticking, Reed, GreenMetal, RustyMetal;
        public static Tex Ceramic, MirrorOld, BlackSmudge, PlasticSmudge, Enamel, GoldDirty, BronzeOld, PaperWhite, PaperAged;
        // 第六轮：院外的老柏油路、枯草地、锈铁皮、长苔的水泥（Poly Haven，Tools~/prep_tex_r6.py）
        public static Tex Asphalt, FieldGrass, TinRoof, ConcreteMoss;
        static bool loaded;

        // =====================================================================
        // 导入设置
        // =====================================================================
        /// <summary>
        /// AlbedoAlpha：模型的镂空 / 叶片漫反射（*_diff_a.png，透明通道 = 不透明度）；Painting：Met 的画（Clamp）；
        /// Sky：天空的经纬图 .hdr（4K、经度循环、纬度 Clamp，BC6H）。
        /// </summary>
        enum Kind { Albedo, AlbedoAlpha, Painting, Normal, Mask, Decal, Sky }

        public const string MetDir = Root + "/Met";

        static Kind KindOf(string path)
        {
            string f = Path.GetFileName(path);
            if (f.EndsWith(".hdr") || f.EndsWith(".exr")) return Kind.Sky;
            if (path.StartsWith(DecalDir) || f.EndsWith("_decal.png")) return Kind.Decal;
            if (f.Contains("_nor_gl") || f.Contains("_NormalGL")) return Kind.Normal;
            if (f.Contains("_mask")) return Kind.Mask;
            if (f.EndsWith("_diff_a.png")) return Kind.AlbedoAlpha;
            if (path.StartsWith(MetDir + "/")) return Kind.Painting;
            return Kind.Albedo;
        }

        internal static bool Owns(string path) => path.StartsWith(Root + "/") || path.StartsWith(DecalDir + "/");

        internal static void Configure(TextureImporter imp, string path)
        {
            var kind = KindOf(path);
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = kind == Kind.Decal ? 4 : 8;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.Compressed;
            switch (kind)
            {
                case Kind.Normal:
                    imp.textureType = TextureImporterType.NormalMap;
                    imp.wrapMode = TextureWrapMode.Repeat;
                    break;
                case Kind.Mask:
                    imp.textureType = TextureImporterType.Default;
                    imp.sRGBTexture = false;
                    imp.alphaSource = TextureImporterAlphaSource.FromInput;
                    imp.alphaIsTransparency = false;
                    imp.wrapMode = TextureWrapMode.Repeat;
                    break;
                case Kind.Decal:
                    imp.textureType = TextureImporterType.Default;
                    imp.sRGBTexture = true;
                    imp.alphaSource = TextureImporterAlphaSource.FromInput;
                    imp.alphaIsTransparency = true;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    break;
                case Kind.AlbedoAlpha:
                    imp.textureType = TextureImporterType.Default;
                    imp.sRGBTexture = true;
                    imp.alphaSource = TextureImporterAlphaSource.FromInput;
                    imp.alphaIsTransparency = true;
                    imp.wrapMode = TextureWrapMode.Repeat;
                    // 第五轮：枝叶面片远了以后别被 mip 吃瘦（只对新导入的贴图生效）
                    imp.mipMapsPreserveCoverage = true;
                    imp.alphaTestReferenceValue = .4f;
                    break;
                case Kind.Painting:
                    imp.textureType = TextureImporterType.Default;
                    imp.sRGBTexture = true;
                    imp.alphaSource = TextureImporterAlphaSource.None;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    break;
                case Kind.Sky:
                    // 第六轮：天空经纬图。4K 不缩；经度方向循环，纬度方向 Clamp（天顶、地底不串）；BC6H 高质量（暗部渐变不出色阶）
                    imp.textureType = TextureImporterType.Default;
                    imp.textureShape = TextureImporterShape.Texture2D;
                    imp.sRGBTexture = false;
                    imp.alphaSource = TextureImporterAlphaSource.None;
                    imp.maxTextureSize = 4096;
                    imp.anisoLevel = 1;
                    imp.wrapModeU = TextureWrapMode.Repeat;
                    imp.wrapModeV = TextureWrapMode.Clamp;
                    imp.textureCompression = TextureImporterCompression.CompressedHQ;
                    break;
                default:
                    imp.textureType = TextureImporterType.Default;
                    imp.sRGBTexture = true;
                    imp.alphaSource = TextureImporterAlphaSource.None;
                    imp.wrapMode = TextureWrapMode.Repeat;
                    break;
            }
        }

        static bool Matches(TextureImporter imp, string path)
        {
            var kind = KindOf(path);
            switch (kind)
            {
                case Kind.Normal: return imp.textureType == TextureImporterType.NormalMap;
                case Kind.Mask: return imp.textureType == TextureImporterType.Default && !imp.sRGBTexture;
                case Kind.Decal: return imp.alphaIsTransparency && imp.wrapMode == TextureWrapMode.Clamp;
                case Kind.AlbedoAlpha: return imp.sRGBTexture && imp.alphaSource == TextureImporterAlphaSource.FromInput && imp.alphaIsTransparency;
                case Kind.Painting: return imp.sRGBTexture && imp.wrapMode == TextureWrapMode.Clamp;
                case Kind.Sky: return imp.maxTextureSize >= 4096 && imp.wrapModeV == TextureWrapMode.Clamp && imp.textureShape == TextureImporterShape.Texture2D;
                default: return imp.textureType == TextureImporterType.Default && imp.sRGBTexture;
            }
        }

        /// <summary>第一次导入时 ExternalTexImport 已经设好；这里只补救类型 / 色彩空间不对的（比如脚本还没编译就先导入了）。</summary>
        public static void EnsureImportSettings()
        {
            var dirs = new List<string>();
            if (AssetDatabase.IsValidFolder(Root)) dirs.Add(Root);
            if (AssetDatabase.IsValidFolder(DecalDir)) dirs.Add(DecalDir);
            if (dirs.Count == 0) return;
            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", dirs.ToArray()))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp) || Matches(imp, path)) continue;
                Configure(imp, path);
                imp.SaveAndReimport();
                fixedCount++;
            }
            if (fixedCount > 0) Debug.Log($"[Hako Art] 外部贴图导入设置修正 {fixedCount} 张");
        }

        // =====================================================================
        // 贴图
        // =====================================================================
        static Texture2D Load(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogWarning($"[Hako Art] 缺少外部贴图：{path}（材质保持原样）");
            return t;
        }

        static Tex PH(string id, string diff, string nor, string mask, float mx, float my, float r, float g, float b)
        {
            string d = $"{Root}/PolyHaven/{id}/{id}_";
            return new Tex { albedo = Load(d + diff), normal = Load(d + nor), mask = Load(d + mask), metres = new Vector2(mx, my), avgLin = new Vector3(r, g, b) };
        }

        static Tex ACG(string id, float m, float r, float g, float b)
        {
            string d = $"{Root}/ambientCG/{id}/{id}_";
            return new Tex { albedo = Load(d + "Color.jpg"), normal = Load(d + "NormalGL.jpg"), mask = Load(d + "mask.png"), metres = new Vector2(m, m), avgLin = new Vector3(r, g, b) };
        }

        /// <summary>第四轮合成的贴图：sub/name/name_diff.jpg、_nor_gl.jpg、_mask.png，正方形，一张 = m 米。</summary>
        static Tex R4(string sub, string name, float m, float r, float g, float b)
        {
            string d = $"{Root}/{sub}/{name}/{name}_";
            return new Tex { albedo = Load(d + "diff.jpg"), normal = Load(d + "nor_gl.jpg"), mask = Load(d + "mask.png"), metres = new Vector2(m, m), avgLin = new Vector3(r, g, b) };
        }

        public static void Load()
        {
            if (loaded && Tatami != null && Tatami.albedo != null) return;
            EnsureImportSettings();
            // 尺寸来自 Poly Haven 的 dimensions；平均色由 Tools~/make_textures.py 算出
            Tatami = PH("tatami_mat", "diff_2k.jpg", "nor_gl_2k.jpg", "mask.png", 1.8f, 1.8f, .2782f, .2274f, .1195f);
            Clay = PH("clay_floor_001", "diff_2k.jpg", "nor_gl_2k.jpg", "mask.png", 2f, 2f, .1955f, .1162f, .0477f);
            PaintedPlaster = PH("painted_plaster_wall", "diff_2k.jpg", "nor_gl_2k.jpg", "mask.png", 2f, 2f, .4069f, .3661f, .3508f);
            DarkPlanks = PH("dark_wooden_planks", "diff_2k.jpg", "nor_gl_2k.jpg", "mask.png", 2f, 2f, .0879f, .0691f, .0498f);
            WhitePlaster = PH("white_rough_plaster", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 1f, 1f, .4893f, .4835f, .4471f);
            CedarU = PH("japanese_cedar_planks", "diff_aged_u.jpg", "nor_gl_u.jpg", "mask_u.png", 1.13f, 1.13f, .1741f, .1000f, .0424f);
            Weathered = PH("weathered_planks", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 2f, 2f, .0814f, .0578f, .0439f);
            Cabinet = PH("wood_cabinet_worn_long", "diff_aged.jpg", "nor_gl_1k.jpg", "mask.png", 1f, .5f, .0869f, .0445f, .0202f);
            Table = PH("wood_table_worn", "diff_aged.jpg", "nor_gl_1k.jpg", "mask.png", .55f, .55f, .0759f, .0338f, .0146f);
            Bamboo = PH("bamboo_wall", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 2f, 2f, .2398f, .1511f, .0894f);
            StoneWall = PH("japanese_stone_wall", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 1.9f, 1.9f, .2057f, .1905f, .1628f);
            MudLeaves = PH("brown_mud_leaves_01", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 1.3f, 1.3f, .1337f, .0938f, .0346f);
            MudDry = PH("brown_mud_dry", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 1.3f, 1.3f, .1822f, .1108f, .0517f);
            GroundGrey = PH("ground_grey", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 1f, 1f, .2954f, .2594f, .2170f);
            PineBark = PH("pine_bark", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 2f, 2f, .1375f, .0882f, .0533f);
            Rust = PH("rust_coarse_01", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 2.2f, 2.2f, .1282f, .0430f, .0148f);
            Tiles = ACG("Tiles019", .8f, .1295f, .2032f, .2968f);
            Wicker = ACG("Wicker010B", .6f, .1607f, .1188f, .0850f);
            // 第四轮：尺寸、平均色由 Tools~/prep_tex_r4.py 算出
            Linen = R4("PolyHaven", "linen_aged", 1.3535f, .5936f, .5906f, .5857f);
            Crepe = R4("PolyHaven", "crepe_aged", .7896f, .5928f, .5898f, .5849f);
            Velour = R4("PolyHaven", "velour_aged", .8526f, .5964f, .5943f, .5897f);
            Terry = R4("PolyHaven", "terry_aged", 1.1013f, .6344f, .6309f, .6236f);
            Check = R4("PolyHaven", "check_aged", 1f, .3026f, .3098f, .2292f);
            GinghamRed = R4("PolyHaven", "gingham_red_aged", .8f, .3502f, .1530f, .1287f);
            GinghamGreen = R4("PolyHaven", "gingham_green_aged", .8f, .2087f, .2777f, .1600f);
            Ticking = R4("PolyHaven", "ticking_aged", 1.8949f, .4580f, .4544f, .4486f);
            Reed = R4("PolyHaven", "reed_roof_04", 2.5f, .3534f, .2581f, .1498f);
            GreenMetal = R4("PolyHaven", "green_metal_rust", 1f, .0499f, .0742f, .0342f);
            RustyMetal = R4("PolyHaven", "rusty_metal_04", 2f, .1063f, .0739f, .0564f);
            Ceramic = R4("ambientCG", "ceramic_aged", .5f, .6848f, .6807f, .6741f);
            MirrorOld = R4("ambientCG", "mirror_old", .6f, .4908f, .4908f, .4585f);
            BlackSmudge = R4("ambientCG", "black_smudged", .6f, .0053f, .0053f, .0053f);
            PlasticSmudge = R4("ambientCG", "plastic_smudged", .6f, .6938f, .6938f, .6938f);
            Enamel = R4("ambientCG", "enamel_chipped", .6f, .6857f, .6773f, .6713f);
            GoldDirty = R4("ambientCG", "gold_dirty", .5f, .9655f, .7501f, .3232f);
            BronzeOld = R4("ambientCG", "bronze_old", .6f, .2763f, .2021f, .1481f);
            PaperWhite = R4("ambientCG", "paper_white", .5f, .7392f, .7392f, .7392f);
            PaperAged = R4("ambientCG", "paper_aged", .5f, .6315f, .6270f, .6192f);
            // 第六轮（院外）：尺寸来自 Poly Haven 的 dimensions，平均色由 Tools~/prep_tex_r6.py 算出
            Asphalt = PH("asphalt_02", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 3f, 3f, .1120f, .1104f, .0982f);
            FieldGrass = PH("withered_grass", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 2f, 2f, .4256f, .3040f, .2016f);
            TinRoof = PH("rusty_corrugated_iron", "diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 2f, 2f, .1002f, .0246f, .0102f);
            // 第六轮 b：原图满是黄绿的青苔，颜色压不回灰（倍数上限 1.8）；用 prep_tex_r6b.py 去掉 78% 饱和度的那张
            ConcreteMoss = PH("concrete_moss", "grey_diff_1k.jpg", "nor_gl_1k.jpg", "mask.png", 3f, 3f, .0868f, .0841f, .0574f);
            loaded = true;
        }

        // =====================================================================
        // 材质
        // =====================================================================
        /// <summary>让贴图平均起来等于 target（sRGB）的 _BaseColor；超过 1 的通道也允许（深色贴图要提亮），上限 1.8。</summary>
        static Color TintFor(Color target, Vector3 avgLin)
        {
            var t = target.linear;
            var k = new Color(Mathf.Min(1.8f, t.r / Mathf.Max(avgLin.x, 1e-4f)), Mathf.Min(1.8f, t.g / Mathf.Max(avgLin.y, 1e-4f)),
                Mathf.Min(1.8f, t.b / Mathf.Max(avgLin.z, 1e-4f)), 1f);
            return k.gamma;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>
        /// 接上漫反射 / 法线 / mask。smooth 是乘在 mask.A 上的系数（URP：有 _MetallicGlossMap 时 _Smoothness 变成倍数）。
        /// metres 覆盖贴图的实际尺寸（UV 以米为单位时 Tiling = 1/米数）。
        /// </summary>
        public static void Apply(Material m, Tex t, Color target, float smooth, float bump = 1f, Vector2? metres = null, float occlusion = 1f)
        {
            if (m == null || t == null || !t.Ok) return;
            var size = metres ?? t.metres;
            m.SetTexture("_BaseMap", t.albedo);
            m.SetTextureScale("_BaseMap", new Vector2(1f / size.x, 1f / size.y));
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            m.SetColor("_BaseColor", TintFor(target, t.avgLin));
            if (t.normal != null)
            {
                m.SetTexture("_BumpMap", t.normal);
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            if (t.mask != null)
            {
                m.SetTexture("_MetallicGlossMap", t.mask);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                m.SetTexture("_OcclusionMap", t.mask);
                m.SetFloat("_OcclusionStrength", occlusion);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
        }

        /// <summary>只升级一次：没有 rev 这个版本标签的材质才改，改完记上标签。单个材质要重做时给它一个新的 rev。</summary>
        static void Up(Material m, System.Action<Material> f, string rev = Rev)
        {
            if (m == null || m.GetTag(RevTag, false, "") == rev) return;
            f(m);
            m.SetOverrideTag(RevTag, rev);
            EditorUtility.SetDirty(m);
        }

        static void Up(Material m, Tex t, Color target, float smooth, float bump = 1f, Vector2? metres = null, string rev = Rev) =>
            Up(m, x => Apply(x, t, target, smooth, bump, metres), rev);

        /// <summary>灰盒占位材质：墙（按房间分四种）、围墙、庭院地面、木料、藤编、锈铁。</summary>
        public static void UpgradePalette(Palette p)
        {
            Load();
            Up(p.Plaster, Clay, C(.50f, .43f, .33f), .8f);                       // 土壁：和室、玄関、縁側
            Up(p.WallPlaster, PaintedPlaster, C(.55f, .52f, .46f), 1f);           // 发黄的漆喰：走廊、納戸、物置、浴室厕所
            Up(p.WallSoot, PaintedPlaster, C(.20f, .18f, .16f), .8f);             // 熏黑的台所
            // 土蔵：剥落的白漆喰。r2b：1.2 m 一块时远看斑块重复太明显，放大到 1.8 m
            Up(p.WallKura, WhitePlaster, C(.62f, .61f, .57f), .7f, 1f, new Vector2(1.8f, 1.8f), "r2b");
            Up(p.Fence, Clay, C(.32f, .29f, .25f), .6f, 1f, new Vector2(2.5f, 2.5f));
            Up(p.Ground, MudLeaves, C(.16f, .15f, .12f), 1f);
            Up(p.WoodDarkA, Table, C(.22f, .14f, .09f), .9f);
            Up(p.WoodDarkB, Table, C(.27f, .18f, .11f), .9f);
            Up(p.WoodMidA, Table, C(.42f, .30f, .19f), .9f);
            Up(p.WoodMidB, Table, C(.38f, .28f, .20f), .9f);
            Up(p.WoodLightA, CedarU, C(.62f, .50f, .36f), .8f);
            Up(p.WoodLightB, CedarU, C(.56f, .46f, .34f), .8f);
            Up(p.WickerA, Wicker, C(.55f, .42f, .25f), .8f);
            Up(p.WickerB, Wicker, C(.48f, .38f, .24f), .8f);
            Up(p.Rust, Rust, C(.35f, .20f, .12f), .8f, 1f, new Vector2(1f, 1f));
            // r4：剩下的纯色占位材质（障子纸、天花板、土间石、地毯、地板、黑漆、浴室砖、屋顶）。
            // 不动 M_Fusuma_A/B、M_Container_Interior、M_Metal_Dark：押入れ容器和暗门预制体也在用
            const string r4 = "r4";
            Up(p.Paper, PaperWhite, C(.80f, .77f, .68f), .25f, .5f, new Vector2(.9f, .9f), r4);
            Up(p.Ceiling, CedarU, C(.30f, .23f, .16f), .5f, 1f, null, r4);
            Up(p.Stone, GroundGrey, C(.36f, .35f, .33f), .5f, 1f, new Vector2(1.4f, 1.4f), r4);
            Up(p.Carpet, Velour, C(.30f, .11f, .09f), .5f, 1f, new Vector2(1.2f, 1.2f), r4);
            Up(p.Tatami, Tatami, C(.50f, .45f, .31f), .8f, .8f, null, r4);
            Up(p.WoodFloor, DarkPlanks, C(.30f, .24f, .18f), 1.2f, 1f, null, r4);
            Up(p.Lacquer, Cabinet, C(.06f, .035f, .03f), 1.1f, .6f, new Vector2(.6f, .3f), r4);
            Up(p.Tile, Tiles, C(.34f, .40f, .42f), 1.2f, 1f, new Vector2(1.6f, 1.6f), r4);
            Up(p.Roof, Ceramic, C(.10f, .10f, .11f), .5f, 1f, new Vector2(1.5f, 1.5f), r4);
            AssetDatabase.SaveAssets();
        }

        /// <summary>细节材质（MD_* / MX_*）。</summary>
        public static void UpgradeDetail(DetailMats d)
        {
            Load();
            // 榻榻米：整张贴图 = 两张席子，UV 在 TatamiFloor 里按席子单独算（Tiling 1）
            Up(d.Tatami, Tatami, C(.50f, .45f, .31f), .8f, .8f, Vector2.one);
            Up(d.TatamiWorn, Tatami, C(.40f, .34f, .23f), .6f, .8f, Vector2.one);
            Up(d.Heri, Tatami, C(.50f, .45f, .31f), .8f, .8f, Vector2.one);
            Up(d.FloorWood, DarkPlanks, C(.30f, .24f, .18f), 1.2f);
            Up(d.KuraFloor, Weathered, C(.30f, .27f, .24f), .7f);
            Up(d.Ceiling, CedarU, C(.40f, .31f, .22f), .6f);
            Up(d.CeilingDark, CedarU, C(.14f, .11f, .09f), .5f);
            Up(d.Soffit, CedarU, C(.30f, .24f, .18f), .4f);
            Up(d.Tataki, MudDry, C(.30f, .27f, .23f), .5f);
            // r3：浴室地砖原来 0.8 m 一张贴图，砖太碎；放大到 1.6 m
            Up(d.BathTile, Tiles, C(.34f, .40f, .42f), 1.2f, 1f, new Vector2(1.6f, 1.6f), "r3");
            // r3：黑漆原来是没有贴图、光滑度 0.75 的纯黑，夜里反射天空发蓝（仏間的盆提灯顶盖）。
            // 换成磨旧的漆：底下透出木纹，哑一些
            Up(d.Lacquer, Cabinet, C(.05f, .036f, .03f), 1.1f, .6f, new Vector2(.6f, .3f), "r3");
            Up(d.Black, m => m.SetFloat("_Smoothness", .5f), "r3");
            Up(d.Post, Cabinet, C(.28f, .20f, .13f), 1f);
            Up(d.BeamDark, Cabinet, C(.12f, .085f, .06f), .8f);
            Up(d.TrimMid, Cabinet, C(.42f, .31f, .21f), 1f);
            Up(d.TrimLight, CedarU, C(.55f, .44f, .31f), .8f);
            Up(d.Wainscot, Weathered, C(.32f, .26f, .20f), .8f);
            Up(d.WainscotDark, Weathered, C(.17f, .14f, .12f), .6f);
            Up(d.PlasterWhite, WhitePlaster, C(.66f, .65f, .61f), .6f, 1f, new Vector2(1.2f, 1.2f));
            Up(d.Ground, MudLeaves, C(.20f, .17f, .12f), 1f);
            Up(d.Gravel, GroundGrey, C(.42f, .41f, .38f), .8f);
            Up(d.Bark, PineBark, C(.25f, .19f, .15f), .8f);
            Up(d.BambooDry, Bamboo, C(.48f, .41f, .29f), .9f);
            Up(d.Iron, Rust, C(.16f, .10f, .065f), .8f, 1f, new Vector2(1f, 1f));
            Up(d.StoneWall, StoneWall, C(.38f, .37f, .35f), .8f);
            UpgradeDetailR4(d);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 第四轮：剩下的纯色细节材质全部接上贴图（布、毛巾、床垫布、格子被面、瓷、搪瓷、金 / 铜、旧镜子、玻璃、塑料、纸、草、灰、土）。
        /// smooth 是乘在 mask.A 上的系数，按各贴图 mask.A 的平均值反推（布 ≈ .2、瓷釉 ≈ .75、黑漆 ≈ .5）。
        /// </summary>
        static void UpgradeDetailR4(DetailMats d)
        {
            const string r4 = "r4";
            // 布：素色旧布（亚麻 / 绉绸 / 天鹅绒），都带水渍、黄斑
            Up(d.ClothWhite, Linen, C(.70f, .67f, .60f), .6f, 1f, null, r4);
            Up(d.ClothRed, Crepe, C(.42f, .07f, .06f), .7f, 1f, null, r4);
            Up(d.ClothIndigo, Linen, C(.08f, .11f, .22f), .6f, 1f, null, r4);
            Up(d.ClothPurple, Velour, C(.23f, .08f, .21f), .6f, 1f, null, r4);
            Up(d.DustSheet, Linen, C(.60f, .59f, .55f), .1f, 1f, null, r4);
            Up(d.DustSheetOld, Linen, C(.57f, .53f, .44f), .1f, 1f, null, r4);
            Up(d.DustSheetTop, Linen, C(.43f, .41f, .37f), .06f, 1f, null, r4);
            Up(d.Towel, Terry, C(.72f, .70f, .63f), .5f, 1f, null, r4);
            Up(d.TowelBlue, Terry, C(.22f, .32f, .46f), .5f, 1f, null, r4);
            Up(d.Ticking, Ticking, C(.74f, .72f, .66f), .6f, 1f, null, r4);
            Up(d.Check, Check, C(.52f, .53f, .45f), 1f, 1f, null, r4);
            Up(d.GinghamRed, GinghamRed, C(.50f, .34f, .31f), .6f, 1f, null, r4);
            Up(d.GinghamGreen, GinghamGreen, C(.42f, .47f, .37f), .6f, 1f, null, r4);
            // 瓷：带开片和污垢的釉面
            Up(d.CeramicWhite, Ceramic, C(.78f, .76f, .70f), .85f, 1f, null, r4);
            Up(d.CeramicBlue, Ceramic, C(.16f, .22f, .36f), .85f, 1f, null, r4);
            Up(d.CeramicBrown, Ceramic, C(.27f, .16f, .09f), .7f, 1f, null, r4);
            Up(d.RidgeTile, Ceramic, C(.13f, .135f, .15f), .5f, 1f, new Vector2(1f, 1f), r4);
            // 金属：掉瓷的搪瓷、发乌的金、旧铜、绿漆铁皮、锈铁皮
            Up(d.Enamel, Enamel, C(.80f, .79f, .75f), 1f, 1f, null, r4);
            Up(d.Gold, GoldDirty, C(.80f, .60f, .26f), .75f, 1f, null, r4);
            Up(d.GoldLeaf, GoldDirty, C(.70f, .54f, .25f), .45f, 1f, null, r4);
            Up(d.Brass, BronzeOld, C(.52f, .40f, .20f), .85f, 1f, null, r4);
            Up(d.PaintGreen, GreenMetal, C(.26f, .33f, .24f), 1f, 1f, null, r4);
            Up(d.RustSheet, RustyMetal, C(.33f, .24f, .18f), 1f, 1f, null, r4);
            // 旧镜子：水银脱落的斑点 + 手印、擦痕；上面那层灰雾淡一些（镜面自己已经花了）
            Up(d.Mirror, MirrorOld, C(.52f, .53f, .52f), 1f, .4f, null, r4);
            Up(d.MirrorFog, m =>
            {
                var c = m.GetColor("_BaseColor");
                c.a = .32f;
                m.SetColor("_BaseColor", c);
            }, r4);
            // 玻璃、黑漆、塑料、蜡烛：表面有指纹和擦痕
            Up(d.Glass, BlackSmudge, C(.05f, .06f, .07f), 1.2f, 1f, null, r4);
            Up(d.Black, BlackSmudge, C(.03f, .03f, .03f), .7f, 1f, null, r4);
            Up(d.LampGlass, PlasticSmudge, C(.82f, .80f, .74f), 1.3f, 1f, null, r4);
            Up(d.PlasticRed, PlasticSmudge, C(.48f, .05f, .04f), .95f, 1f, null, r4);
            Up(d.Candle, PlasticSmudge, C(.86f, .84f, .78f), .5f, 1f, null, r4);
            // 纸
            Up(d.Paper, PaperWhite, C(.84f, .82f, .76f), .2f, 1f, null, r4);
            Up(d.PaperLamp, PaperWhite, C(.86f, .82f, .70f), .2f, 1f, null, r4);
            Up(d.PaperOld, PaperAged, C(.60f, .55f, .43f), .1f, 1f, null, r4);
            Up(d.PaperText, PaperAged, C(.42f, .39f, .33f), .1f, 1f, new Vector2(.25f, .25f), r4);
            // 草、灰、土、苔、竹
            Up(d.Straw, Reed, C(.55f, .46f, .27f), 1f, 1f, new Vector2(.8f, .8f), r4);
            Up(d.Ash, GroundGrey, C(.36f, .35f, .33f), .1f, 1f, new Vector2(.6f, .6f), r4);
            Up(d.Dirt, MudDry, C(.30f, .24f, .17f), .3f, 1f, null, r4);
            Up(d.Moss, MudLeaves, C(.10f, .16f, .055f), .5f, 1f, new Vector2(.8f, .8f), r4);
            Up(d.Bamboo, Bamboo, C(.32f, .38f, .18f), .9f, 1f, null, r4);
            // 第六轮：院外（村道、荒掉的梯田和田埂、仓房的锈铁皮顶、U 形沟、水泥电线杆）
            const string r6 = "r6";
            Up(d.Asphalt, Asphalt, C(.20f, .20f, .19f), .9f, 1f, null, r6);
            Up(d.FieldGrass, FieldGrass, C(.36f, .31f, .22f), .8f, 1f, null, r6);
            Up(d.FieldBank, FieldGrass, C(.25f, .24f, .16f), .8f, 1f, new Vector2(1.5f, 1.5f), r6);
            Up(d.TinRoof, TinRoof, C(.30f, .17f, .11f), 1f, 1f, null, r6);
            Up(d.Concrete, ConcreteMoss, C(.34f, .34f, .32f), .9f, 1f, null, "r6b");
            Up(d.ConcretePole, WhitePlaster, C(.42f, .42f, .40f), .6f, .6f, new Vector2(1.2f, 1.2f), r6);
            Up(d.FieldFallow, FieldGrass, C(.26f, .25f, .17f), .8f, 1f, new Vector2(2.7f, 2.7f), r6);
            // 第六轮 b：柿子稍亮一点（第一版远看几乎看不见），还是比仏壇的红暗得多
            Up(d.Persimmon, m =>
            {
                m.SetColor("_BaseColor", C(.42f, .17f, .05f));
                m.SetFloat("_Smoothness", .5f);
            }, "r6b");
        }

        /// <summary>封门后钉上去的木板：浅色杉木（比旧门板浅，一眼看出是后来钉的）。在 DM.Load 末尾调用。</summary>
        public static void UpgradeHorror()
        {
            Load();
            Up(DM.Weathered, CedarU, C(.60f, .52f, .42f), .5f);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>外部贴图和贴花第一次导入时就用对的设置（之后手动改的不再覆盖）。</summary>
    internal sealed class ExternalTexImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!ExternalMats.Owns(assetPath) || !assetImporter.importSettingsMissing) return;
            ExternalMats.Configure((TextureImporter)assetImporter, assetPath);
        }
    }

    /// <summary>
    /// Unity 自带立方体每个面的 UV 都是 0..1，贴了按米平铺的贴图会被拉伸。这里给场景里用到带贴图材质的立方体
    /// 换一个同样形状、UV 以米为单位的网格（碰撞体、缩放、名字都不变）：
    /// 墙类材质按世界坐标铺（v 朝上，相邻几段墙接缝对得上），其余按物体最长边方向铺（木纹顺着构件）。
    /// </summary>
    internal static class PrimitiveUV
    {
        static readonly float[] S1 = { -1, 1, 1, -1 }, S2 = { -1, -1, 1, 1 };

        static bool IsBuiltinCube(Mesh m) =>
            m != null && m.name == "Cube" && AssetDatabase.GetAssetPath(m).Contains("unity default resources");

        public static int Apply(Transform root, ICollection<Material> wallMats, System.Func<Transform, bool> skip)
        {
            int n = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!IsBuiltinCube(mf.sharedMesh)) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                var mat = mr.sharedMaterial;
                if (mat == null || !mat.HasProperty("_BaseMap") || mat.GetTexture("_BaseMap") == null) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(mf.gameObject) || (skip != null && skip(mf.transform))) continue;
                var mesh = Build(mf.transform, wallMats.Contains(mat));
                MeshBatch.Created.Add(mesh);
                mf.sharedMesh = mesh;
                n++;
            }
            return n;
        }

        /// <summary>和 t 同形的立方体网格，UV 按米铺（容器预制体也用，见 ContainerPrefabBuilder.SaveUvMeshes）。</summary>
        internal static Mesh Build(Transform t, bool wall)
        {
            var s = t.lossyScale;
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            var l2w = t.localToWorldMatrix;
            var axes = new[] { t.TransformDirection(Vector3.right), t.TransformDirection(Vector3.up), t.TransformDirection(Vector3.forward) };
            int longest = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
            var v = new List<Vector3>(24);
            var nrm = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var tri = new List<int>(36);
            for (int a = 0; a < 3; a++)
            {
                int b = (a + 1) % 3, c = (a + 2) % 3;
                int ua, va;
                if (wall)
                {
                    float yb = Mathf.Abs(axes[b].y), yc = Mathf.Abs(axes[c].y);
                    if (Mathf.Max(yb, yc) > .7f) va = yb >= yc ? b : c;               // 竖直面：v 朝上
                    else va = Mathf.Abs(axes[b].z) >= Mathf.Abs(axes[c].z) ? b : c; // 水平面：u = x，v = z
                    ua = va == b ? c : b;
                }
                else if (longest == b || longest == c) { ua = longest; va = longest == b ? c : b; }
                else { ua = s[b] >= s[c] ? b : c; va = ua == b ? c : b; }

                for (int sg = -1; sg <= 1; sg += 2)
                {
                    var n = Vector3.zero;
                    n[a] = sg;
                    int start = v.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        var lp = Vector3.zero;
                        lp[a] = .5f * sg;
                        lp[b] = S1[i] * .5f;
                        lp[c] = S2[i] * .5f;
                        var wp = l2w.MultiplyPoint3x4(lp);
                        v.Add(lp);
                        nrm.Add(n);
                        uv.Add(new Vector2(Vector3.Dot(wp, axes[ua]), Vector3.Dot(wp, axes[va])));
                    }
                    // Unity 的正面：Cross(b - a, c - a) 指向法线
                    bool ok = Vector3.Dot(Vector3.Cross(v[start + 1] - v[start], v[start + 2] - v[start]), n) > 0;
                    if (ok) tri.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    else tri.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
            }
            var mesh = new Mesh { name = "UV_" + t.name };
            mesh.SetVertices(v);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;
        }
    }
}
