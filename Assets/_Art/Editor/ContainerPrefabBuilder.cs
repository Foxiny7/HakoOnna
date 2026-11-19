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
    /// 生成容器灰盒预制体。所有容器：
    ///   本地坐标原点 = 底面中心，+Z 为正面（开口方向），尺寸单位米。
    /// 结构：
    ///   Container_X_A            ← 根节点留给程序挂逻辑组件
    ///   ├─ Visual (ContainerVisual)
    ///   │   ├─ 箱体 / 门 / 抽屉 / 盖子
    ///   │   └─ Collision (BoxCollider)
    ///   ├─ InteractPoint
    ///   ├─ ItemAnchor   （Tansu 的 ItemAnchor 在 Drawer_0 下，跟随抽屉移动）
    ///   └─ GrabAnchor
    /// </summary>
    public static class ContainerPrefabBuilder
    {
        public const string Dir = "Assets/_Art/Prefabs/Containers";
        public const string HatchPath = "Assets/_Art/Prefabs/SecretExit_Hatch.prefab";
        public static readonly string[] Variants = { "A", "B" };
        const float K = 0.03f; // 板材厚度

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
            foreach (var kind in Sizes.Keys)
            {
                for (int v = 0; v < Variants.Length; v++)
                {
                    string path = PathFor(kind, Variants[v]);
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!force && existing != null && !BlockoutSizeChanged(existing, Sizes[kind])) continue;
                    var go = Build(kind, v, pal);
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
        /// 还是灰盒（Visual 下有 Shell 生成的 Back）且碰撞体尺寸和 Sizes 不一致 → 需要重建。
        /// 手动换过模型的预制体不动。
        /// </summary>
        static bool BlockoutSizeChanged(GameObject prefab, Vector3 size)
        {
            var visual = prefab.transform.Find("Visual");
            if (visual == null || visual.Find("Back") == null) return false;
            var col = visual.Find("Collision")?.GetComponent<BoxCollider>();
            return col != null && (col.size - size).sqrMagnitude > 1e-6f;
        }

        static GameObject Build(ContainerKind kind, int variant, Palette pal)
        {
            var size = Sizes[kind];
            float w = size.x, h = size.y, d = size.z;
            bool b = variant == 1;

            var root = new GameObject($"Container_{kind}_{Variants[variant]}");
            var visual = ArtKit.Empty(root.transform, "Visual", Vector3.zero);
            var cv = visual.gameObject.AddComponent<ContainerVisual>();
            var parts = new List<ContainerVisual.MovingPart>();

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
                    // 左扇在后轨道，固定；右扇在前轨道，向左推开
                    ArtKit.Box(visual, "Fusuma_L", new Vector3(-w / 4f, h / 2f, d / 2f + 0.012f),
                        new Vector3(w / 2f, h - 0.02f, 0.022f), paper, false);
                    var right = ArtKit.Box(visual, "Fusuma_R", new Vector3(w / 4f, h / 2f, d / 2f + 0.038f),
                        new Vector3(w / 2f, h - 0.02f, 0.022f), paper, false);
                    parts.Add(new ContainerVisual.MovingPart { part = right.transform, openOffset = new Vector3(-w / 2f + 0.03f, 0f, 0f) });
                    itemPos = new Vector3(w / 4f, h * 0.5f + K, 0f);
                    grabPos = new Vector3(w / 4f, h * 0.55f, d / 2f);
                    break;
                }
                case ContainerKind.YofukuTansu:
                case ContainerKind.ChaTansu:
                {
                    var body = kind == ContainerKind.YofukuTansu
                        ? (b ? pal.WoodDarkB : pal.WoodDarkA)
                        : (b ? pal.WoodMidB : pal.WoodDarkA);
                    Shell(visual, w, h, d, body, pal.Interior, top: true, front: false);
                    parts.Add(HingedDoor(visual, "Door_L", -w / 2f, w / 2f, h, d, true, body));
                    parts.Add(HingedDoor(visual, "Door_R", w / 2f, w / 2f, h, d, false, body));
                    grabPos = new Vector3(0f, h * 0.55f, d / 2f);
                    break;
                }
                case ContainerKind.GetaBako:
                {
                    var body = b ? pal.WoodMidB : pal.WoodMidA;
                    Shell(visual, w, h, d, body, pal.Interior, top: true, front: false);
                    parts.Add(HingedDoor(visual, "Door", -w / 2f, w, h, d, true, body));
                    grabPos = new Vector3(0f, h * 0.55f, d / 2f);
                    break;
                }
                case ContainerKind.Tansu:
                {
                    var body = b ? pal.WoodLightB : pal.WoodLightA;
                    Shell(visual, w, h, d, body, pal.Interior, top: true, front: false);
                    float dh = (h - 2 * K) / 3f;
                    Transform drawer0 = null;
                    for (int i = 0; i < 3; i++)
                    {
                        float yBottom = h - K - (i + 1) * dh;
                        var drawer = ArtKit.Empty(visual, $"Drawer_{i}", new Vector3(0f, yBottom, 0f));
                        ArtKit.Box(drawer, "Front", new Vector3(0f, dh / 2f, d / 2f + 0.0125f),
                            new Vector3(w - 2 * K - 0.006f, dh - 0.006f, 0.025f), body, false);
                        ArtKit.Cylinder(drawer, "Handle", new Vector3(0f, dh / 2f, d / 2f + 0.03f),
                            new Vector3(0.02f, 0.06f, 0.02f), Quaternion.Euler(0f, 0f, 90f), pal.Metal);
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
                    if (kind == ContainerKind.Nagamochi)
                    {
                        // 金属包角，让長持在暗处有辨识度
                        ArtKit.Box(visual, "Band_L", new Vector3(-w / 2f + 0.15f, h / 2f, 0f), new Vector3(0.04f, h + 0.005f, d + 0.005f), pal.Rust, false);
                        ArtKit.Box(visual, "Band_R", new Vector3(w / 2f - 0.15f, h / 2f, 0f), new Vector3(0.04f, h + 0.005f, d + 0.005f), pal.Rust, false);
                    }
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
