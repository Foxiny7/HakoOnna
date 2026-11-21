using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第三轮：Poly Haven 的 CC0 模型（来源见 Textures/External/README_来源.md）。
    /// <list type="bullet">
    /// <item>模型：Models/External/PolyHaven/&lt;id&gt;/&lt;id&gt;.fbx；贴图：Textures/External/PolyHavenModels/&lt;id&gt;/（导入设置归 ExternalMats）。</item>
    /// <item>导入：不要相机、灯光、动画，不加碰撞体（挡路的另放 Blocker_*）。</item>
    /// <item>材质：按 FBX 里的材质名重映射到 Materials/External/Models/ 下的 URP Lit 材质。
    ///   只建一次、打 HakoArtRev 标签，之后在 Unity 里手调的不会被覆盖；想重来就换 Rev 或删掉材质。</item>
    /// <item>摆放：生成场景时经 MeshBatch.Model / Kit.Model 登记（跟着家具一起搬家、放大），
    ///   Build 时复制一份（不连预制体）放到家具节点下；only 按子物体名只留一部分（换一株草、只要脏靴子）。</item>
    /// </list>
    /// </summary>
    internal static class ExternalModels
    {
        public const string Root = "Assets/_Art/Models/External";
        public const string PolyHaven = Root + "/PolyHaven";
        public const string TexDir = ExternalMats.Root + "/PolyHavenModels";
        const string MatDir = "Assets/_Art/Materials/External/Models";
        const string RevTag = "HakoArtRev", Rev = "r3";

        enum Surf { Opaque, Cutout, Glass }

        sealed class Spec
        {
            public string mat, id, set, nset;   // FBX 材质名、模型 id、漫反射贴图组、法线 / mask 贴图组（默认同 set）
            public Surf surf;
            public bool twoSided;
            public Color tint = Dust;
            public float smooth = .75f;         // 乘在 mask.A（1 - 粗糙度）上
        }

        /// <summary>积灰：整体压暗、略去饱和。</summary>
        static readonly Color Dust = new Color(.80f, .77f, .73f, 1f);

        static Spec S(string id, string mat = null, string set = null, Surf surf = Surf.Opaque, bool twoSided = false,
            Color? tint = null, float smooth = .75f, string nset = null) =>
            new Spec { id = id, mat = mat ?? id, set = set ?? id, nset = nset ?? set ?? id, surf = surf, twoSided = twoSided, tint = tint ?? Dust, smooth = smooth };

        static readonly Spec[] Specs =
        {
            // 煤油提灯：发暗的黄铜（贴图带镂空），玻璃罩熏黑
            S("Lantern_01", "Lantern_01_brass", "Lantern_01_brass", Surf.Cutout, true, new Color(.62f, .56f, .46f, 1f), .6f),
            new Spec { id = "Lantern_01", mat = "Lantern_01_glass", surf = Surf.Glass, tint = new Color(.20f, .18f, .15f, .55f), smooth = .82f },
            S("Television_01", tint: new Color(.74f, .71f, .67f, 1f), smooth: .7f),
            S("antique_ceramic_vase_01", smooth: .8f),
            S("baseball_01", tint: new Color(.62f, .58f, .52f, 1f), smooth: .5f),
            S("brass_vase_01", "brass_vase_001", tint: new Color(.55f, .49f, .40f, 1f), smooth: .55f),
            S("dead_quiver_branch_01", twoSided: true, tint: new Color(.70f, .66f, .62f, 1f)),
            S("dry_branches_medium_01", twoSided: true, tint: new Color(.70f, .66f, .62f, 1f)),
            S("dustpan"),
            // 灯泡：玻璃部分用贴图的透明通道，灯头不透明；不亮
            new Spec { id = "lightbulb_01", mat = "lightbulb_01_glass", set = "lightbulb_01", nset = "lightbulb_01", surf = Surf.Glass, tint = new Color(.75f, .72f, .66f, 1f), smooth = .9f },
            S("lightbulb_01", "lightbulb_01_base"),
            S("mousetrap"),
            // 荨麻：用枯黄的那套漫反射（dry），法线 / mask 共用
            S("nettle_plant", set: "nettle_plant_dry", nset: "nettle_plant", surf: Surf.Cutout, twoSided: true, tint: new Color(.78f, .74f, .66f, 1f), smooth: .6f),
            S("oil_tin"),
            S("planter_pot_clay"),
            S("plunger"),
            S("pot_enamel_01"),
            S("pull_chain_light_socket"),
            S("rubber_boots", "rubber_boots_dirty", "rubber_boots_dirty"),
            S("rubber_boots"),
            S("rusted_spade_01"),
            S("watering_can_metal_01"),
            S("weed_plant_02", surf: Surf.Cutout, twoSided: true, tint: new Color(.72f, .70f, .60f, 1f), smooth: .6f),
            S("wooden_bookshelf_worn"),
            S("wooden_broom"),
            S("wooden_broom", "wooden_broom_bristles", "wooden_broom_bristles", Surf.Cutout, true),
            S("wooden_bucket_01"),
            S("wooden_bucket_02"),
            S("wooden_cutting_board"),
            S("wooden_ladder_02"),
            S("wooden_stool_01"),
            S("wooden_stool_02"),
            // 第四轮（2026-11-20）：换掉代码拼的方块家具——铁床架、桌椅、学生桌、床头小桌、洗衣筐
            S("old_bed_frame", tint: new Color(.74f, .71f, .67f, 1f), smooth: .65f),
            S("painted_wooden_chair_01", smooth: .6f),
            S("painted_wooden_chair_02", smooth: .6f),
            S("painted_wooden_table", smooth: .6f),
            S("SchoolDesk_01", smooth: .6f),
            S("side_table_01", smooth: .6f),
            S("wicker_basket_01", tint: new Color(.74f, .70f, .64f, 1f), smooth: .6f),
        };

        public static IEnumerable<string> Ids => Specs.Select(s => s.id).Distinct();

        public static string ModelPath(string id) => $"{PolyHaven}/{id}/{id}.fbx";

        // =====================================================================
        // 材质与重映射
        // =====================================================================
        static Texture2D Tex(string id, string file, bool warn = true)
        {
            string path = $"{TexDir}/{id}/{file}";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null && warn) Debug.LogWarning($"[Hako Art] 缺少模型贴图：{path}");
            return t;
        }

        static Material MaterialFor(Spec s)
        {
            ArtKit.EnsureFolder(MatDir);
            string path = $"{MatDir}/{s.mat}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = s.mat };
            else if (m.GetTag(RevTag, false, "") == Rev) return m;
            Setup(m, s);
            m.SetOverrideTag(RevTag, Rev);
            if (created) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        static void Setup(Material m, Spec s)
        {
            Texture2D albedo = null, normal = null, mask = null;
            if (s.set != null)
            {
                albedo = Tex(s.id, s.set + "_diff_a.png", false) ?? Tex(s.id, s.set + "_diff_1k.jpg");
                normal = Tex(s.id, s.nset + "_nor_gl_1k.jpg");
                mask = Tex(s.id, s.nset + "_mask.png");
            }
            m.SetTexture("_BaseMap", albedo);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            m.SetColor("_BaseColor", s.tint);
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", s.smooth);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (mask != null)
            {
                m.SetTexture("_MetallicGlossMap", mask);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", mask);
                m.SetFloat("_OcclusionStrength", 1f);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            m.SetFloat("_Cull", s.twoSided ? (float)CullMode.Off : (float)CullMode.Back);
            m.doubleSidedGI = s.twoSided;
            switch (s.surf)
            {
                case Surf.Cutout:
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", .45f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                    break;
                case Surf.Glass:
                    // 同 DetailMats.GlassMat：Alpha 混合、保留高光，不写深度、不投影
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetFloat("_BlendModePreserveSpecular", 1f);
                    m.SetFloat("_AlphaClip", 0f);
                    m.SetFloat("_CastShadows", 0f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.One);
                    m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.renderQueue = (int)RenderQueue.Transparent;
                    m.SetShaderPassEnabled("ShadowCaster", false);
                    m.SetShaderPassEnabled("DepthOnly", false);
                    break;
                default:
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_AlphaClip", 0f);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = -1;
                    break;
            }
        }

        static bool ensured;

        /// <summary>建材质、把每个 FBX 的材质重映射过去（缺的才补，补了就重新导入）。</summary>
        public static void Ensure()
        {
            if (ensured) return;
            ensured = true;
            ExternalMats.EnsureImportSettings();
            int reimported = 0;
            foreach (var id in Ids)
            {
                string path = ModelPath(id);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter imp))
                {
                    Debug.LogWarning($"[Hako Art] 缺少模型：{path}");
                    continue;
                }
                var map = imp.GetExternalObjectMap();
                bool dirty = false;
                foreach (var s in Specs.Where(x => x.id == id))
                {
                    var mat = MaterialFor(s);
                    var key = new AssetImporter.SourceAssetIdentifier(typeof(Material), s.mat);
                    if (map.TryGetValue(key, out var cur) && cur == mat) continue;
                    imp.AddRemap(key, mat);
                    dirty = true;
                }
                if (!dirty) continue;
                imp.SaveAndReimport();
                reimported++;
            }
            AssetDatabase.SaveAssets();
            if (reimported > 0) Debug.Log($"[Hako Art] 外部模型材质重映射 {reimported} 个");
        }

        // =====================================================================
        // 尺寸与摆放
        // =====================================================================
        static readonly Dictionary<string, Bounds> boundsCache = new Dictionary<string, Bounds>();
        static readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>
        /// 复制一份模型（不连预制体），套一个空的外壳节点：只有一个物体的 FBX，导入后的根节点自带
        /// 轴向转换（X 转 90°）和 ×100 缩放，外壳保留它们，外壳的坐标系就是 FBX 的世界坐标（Y 朝上、米）。
        /// only：用 | 分隔的子物体名片段，只留名字含其中之一的子物体（及其子孙）。
        /// </summary>
        static GameObject Copy(string id, string only)
        {
            Ensure();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(id));
            if (prefab == null)
            {
                if (missing.Add(id)) Debug.LogWarning($"[Hako Art] 缺少模型：{ModelPath(id)}（跳过）");
                return null;
            }
            var holder = new GameObject(id);
            var go = Object.Instantiate(prefab, holder.transform, false);
            go.name = prefab.name;
            go.transform.localPosition = prefab.transform.localPosition;
            go.transform.localRotation = prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale;
            if (!string.IsNullOrEmpty(only)) Filter(go.transform, only.Split('|'));
            return holder;
        }

        static void Filter(Transform root, string[] keep)
        {
            var kept = new HashSet<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root || !keep.Any(k => t.name.Contains(k))) continue;
                for (var a = t; a != null && a != root; a = a.parent) kept.Add(a);
                foreach (var d in t.GetComponentsInChildren<Transform>(true)) kept.Add(d);
            }
            var dead = new List<GameObject>();
            void Walk(Transform t)
            {
                foreach (Transform c in t)
                {
                    if (!kept.Contains(c)) dead.Add(c.gameObject);
                    else Walk(c);
                }
            }
            Walk(root);
            foreach (var d in dead) Object.DestroyImmediate(d);
            // 根节点自己带网格、又没被选中时，只留下选中的子物体
            if (!keep.Any(k => root.name.Contains(k)) && root.GetComponent<MeshFilter>() != null && kept.Count > 0)
            {
                Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(root.GetComponent<MeshFilter>());
            }
        }

        /// <summary>模型（或 only 选中的部分）在模型根节点坐标系里的包围盒；缺模型时 size = 0。</summary>
        public static Bounds LocalBounds(string id, string only = null)
        {
            string key = id + "|" + only;
            if (boundsCache.TryGetValue(key, out var b)) return b;
            var go = Copy(id, only);
            bool has = false;
            if (go != null)
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var mb = mf.sharedMesh.bounds;
                    var l2w = mf.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = l2w.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, Corner(i)));
                        if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                        else b.Encapsulate(p);
                    }
                }
                Object.DestroyImmediate(go);
            }
            boundsCache[key] = b;
            return b;
        }

        /// <summary>模型在根节点坐标系里的全部顶点（和 LocalBounds 同一个坐标系；第四轮量床头、椅背朝哪边用）。</summary>
        public static List<Vector3> LocalVerts(string id, string only = null)
        {
            var list = new List<Vector3>();
            var go = Copy(id, only);
            if (go == null) return list;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var l2w = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) list.Add(l2w.MultiplyPoint3x4(v));
            }
            Object.DestroyImmediate(go);
            return list;
        }

        public static Vector3 Corner(int i) => new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);

        public static bool Has(string id, string only = null) => LocalBounds(id, only).size != Vector3.zero;

        /// <summary>
        /// 模型根节点的局部矩阵：按 rot、scale 转好以后，最低点落在 basePos.y，水平方向居中在 basePos
        /// （放倒的凳子、侧躺的桶也贴着地）。
        /// </summary>
        public static Matrix4x4 Place(string id, string only, Vector3 basePos, Quaternion rot, float scale)
        {
            var b = LocalBounds(id, only);
            Vector3 mn = Vector3.one * float.MaxValue, mx = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var p = rot * ((b.center + Vector3.Scale(b.extents, Corner(i))) * scale);
                mn = Vector3.Min(mn, p);
                mx = Vector3.Max(mx, p);
            }
            var offset = new Vector3(-(mn.x + mx.x) / 2, -mn.y, -(mn.z + mx.z) / 2);
            return Matrix4x4.TRS(basePos + offset, rot, Vector3.one * scale);
        }

        /// <summary>按高度换算缩放（模型真实尺寸不合适时，比如凳子要矮一点）。</summary>
        public static float ScaleToHeight(string id, float h, string only = null)
        {
            var b = LocalBounds(id, only);
            return b.size.y > 1e-4f ? h / b.size.y : 1f;
        }

        /// <summary>等比缩放到放进 size（米）里。</summary>
        public static float ScaleToFit(string id, Vector3 size, string only = null)
        {
            var b = LocalBounds(id, only).size;
            float s = float.MaxValue;
            for (int a = 0; a < 3; a++) if (b[a] > 1e-4f && size[a] > 0f) s = Mathf.Min(s, size[a] / b[a]);
            return s == float.MaxValue ? 1f : s;
        }

        /// <summary>在 parent 下按 m（相对 parent，含旋转和等比缩放）放一份模型。</summary>
        public static GameObject Spawn(string id, Transform parent, Matrix4x4 m, string only = null, bool shadows = true)
        {
            var go = Copy(id, only);
            if (go == null) return null;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = m.GetColumn(3);
            go.transform.localRotation = m.rotation;
            go.transform.localScale = m.lossyScale;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                if (!shadows && t.TryGetComponent<MeshRenderer>(out var mr)) mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        // =====================================================================
        // 批处理：导入、建材质，打印每个模型的尺寸（写摆放代码前量尺寸用）
        // =====================================================================
        public static void SetupAndReport()
        {
            Ensure();
            var sb = new System.Text.StringBuilder("[Hako Models] 外部模型尺寸（米，模型根节点坐标系）\n");
            foreach (var id in Ids)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(id));
                if (prefab == null) { sb.AppendLine($"{id}: 缺少"); continue; }
                var b = LocalBounds(id);
                sb.AppendLine($"{id}: size {b.size.x:F3} x {b.size.y:F3} x {b.size.z:F3}  center {b.center.x:F3},{b.center.y:F3},{b.center.z:F3}  root rot {prefab.transform.localEulerAngles} scale {prefab.transform.localScale}");
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                {
                    var mf = t.GetComponent<MeshFilter>();
                    var mr = t.GetComponent<MeshRenderer>();
                    string path = t == prefab.transform ? "(root)" : AnimationUtility.CalculateTransformPath(t, prefab.transform);
                    long tris = 0;
                    if (mf != null && mf.sharedMesh != null)
                        for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) tris += mf.sharedMesh.GetIndexCount(i) / 3;
                    string mesh = mf != null && mf.sharedMesh != null ? $" mesh {mf.sharedMesh.name} tris {tris}" : "";
                    string mats = mr != null ? " mats " + string.Join(",", mr.sharedMaterials.Select(x => x == null ? "null" : (AssetDatabase.GetAssetPath(x).StartsWith(MatDir) ? "" : "!") + x.name)) : "";
                    string part = "";
                    if (t != prefab.transform && mf != null)
                    {
                        var pb = LocalBounds(id, t.name);
                        part = $" size {pb.size.x:F3} x {pb.size.y:F3} x {pb.size.z:F3} center {pb.center.x:F3},{pb.center.y:F3},{pb.center.z:F3}";
                    }
                    sb.AppendLine($"    {path}{mesh}{mats}{part}");
                }
            }
            Debug.Log(sb.ToString());
            var args = System.Environment.GetCommandLineArgs();
            int ai = System.Array.IndexOf(args, "-hakoSheet");
            if (ai >= 0 && ai + 1 < args.Length) ContactSheet(args[ai + 1]);
        }

        /// <summary>每个模型一格的总览图（斜前方看；红块 = +X，蓝块 = +Z），确认朝向用。</summary>
        static void ContactSheet(string file)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.5f;
            key.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.45f, .45f, .47f);
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = sh;
            RenderSettings.fog = false;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.16f, .16f, .18f);
            cam.fieldOfView = 30f;
            Material Unlit(Color c)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                m.SetColor("_BaseColor", c);
                return m;
            }
            var mx = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mx.GetComponent<MeshRenderer>().sharedMaterial = Unlit(Color.red);
            var mz = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mz.GetComponent<MeshRenderer>().sharedMaterial = Unlit(new Color(.2f, .4f, 1f));
            var ids = Ids.ToList();
            const int cell = 256, cols = 6;
            int rows = (ids.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGB24, false);
            var rt = new RenderTexture(cell, cell, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                string only = id == "rubber_boots" ? "dirt" : null;
                var go = Spawn(id, null, Matrix4x4.identity, only);
                if (go == null) continue;
                var b = LocalBounds(id, only);
                float r = Mathf.Max(b.extents.magnitude, .01f);
                foreach (var (m, dir) in new[] { (mx, Vector3.right), (mz, Vector3.forward) })
                {
                    m.transform.localScale = Vector3.one * r * .12f;
                    m.transform.position = b.center + dir * (Vector3.Scale(b.extents, dir).magnitude + r * .15f);
                }
                var view = new Vector3(.9f, .55f, 1.4f).normalized;
                float dist = r * 1.3f / Mathf.Sin(cam.fieldOfView * .5f * Mathf.Deg2Rad);
                cam.transform.position = b.center + view * dist;
                cam.transform.LookAt(b.center);
                cam.nearClipPlane = dist * .02f;
                cam.farClipPlane = dist * 4f;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, cell, cell), (i % cols) * cell, (rows - 1 - i / cols) * cell);
                RenderTexture.active = prev;
                Object.DestroyImmediate(go);
            }
            sheet.Apply();
            System.IO.File.WriteAllBytes(file, sheet.EncodeToPNG());
            cam.targetTexture = null;
            rt.Release();
            Debug.Log($"[Hako Models] 总览图：{file}（顺序：{string.Join(", ", ids)}）");
        }
    }

    /// <summary>外部模型第一次导入时就用对的设置（之后手动改的不再覆盖）。</summary>
    internal sealed class ExternalModelImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ExternalModels.Root + "/") || !assetImporter.importSettingsMissing) return;
            var imp = (ModelImporter)assetImporter;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importVisibility = false;
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.importBlendShapes = false;
            imp.addCollider = false;
            imp.isReadable = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.useFileScale = true;
            imp.globalScale = 1f;
            imp.bakeAxisConversion = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }
}
