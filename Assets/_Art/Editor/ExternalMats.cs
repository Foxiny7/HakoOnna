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
        static bool loaded;

        // =====================================================================
        // 导入设置
        // =====================================================================
        /// <summary>AlbedoAlpha：模型的镂空 / 叶片漫反射（*_diff_a.png，透明通道 = 不透明度）；Painting：Met 的画（Clamp）。</summary>
        enum Kind { Albedo, AlbedoAlpha, Painting, Normal, Mask, Decal }

        public const string MetDir = Root + "/Met";

        static Kind KindOf(string path)
        {
            string f = Path.GetFileName(path);
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
                    break;
                case Kind.Painting:
                    imp.textureType = TextureImporterType.Default;
                    imp.sRGBTexture = true;
                    imp.alphaSource = TextureImporterAlphaSource.None;
                    imp.wrapMode = TextureWrapMode.Clamp;
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
            AssetDatabase.SaveAssets();
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
