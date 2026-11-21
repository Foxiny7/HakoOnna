using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    public enum ContainerKind
    {
        Oshiire,     // 押入れ  壁橱（推拉门）      —— 设计文档“衣柜”
        YofukuTansu, // 洋服箪笥 西式衣柜（双开门）  —— “衣柜”
        Tansu,       // 箪笥    桐木抽屉柜          —— “抽屉”
        ChaTansu,    // 茶箪笥  餐具柜（双开小门）  —— “橱柜”
        GetaBako,    // 下駄箱  鞋柜（单开门）      —— “储物柜”
        Nagamochi,   // 長持    大木箱（掀盖）      —— “木箱”
        Tsuzura,     // 葛籠    藤编箱（掀盖）      —— “收纳箱”
        Komebitsu,   // 米びつ  米柜（掀盖）        —— “收纳箱”
    }

    /// <summary>
    /// 生成容器预制体。所有容器：
    ///   本地坐标原点 = 底面中心，+Z 为正面（开口方向），尺寸单位米。
    /// 结构：
    ///   Container_X_A            ← 根节点留给程序挂逻辑组件
    ///   ├─ Visual (ContainerVisual)
    ///   │   ├─ 箱体 / 门 / 抽屉 / 盖子（名字是玩法代码和测试的接口：Drawer_0..2 和其下的 Handle、Tray*，
    ///   │   │   Lid_Pivot、Door_L_Pivot / Door_R_Pivot / Door_Pivot、Fusuma_R——改名前先和玩法那边对一下）
    ///   │   ├─ Fittings_r3c（第三轮：不动的金具、漆边、家纹、纸签；所有金具都叫 Fit_*）
    ///   │   └─ Collision (BoxCollider)
    ///   ├─ InteractPoint
    ///   ├─ ItemAnchor   （Tansu 的 ItemAnchor 在 Drawer_0 下，跟随抽屉移动）
    ///   └─ GrabAnchor
    /// 第三轮：
    /// <list type="bullet">
    /// <item>带贴图的立方体换成按米铺 UV 的同形网格（存在预制体旁边的 *_Meshes.asset），木纹不再拉伸；
    ///   缩放、位置都不变（玩法代码会读 TrayBottom 的 localScale）。</item>
    /// <item>金具只贴外表面、没有碰撞体、不进开口——打开时检视镜头要看得到里面的物品（ContainerViewTests）；
    ///   跟着动的金具挂在门、抽屉、盖子的枢轴下面。</item>
    /// <item>押入れ的右扇 Fusuma_R 从“缩放过的立方体”改成同名空节点 + 纸面子物体（Paper），漆框、引手、
    ///   破口挂在它下面不会被拉歪；名字、位置、开法都没变。</item>
    /// <item>金具一律叫 Fit_*：搬家求解、容器正面禁区、血字避让量容器尺寸时跳过它们（WorldBounds），
    ///   所以加金具不会让容器挪位置。</item>
    /// <item>不加挂锁：容器看起来都要是能打开的。</item>
    /// <item>2026-11-20：長持的两道铁箍改成只贴前后两面的 Fit_Band，箱内不再有挡身体的实心板。</item>
    /// </list>
    /// </summary>
    public static class ContainerPrefabBuilder
    {
        public const string Dir = "Assets/_Art/Prefabs/Containers";
        public const string HatchPath = "Assets/_Art/Prefabs/SecretExit_Hatch.prefab";
        public static readonly string[] Variants = { "A", "B" };
        const float K = 0.03f; // 板材厚度
        /// <summary>第三轮的金具组；灰盒预制体里还没有它就重建一次。</summary>
        const string FittingsName = "Fittings_r3c";
        static readonly Quaternion AlongX = Quaternion.Euler(0f, 0f, 90f);
        static readonly Quaternion AlongZ = Quaternion.Euler(90f, 0f, 0f);
        static DetailMats D;

        /// <summary>
        /// 宽 × 高 × 深（米，真实尺寸；场景里统一再放大 FurnitureScale = 1.25）。
        /// 每种都要“看起来藏得下一个蜷着的人”：放大后内腔最小边约 0.5 m 以上（抽屉柜每层约 0.5 m 高、
        /// 0.7 m 深；掀盖箱内腔约 1.0 × 0.7 × 0.7 m）。
        /// </summary>
        public static readonly Dictionary<ContainerKind, Vector3> Sizes = new Dictionary<ContainerKind, Vector3>
        {
            { ContainerKind.Oshiire, new Vector3(1.8f, 1.8f, 0.85f) },
            { ContainerKind.YofukuTansu, new Vector3(1.0f, 1.8f, 0.6f) },
            { ContainerKind.Tansu, new Vector3(1.2f, 1.25f, 0.6f) },     // 衣装箪笥：大抽屉
            { ContainerKind.ChaTansu, new Vector3(1.0f, 1.2f, 0.55f) },  // 大号茶箪笥
            { ContainerKind.GetaBako, new Vector3(0.9f, 1.5f, 0.5f) },   // 高的下駄箱
            { ContainerKind.Nagamochi, new Vector3(1.6f, 0.75f, 0.8f) },
            { ContainerKind.Tsuzura, new Vector3(0.9f, 0.6f, 0.6f) },    // 大葛籠
            { ContainerKind.Komebitsu, new Vector3(0.8f, 0.75f, 0.6f) },
        };

        public static string PathFor(ContainerKind k, string variant) => $"{Dir}/Container_{k}_{variant}.prefab";

        [MenuItem("Hako/Art/1 生成容器预制体（只补缺失的）", priority = 1)]
        static void MenuBuild() => BuildAll(false);

        [MenuItem("Hako/Art/1b 强制重建全部容器预制体", priority = 2)]
        static void MenuRebuild()
        {
            if (EditorUtility.DisplayDialog("强制重建", "会覆盖 " + Dir + " 下所有容器预制体，包括你手动替换过的模型。确定？", "重建", "取消"))
                BuildAll(true);
        }

        public static void BuildAll(bool force)
        {
            ArtKit.EnsureFolder(Dir);
            var pal = Palette.Load();
            D = null;
            foreach (var kind in Sizes.Keys)
            {
                for (int v = 0; v < Variants.Length; v++)
                {
                    string path = PathFor(kind, Variants[v]);
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!force && existing != null && !NeedsRebuild(existing, Sizes[kind])) continue;
                    if (D == null) D = DetailMats.Load();
                    var go = Build(kind, v, pal);
                    SaveUvMeshes(go, path);
                    PrefabUtility.SaveAsPrefabAsset(go, path);
                    Object.DestroyImmediate(go);
                }
            }

            if (force || AssetDatabase.LoadAssetAtPath<GameObject>(HatchPath) == null)
            {
                var hatch = BuildHatch(pal);
                PrefabUtility.SaveAsPrefabAsset(hatch, HatchPath);
                Object.DestroyImmediate(hatch);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 还是灰盒（Visual 下有 Shell 生成的 Back），并且碰撞体尺寸和 Sizes 不一致、或者还没有第三轮的金具组、
        /// 或者長持还是穿过箱内的旧铁条（Band_L）→ 需要重建。手动换过模型的预制体不动。
        /// </summary>
        static bool NeedsRebuild(GameObject prefab, Vector3 size)
        {
            var visual = prefab.transform.Find("Visual");
            if (visual == null || visual.Find("Back") == null) return false;
            if (visual.Find(FittingsName) == null || visual.Find("Band_L") != null) return true;
            var col = visual.Find("Collision")?.GetComponent<BoxCollider>();
            return col != null && (col.size - size).sqrMagnitude > 1e-6f;
        }

        /// <summary>
        /// 带贴图的立方体换成按米铺 UV 的同形网格（u 沿最长边，和场景里的家具一致）。
        /// 预制体不能引用临时网格，所以存进预制体旁边的 *_Meshes.asset，重建时整份换掉。
        /// </summary>
        static void SaveUvMeshes(GameObject go, string prefabPath)
        {
            string meshPath = prefabPath.Replace(".prefab", "_Meshes.asset");
            if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
            bool created = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null || m.name != "Cube" || !AssetDatabase.GetAssetPath(m).Contains("unity default resources")) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                var mat = mr.sharedMaterial;
                if (mat == null || !mat.HasProperty("_BaseMap") || mat.GetTexture("_BaseMap") == null) continue;
                var mesh = PrimitiveUV.Build(mf.transform, false);
                if (!created) { AssetDatabase.CreateAsset(mesh, meshPath); created = true; }
                else AssetDatabase.AddObjectToAsset(mesh, meshPath);
                mf.sharedMesh = mesh;
            }
        }

        static GameObject Build(ContainerKind kind, int variant, Palette pal)
        {
            var size = Sizes[kind];
            float w = size.x, h = size.y, d = size.z;
            bool b = variant == 1;
            var rng = new System.Random((int)kind * 31 + variant * 7 + 3);

            var root = new GameObject($"Container_{kind}_{Variants[variant]}");
            var visual = ArtKit.Empty(root.transform, "Visual", Vector3.zero);
            var cv = visual.gameObject.AddComponent<ContainerVisual>();
            var parts = new List<ContainerVisual.MovingPart>();
            var fit = ArtKit.Empty(visual, FittingsName, Vector3.zero);

            Transform itemParent = root.transform;
            Vector3 itemPos = new Vector3(0f, K + 0.01f, 0f);
            Vector3 grabPos;

            switch (kind)
            {
                case ContainerKind.Oshiire:
                {
                    var body = b ? pal.WoodMidB : pal.WoodMidA;
                    var paper = b ? pal.FusumaB : pal.FusumaA;
                    Shell(visual, w, h, d, body, pal.Interior, top: true, front: false);
                    // 中间的隔板（押入れ典型的上下两层）
                    ArtKit.Box(visual, "Shelf", new Vector3(0f, h * 0.5f, 0f), new Vector3(w - 2 * K, K, d - K), body, false);
                    // 左扇在后轨道，固定；右扇在前轨道，向左推开（右扇：同名空节点 + 纸面，见类注释）
                    ArtKit.Box(visual, "Fusuma_L", new Vector3(-w / 4f, h / 2f, d / 2f + 0.012f),
                        new Vector3(w / 2f, h - 0.02f, 0.022f), paper, false);
                    var right = ArtKit.Empty(visual, "Fusuma_R", new Vector3(w / 4f, h / 2f, d / 2f + 0.038f));
                    ArtKit.Box(right, "Paper", Vector3.zero, new Vector3(w / 2f, h - 0.02f, 0.022f), paper, false);
                    parts.Add(new ContainerVisual.MovingPart { part = right, openOffset = new Vector3(-w / 2f + 0.03f, 0f, 0f) });
                    OshiireFittings(fit, right, w, h, d, paper, rng);
                    itemPos = new Vector3(w / 4f, h * 0.5f + K, 0f);
                    grabPos = new Vector3(w / 4f, h * 0.55f, d / 2f);
                    break;
                }
                case ContainerKind.YofukuTansu:
                case ContainerKind.ChaTansu:
                {
                    bool yofuku = kind == ContainerKind.YofukuTansu;
                    var body = yofuku
                        ? (b ? pal.WoodDarkB : pal.WoodDarkA)
                        : (b ? pal.WoodMidB : pal.WoodDarkA);
                    var panel = b ? pal.WoodDarkA : pal.WoodDarkB;
                    Shell(visual, w, h, d, body, pal.Interior, top: true, front: false);
                    var doorL = HingedDoor(visual, "Door_L", -w / 2f, w / 2f, h, d, true, body);
                    var doorR = HingedDoor(visual, "Door_R", w / 2f, w / 2f, h, d, false, body);
                    parts.Add(doorL);
                    parts.Add(doorR);
                    if (yofuku)
                    {
                        // 昭和的洋服箪笥：左门上半一面旧镜子，右门拉手下有钥匙孔
                        var knob = b ? D.Brass : D.Iron;
                        DoorFittings(doorL.part, w / 2f, h, true, h * 0.5f, knob, panel, DoorStyle.Mirror, false, rng);
                        DoorFittings(doorR.part, w / 2f, h, false, h * 0.5f, knob, panel, DoorStyle.Two, true, rng);
                    }
                    else
                    {
                        DoorFittings(doorL.part, w / 2f, h, true, h * 0.55f, D.Brass, panel, DoorStyle.One, false, rng);
                        DoorFittings(doorR.part, w / 2f, h, false, h * 0.55f, D.Brass, panel, DoorStyle.One, false, rng);
                    }
                    grabPos = new Vector3(0f, h * 0.55f, d / 2f);
                    break;
                }
                case ContainerKind.GetaBako:
                {
                    var body = b ? pal.WoodMidB : pal.WoodMidA;
                    Shell(visual, w, h, d, body, pal.Interior, top: true, front: false);
                    var door = HingedDoor(visual, "Door", -w / 2f, w, h, d, true, body);
                    parts.Add(door);
                    // 下駄箱：上半是通风的百叶
                    DoorFittings(door.part, w, h, true, h * 0.55f, b ? D.Brass : D.Iron, b ? pal.WoodMidA : pal.WoodMidB,
                        DoorStyle.Louver, false, rng);
                    grabPos = new Vector3(0f, h * 0.55f, d / 2f);
                    break;
                }
                case ContainerKind.Tansu:
                {
                    var body = b ? pal.WoodLightB : pal.WoodLightA;
                    var metal = b ? D.Brass : D.Iron;
                    Shell(visual, w, h, d, body, pal.Interior, top: true, front: false);
                    float dh = (h - 2 * K) / 3f;
                    Transform drawer0 = null;
                    var drawers = new Transform[3];
                    for (int i = 0; i < 3; i++)
                    {
                        float yBottom = h - K - (i + 1) * dh;
                        var drawer = ArtKit.Empty(visual, $"Drawer_{i}", new Vector3(0f, yBottom, 0f));
                        drawers[i] = drawer;
                        ArtKit.Box(drawer, "Front", new Vector3(0f, dh / 2f, d / 2f + 0.0125f),
                            new Vector3(w - 2 * K - 0.006f, dh - 0.006f, 0.025f), body, false);
                        // 横杆拉手（检视时手抓这里）：位置、尺寸和灰盒一样（容器尺寸不能变），底板在 TansuFittings
                        ArtKit.Cylinder(drawer, "Handle", new Vector3(0f, dh / 2f, d / 2f + 0.03f),
                            new Vector3(0.02f, 0.06f, 0.02f), Quaternion.Euler(0f, 0f, 90f), metal);
                        if (i == 0)
                        {
                            drawer0 = drawer;
                            float trayLen = d - K - 0.01f;
                            float trayZ = (K + 0.01f) / 2f;
                            float iw = w - 2 * K - 0.02f;
                            ArtKit.Box(drawer, "TrayBottom", new Vector3(0f, 0.008f, trayZ), new Vector3(iw, 0.012f, trayLen), pal.Interior, false);
                            ArtKit.Box(drawer, "TraySide_L", new Vector3(-iw / 2f, dh * 0.35f, trayZ), new Vector3(0.012f, dh * 0.7f, trayLen), body, false);
                            ArtKit.Box(drawer, "TraySide_R", new Vector3(iw / 2f, dh * 0.35f, trayZ), new Vector3(0.012f, dh * 0.7f, trayLen), body, false);
                            ArtKit.Box(drawer, "TrayBack", new Vector3(0f, dh * 0.35f, trayZ - trayLen / 2f), new Vector3(iw, dh * 0.7f, 0.012f), body, false);
                            parts.Add(new ContainerVisual.MovingPart { part = drawer, openOffset = new Vector3(0f, 0f, d * 0.75f) });
                        }
                    }
                    TansuFittings(fit, drawers, w, h, d, dh, metal);
                    itemParent = drawer0;
                    itemPos = new Vector3(0f, 0.02f, 0f);
                    grabPos = new Vector3(0f, h - K - dh / 2f, d / 2f);
                    break;
                }
                default: // 掀盖类：Nagamochi / Tsuzura / Komebitsu
                {
                    Material body = kind switch
                    {
                        ContainerKind.Nagamochi => b ? pal.WoodDarkB : pal.WoodDarkA,
                        ContainerKind.Tsuzura => b ? pal.WickerB : pal.WickerA,
                        _ => b ? pal.WoodLightB : pal.WoodLightA,
                    };
                    Shell(visual, w, h, d, body, pal.Interior, top: false, front: true);
                    var pivot = ArtKit.Empty(visual, "Lid_Pivot", new Vector3(0f, h, -d / 2f));
                    ArtKit.Box(pivot, "Lid", new Vector3(0f, 0.0175f, d / 2f), new Vector3(w + 0.02f, 0.035f, d + 0.02f), body, false);
                    if (kind == ContainerKind.Nagamochi) NagamochiFittings(fit, pivot, w, h, d, pal.Rust);
                    else if (kind == ContainerKind.Tsuzura) TsuzuraFittings(fit, pivot, w, h, d);
                    else KomebitsuFittings(fit, pivot, w, h, d, b, rng);
                    parts.Add(new ContainerVisual.MovingPart { part = pivot, openEuler = new Vector3(-105f, 0f, 0f) });
                    grabPos = new Vector3(0f, h, 0f);
                    break;
                }
            }

            var col = ArtKit.Empty(visual, "Collision", Vector3.zero).gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h / 2f, 0f);
            col.size = new Vector3(w, h, d);

            ArtKit.Empty(root.transform, "InteractPoint", new Vector3(0f, 0f, d / 2f + 0.6f));
            ArtKit.Empty(itemParent, "ItemAnchor", itemPos);
            ArtKit.Empty(root.transform, "GrabAnchor", grabPos);

            cv.ConfigureParts(parts.ToArray());
            return root;
        }

        /// <summary>柜体：背板、两侧、底板，可选顶板和前板；内部背面贴一块近黑色面板制造“看不清”的内腔。</summary>
        static void Shell(Transform p, float w, float h, float d, Material body, Material interior, bool top, bool front)
        {
            ArtKit.Box(p, "Back", new Vector3(0f, h / 2f, -d / 2f + K / 2f), new Vector3(w, h, K), body, false);
            ArtKit.Box(p, "Side_L", new Vector3(-w / 2f + K / 2f, h / 2f, 0f), new Vector3(K, h, d), body, false);
            ArtKit.Box(p, "Side_R", new Vector3(w / 2f - K / 2f, h / 2f, 0f), new Vector3(K, h, d), body, false);
            ArtKit.Box(p, "Bottom", new Vector3(0f, K / 2f, 0f), new Vector3(w - 2 * K, K, d), body, false);
            if (top) ArtKit.Box(p, "Top", new Vector3(0f, h - K / 2f, 0f), new Vector3(w, K, d), body, false);
            if (front) ArtKit.Box(p, "Front", new Vector3(0f, h / 2f, d / 2f - K / 2f), new Vector3(w - 2 * K, h, K), body, false);

            if (top)
                ArtKit.Box(p, "Interior", new Vector3(0f, h / 2f, -d / 2f + K + 0.003f), new Vector3(w - 2 * K, h - 2 * K, 0.005f), interior, false);
            else
                ArtKit.Box(p, "Interior", new Vector3(0f, K + 0.003f, 0f), new Vector3(w - 2 * K, 0.005f, d - 2 * K), interior, false);
        }

        /// <summary>铰链门。hingeX 为铰链所在 x，门向外（+Z）转开约 100°。</summary>
        static ContainerVisual.MovingPart HingedDoor(Transform p, string name, float hingeX, float doorW, float h, float d,
            bool hingeOnLeft, Material mat)
        {
            var pivot = ArtKit.Empty(p, name + "_Pivot", new Vector3(hingeX, 0f, d / 2f));
            float dir = hingeOnLeft ? 1f : -1f;
            ArtKit.Box(pivot, name, new Vector3(dir * doorW / 2f, h / 2f, 0.0125f), new Vector3(doorW - 0.005f, h - 0.02f, 0.025f), mat, false);
            return new ContainerVisual.MovingPart
            {
                part = pivot,
                openEuler = new Vector3(0f, hingeOnLeft ? -100f : 100f, 0f),
            };
        }

        // =====================================================================
        // 第三轮：金具和细节（全部没有碰撞体；坐标是容器本地的真实尺寸）
        // =====================================================================
        enum DoorStyle { One, Two, Mirror, Louver }

        const string Fit = "Fit_";

        static GameObject Plate(Transform p, string name, Vector3 c, Vector3 s, Material m) =>
            ArtKit.Box(p, Fit + name, c, s, m, false, false);

        static GameObject Plate(Transform p, string name, Vector3 c, Vector3 s, Material m, Vector3 euler)
        {
            var go = Plate(p, name, c, s, m);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        static GameObject Rod(Transform p, string name, Vector3 c, float dia, float len, Quaternion rot, Material m) =>
            ArtKit.Cylinder(p, Fit + name, c, new Vector3(dia, len / 2f, dia), rot, m);

        /// <summary>容器金具（Fit_*）：量容器尺寸时跳过。</summary>
        internal static bool IsFitting(Component c) => c.name.StartsWith(Fit);

        static GameObject Ball(Transform p, string name, Vector3 c, float dia, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = Fit + name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = c;
            go.transform.localScale = Vector3.one * dia;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>
        /// 箪笥：每层抽屉一块拉手底板（横杆是 Handle）、四角 L 形包角，顶层加锁板和钥匙孔；
        /// 柜体两侧八个包角、顶面四角、两侧平贴的提手（只凸出 1.5 cm，贴墙摆也不会戳进墙）。
        /// </summary>
        static void TansuFittings(Transform fit, Transform[] drawers, float w, float h, float d, float dh, Material metal)
        {
            float fz = d / 2f + 0.025f; // 抽屉面板正面
            float hw = (w - 2 * K - 0.006f) / 2f, hh = (dh - 0.006f) / 2f, cy = dh / 2f;
            for (int i = 0; i < drawers.Length; i++)
            {
                var dr = drawers[i];
                Plate(dr, "KanPlate", new Vector3(0f, cy, fz + 0.002f), new Vector3(0.17f, 0.06f, 0.004f), metal);
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sy in new[] { -1f, 1f })
                    {
                        Plate(dr, "Corner", new Vector3(sx * (hw - 0.025f), cy + sy * (hh - 0.007f), fz + 0.0015f),
                            new Vector3(0.05f, 0.014f, 0.003f), metal);
                        Plate(dr, "Corner", new Vector3(sx * (hw - 0.007f), cy + sy * (hh - 0.032f), fz + 0.0015f),
                            new Vector3(0.014f, 0.036f, 0.003f), metal);
                    }
                if (i == 0)
                {
                    Plate(dr, "LockPlate", new Vector3(0f, cy + 0.11f, fz + 0.002f), new Vector3(0.07f, 0.08f, 0.004f), metal);
                    Plate(dr, "Keyhole", new Vector3(0f, cy + 0.103f, fz + 0.0045f), new Vector3(0.008f, 0.024f, 0.002f), D.Black);
                    Rod(dr, "Keyhole", new Vector3(0f, cy + 0.117f, fz + 0.0045f), 0.015f, 0.002f, AlongZ, D.Black);
                }
            }

            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (w / 2f + 0.0015f);
                foreach (float y in new[] { 0.045f, h - 0.045f })
                    foreach (float z in new[] { d / 2f - 0.045f, -d / 2f + 0.045f })
                        Plate(fit, "SideCorner", new Vector3(x, y, z), new Vector3(0.003f, 0.09f, 0.09f), metal);
                Plate(fit, "SideHandlePlate", new Vector3(x, h - 0.19f, 0f), new Vector3(0.003f, 0.06f, 0.2f), metal);
                Rod(fit, "SideHandle", new Vector3(sx * (w / 2f + 0.009f), h - 0.23f, 0f), 0.012f, 0.17f, AlongZ, metal);
                foreach (float z in new[] { -0.08f, 0.08f })
                    Rod(fit, "SideHandleLink", new Vector3(sx * (w / 2f + 0.008f), h - 0.205f, z), 0.009f, 0.05f, Quaternion.identity, metal);
                foreach (float sz in new[] { -1f, 1f })
                    Plate(fit, "TopCorner", new Vector3(sx * (w / 2f - 0.045f), h + 0.0015f, sz * (d / 2f - 0.045f)),
                        new Vector3(0.09f, 0.003f, 0.09f), metal);
            }
        }

        /// <summary>
        /// 门上的金具（挂在门的枢轴下，跟着门转）：铰链片、嵌板（深一号的木头，比门面凸 2 mm）、圆拉手；
        /// Mirror：上半一面起雾、脱银、带裂纹的旧镜子；Louver：上半是通风的百叶；keyhole：拉手下面的钥匙孔。
        /// </summary>
        static void DoorFittings(Transform pv, float doorW, float h, bool left, float knobY, Material knob, Material panel,
            DoorStyle style, bool keyhole, System.Random rng)
        {
            float dir = left ? 1f : -1f, fz = 0.025f, cx = dir * doorW / 2f;
            foreach (float y in new[] { 0.13f, h - 0.13f })
                Plate(pv, "Hinge", new Vector3(dir * 0.009f, y, fz + 0.0015f), new Vector3(0.016f, 0.075f, 0.003f), D.Iron);
            float pw = doorW - 0.12f, y0 = 0.07f, y1 = h - 0.07f;
            void Panel(float a, float top) =>
                Plate(pv, "Panel", new Vector3(cx, (a + top) / 2f, fz + 0.002f), new Vector3(pw, top - a, 0.004f), panel);
            switch (style)
            {
                case DoorStyle.One:
                    Panel(y0, y1);
                    break;
                case DoorStyle.Two:
                {
                    float split = y0 + (y1 - y0) * 0.3f;
                    Panel(y0, split - 0.03f);
                    Panel(split + 0.03f, y1);
                    break;
                }
                case DoorStyle.Mirror:
                {
                    float split = y0 + (y1 - y0) * 0.3f;
                    Panel(y0, split - 0.03f);
                    DoorMirror(pv, new Vector3(cx, (split + 0.03f + y1) / 2f, fz), pw, y1 - split - 0.03f, panel, rng);
                    break;
                }
                case DoorStyle.Louver:
                {
                    float split = y0 + (y1 - y0) * 0.45f;
                    Panel(y0, split - 0.03f);
                    float ly0 = split + 0.03f;
                    const int n = 9;
                    for (int i = 0; i < n; i++)
                        Plate(pv, "Louver", new Vector3(cx, ly0 + (i + 0.5f) * (y1 - ly0) / n, fz + 0.001f),
                            new Vector3(pw, 0.013f, 0.002f), D.Black);
                    break;
                }
            }
            float kx = dir * (doorW - 0.045f);
            Rod(pv, "KnobRose", new Vector3(kx, knobY, fz + 0.003f), 0.034f, 0.004f, AlongZ, knob);
            Rod(pv, "KnobStem", new Vector3(kx, knobY, fz + 0.012f), 0.011f, 0.016f, AlongZ, knob);
            Ball(pv, "Knob", new Vector3(kx, knobY, fz + 0.025f), 0.026f, knob);
            if (keyhole)
            {
                Plate(pv, "Escutcheon", new Vector3(kx, knobY - 0.07f, fz + 0.003f), new Vector3(0.03f, 0.055f, 0.004f), knob);
                Plate(pv, "Keyhole", new Vector3(kx, knobY - 0.076f, fz + 0.0055f), new Vector3(0.006f, 0.016f, 0.002f), D.Black);
                Rod(pv, "Keyhole", new Vector3(kx, knobY - 0.065f, fz + 0.0055f), 0.011f, 0.002f, AlongZ, D.Black);
            }
        }

        /// <summary>门上的旧镜子：木框、镜面、边缘多中间少的脱银黑斑、从一点放射的裂纹，最外面一层雾。c = 门面上的中心。</summary>
        static void DoorMirror(Transform pv, Vector3 c, float mw, float mh, Material frame, System.Random rng)
        {
            Plate(pv, "MirrorFrame", c + new Vector3(0f, 0f, 0.002f), new Vector3(mw, mh, 0.004f), frame);
            float gw = mw - 0.03f, gh = mh - 0.03f;
            Plate(pv, "Mirror", c + new Vector3(0f, 0f, 0.0045f), new Vector3(gw, gh, 0.001f), D.Mirror);
            for (int i = 0; i < 8; i++)
            {
                float u = R(rng, -0.45f, 0.45f), v = R(rng, -0.45f, 0.45f);
                if (i < 6)
                {
                    if (rng.Next(2) == 0) u = (u < 0 ? -1f : 1f) * R(rng, 0.36f, 0.46f);
                    else v = (v < 0 ? -1f : 1f) * R(rng, 0.4f, 0.47f);
                }
                Rod(pv, "Desilver", c + new Vector3(u * gw, v * gh, 0.0052f), R(rng, 0.02f, 0.055f), 0.0004f, AlongZ, D.Black);
            }
            var hit = new Vector3(c.x + R(rng, -0.2f, 0.2f) * gw, c.y + R(rng, -0.05f, 0.3f) * gh, c.z + 0.0055f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + R(rng, -18f, 18f), len = R(rng, 0.08f, 0.25f);
                var dv = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f);
                float mx = Mathf.Abs(dv.x) > 1e-3f ? ((dv.x > 0 ? c.x + gw / 2f : c.x - gw / 2f) - hit.x) / dv.x - 0.005f : 99f;
                float my = Mathf.Abs(dv.y) > 1e-3f ? ((dv.y > 0 ? c.y + gh / 2f : c.y - gh / 2f) - hit.y) / dv.y - 0.005f : 99f;
                len = Mathf.Min(len, Mathf.Min(mx, my));
                if (len < 0.02f) continue;
                Plate(pv, "Crack", hit + dv * len / 2f, new Vector3(0.0016f, len, 0.0004f), D.Black, new Vector3(0f, 0f, a - 90f));
            }
            Plate(pv, "MirrorFog", c + new Vector3(0f, 0f, 0.0062f), new Vector3(gw, gh, 0.0008f), D.MirrorFog);
        }

        /// <summary>押入れ：两扇襖的黑漆框（縁）和引手；右扇（跟着动）下面破了个口、翘起一片纸。</summary>
        static void OshiireFittings(Transform fit, Transform right, float w, float h, float d, Material paper, System.Random rng)
        {
            float pw = w / 2f, ph = h - 0.02f, hy = 0.8f - h / 2f;
            FusumaTrim(fit, new Vector3(-w / 4f, h / 2f, d / 2f + 0.023f), pw, ph, -1f, hy);
            FusumaTrim(right, new Vector3(0f, 0f, 0.011f), pw, ph, 1f, hy);
            // 破口：几块黑片叠成不规则的洞，边上几条毛边纸屑伸进洞里，上下各翘起一片纸
            var hole = new Vector3(-0.1f, -0.42f, 0f);
            float[,] blobs = { { 0f, 0f, 0.11f, 0.15f, 12f }, { 0.025f, 0.035f, 0.08f, 0.11f, -24f }, { -0.03f, -0.05f, 0.065f, 0.08f, 38f }, { 0.02f, -0.06f, 0.05f, 0.06f, -8f } };
            for (int i = 0; i < blobs.GetLength(0); i++)
                Plate(right, "Tear", hole + new Vector3(blobs[i, 0], blobs[i, 1], 0.0114f + i * 0.0002f),
                    new Vector3(blobs[i, 2], blobs[i, 3], 0.002f), D.Black, new Vector3(0f, 0f, blobs[i, 4] + R(rng, -6f, 6f)));
            for (int i = 0; i < 7; i++)
            {
                float a = i * 51f + R(rng, -12f, 12f), rr = R(rng, 0.045f, 0.06f);
                var dv = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad) * 1.3f, 0f);
                Plate(right, "TearEdge", hole + dv * rr + new Vector3(0f, 0f, 0.0135f), new Vector3(R(rng, 0.008f, 0.014f), R(rng, 0.03f, 0.05f), 0.0012f),
                    paper, new Vector3(0f, 0f, a + 90f + R(rng, -25f, 25f)));
            }
            Plate(right, "TearFlap", hole + new Vector3(-0.015f, -0.085f, 0.03f), new Vector3(0.1f, 0.075f, 0.0015f), paper,
                new Vector3(-35f, 0f, R(rng, 10f, 22f)));
            Plate(right, "TearFlap", hole + new Vector3(0.02f, 0.085f, 0.022f), new Vector3(0.07f, 0.05f, 0.0015f), paper,
                new Vector3(30f, 0f, R(rng, -20f, -8f)));
        }

        /// <summary>襖的黑漆框和引手。c = 襖正面中心；side = 引手靠哪边（-1 左、+1 右）；hikiteY 相对 c。</summary>
        static void FusumaTrim(Transform p, Vector3 c, float pw, float ph, float side, float hikiteY)
        {
            foreach (float sy in new[] { -1f, 1f })
                Plate(p, "Fuchi", c + new Vector3(0f, sy * (ph / 2f - 0.015f), 0.001f), new Vector3(pw, 0.03f, 0.006f), D.Black);
            foreach (float sx in new[] { -1f, 1f })
                Plate(p, "Fuchi", c + new Vector3(sx * (pw / 2f - 0.0125f), 0f, 0.001f), new Vector3(0.025f, ph - 0.06f, 0.006f), D.Black);
            var hp = c + new Vector3(side * (pw / 2f - 0.11f), hikiteY, 0f);
            Rod(p, "Hikite", hp + new Vector3(0f, 0f, 0.0015f), 0.075f, 0.003f, AlongZ, D.Brass);
            Rod(p, "Hikite", hp + new Vector3(0f, 0f, 0.0028f), 0.052f, 0.0015f, AlongZ, D.Black);
        }

        /// <summary>
        /// 長持：盖子上接着箱身铁箍的铁条、盖角包铁、前沿垂下的搭扣（跟着盖子）；箱身八角包铁、前面带钥匙孔的锁板；
        /// 两头平贴的穿竿方环（只凸出 2 cm）；箱身前后两面的两道铁箍（band 材质，让長持在暗处有辨识度）。
        /// </summary>
        static void NagamochiFittings(Transform fit, Transform lid, float w, float h, float d, Material band)
        {
            var iron = D.Iron;
            foreach (float sx in new[] { -1f, 1f })
            {
                Plate(lid, "LidBand", new Vector3(sx * (w / 2f - 0.15f), 0.0175f, d / 2f), new Vector3(0.042f, 0.041f, d + 0.026f), iron);
                foreach (float sz in new[] { -1f, 1f })
                    Plate(lid, "LidCorner", new Vector3(sx * (w / 2f - 0.03f), 0.0365f, d / 2f + sz * (d / 2f - 0.025f)),
                        new Vector3(0.08f, 0.004f, 0.07f), iron);
            }
            Plate(lid, "Hasp", new Vector3(0f, -0.015f, d + 0.016f), new Vector3(0.05f, 0.1f, 0.006f), iron);
            Plate(fit, "LockPlate", new Vector3(0f, h - 0.11f, d / 2f + 0.002f), new Vector3(0.13f, 0.1f, 0.004f), iron);
            Plate(fit, "Keyhole", new Vector3(0f, h - 0.125f, d / 2f + 0.0045f), new Vector3(0.01f, 0.03f, 0.002f), D.Black);
            Rod(fit, "Keyhole", new Vector3(0f, h - 0.108f, d / 2f + 0.0045f), 0.018f, 0.002f, AlongZ, D.Black);

            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    foreach (float y in new[] { 0.05f, h - 0.05f })
                    {
                        Plate(fit, "Corner", new Vector3(sx * (w / 2f - 0.04f), y, sz * (d / 2f + 0.0015f)), new Vector3(0.08f, 0.1f, 0.003f), iron);
                        Plate(fit, "Corner", new Vector3(sx * (w / 2f + 0.0015f), y, sz * (d / 2f - 0.04f)), new Vector3(0.003f, 0.1f, 0.08f), iron);
                    }

            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (w / 2f + 0.0015f), xr = sx * (w / 2f + 0.011f);
                Plate(fit, "PoleBracket", new Vector3(x, h - 0.19f, 0f), new Vector3(0.003f, 0.12f, 0.22f), iron);
                foreach (float y in new[] { h - 0.13f, h - 0.25f })
                    Rod(fit, "PoleRing", new Vector3(xr, y, 0f), 0.014f, 0.2f, AlongZ, iron);
                foreach (float z in new[] { -0.093f, 0.093f })
                    Rod(fit, "PoleRing", new Vector3(xr, h - 0.19f, z), 0.014f, 0.134f, Quaternion.identity, iron);
            }

            // 两道铁箍只贴前、后两面，顶上接盖子的 LidBand。2026-11-20 以前是整块穿过箱内的实心板（Band_L / Band_R）：
            // 掀开盖子就看得见，箱女的藏匿姿势也只能塞在两块板之间
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Plate(fit, "Band", new Vector3(sx * (w / 2f - 0.15f), h / 2f, sz * (d / 2f + 0.002f)), new Vector3(0.04f, h, 0.004f), band);
        }

        /// <summary>葛籠：黑漆的口沿、底边、四条竖棱和盖沿（盖沿跟着盖子），正面一个白色家纹（丸に梅鉢）。</summary>
        static void TsuzuraFittings(Transform fit, Transform lid, float w, float h, float d)
        {
            var lac = D.Black;
            foreach (float y in new[] { 0.022f, h - 0.022f })
            {
                foreach (float sz in new[] { -1f, 1f })
                    Plate(fit, "RimBand", new Vector3(0f, y, sz * (d / 2f + 0.003f)), new Vector3(w + 0.012f, 0.044f, 0.006f), lac);
                foreach (float sx in new[] { -1f, 1f })
                    Plate(fit, "RimBand", new Vector3(sx * (w / 2f + 0.003f), y, 0f), new Vector3(0.006f, 0.044f, d), lac);
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Plate(fit, "EdgeBand", new Vector3(sx * (w / 2f - 0.008f), h / 2f, sz * (d / 2f - 0.008f)), new Vector3(0.03f, h - 0.09f, 0.03f), lac);
            foreach (float sz in new[] { -1f, 1f })
                Plate(lid, "LidBand", new Vector3(0f, 0.0175f, d / 2f + sz * (d / 2f + 0.013f)), new Vector3(w + 0.032f, 0.045f, 0.006f), lac);
            foreach (float sx in new[] { -1f, 1f })
                Plate(lid, "LidBand", new Vector3(sx * (w / 2f + 0.013f), 0.0175f, d / 2f), new Vector3(0.006f, 0.045f, d + 0.02f), lac);

            // 家纹：丸に梅鉢（白圈里五个圆瓣 + 花心）。原来的丸に一つ引き远看像“禁止通行”标志
            float cy = h * 0.48f, cz = d / 2f;
            Rod(fit, "Crest", new Vector3(0f, cy, cz + 0.0015f), 0.15f, 0.003f, AlongZ, D.ClothWhite);
            Rod(fit, "Crest", new Vector3(0f, cy, cz + 0.0035f), 0.12f, 0.002f, AlongZ, lac);
            for (int i = 0; i < 5; i++)
            {
                float a = (90f + i * 72f) * Mathf.Deg2Rad;
                Rod(fit, "Crest", new Vector3(Mathf.Cos(a) * 0.034f, cy + Mathf.Sin(a) * 0.034f, cz + 0.005f), 0.03f, 0.002f, AlongZ, D.ClothWhite);
            }
            Rod(fit, "Crest", new Vector3(0f, cy, cz + 0.005f), 0.016f, 0.002f, AlongZ, D.ClothWhite);
        }

        /// <summary>米びつ：四条竖棱包铁皮、盖子前沿铁条和抠手槽（跟着盖子），正面贴一张写着“米”的旧纸签。</summary>
        static void KomebitsuFittings(Transform fit, Transform lid, float w, float h, float d, bool b, System.Random rng)
        {
            var metal = b ? D.Brass : D.Iron;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Plate(fit, "EdgeIron", new Vector3(sx * (w / 2f - 0.012f), h / 2f, sz * (d / 2f - 0.012f)), new Vector3(0.03f, h - 0.004f, 0.03f), metal);
            Plate(lid, "LidEdge", new Vector3(0f, 0.0175f, d + 0.0125f), new Vector3(w + 0.02f, 0.037f, 0.005f), metal);
            Plate(lid, "FingerSlot", new Vector3(0f, 0.0352f, d - 0.05f), new Vector3(0.16f, 0.002f, 0.03f), D.Black);

            // 纸签（微微歪着）+ 墨写的“米”
            float ly = h * 0.56f, lz = d / 2f + 0.0012f, tilt = R(rng, -6f, 6f);
            var label = ArtKit.Empty(fit, "Label", new Vector3(0f, ly, lz));
            label.localRotation = Quaternion.Euler(0f, 0f, tilt);
            Plate(label, "Paper", Vector3.zero, new Vector3(0.13f, 0.17f, 0.0015f), D.Paper);
            const float iz = 0.0012f;
            Plate(label, "Ink", new Vector3(0f, 0f, iz), new Vector3(0.09f, 0.012f, 0.001f), D.Ink);                       // 横
            Plate(label, "Ink", new Vector3(0f, -0.005f, iz), new Vector3(0.012f, 0.13f, 0.001f), D.Ink);                  // 竖
            Plate(label, "Ink", new Vector3(-0.03f, 0.035f, iz), new Vector3(0.01f, 0.03f, 0.001f), D.Ink, new Vector3(0f, 0f, 30f));   // 左上点
            Plate(label, "Ink", new Vector3(0.03f, 0.035f, iz), new Vector3(0.01f, 0.03f, 0.001f), D.Ink, new Vector3(0f, 0f, -30f));   // 右上点
            Plate(label, "Ink", new Vector3(-0.028f, -0.035f, iz), new Vector3(0.01f, 0.05f, 0.001f), D.Ink, new Vector3(0f, 0f, -40f)); // 撇
            Plate(label, "Ink", new Vector3(0.028f, -0.035f, iz), new Vector3(0.01f, 0.05f, 0.001f), D.Ink, new Vector3(0f, 0f, 40f));   // 捺
        }

        /// <summary>秘密出口：地板暗门 + 锁链 + 挂锁。发现时由程序在 HatchAnchor 处生成。</summary>
        static GameObject BuildHatch(Palette pal)
        {
            const float ow = 1.0f, od = 0.8f;
            var root = new GameObject("SecretExit_Hatch");
            var visual = ArtKit.Empty(root.transform, "Visual", Vector3.zero);
            ArtKit.Box(visual, "DarkHole", new Vector3(0f, 0.002f, 0f), new Vector3(ow, 0.004f, od), pal.Interior, false);
            ArtKit.Box(visual, "Frame_N", new Vector3(0f, 0.015f, od / 2f + 0.03f), new Vector3(ow + 0.12f, 0.03f, 0.06f), pal.WoodDarkA, false);
            ArtKit.Box(visual, "Frame_S", new Vector3(0f, 0.015f, -od / 2f - 0.03f), new Vector3(ow + 0.12f, 0.03f, 0.06f), pal.WoodDarkA, false);
            ArtKit.Box(visual, "Frame_W", new Vector3(-ow / 2f - 0.03f, 0.015f, 0f), new Vector3(0.06f, 0.03f, od), pal.WoodDarkA, false);
            ArtKit.Box(visual, "Frame_E", new Vector3(ow / 2f + 0.03f, 0.015f, 0f), new Vector3(0.06f, 0.03f, od), pal.WoodDarkA, false);

            var pivot = ArtKit.Empty(visual, "HatchDoor_Pivot", new Vector3(0f, 0.03f, od / 2f));
            ArtKit.Box(pivot, "HatchDoor", new Vector3(0f, 0.02f, -od / 2f), new Vector3(ow - 0.04f, 0.04f, od - 0.04f), pal.WoodMidA, false);

            // 锁链组：剪断后由程序隐藏或播放断裂动画
            var chain = ArtKit.Empty(visual, "Chain", new Vector3(0f, 0.075f, 0f));
            ArtKit.Box(chain, "Chain_A", Vector3.zero, new Vector3(1.15f, 0.02f, 0.03f), pal.Rust, false).transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            ArtKit.Box(chain, "Chain_B", Vector3.zero, new Vector3(1.15f, 0.02f, 0.03f), pal.Rust, false).transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
            ArtKit.Box(chain, "Padlock", new Vector3(0f, 0.03f, 0f), new Vector3(0.07f, 0.09f, 0.03f), pal.Metal, false);

            var col = ArtKit.Empty(visual, "Collision", new Vector3(0f, 0.04f, 0f)).gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(ow + 0.12f, 0.08f, od + 0.12f);

            ArtKit.Empty(root.transform, "InteractPoint", new Vector3(0f, 0f, -od / 2f - 0.6f));
            ArtKit.Empty(root.transform, "EscapePoint", new Vector3(0f, -1f, 0f));
            return root;
        }
    }
}
