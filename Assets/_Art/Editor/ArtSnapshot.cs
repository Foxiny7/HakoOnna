using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 美术检查用截图：从固定机位把当前老宅场景渲染成 PNG（Docs/screenshots/detail）。
    /// 白天 / 室内补光只在截图时临时改，结束后恢复，不会存进场景。
    /// 命令行：Unity -batchmode -executeMethod HakoOnna.Art.EditorTools.ArtSnapshot.GenerateAndShoot -quit
    /// </summary>
    public static partial class ArtSnapshot
    {
        const string OutDir = "Docs/screenshots/detail";

        /// <summary>游戏光里手电的强度：2026-11-19 用户看了 5 / 12 / 25 的对比后定为 25（Codex 的 NetworkPlayer.Inspection.cs 原来是 5）。</summary>
        const float GameBeamIntensity = 60f; // 2026-11-23 用户两次要求调高（25 → 40 → 60）；和 NetworkPlayer.Inspection.cs 一致

        /// <summary>
        /// Detail：室内灯光，机位直接用新坐标书写（不搬家映射，也不参与“机位前方不放家具”）。
        /// MoodX：和 Mood 一样是游戏里的光（场景环境光 + 雾 + 手电），但不参与“机位前方不放家具”，
        /// 加机位不会让家具搬家（第二轮检查暗度用）。
        /// 第六轮（院外风景，机位都是新坐标）：
        /// OutX：探索者看到的——和 MoodX 一样的光（场景环境光 + 雾 + 手电），清屏用天空盒、远裁 1000 m（和 Explorer.prefab 的相机一样）。
        /// Ghost：箱女视角——照 GhostSight.cs：关雾、关后处理、环境光 (.66, .92, .84)、相机上一盏 (.67, 1, .86) 射程 20 强度 5 的点光。
        /// Aerial：白天的光 + 天空盒，从高处看院外的整体布局（只给审图用）。
        /// </summary>
        enum Mode { Day, Interior, Mood, Plan1F, Plan2F, Detail, MoodX, OutX, Ghost, Aerial }

        /// <summary>室外机位：平面和高度一起放大，构图和旧版一致。</summary>
        static float ExteriorScale => HouseBlockoutGenerator.PlanStretch;

        struct Shot
        {
            public string name;
            public Vector3 pos, look;
            public float fov;
            public Mode mode;
            public bool raw; // 机位已是新坐标（场景里现找的，比如血字特写）
            public string closeDoor; // 拍这张时先把这扇门关上（Doors 下的名字），拍完再打开
            public bool openGate;    // 第六轮：拍这张时把大门两扇门板往里打开，拍完关回去
            public Shot(string n, Mode m, Vector3 p, Vector3 l, float f = 70f) { name = n; mode = m; pos = p; look = l; fov = f; raw = false; closeDoor = null; openGate = false; }
        }

        static bool IsMood(Mode m) => m == Mode.Mood || m == Mode.MoodX || m == Mode.OutX;

        /// <summary>这一张清屏用天空盒（院外的机位、院外东西的白天审图）；别的照旧用纯色背景。</summary>
        static bool UsesSky(Shot s) => s.mode == Mode.OutX || s.mode == Mode.Ghost || s.mode == Mode.Aerial || s.name.StartsWith("rev_r6_") || s.name.StartsWith("rev_r7_");

        /// <summary>
        /// 第六轮：只生成场景、拍院外的机位（10 个视点 × 探索者 / 箱女视角、路上 3 个、高处 4 张、生成时记下的 r6_ 特写），
        /// 再拍 3 个室内机位“有天空盒 / 没有天空盒”各一张，比较亮度（天空盒不该改变屋里的光）。
        /// 命令行：Unity -batchmode -executeMethod HakoOnna.Art.EditorTools.ArtSnapshot.GenerateAndShootOutskirts -quit
        /// </summary>
        public static void GenerateAndShootOutskirts()
        {
            HouseBlockoutGenerator.Generate();
            ShootOutskirts();
        }

        [MenuItem("Hako/Art/9b 院外风景截图检查", priority = 91)]
        public static void ShootOutskirts()
        {
            var shots = new List<Shot>(OutskirtsShots());
            AddRuinShots(shots, true);
            ShootList(shots);
            SkyAB();
        }

        /// <summary>
        /// 第七轮：只生成场景、拍主庭园陈设的机位（生成时记下的 r7_：隔着玻璃门从縁側往外看的 7 张、院子里的特写和全景），
        /// 每个机位白天、探索者视角、箱女视角各一张。
        /// 命令行：Unity -batchmode -executeMethod HakoOnna.Art.EditorTools.ArtSnapshot.GenerateAndShootGarden -quit
        /// </summary>
        public static void GenerateAndShootGarden()
        {
            HouseBlockoutGenerator.Generate();
            ShootGarden();
        }

        [MenuItem("Hako/Art/9d 主庭园陈设截图检查", priority = 93)]
        public static void ShootGarden()
        {
            var shots = new List<Shot>();
            AddRuinShots(shots, true, "r7_");
            ShootList(shots);
        }

        static IEnumerable<Shot> OutskirtsShots()
        {
            var views = new (string name, Vector3 pos, Vector3 look, bool open)[]
            {
                // 院子里往墙外看（二楼、土蔵的窗都是纸障子，縁側是关着的玻璃门，屋里看不到院外；第六轮 b 换成院子里的机位）
                ("gate_closed", new Vector3(5.46f, 1.6f, -5f), new Vector3(5.46f, 2.6f, -14f), false),
                ("gate_open", new Vector3(5.46f, 1.6f, -6.5f), new Vector3(5.46f, 1.1f, -16f), true),
                ("gate_inside", new Vector3(9.5f, 1.6f, -4.5f), new Vector3(9.3f, 8.5f, -11.5f), false),
                ("garden_south", new Vector3(34f, 1.6f, -2.5f), new Vector3(31f, 6.5f, -24f), false),
                ("garden_east", new Vector3(40.5f, 1.6f, 23f), new Vector3(48.5f, 5.2f, 26.5f), false),
                ("backyard", new Vector3(4f, 1.6f, 40f), new Vector3(0f, 6f, 60f), false),
                ("backyard_ne", new Vector3(40f, 1.6f, 47.5f), new Vector3(36f, 11f, 72f), false),
                ("backyard_bamboo", new Vector3(-5.5f, 1.6f, 28f), new Vector3(-13f, 7.5f, 31f), false),
                ("west", new Vector3(-2f, 1.6f, 8f), new Vector3(-20f, 4f, 2f), false),
                // 大门外的路上（门开着）：往西、往东、回头看大门、往东北看仓房
                ("road_west", new Vector3(5.46f, 1.65f, -13.2f), new Vector3(-20f, 1.8f, -13.4f), true),
                ("road_east", new Vector3(5.46f, 1.65f, -13.2f), new Vector3(40f, 1.8f, -13.4f), true),
                ("road_gate", new Vector3(6.2f, 1.65f, -15.8f), new Vector3(5.46f, 2.4f, -10.9f), true),
                ("road_barn", new Vector3(30f, 1.65f, -13.2f), new Vector3(60f, 2.5f, 3f), false),
            };
            foreach (var v in views)
            {
                yield return new Shot("out_" + v.name, Mode.OutX, v.pos, v.look, 75f) { raw = true, openGate = v.open };
                yield return new Shot("outghost_" + v.name, Mode.Ghost, v.pos, v.look, 75f) { raw = true, openGate = v.open };
            }
            yield return new Shot("aerial_southwest", Mode.Aerial, new Vector3(-70f, 48f, -78f), new Vector3(19f, -2f, 18f), 55f) { raw = true };
            yield return new Shot("aerial_southeast", Mode.Aerial, new Vector3(105f, 48f, -78f), new Vector3(19f, -2f, 18f), 55f) { raw = true };
            yield return new Shot("aerial_north", Mode.Aerial, new Vector3(19f, 120f, 175f), new Vector3(19f, 0f, 8f), 55f) { raw = true };
            yield return new Shot("aerial_valley", Mode.Aerial, new Vector3(19f, 22f, -160f), new Vector3(19f, 4f, 10f), 50f) { raw = true };
        }

        /// <summary>
        /// 生成时记下的审图机位（RuinShots）。院外的（r6_）：白天看形状（清屏用天空盒），探索者视角、箱女视角各一张；
        /// 别的照第三～五轮的规矩。onlyOutskirts = true 时只要 r6_ 的。
        /// </summary>
        static void AddRuinShots(List<Shot> shots, bool onlyOutskirts, string onlyPrefix = "r6_")
        {
            foreach (var (name, pos, look) in HouseBlockoutGenerator.RuinShots)
            {
                if (name.StartsWith("r6_") || name.StartsWith("r7_"))
                {
                    if (onlyOutskirts && !name.StartsWith(onlyPrefix)) continue;
                    shots.Add(new Shot("rev_" + name, Mode.Day, pos, look) { raw = true });
                    shots.Add(new Shot("revout_" + name, Mode.OutX, pos, look, 75f) { raw = true });
                    shots.Add(new Shot("revghost_" + name, Mode.Ghost, pos, look, 75f) { raw = true });
                    continue;
                }
                if (onlyOutskirts) continue;
                // 第三轮：废弃感里随机摆的东西（水渍、破障子、蜘蛛网），生成时记下的位置
                // 第四轮：院子里的（杂草、晾的床单）用白天的光拍（夜里室外只有手电一小圈，看不清）
                // 第五轮：院子的树、灌木、石头（r5_）白天拍一张看形状，再按游戏里的夜 + 手电拍一张
                bool r5 = name.StartsWith("r5_");
                bool outdoor = name.StartsWith("r4_weeds") || name.StartsWith("r4_garden") || r5;
                shots.Add(new Shot("rev_" + name, outdoor ? Mode.Day : Mode.Detail, pos, look) { raw = true });
                if (r5 && !name.StartsWith("r5_gal") && (pos - look).magnitude < 4.6f) shots.Add(new Shot("revmood_" + name, Mode.MoodX, pos, look) { raw = true }); // 雾里 7 m 以外看不见
            }
        }

        /// <summary>
        /// 天空盒会不会改变屋里的光（反射、透过窗看到的天）：3 个室内机位按游戏光各拍“没有天空盒 / 有天空盒”两张，
        /// 记平均亮度和最大的单像素差（文件名 ab_nosky_* / ab_sky_*）。
        /// </summary>
        static void SkyAB()
        {
            var sky = RenderSettings.skybox;
            var views = new[]
            {
                new Shot("ima", Mode.MoodX, new Vector3(12.2f, 2.1f, 7.6f), new Vector3(8.5f, 1.5f, 12.0f)),
                new Shot("zashiki", Mode.MoodX, new Vector3(22.8f, 2.1f, 3.0f), new Vector3(19.5f, 1.2f, 6.8f)),
                new Shot("kura", Mode.MoodX, new Vector3(8.4f, 1.85f, 25.0f), new Vector3(14.5f, 1.0f, 29.8f)),
            };
            var pairs = new List<Shot>();
            foreach (var v in views)
            {
                var a = v; a.name = "ab_nosky_" + v.name;
                var b = v; b.name = "ab_sky_" + v.name;
                pairs.Add(a);
                pairs.Add(b);
            }
            var lum = new Dictionary<string, (float mean, float maxDiff, Color32[] px)>();
            try
            {
                foreach (var s in pairs)
                {
                    RenderSettings.skybox = s.name.StartsWith("ab_sky_") ? sky : null;
                    ShootList(new List<Shot> { s }, (name, px) =>
                    {
                        float sum = 0f;
                        foreach (var c in px) sum += .2126f * c.r + .7152f * c.g + .0722f * c.b;
                        lum[name] = (sum / px.Length / 255f, 0f, px);
                    }, true);
                }
            }
            finally { RenderSettings.skybox = sky; }
            foreach (var v in views)
            {
                if (!lum.TryGetValue("ab_nosky_" + v.name, out var a) || !lum.TryGetValue("ab_sky_" + v.name, out var b)) continue;
                int maxD = 0, changed = 0;
                for (int i = 0; i < a.px.Length && i < b.px.Length; i++)
                {
                    int d = Mathf.Max(Mathf.Abs(a.px[i].r - b.px[i].r), Mathf.Max(Mathf.Abs(a.px[i].g - b.px[i].g), Mathf.Abs(a.px[i].b - b.px[i].b)));
                    if (d > maxD) maxD = d;
                    if (d > 2) changed++;
                }
                Debug.Log($"[Hako Art] 天空盒 A/B {v.name}：平均亮度 {a.mean:F4} → {b.mean:F4}，最大单像素差 {maxD}/255，差 > 2 的像素 {changed}（共 {a.px.Length}）");
            }
        }

        public static void GenerateAndShoot()
        {
            HouseBlockoutGenerator.Generate();
            // 第五轮：这一版布局里放不下的几件（糸車、木马、手压泵……）临时摆到院墙外拍一张，拍完删掉，不存进场景
            var gallery = HouseBlockoutGenerator.BuildReviewGallery();
            // 3D 地图平时是关着的：打开、错开摆好拍几张，拍完关掉（场景已经存过，这些改动不会进场景）
            var map = HouseBlockoutGenerator.ShowMapProxyForReview();
            Shoot();
            if (gallery != null) Object.DestroyImmediate(gallery);
            if (map != null) { map.Explode(0f, 0f); map.Show(false); }
        }

        /// <summary>室内机位和注视点（新坐标）。按房间补家具时机位前方留空，截图不会被贴脸的家具挡住。</summary>
        internal static IEnumerable<(Vector3 pos, Vector3 look)> InteriorCameraSpots()
        {
            foreach (var s in Shots())
                if (s.mode == Mode.Interior || s.mode == Mode.Mood)
                    yield return (HouseBlockoutGenerator.MapOldPoint(s.pos), HouseBlockoutGenerator.MapOldPoint(s.look));
        }

        static IEnumerable<Shot> Shots()
        {
            yield return new Shot("plan_1F", Mode.Plan1F, new Vector3(13.2f, 2.5f, 14.5f), Vector3.zero, 17f);
            yield return new Shot("plan_2F", Mode.Plan2F, new Vector3(12.9f, 5.3f, 13.8f), Vector3.zero, 8.6f);
            yield return new Shot("ext_southwest", Mode.Day, new Vector3(-9f, 10f, -15f), new Vector3(13f, 2f, 9f), 50f);
            yield return new Shot("ext_northeast", Mode.Day, new Vector3(36f, 10f, 34f), new Vector3(13f, 3f, 15f), 50f);
            yield return new Shot("ext_garden_engawa", Mode.Day, new Vector3(17f, 1.6f, -6.2f), new Vector3(16f, 1.8f, 1f), 65f);
            yield return new Shot("ext_gate", Mode.Day, new Vector3(3.86f, 1.65f, -11.4f), new Vector3(3.86f, 2.3f, -6f), 60f);
            yield return new Shot("ext_back_kura", Mode.Day, new Vector3(-3.5f, 2.2f, 32f), new Vector3(8f, 2f, 21f), 60f);
            // 门前（大门内侧看玄関和东边的竹篱笆）、后院（水井边看薪棚和台所后门）
            yield return new Shot("ext_forecourt", Mode.Day, new Vector3(2.12f, 1.17f, -6.72f), new Vector3(6.36f, 1.06f, -.71f), 60f);
            yield return new Shot("ext_backyard", Mode.Day, new Vector3(1.06f, 1.2f, 22.27f), new Vector3(4.6f, .85f, 16.6f), 60f);
            // 隔着縁側、東縁側的玻璃拉门看主庭园
            yield return new Shot("engawa_garden", Mode.Day, new Vector3(12.73f, 1.414f, 1.485f), new Vector3(11.6f, .85f, -4.53f), 65f);
            yield return new Shot("engawaE_garden", Mode.Day, new Vector3(25.31f, 1.414f, 6.36f), new Vector3(31.47f, .71f, 7.42f), 65f);
            yield return new Shot("genkan", Mode.Interior, new Vector3(1.2f, 1.55f, 1.2f), new Vector3(7f, 1.0f, 6f));
            yield return new Shot("genkan_west", Mode.Interior, new Vector3(6.4f, 1.6f, 1.4f), new Vector3(0.4f, 1.0f, 3.6f));
            yield return new Shot("daidokoro", Mode.Interior, new Vector3(6.6f, 1.55f, 8.0f), new Vector3(1.0f, 1.0f, 13.0f));
            yield return new Shot("tsugi", Mode.Interior, new Vector3(12.3f, 2.1f, 2.4f), new Vector3(8.5f, 1.0f, 6.2f));
            yield return new Shot("butsuma", Mode.Interior, new Vector3(14.0f, 2.1f, 2.5f), new Vector3(16.8f, 1.3f, 6.9f));
            yield return new Shot("zashiki", Mode.Interior, new Vector3(23.9f, 2.15f, 4.3f), new Vector3(19.2f, 1.3f, 6.8f));
            yield return new Shot("ima", Mode.Interior, new Vector3(12.2f, 2.1f, 7.6f), new Vector3(8.5f, 1.5f, 12.0f));
            yield return new Shot("nando", Mode.Interior, new Vector3(17.5f, 2.1f, 8.0f), new Vector3(14.6f, 1.0f, 11.6f));
            yield return new Shot("oku", Mode.Interior, new Vector3(18.7f, 2.1f, 9.3f), new Vector3(23.8f, 1.2f, 10.4f));
            yield return new Shot("engawa", Mode.Interior, new Vector3(8.3f, 2.1f, 1.0f), new Vector3(24f, 1.3f, 1.0f));
            yield return new Shot("rouka", Mode.Interior, new Vector3(8.2f, 2.1f, 13.2f), new Vector3(24f, 1.3f, 13.2f));
            yield return new Shot("furo", Mode.Interior, new Vector3(11.0f, 2.1f, 15.2f), new Vector3(16.5f, 1.2f, 19.8f));
            yield return new Shot("furo_east", Mode.Interior, new Vector3(12.0f, 2.0f, 17.0f), new Vector3(17.9f, 1.1f, 17.6f));
            yield return new Shot("benjo", Mode.Interior, new Vector3(9.9f, 2.0f, 15.0f), new Vector3(8.2f, 1.0f, 19.5f));
            yield return new Shot("kura", Mode.Interior, new Vector3(8.4f, 1.85f, 25.0f), new Vector3(14.5f, 1.0f, 29.8f));
            yield return new Shot("shinshitsu", Mode.Interior, new Vector3(12.3f, 4.6f, 11.7f), new Vector3(6.0f, 3.6f, 8.0f));
            yield return new Shot("washitsu2F", Mode.Interior, new Vector3(13.6f, 4.6f, 10.7f), new Vector3(20.3f, 3.9f, 9.6f));
            yield return new Shot("kodomo", Mode.Interior, new Vector3(12.2f, 4.6f, 18.1f), new Vector3(16.6f, 3.6f, 14.7f));
            yield return new Shot("monooki", Mode.Interior, new Vector3(8.2f, 4.6f, 18.2f), new Vector3(11.4f, 3.8f, 15.0f));
            yield return new Shot("mood_zashiki", Mode.Mood, new Vector3(22.8f, 2.1f, 3.0f), new Vector3(19.5f, 1.2f, 6.8f));
            yield return new Shot("mood_rouka", Mode.Mood, new Vector3(24.0f, 2.1f, 13.2f), new Vector3(8f, 1.3f, 13.2f));
            // 门（2026-11-19）：钉死的旧门四面、居間的板戸和茶箪笥、座敷的箪笥（新坐标）
            yield return new Shot("sealed_genkan", Mode.Detail, new Vector3(7.6f, 1.6f, 2.9f), new Vector3(10.84f, 1.25f, 1.36f));
            yield return new Shot("sealed_butsuma", Mode.Detail, new Vector3(21.9f, 2.05f, 6.4f), new Vector3(20f, 1.75f, 9.93f));
            yield return new Shot("sealed_oku", Mode.Detail, new Vector3(29.2f, 2.05f, 13f), new Vector3(25.56f, 1.75f, 13.65f));
            yield return new Shot("sealed_nando", Mode.Detail, new Vector3(21.6f, 2.05f, 15.6f), new Vector3(25.4f, 1.35f, 13.65f));
            yield return new Shot("doors_ima", Mode.Detail, new Vector3(16.5f, 2.05f, 15.5f), new Vector3(11f, 1.2f, 12f));
            yield return new Shot("doors_zashiki", Mode.Detail, new Vector3(30.5f, 2.05f, 8.8f), new Vector3(26f, 1.2f, 4.5f));
            // 第三轮：便所、風呂隔成小间以后（新坐标）
            yield return new Shot("wet_benjo_front", Mode.Detail, new Vector3(13.95f, 2.0f, 20.35f), new Vector3(11.3f, 1.55f, 23.3f));
            yield return new Shot("wet_benjo_back", Mode.Detail, new Vector3(12.74f, 2.0f, 24.25f), new Vector3(13.4f, 1.0f, 28.9f));
            yield return new Shot("wet_stall", Mode.Detail, new Vector3(12.55f, 2.0f, 26.2f), new Vector3(13.95f, .75f, 28.55f));
            // 便所隔间的铰链门（第三轮）：开着（默认）、关上
            yield return new Shot("rev_stall_open", Mode.Detail, new Vector3(12.35f, 1.95f, 25.05f), new Vector3(13.6f, 1.2f, 27.2f));
            yield return new Shot("rev_stall_closed", Mode.Detail, new Vector3(12.35f, 1.95f, 25.05f), new Vector3(13.6f, 1.2f, 27.2f)) { closeDoor = "Door_Benjo_Stall" };
            yield return new Shot("wet_senmen", Mode.Detail, new Vector3(16.65f, 2.0f, 24.2f), new Vector3(14.7f, 1.6f, 21.7f));
            yield return new Shot("wet_datsui", Mode.Detail, new Vector3(18.0f, 2.0f, 20.6f), new Vector3(23.8f, 1.2f, 25.9f));
            yield return new Shot("moodxw_benjo", Mode.MoodX, new Vector3(13.95f, 2.0f, 20.35f), new Vector3(11.3f, 1.55f, 23.3f)) { raw = true };
            yield return new Shot("moodxw_senmen", Mode.MoodX, new Vector3(16.65f, 2.0f, 24.2f), new Vector3(14.7f, 1.6f, 21.7f)) { raw = true };
            // 第三轮审图：容器（金具、按米铺的木纹）、古画屏风和挂轴、床の間的模型、红书包（新坐标）
            yield return new Shot("rev_box_kura", Mode.Detail, new Vector3(16.9f, 1.85f, 40.4f), new Vector3(16.9f, .7f, 43.0f));
            yield return new Shot("rev_box_wardrobe", Mode.Detail, new Vector3(19.6f, 1.85f, 37.6f), new Vector3(21.0f, 1.1f, 35.2f));
            yield return new Shot("rev_box_oshiire", Mode.Detail, new Vector3(27.5f, 2.0f, 13.3f), new Vector3(27.5f, 1.3f, 10.9f));
            yield return new Shot("rev_box_daidokoro", Mode.Detail, new Vector3(2.8f, 1.5f, 13.4f), new Vector3(1.0f, .7f, 11.6f));
            yield return new Shot("rev_box_genkan", Mode.Detail, new Vector3(2.9f, 1.5f, 2.9f), new Vector3(1.0f, .7f, .9f));
            yield return new Shot("rev_byobu_butsuma", Mode.Detail, new Vector3(21.0f, 1.9f, 7.0f), new Vector3(18.6f, 1.4f, 7.0f));
            yield return new Shot("rev_byobu_tsugi", Mode.Detail, new Vector3(17.19f, 1.9f, 7.2f), new Vector3(17.19f, 1.3f, 9.74f));
            yield return new Shot("rev_toko_zashiki", Mode.Detail, new Vector3(27.0f, 1.9f, 7.3f), new Vector3(27.0f, 1.3f, 9.8f));
            yield return new Shot("rev_toko_2f", Mode.Detail, new Vector3(26.3f, 5.5f, 15.0f), new Vector3(28.8f, 4.8f, 15.25f));
            yield return new Shot("rev_randoseru", Mode.Detail, new Vector3(21.4f, 5.4f, 22.0f), new Vector3(23.0f, 4.3f, 20.4f));
            yield return new Shot("rev_ikou", Mode.Detail, new Vector3(12.73f, 5.6f, 12.4f), new Vector3(12.73f, 5.1f, 15.24f));
            // 第三轮废弃感：物置塌顶（从南门进来看、从下面往上看）、便所天花角的蜘蛛网、座敷地上的碎屑
            yield return new Shot("rev_collapse", Mode.Detail, new Vector3(13.7f, 5.9f, 20.5f), new Vector3(15.4f, 5.0f, 22.4f));
            yield return new Shot("rev_collapse_up", Mode.Detail, new Vector3(14.4f, 5.5f, 21.2f), new Vector3(15.45f, 7.2f, 22.3f));
            yield return new Shot("rev_web_benjo", Mode.Detail, new Vector3(13.2f, 2.6f, 22.6f), new Vector3(11.1f, 3.7f, 20.2f));
            yield return new Shot("rev_debris_zashiki", Mode.Detail, new Vector3(30.0f, 2.3f, 4.0f), new Vector3(31.0f, .55f, 7.0f));
            // 第二轮：游戏里的暗度（和同名 Interior 机位一样，只换光）
            yield return new Shot("moodx_genkan", Mode.MoodX, new Vector3(1.2f, 1.55f, 1.2f), new Vector3(7f, 1.0f, 6f));
            yield return new Shot("moodx_daidokoro", Mode.MoodX, new Vector3(6.6f, 1.55f, 8.0f), new Vector3(1.0f, 1.0f, 13.0f));
            yield return new Shot("moodx_ima", Mode.MoodX, new Vector3(12.2f, 2.1f, 7.6f), new Vector3(8.5f, 1.5f, 12.0f));
            yield return new Shot("moodx_nando", Mode.MoodX, new Vector3(17.5f, 2.1f, 8.0f), new Vector3(14.6f, 1.0f, 11.6f));
            yield return new Shot("moodx_kura", Mode.MoodX, new Vector3(8.4f, 1.85f, 25.0f), new Vector3(14.5f, 1.0f, 29.8f));
            yield return new Shot("moodx_kodomo", Mode.MoodX, new Vector3(12.2f, 4.6f, 18.1f), new Vector3(16.6f, 3.6f, 14.7f));
        }

        /// <summary>
        /// 血字特写（第二轮）：场景里找 Writing_* 贴花，从正前方 2 m（被家具、墙挡住就近一点）稍微偏一点拍，
        /// 室内灯光一张看清楚字，游戏光一张看实际效果。
        /// </summary>
        static IEnumerable<Shot> DecalShots()
        {
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!mr.name.StartsWith("Writing_") || !mr.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) continue;
                var normals = mf.sharedMesh.normals;
                if (normals.Length == 0) continue;
                var n = mr.transform.TransformDirection(normals[0]).normalized;
                var c = mr.bounds.center;
                var side = Vector3.Cross(Vector3.up, n).normalized;
                var dir = (n + side * .25f + Vector3.up * .12f).normalized;
                float dist = 2f;
                if (Physics.Raycast(c + n * .05f, dir, out var hit, 2.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    dist = Mathf.Clamp(hit.distance - .3f, .8f, 2f);
                var pos = c + dir * dist;
                string key = mr.name.Substring("Writing_".Length);
                yield return new Shot("decal_" + key, Mode.Detail, pos, c, 55f) { raw = true };
                yield return new Shot("decalmood_" + key, Mode.MoodX, pos, c, 55f) { raw = true };
            }
        }

        [MenuItem("Hako/Art/9 场景截图检查", priority = 90)]
        public static void Shoot()
        {
            var shots = new List<Shot>(Shots());
            shots.AddRange(DecalShots());
            AddRuinShots(shots, false);
            shots.AddRange(OutskirtsShots());
            ShootList(shots);
        }

        /// <summary>箱女视角（GhostSight.cs）的环境光和点光。</summary>
        static readonly Color GhostAmbient = new Color(.66f, .92f, .84f), GhostLightColor = new Color(.67f, 1f, .86f);

        /// <summary>
        /// 按顺序拍一组机位。onPixels：每张拍完把像素交给调用方（天空盒 A/B 用）；
        /// fixedGrain：每张拍之前把随机数种子定死，胶片颗粒每张一样（A/B 比较时颗粒不算差别）。
        /// </summary>
        static void ShootList(List<Shot> shots, System.Action<string, Color32[]> onPixels = null, bool fixedGrain = false)
        {
            Directory.CreateDirectory(OutDir);
            var moonGo = GameObject.Find("Moonlight");
            var moon = moonGo != null ? moonGo.GetComponent<Light>() : null;
            var ambMode = RenderSettings.ambientMode;
            var ambColor = RenderSettings.ambientLight;
            var ambProbe = RenderSettings.ambientProbe;
            bool fog = RenderSettings.fog;
            float moonI = moon ? moon.intensity : 0f;
            var moonC = moon ? moon.color : Color.white;
            var moonS = moon ? moon.shadows : LightShadows.None;

            var camGo = new GameObject("ArtSnapshotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = .05f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var data = cam.GetUniversalAdditionalCameraData();
            var spotGo = new GameObject("ArtSnapshotFlashlight");
            spotGo.transform.SetParent(camGo.transform, false);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 80f;
            spot.innerSpotAngle = 45f;
            spot.range = 30f;
            spot.color = new Color(1f, .93f, .82f);
            spot.shadows = LightShadows.Soft;
            // 第六轮：箱女视角的点光（GhostSight.cs 挂在相机上的那盏）
            var ghostGo = new GameObject("ArtSnapshotGhostLight");
            ghostGo.transform.SetParent(camGo.transform, false);
            var ghostLight = ghostGo.AddComponent<Light>();
            ghostLight.type = LightType.Point;
            ghostLight.color = GhostLightColor;
            ghostLight.range = 20f;
            ghostLight.intensity = 5f;
            ghostLight.enabled = false;
            // 浮尘粒子在编辑器里不会自己播放：先模拟十几秒，截图里才有
            var dust = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var ps in dust) ps.Simulate(14f, true, true, true);
            int count = 0;
            var safeDoorGo = GameObject.Find("House_Blockout/Gameplay/Safe_Kinko/Door");
            var safeDoor = safeDoorGo ? safeDoorGo.transform : null;
            try
            {
                foreach (var s in shots)
                {
                    bool plan = s.mode == Mode.Plan1F || s.mode == Mode.Plan2F;
                    bool ghost = s.mode == Mode.Ghost, aerial = s.mode == Mode.Aerial, dayLight = s.mode == Mode.Day || aerial, sky = UsesSky(s);
                    cam.orthographic = plan;
                    cam.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                    data.renderPostProcessing = !plan && !ghost;
                    // 第四轮：院子里的审图机位（白天 + raw）也开检查光，不然墙根的杂草在背光面一片黑
                    spot.enabled = !ghost && !aerial && (s.mode == Mode.Interior || IsMood(s.mode) || s.mode == Mode.Detail || (s.mode == Mode.Day && s.raw));
                    ghostLight.enabled = ghost;
                    // 游戏光：手电 55° / 内 25°、射程 16 m、强度 25（2026-11-19 用户定的 25，Codex 那边原来是 5，待改）；检查光：大角度补光
                    bool mood = IsMood(s.mode);
                    if (safeDoor) safeDoor.localRotation = Quaternion.Euler(0f, s.name.Contains("safe_open") ? HouseBlockoutGenerator.SafeDoorOpenAngle : 0f, 0f);
                    spot.spotAngle = mood ? 70f : 80f;
                    spot.innerSpotAngle = mood ? 35f : 45f;
                    // 院子的审图机位离得远（3 m 上下）、泥地很暗：检查光加到 14，亮度和屋里 1.5–2 m 拍的差不多
                    spot.intensity = mood ? GameBeamIntensity : s.mode == Mode.Day ? 14f : 3f;
                    spot.color = mood ? new Color(1f, .95f, .84f) : new Color(1f, .93f, .82f);
                    RenderSettings.fog = mood && fog;
                    if (moon)
                    {
                        moon.intensity = dayLight ? 1.5f : plan ? .9f : moonI;
                        moon.color = dayLight || plan ? new Color(1f, .97f, .92f) : moonC;
                        moon.shadows = plan ? LightShadows.None : moonS;
                    }
                    Color amb = dayLight ? new Color(.36f, .38f, .42f)
                        : ghost ? GhostAmbient
                        : plan ? new Color(.6f, .6f, .6f)
                        : s.mode == Mode.Interior || s.mode == Mode.Detail ? new Color(.36f, .34f, .32f) : ambColor;
                    SetAmbient(amb);
                    cam.backgroundColor = s.mode == Mode.Day ? new Color(.45f, .52f, .62f) : new Color(.01f, .012f, .02f);

                    int w = 1600, h = plan ? 1600 : 900;
                    if (plan)
                    {
                        float k = HouseBlockoutGenerator.PlanStretch;
                        cam.orthographicSize = s.fov * k;
                        cam.nearClipPlane = .01f;
                        cam.farClipPlane = 30f;
                        var pp = new Vector3(s.pos.x * k, HouseBlockoutGenerator.PlanCutHeight(s.mode == Mode.Plan2F), s.pos.z * k);
                        camGo.transform.SetPositionAndRotation(pp, Quaternion.Euler(90f, 0f, 0f));
                    }
                    else
                    {
                        cam.fieldOfView = s.fov;
                        cam.nearClipPlane = .05f;
                        cam.farClipPlane = sky ? 1000f : 300f;
                        // 机位按 V3 旧坐标书写：室内跟着房间搬家，室外整体按比例放大
                        Vector3 pos, look;
                        if (s.mode == Mode.Day && !s.raw)
                        {
                            float k = ExteriorScale;
                            pos = s.pos * k; look = s.look * k;
                        }
                        else if (s.mode == Mode.Detail || s.raw) { pos = s.pos; look = s.look; }
                        else { pos = HouseBlockoutGenerator.MapOldPoint(s.pos); look = HouseBlockoutGenerator.MapOldPoint(s.look); }
                        camGo.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                    }
                    HakoOnna.Art.DoorVisual closed = null;
                    if (s.closeDoor != null)
                        foreach (var dv in Object.FindObjectsByType<HakoOnna.Art.DoorVisual>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                            if (dv.name == s.closeDoor) { closed = dv; dv.SetOpenImmediate(false); }
                    var gate = s.openGate ? OpenGate() : null;
                    if (fixedGrain) Random.InitState(6024);
                    try { Render(cam, s.name, w, h, onPixels); }
                    finally
                    {
                        if (closed != null) closed.SetOpenImmediate(closed.StartsOpen);
                        if (gate != null)
                            foreach (var (t, p, r) in gate) { t.localPosition = p; t.localRotation = r; }
                    }
                    count++;
                }
            }
            finally
            {
                if (safeDoor) safeDoor.localRotation = Quaternion.identity;
                RenderSettings.ambientMode = ambMode;
                RenderSettings.ambientLight = ambColor;
                RenderSettings.ambientProbe = ambProbe;
                RenderSettings.fog = fog;
                if (moon)
                {
                    moon.intensity = moonI;
                    moon.color = moonC;
                    moon.shadows = moonS;
                }
                Object.DestroyImmediate(camGo);
                foreach (var ps in dust)
                    if (ps != null) ps.Clear(true);
            }
            Debug.Log($"[Hako Art] 截图 {count} 张 → {OutDir}");
        }

        static void SetAmbient(Color c)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = c;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(c);
            RenderSettings.ambientProbe = sh;
        }

        /// <summary>第六轮：大门两扇门板绕合页往里转 100°（院子那一侧），返回原来的位置好放回去。门板上的细节是子物体，跟着转。</summary>
        static List<(Transform t, Vector3 p, Quaternion r)> OpenGate()
        {
            var saved = new List<(Transform, Vector3, Quaternion)>();
            var gate = GameObject.Find("Gate_Main");
            if (gate == null) return saved;
            foreach (Transform leaf in gate.transform)
            {
                bool left = leaf.name == "GateDoor_L";
                if (!left && leaf.name != "GateDoor_R") continue;
                saved.Add((leaf, leaf.localPosition, leaf.localRotation));
                var r = leaf.GetComponent<Renderer>();
                float half = r != null ? r.bounds.extents.x : .6f;
                var hinge = leaf.position + gate.transform.right * (left ? -half : half);
                leaf.RotateAround(hinge, Vector3.up, left ? -100f : 100f);
            }
            return saved;
        }

        static void Render(Camera cam, string file, int w, int h, System.Action<string, Color32[]> onPixels = null)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            onPixels?.Invoke(file, tex.GetPixels32());
            // 截图正被别的程序打开（看图软件、预览）时写不进去：跳过这一张，别让整次生成以失败退出
            try { File.WriteAllBytes(Path.Combine(OutDir, file + ".png"), tex.EncodeToPNG()); }
            catch (IOException e) { Debug.LogWarning($"[Hako Art] 截图 {file}.png 写不进去（文件被占用？），跳过：{e.Message}"); }
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
