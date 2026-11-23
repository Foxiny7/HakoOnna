using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 生成日式老宅灰盒场景：两层主屋 + 土蔵（仓库）+ 围墙庭院。
    /// 坐标：1 单位 = 1 米，x 向东，z 向北，原点 = 主屋西南角。
    /// 基于 V3 平面（京間）。建筑尺度参照 CS / Apex：平面 ×2、一楼净高 3.4 m、二楼 3.2 m、
    /// 门高 2.2 m、门宽 ×1.25（和家具放大比例一致）；家具、容器、玩家保持真实尺寸（搬家规则见 HouseBlockoutGenerator.Rescale.cs）。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        const string ScenePath = "Assets/_Art/Scenes/House_Blockout.unity";
        const float T = 0.08f;       // 单侧墙厚（相邻房间各建一层，内墙总厚 0.16）
        // 只放大建筑：平面坐标、层高、门窗。家具和容器以实际米数搭建。
        const float PlanScale = 2.0f;
        static Vector3 Plan(float x, float y, float z) => new Vector3(x * PlanScale, y, z * PlanScale);

        const float DoorTop = 2.2f;             // 门楣（鴨居）离地高度
        const float OpeningWidthScale = 1.25f;  // 门窗宽度相对 V3 的倍数（= FurnitureScale）
        const float OpeningTopScale = 1.25f;    // 一楼自定义门顶（绝对高度）相对 V3 的倍数

        // ---------- 楼层 ----------
        struct Level
        {
            public float wallBase, floorTop, wallTop;
            public Level(float b, float f, float t) { wallBase = b; floorTop = f; wallTop = t; }
        }

        const int G0 = 0, G1 = 1, GKura = 2, GFence = 3;
        static readonly Level[] Levels =
        {
            new Level(0f, 0.55f, 3.95f),
            new Level(4.15f, 4.15f, 7.35f),
            new Level(0f, 0.30f, 4.2f),
            new Level(0f, 0f, 3.0f),    // 围墙
        };

        // ---------- 房间与开口 ----------
        class Room
        {
            public string id, label;
            public int group;
            public Rect r;  // x, y(=z), width, depth
            public Rect r0; // V3 旧尺寸下的范围（家具按它搬家）
            public Material floor;
            public bool hasFloor = true;
        }

        class Opening
        {
            public int group;
            public bool alongX; // true: 墙沿 x 方向，位于 z = line；false: 墙沿 z 方向，位于 x = line
            public float line, center, width, sill, top;
            public float line0, center0; // V3 旧尺寸下的世界坐标
            public bool window;
            public bool glass; // 落地玻璃拉门：按窗处理（有碰撞，关着），只是看得见外面
            public DoorKind kind;  // 推拉门的种类；None = 没有门扇的开口（土间连通、楼梯口）
            public int leaves;     // 门扇数（1 / 2 / 4）
        }

        static Room R(string id, string label, int g, float x0, float z0, float x1, float z1, Material floor, bool hasFloor = true) =>
            new Room
            {
                id = id, label = label, group = g, floor = floor, hasFloor = hasFloor,
                r = Rect.MinMaxRect(x0 * PlanScale, z0 * PlanScale, x1 * PlanScale, z1 * PlanScale),
                r0 = Rect.MinMaxRect(x0 * OldPS, z0 * OldPS, x1 * OldPS, z1 * OldPS),
            };

        // 门窗宽度和门顶在这里统一换成新尺度；w 以 V3 的米数书写。
        static float DoorTopFor(float oldTop) =>
            oldTop == OldDoorTop ? DoorTop : oldTop >= 2.85f ? Levels[G0].wallTop : oldTop * OpeningTopScale;

        static Opening DX(int g, float z, float x, float w, float top = OldDoorTop) =>
            new Opening { group = g, alongX = true, line = z, center = x, width = w * OpeningWidthScale, sill = 0f, top = DoorTopFor(top) };

        static Opening DZ(int g, float x, float z, float w, float top = OldDoorTop) =>
            new Opening { group = g, alongX = false, line = x, center = z, width = w * OpeningWidthScale, sill = 0f, top = DoorTopFor(top) };

        /// <summary>整面打通的开口，宽度用平面单位（跟着 PlanScale 走）。</summary>
        static Opening DXP(int g, float z, float x, float wPlan) =>
            new Opening { group = g, alongX = true, line = z, center = x, width = wPlan * PlanScale, sill = 0f, top = DoorTop };

        static Opening DZP(int g, float x, float z, float wPlan) =>
            new Opening { group = g, alongX = false, line = x, center = z, width = wPlan * PlanScale, sill = 0f, top = DoorTop };

        // 窗：窗台不变，窗顶跟门楣一起抬高
        static Opening WX(int g, float z, float x, float w, float sill, float top) =>
            new Opening { group = g, alongX = true, line = z, center = x, width = w * OpeningWidthScale, sill = sill, top = top + DoorTop - OldDoorTop, window = true };

        static Opening WZ(int g, float x, float z, float w, float sill, float top) =>
            new Opening { group = g, alongX = false, line = x, center = z, width = w * OpeningWidthScale, sill = sill, top = top + DoorTop - OldDoorTop, window = true };

        // 朝院子的落地玻璃拉门（ガラス戸）：关着，看得到院子但出不去。窗台 0，窗顶 = 门楣
        static Opening GX(int g, float z, float x, float w) { var o = WX(g, z, x, w, 0f, OldDoorTop); o.glass = true; return o; }
        static Opening GZ(int g, float x, float z, float w) { var o = WZ(g, x, z, w, 0f, OldDoorTop); o.glass = true; return o; }

        // ---------- 容器摆放 ----------
        struct Place
        {
            public string room;
            public ContainerKind kind;
            public float x, z, yaw;
            public Place(string room, ContainerKind kind, float x, float z, float yaw)
            { this.room = room; this.kind = kind; this.x = x; this.z = z; this.yaw = yaw; }
        }

        [MenuItem("Hako/Art/2 生成老宅灰盒场景", priority = 20)]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ContainerPrefabBuilder.BuildAll(false);
            var pal = Palette.Load();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("House_Blockout").transform;
            var structure = ArtKit.Empty(root, "Structure", Vector3.zero);
            var containers = ArtKit.Empty(root, "Containers", Vector3.zero);
            var gameplay = ArtKit.Empty(root, "Gameplay", Vector3.zero);
            var props = ArtKit.Empty(root, "Props", Vector3.zero);
            var lighting = ArtKit.Empty(root, "Lighting", Vector3.zero);

            var rooms = DefineRooms(pal);
            var openings = DefineOpenings();
            foreach (var opening in openings)
            {
                opening.line0 = opening.line * OldPS;
                opening.center0 = opening.center * OldPS;
                opening.line *= PlanScale;
                opening.center *= PlanScale;
            }
            var roomById = new Dictionary<string, Room>();
            foreach (var room in rooms) roomById[room.id] = room;

            foreach (var room in rooms) BuildRoom(structure, room, openings, pal);
            BuildSlabs(structure, rooms, pal);
            BuildStairs(structure, pal);
            var ground = ArtKit.Box(structure, "Ground_Garden", Plan(9.55f, -0.05f, 10f), Plan(40f, 0.1f, 36.5f), pal.Ground);
            // 地面很大，降低光照贴图精度，避免超出图集大小的警告
            var so = new SerializedObject(ground.GetComponent<MeshRenderer>());
            so.FindProperty("m_ScaleInLightmap").floatValue = 0.15f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ArtKit.MarkStaticRecursive(structure.gameObject);

            PlaceContainers(containers, roomById);
            BuildGameplayMarkers(gameplay, roomById, pal);
            BuildProps(props, pal);
            BuildDetails(root, structure, props, rooms, openings, pal);
            AtmosphereBuilder.Build(lighting);
            BuildDust(lighting);
            BuildLandmarkLights(lighting);

            ArtKit.EnsureFolder("Assets/_Art/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Hako Art] 灰盒场景已生成：{ScenePath}");
        }

        // =====================================================================
        // 布局数据
        // =====================================================================
        static List<Room> DefineRooms(Palette p) => new List<Room>
        {
            R("Genkan", "玄関", G0, 0f, 0f, 5.46f, 5.005f, p.Stone),
            R("Daidokoro", "台所", G0, 0f, 5.005f, 5.46f, 10.01f, p.Stone),
            R("Tsugi", "次の間", G0, 5.46f, 1.365f, 9.1f, 5.005f, p.Tatami),
            R("Butsuma", "仏間", G0, 9.1f, 1.365f, 12.74f, 5.005f, p.Tatami),
            R("Zashiki", "座敷", G0, 12.74f, 1.365f, 17.29f, 5.005f, p.Tatami),
            R("Ima", "居間", G0, 5.46f, 5.005f, 9.1f, 8.645f, p.Tatami),
            R("Nando", "納戸", G0, 9.1f, 5.005f, 12.74f, 8.645f, p.WoodFloor),
            R("Oku", "奥の間", G0, 12.74f, 5.005f, 17.29f, 8.645f, p.Tatami),
            R("Engawa", "縁側", G0, 5.46f, 0f, 17.29f, 1.365f, p.WoodFloor),
            R("EngawaE", "東縁側", G0, 17.29f, 0f, 18.655f, 10.01f, p.WoodFloor),
            R("Rouka", "廊下", G0, 5.46f, 8.645f, 17.29f, 10.01f, p.WoodFloor),
            R("UraKaidan", "裏階段", G0, 3.64f, 10.01f, 5.46f, 14.56f, p.Stone),
            R("Benjo", "便所", G0, 5.46f, 10.01f, 7.28f, 14.56f, p.Tile),
            R("Furo", "風呂", G0, 7.28f, 10.01f, 12.74f, 14.56f, p.Tile),
            R("Kaidan", "階段", G0, 12.74f, 10.01f, 14.56f, 14.56f, p.WoodFloor),
            R("Kura", "土蔵", GKura, 5.46f, 17.29f, 10.92f, 21.84f, p.Stone),
            R("Shinshitsu", "寝室", G1, 3.64f, 5.005f, 9.1f, 8.645f, p.Tatami),
            R("Washitsu2F", "二階和室", G1, 9.1f, 5.005f, 14.56f, 8.645f, p.Tatami),
            R("Rouka2F", "二階中廊下", G1, 3.64f, 8.645f, 14.56f, 10.01f, p.WoodFloor),
            R("StairW2F", "裏階段井", G1, 3.64f, 10.01f, 5.46f, 13.195f, null, false),
            R("Monooki", "物置", G1, 5.46f, 10.01f, 8.19f, 13.195f, p.WoodFloor),
            R("Kodomo", "子供部屋", G1, 8.19f, 10.01f, 12.74f, 13.195f, p.Tatami),
            R("StairE2F", "主階段井", G1, 12.74f, 10.01f, 14.56f, 13.195f, null, false),
            R("Rouka2FN", "二階北廊下", G1, 3.64f, 13.195f, 14.56f, 14.56f, p.WoodFloor),
            R("Fence", "塀", GFence, -4.55f, -5.46f, 23.66f, 25.48f, null, false),
        };

        /// <summary>
        /// 门窗。2026-11-19 用户批准：房间开口减到 1~2 个（玄関、台所这种入口 / 土间算极端情况），
        /// 封掉 11 处（玄関↔縁側、台所↔廊下、次の間↔仏間 / 居間、仏間↔納戸、座敷↔東縁側 / 奥の間、
        /// 居間↔納戸、納戸↔奥の間、便所↔風呂、物置↔子供部屋）。其中 3 处做成钉死的旧门（见 Doors.cs 的 Sealed）。
        /// Door(...) 标出门扇种类和扇数：所有带门扇的门都能开关（DoorVisual），默认开着。
        /// </summary>
        static List<Opening> DefineOpenings() => new List<Opening>
        {
            Door(DX(G0, 0f, 2.73f, 1.8f, 2f), DoorKind.Koshi, 2),       // 玄関正门（格子拉门）
            DX(G0, 5.005f, 2.73f, 3.6f, 2.31f),                          // 玄関↔台所：同一片土间，无门
            Door(DZ(G0, 5.46f, 3.185f, 1.8f), DoorKind.Fusuma, 2),      // 玄関↔次の間
            Door(DZ(G0, 5.46f, 6.825f, 1.8f), DoorKind.Itado, 2),       // 台所↔居間
            Door(DX(G0, 10.01f, 1.82f, 0.9f, 2f), DoorKind.Itado, 1),   // 勝手口（去后院）
            DX(G0, 10.01f, 4.54f, 1.4f, 2.85f),                          // 台所↔裏階段：无门
            Door(DZ(G0, 12.74f, 3.185f, 3.4f), DoorKind.Fusuma, 4),     // 仏間↔座敷
            Door(DX(G0, 1.365f, 7.28f, 3.4f), DoorKind.Shoji, 4),       // 次の間↔縁側
            Door(DX(G0, 1.365f, 10.92f, 3.4f), DoorKind.Shoji, 4),      // 仏間↔縁側
            Door(DX(G0, 1.365f, 15f, 4.3f), DoorKind.Shoji, 4),         // 座敷↔縁側
            Door(DZ(G0, 17.29f, 6.825f, 1.8f), DoorKind.Shoji, 2),      // 奥の間↔東縁側
            Door(DX(G0, 8.645f, 7.28f, 0.9f), DoorKind.Fusuma, 1),      // 居間↔廊下
            Door(DX(G0, 8.645f, 10.92f, 0.9f), DoorKind.Itado, 1),      // 納戸↔廊下
            Door(DX(G0, 8.645f, 15f, 0.9f), DoorKind.Fusuma, 1),        // 奥の間↔廊下
            DZ(G0, 17.29f, 9.33f, 1.2f),                                 // 廊下↔東縁側：无门
            DZP(G0, 17.29f, 0.6825f, 1.365f),                            // 縁側转角：无门
            Door(DX(G0, 10.01f, 6.37f, 0.8f), DoorKind.Itado, 1),       // 廊下↔便所
            Door(DX(G0, 10.01f, 10f, 0.9f), DoorKind.Itado, 1),         // 廊下↔風呂
            DX(G0, 10.01f, 13.65f, 1.4f, 2.85f),                         // 廊下↔階段：无门
            // 縁側朝院子的开口全部是关着的玻璃拉门：只有玄関正门和台所后门能到室外
            GX(G0, 0f, 9f, 2.7f),
            GX(G0, 0f, 15f, 2.7f),
            GZ(G0, 18.655f, 4.5f, 2.7f),
            GX(G0, 10.01f, 17.97f, 1.2f),
            Door(DX(GKura, 17.29f, 8.19f, 1.4f), DoorKind.Kura, 2),     // 土蔵正门
            Door(DZ(GKura, 10.92f, 19.6f, 0.9f), DoorKind.Itado, 1),    // 土蔵侧门
            DX(GFence, -5.46f, 2.73f, 2.4f, 2.4f),                       // 围墙大门（玩法：路线二出口，不归这里管）
            Door(DX(G1, 8.645f, 6.37f, 0.9f), DoorKind.Fusuma, 1),      // 寝室↔二階中廊下
            Door(DX(G1, 8.645f, 11.8f, 1.8f), DoorKind.Fusuma, 2),      // 二階和室↔二階中廊下
            Door(DZ(G1, 9.1f, 6.825f, 1.8f), DoorKind.Fusuma, 2),       // 寝室↔二階和室
            Door(DX(G1, 10.01f, 6.8f, 0.9f), DoorKind.Itado, 1),        // 物置↔二階中廊下
            Door(DX(G1, 10.01f, 10.5f, 0.9f), DoorKind.Fusuma, 1),      // 子供部屋↔二階中廊下
            Door(DX(G1, 13.195f, 6.8f, 0.9f), DoorKind.Itado, 1),       // 物置↔二階北廊下
            Door(DX(G1, 13.195f, 10.5f, 0.9f), DoorKind.Fusuma, 1),     // 子供部屋↔二階北廊下
            DXP(G1, 13.195f, 4.55f, 1.82f),
            DXP(G1, 13.195f, 13.65f, 1.82f),
            WZ(G0, 0, 3.5f, 1.0f, .8f, 1.65f),
            WZ(G0, 0, 8f, 1.2f, .8f, 1.65f),
            WX(G0, 0, 12f, 1.2f, .8f, 1.65f),
            WZ(G0, 18.655f, 7.6f, 1.2f, .8f, 1.65f),
            WX(G0, 10.01f, 15.8f, .8f, .8f, 1.65f),
            WX(G0, 14.56f, 6.37f, .65f, .8f, 1.65f),
            WX(G0, 14.56f, 9.3f, 1f, .8f, 1.65f),
            WX(G0, 14.56f, 4.55f, .65f, .8f, 1.65f),
            WX(G0, 14.56f, 13.65f, .65f, .8f, 1.65f),
            // 楼梯井边的扶手墙：下 0.9 m 实墙，上面全开
            new Opening { group = G1, alongX = true, line = 10.01f, center = 4.55f, width = 1.82f * PlanScale, sill = .9f, top = 9f },
            new Opening { group = G1, alongX = true, line = 10.01f, center = 13.65f, width = 1.82f * PlanScale, sill = .9f, top = 9f },
            WX(G1, 5.005f, 7f, 1.6f, .8f, 1.65f),
            WX(G1, 5.005f, 12f, 1.6f, .8f, 1.65f),
            WZ(G1, 14.56f, 9.33f, .75f, .8f, 1.65f),
            WX(G1, 14.56f, 9f, 1.2f, .8f, 1.65f),
            WX(GKura, 21.84f, 8.5f, 1f, .8f, 1.7f),
        };

        static readonly Place[] Placements =
        {
            new Place("Genkan", ContainerKind.GetaBako, 0.265f, 4.29f, 90f),
            new Place("Genkan", ContainerKind.GetaBako, 1.04f, 0.265f, 0f),
            new Place("Genkan", ContainerKind.Tsuzura, 0.34f, 1.04f, 90f),
            new Place("Nando", ContainerKind.Nagamochi, 12.08f, 7.62f, 270f),
            new Place("Nando", ContainerKind.YofukuTansu, 9.43f, 5.57f, 90f),
            new Place("Daidokoro", ContainerKind.ChaTansu, 0.29f, 6.545f, 90f),
            new Place("Daidokoro", ContainerKind.Tsuzura, 2.99f, 9.645f, 180f),
            new Place("Daidokoro", ContainerKind.Komebitsu, 1.89f, 5.63f, 0f),
            new Place("Daidokoro", ContainerKind.Komebitsu, 4.84f, 8.395f, 270f),
            // 西北角贴北墙；旧尺寸离西墙留 0.44 m，放大后推回西墙（离北门留空区的压入量不超过原来）
            new Place("Tsugi", ContainerKind.Nagamochi, 6.43f, 4.42f, 180f),
            new Place("Tsugi", ContainerKind.Tansu, 5.91f, 1.82f, 90f),
            new Place("Butsuma", ContainerKind.ChaTansu, 11.94f, 1.94f, 0f),
            new Place("Butsuma", ContainerKind.Tsuzura, 9.725f, 2.105f, 90f),
            new Place("Zashiki", ContainerKind.Nagamochi, 15.0f, 4.65f, 180f),
            new Place("Zashiki", ContainerKind.Tansu, 16.23f, 1.965f, 0f),
            new Place("Ima", ContainerKind.Tansu, 6.06f, 7.995f, 90f),
            new Place("Ima", ContainerKind.ChaTansu, 5.81f, 5.45f, 90f),
            new Place("Ima", ContainerKind.Tsuzura, 6.6f, 8.37f, 180f),
            new Place("Nando", ContainerKind.Tansu, 12.04f, 5.32f, 0f),
            // 2026-11-22 用户定“一个房间 2–4 个容器”：納戸 5→4、土蔵 6→4，挪给只有 1 个的縁側、風呂、便所（总数还是 45）
            new Place("Benjo", ContainerKind.Tsuzura, 5.80f, 10.5f, 90f),       // 便所前间西南角
            new Place("Nando", ContainerKind.Komebitsu, 11.20f, 5.36f, 0f),
            new Place("Oku", ContainerKind.Oshiire, 14.015f, 5.52f, 0f),
            new Place("Oku", ContainerKind.YofukuTansu, 13.58f, 8.245f, 180f),
            new Place("Oku", ContainerKind.Tansu, 16.53f, 8.32f, 180f),
            new Place("Engawa", ContainerKind.Tsuzura, 11.85f, 0.34f, 0f),
            new Place("Benjo", ContainerKind.GetaBako, 5.725f, 13.9f, 90f),
            new Place("Furo", ContainerKind.GetaBako, 7.545f, 13.85f, 90f),
            new Place("Kura", ContainerKind.Nagamochi, 5.95f, 18.18f, 90f),
            new Place("Engawa", ContainerKind.Nagamochi, 15.9f, 0.36f, 0f),     // 縁側东段靠南墙
            new Place("Kura", ContainerKind.Nagamochi, 7.75f, 21.33f, 180f),
            new Place("Kura", ContainerKind.YofukuTansu, 10.15f, 17.68f, 0f),
            new Place("Kura", ContainerKind.Tansu, 9.1f, 21.505f, 180f),
            new Place("Furo", ContainerKind.Tsuzura, 12.40f, 11.9f, 270f),      // 脱衣所东墙（长凳和洗衣机之间）
            new Place("Shinshitsu", ContainerKind.Oshiire, 6.18f, 5.52f, 0f),
            new Place("Shinshitsu", ContainerKind.YofukuTansu, 4.43f, 5.395f, 0f),
            new Place("Shinshitsu", ContainerKind.Tansu, 8.0f, 8.32f, 180f),    // 原来三个都在南墙一条线上，这个挪到北墙（门东边；西墙是床）
            new Place("Washitsu2F", ContainerKind.Oshiire, 11.09f, 5.52f, 0f),
            new Place("Washitsu2F", ContainerKind.Nagamochi, 13.90f, 6.05f, 270f), // 同上，挪到东墙
            new Place("Washitsu2F", ContainerKind.ChaTansu, 9.69f, 5.295f, 0f),
            new Place("Monooki", ContainerKind.Nagamochi, 5.87f, 12.0f, 90f),
            new Place("Monooki", ContainerKind.Tsuzura, 5.8f, 10.75f, 90f),
            new Place("Monooki", ContainerKind.Komebitsu, 7.85f, 12.75f, 270f),
            new Place("Kodomo", ContainerKind.Oshiire, 12.205f, 11.6f, 270f),
            new Place("Kodomo", ContainerKind.Tansu, 8.83f, 10.325f, 0f),
            new Place("Kodomo", ContainerKind.Tsuzura, 8.53f, 12.75f, 90f),
        };

        // 构建
        // =====================================================================
        static void BuildRoom(Transform parent, Room room, List<Opening> openings, Palette pal)
        {
            var lv = Levels[room.group];
            lv.floorTop = FloorHeight(room);
            var t = ArtKit.Empty(parent, $"Room_{room.id}_{room.label}", Vector3.zero);
            var r = room.r;
            var wallMat = WallMatFor(room, pal);

            if (room.hasFloor && room.floor != null)
            {
                float depth = room.group == G1 ? .012f : Mathf.Max(.1f, lv.floorTop);
                ArtKit.Box(t, "Floor", new Vector3(r.center.x, lv.floorTop - depth / 2 + (room.group == G1 ? .003f : 0), r.center.y),
                    new Vector3(r.width, depth, r.height), room.floor, room.group != G1);
                if (room.group == G0 && lv.floorTop > 0)
                    ArtKit.Box(t, "Foundation", new Vector3(r.center.x, .26f, r.center.y),
                        new Vector3(r.width + .002f, .52f, r.height + .002f), pal.WoodDarkB);
            }

            if (room.id.StartsWith("Stair"))
            {
                bool west = room.id == "StairW2F";
                BuildEdge(t, room, lv, false, west ? r.xMin : r.xMax, r.yMin, r.yMax,
                    west ? 1f : -1f, wallMat, openings, pal);
                return;
            }
            BuildEdge(t, room, lv, true, r.yMin, r.xMin, r.xMax, +1f, wallMat, openings, pal);
            BuildEdge(t, room, lv, true, r.yMax, r.xMin, r.xMax, -1f, wallMat, openings, pal);
            BuildEdge(t, room, lv, false, r.xMin, r.yMin + T, r.yMax - T, +1f, wallMat, openings, pal);
            BuildEdge(t, room, lv, false, r.xMax, r.yMin + T, r.yMax - T, -1f, wallMat, openings, pal);
        }

        /// <summary>
        /// 按房间选墙面：土壁（和室、玄関、縁側）、发黄的漆喰（走廊、楼梯、納戸、物置、浴室厕所）、
        /// 熏黑的台所、土蔵的白漆喰。每个房间的墙在自己这一侧，所以隔墙两面可以不同。
        /// </summary>
        static Material WallMatFor(Room room, Palette pal)
        {
            if (room.group == GFence) return pal.Fence;
            if (room.group == GKura) return pal.WallKura;
            switch (room.id)
            {
                case "Daidokoro":
                    return pal.WallSoot;
                case "Rouka": case "Rouka2F": case "Rouka2FN": case "Kaidan": case "UraKaidan":
                case "StairW2F": case "StairE2F": case "Nando": case "Monooki": case "Furo": case "Benjo":
                    return pal.WallPlaster;
                default:
                    return pal.Plaster;
            }
        }

        /// <summary>沿房间一条边建墙，在门窗处留洞。墙体向房间内侧偏移半个厚度。</summary>
        static void BuildEdge(Transform parent, Room room, Level lv, bool alongX, float line, float from, float to,
            float inward, Material mat, List<Opening> openings, Palette pal)
        {
            float wallLine = line + inward * T / 2f;
            var ops = new List<Opening>();
            foreach (var o in openings)
            {
                if (o.group != room.group || o.alongX != alongX || Mathf.Abs(o.line - line) > 0.01f) continue;
                if (o.center + o.width / 2f <= from || o.center - o.width / 2f >= to) continue;
                ops.Add(o);
            }
            ops.Sort((a, b) => a.center.CompareTo(b.center));

            float cursor = from;
            foreach (var o in ops)
            {
                float a = Mathf.Max(from, o.center - o.width / 2f);
                float b = Mathf.Min(to, o.center + o.width / 2f);
                if (a > cursor + 0.001f) WallPiece(parent, "Wall", alongX, wallLine, cursor, a, lv.wallBase, lv.wallTop, mat);
                float bottom = o.window || o.sill > 0 ? lv.floorTop + o.sill : lv.wallBase;
                float top = Mathf.Min(lv.wallTop, lv.floorTop + o.top);
                if (!o.window && room.group == G0)
                    top = o.top == DoorTop ? .55f + DoorTop : o.top;
                if (bottom > lv.wallBase + 0.001f) WallPiece(parent, "Wall_Below", alongX, wallLine, a, b, lv.wallBase, bottom, mat);
                if (top < lv.wallTop - 0.001f) WallPiece(parent, "Wall_Lintel", alongX, wallLine, a, b, top, lv.wallTop, mat);
                if (o.glass)
                {
                    // 玻璃拉门：关着，只留一层碰撞挡住玩家（门扇和玻璃在 BuildDetails 里做）
                    var pane = WallPiece(parent, "Window_Glass", alongX, wallLine, a, b, bottom, top, mat);
                    var s = pane.transform.localScale;
                    pane.transform.localScale = alongX ? new Vector3(s.x, s.y, 0.04f) : new Vector3(0.04f, s.y, s.z);
                    Object.DestroyImmediate(pane.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(pane.GetComponent<MeshFilter>());
                }
                else if (o.window)
                {
                    // 障子窗：可透月光（不投射阴影），但有碰撞，玩家不能翻出去
                    var shoji = WallPiece(parent, "Window_Shoji", alongX, wallLine, a, b, bottom, top, pal.Paper);
                    var s = shoji.transform.localScale;
                    shoji.transform.localScale = alongX ? new Vector3(s.x, s.y, 0.02f) : new Vector3(0.02f, s.y, s.z);
                    float face = wallLine + inward * .015f;
                    WallPiece(parent, "WindowRail", alongX, face, a, b, (bottom + top) / 2, (bottom + top) / 2 + .025f, pal.WoodMidA);
                    for (float mid = a + .27f; mid < b - .04f; mid += .27f)
                        WallPiece(parent, "WindowLattice", alongX, face, mid, mid + .024f, bottom, top, pal.WoodMidA);
                    shoji.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                if (!o.window && o.sill == 0 && top + .075f < lv.wallTop && room.group != GFence)
                {
                    // 门扇是能开关的独立物体（Doors.cs），这里只做门框：鴨居 + 两侧门柱
                    float face = wallLine + inward * .025f;
                    WallPiece(parent, "Kamoi", alongX, face, a, b, top, top + .075f, pal.WoodDarkA);
                    if (a - from > .06f)
                        WallPiece(parent, "DoorPost", alongX, face, a - .055f, a, lv.floorTop, top, pal.WoodMidA);
                    if (to - b > .06f)
                        WallPiece(parent, "DoorPost", alongX, face, b, b + .055f, lv.floorTop, top, pal.WoodMidA);
                }
                cursor = Mathf.Max(cursor, b);
            }
            if (to > cursor + 0.001f) WallPiece(parent, "Wall", alongX, wallLine, cursor, to, lv.wallBase, lv.wallTop, mat);
        }

        static GameObject WallPiece(Transform parent, string name, bool alongX, float wallLine, float a, float b, float y0, float y1, Material mat)
        {
            float mid = (a + b) / 2f, len = b - a, h = y1 - y0, ym = (y0 + y1) / 2f;
            return alongX
                ? ArtKit.Box(parent, name, new Vector3(mid, ym, wallLine), new Vector3(len, h, T), mat)
                : ArtKit.Box(parent, name, new Vector3(wallLine, ym, mid), new Vector3(T, h, len), mat);
        }

        static float FloorHeight(Room room) =>
            room.id == "Genkan" || room.id == "Daidokoro" || room.id == "UraKaidan" ? 0 : Levels[room.group].floorTop;

        static void BuildSlabs(Transform parent, List<Room> rooms, Palette pal)
        {
            var t = ArtKit.Empty(parent, "Slabs", Vector3.zero);
            // 一层天花板 / 二层楼板（留出两个楼梯井）
            foreach (var room in rooms)
            {
                if (room.group != G0 && room.group != GKura) continue;
                var r = room.r;
                if (room.id == "Kaidan" || room.id == "UraKaidan") r.yMin = 13.195f * PlanScale;
                bool kura = room.group == GKura;
                ArtKit.Box(t, kura ? "Roof_Kura" : "Slab_1F", new Vector3(r.center.x, Levels[room.group].wallTop + .1f, r.center.y),
                    new Vector3(r.width, .2f, r.height), kura ? pal.Roof : pal.Ceiling);
            }
            ArtKit.Box(t, "Roof_2F", Plan(9.1f, Levels[G1].wallTop + .1f, 9.7825f), new Vector3(10.92f * PlanScale, .2f, 9.555f * PlanScale), pal.Roof);
        }

        static void BuildStairs(Transform parent, Palette pal)
        {
            var t = ArtKit.Empty(parent, "Stairs", Vector3.zero);
            // 踏步高约 0.2 m
            BuildFlight(t, "Stair_Main", 13.65f, .55f, 18, pal.WoodDarkA);
            BuildFlight(t, "Stair_Kitchen", 4.54f, 0, 21, pal.WoodDarkB);
            const float W = OpeningWidthScale;
            foreach (float z in new[] { 3.185f, 6.825f })
                Threshold(t, Plan(5.46f,.55f,z), Vector3.right, 1.8f * W, pal.WoodMidA);
            // 玄関↔縁側的门已经钉死：只剩当年的式台踏板（没有坡道，走不上去）
            ThresholdStepOnly(t, Plan(5.46f,.55f,.68f), Vector3.right, .9f * W, pal.WoodMidA);
            // 縁側的玻璃拉门关着：门外只放一块沓脱石（不再有坡道），西边那块上面摆着下駄
            ShoeStone(t, Plan(9f,.55f,0), Vector3.forward, .4f, pal.Stone);
            ShoeStone(t, Plan(15f,.55f,0), Vector3.forward, -.2f, pal.Stone);
            ShoeStone(t, Plan(18.655f,.55f,4.5f), Vector3.left, .1f, pal.Stone);
            ShoeStone(t, Plan(17.97f,.55f,10.01f), Vector3.back, 0f, pal.Stone);
            Threshold(t, Plan(8.19f,.3f,17.29f), Vector3.forward, 1.4f * W, pal.Stone);
            Threshold(t, Plan(10.92f,.3f,19.6f), Vector3.left, .9f * W, pal.Stone);
        }

        static void Threshold(Transform parent, Vector3 end, Vector3 direction, float width, Material mat)
        {
            Vector3 start = end - direction * .8f;
            start.y = 0;
            var step = ArtKit.Box(parent, "ThresholdStep", (start + end) / 2 - Vector3.up * end.y / 4,
                new Vector3(width, end.y / 2, .78f), mat, false);
            step.transform.rotation = Quaternion.LookRotation(direction);
            Ramp(parent, start, end, width);
        }

        /// <summary>只有踏板、没有坡道碰撞（门洞已封，踏板只是遗留的样子）。</summary>
        static void ThresholdStepOnly(Transform parent, Vector3 end, Vector3 direction, float width, Material mat)
        {
            Vector3 start = end - direction * .8f;
            start.y = 0;
            var step = ArtKit.Box(parent, "ThresholdStep_Sealed", (start + end) / 2 - Vector3.up * end.y / 4,
                new Vector3(width, end.y / 2, .78f), mat, false);
            step.transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// 关着的玻璃拉门外的沓脱石：一块顶面 0.275 m 的自然石，沿墙方向可以偏一点。
        /// 门不能走，所以不加坡道。inward 和 Threshold 一样指向屋内。
        /// </summary>
        static void ShoeStone(Transform parent, Vector3 door, Vector3 inward, float shift, Material mat)
        {
            var side = Vector3.Cross(Vector3.up, inward);
            var c = door - inward * .48f + side * shift;
            c.y = 0;
            var rot = Quaternion.LookRotation(inward) * Quaternion.Euler(0, 4f, 0);
            var top = ArtKit.Box(parent, "ShoeStone", c + Vector3.up * .2f, new Vector3(1.3f, .15f, .62f), mat);
            top.transform.rotation = rot;
            var body = ArtKit.Box(parent, "ShoeStone", c + Vector3.up * .07f, new Vector3(1.18f, .14f, .54f), mat);
            body.transform.rotation = rot * Quaternion.Euler(0, -7f, 0);
        }

        static void BuildFlight(Transform parent, string name, float x, float floor, int count, Material mat)
        {
            var t = ArtKit.Empty(parent, name, Vector3.zero);
            x *= PlanScale;
            float tread = 3.185f * PlanScale / count;
            float top = Levels[G1].floorTop;
            for (int i = 0; i < count; i++)
            {
                float h = (top - floor) * (i + 1) / count;
                float z = 10.01f * PlanScale + (i + .5f) * tread;
                // 2026-11-22：踏步铺满楼梯间两面墙之间（原来 2.4 m 宽，两边各留 0.4 m 的深缝，另有两条带碰撞的侧板）。
                // 这些方块只当“芯”，比外形缩进 1.5 cm，藏在 Stairs.cs 画的踏板、踢板后面；程序按它们的范围判断脚下是木头。
                ArtKit.Box(t, $"Step_{i:00}", new Vector3(x, floor + (h - .015f) / 2, z + .0075f),
                    new Vector3(StairClearWidth - .02f, h - .015f, tread - .015f), mat, false);
            }
            Ramp(t, new Vector3(x,floor,10.01f * PlanScale), new Vector3(x,top,13.195f * PlanScale), StairClearWidth);
        }

        static void Ramp(Transform parent, Vector3 start, Vector3 end, float width)
        {
            var ramp = ArtKit.Empty(parent, "WalkRamp", (start + end) / 2);
            ramp.rotation = Quaternion.LookRotation(end - start);
            var box = ramp.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(width,.06f,Vector3.Distance(start,end));
            box.center = Vector3.down * .03f;
        }

        static void PlaceContainers(Transform parent, Dictionary<string, Room> rooms)
        {
            var rng = new System.Random(4521);
            var counter = new Dictionary<string, int>();
            foreach (var p in Placements)
            {
                var room = rooms[p.room];
                string variant = ContainerPrefabBuilder.Variants[rng.Next(ContainerPrefabBuilder.Variants.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContainerPrefabBuilder.PathFor(p.kind, variant));
                // 先按 V3 旧尺寸摆，BuildDetails 里再整体搬到新尺寸（贴墙的保持离墙距离）
                var pos = OldPlan(p.x, OldFloorHeight(room), p.z);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                counter.TryGetValue(p.room, out int n);
                counter[p.room] = n + 1;
                go.name = $"{p.room}_{p.kind}_{n + 1:00}";
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, p.yaw, 0f));
            }
        }

        static void BuildGameplayMarkers(Transform parent, Dictionary<string, Room> rooms, Palette pal)
        {
            // 探索者出生点（玄関）
            var spawns = ArtKit.Empty(parent, "ExplorerSpawnPoints", Vector3.zero);
            // 出生点、保险箱、仏坛按旧尺寸摆，之后和家具一起搬家（见 Rescale）
            Vector3[] sp = { OldPlan(1.4f,0,1.6f), OldPlan(2.7f,0,1.6f), OldPlan(4f,0,1.6f), OldPlan(2f,0,3.2f), OldPlan(3.4f,0,3.2f) };
            for (int i = 0; i < sp.Length; i++)
            {
                var s = ArtKit.Empty(spawns, $"SpawnPoint_{i}", sp[i]);
                s.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            // 秘密出口候选点：地毯 + HatchAnchor。暗门只在被发现时由程序生成。
            var candidates = ArtKit.Empty(parent, "SecretExitCandidates", Vector3.zero);
            // 各房间中央：居間、座敷、納戸、土蔵
            string[] cn = { "Ima", "Zashiki", "Nando", "Kura" };
            for (int i = 0; i < cn.Length; i++)
            {
                var room = rooms[cn[i]];
                var center = room.r.center;
                var c = ArtKit.Empty(candidates, $"SecretExitCandidate_{i}_{cn[i]}", new Vector3(center.x, FloorHeight(room), center.y));
                ArtKit.Box(c, "Carpet", new Vector3(0f, 0.006f, 0f), new Vector3(2.0f, 0.012f, 1.5f), pal.Carpet, false);
                ArtKit.Empty(c, "HatchAnchor", Vector3.zero);
            }

            // 保险箱（居間北墙）
            var safe = ArtKit.Empty(parent, "Safe_Kinko", OldPlan(8.5f, .55f, 8.645f) - Vector3.forward * (T + .275f));
            safe.rotation = Quaternion.Euler(0f, 180f, 0f);
            ArtKit.Box(safe, "Body", new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.55f), pal.Metal);
            ArtKit.Cylinder(safe, "Dial", new Vector3(0f, 0.5f, 0.285f), new Vector3(0.12f, 0.01f, 0.12f), Quaternion.Euler(90f, 0f, 0f), pal.Rust);
            ArtKit.Empty(safe, "InteractPoint", new Vector3(0f, 0f, 0.9f));
            ArtKit.Empty(safe, "KeyAnchor", new Vector3(0f, 0.3f, 0f));

            // 超度地点：仏壇（仏間东墙）
            var memorial = ArtKit.Empty(parent, "MemorialPoint_Butsudan", OldPlan(11.9f, .55f, 5.005f) - Vector3.forward * (T + .3f));
            memorial.rotation = Quaternion.Euler(0f, 180f, 0f);
            ArtKit.Box(memorial, "Butsudan", new Vector3(0f, 0.8f, 0f), new Vector3(1.2f, 1.6f, 0.6f), pal.Lacquer);
            ArtKit.Box(memorial, "Altar", new Vector3(0f, 0.45f, 0.45f), new Vector3(1.0f, 0.05f, 0.3f), pal.WoodDarkA);
            ArtKit.Empty(memorial, "InteractPoint", new Vector3(0f, 0f, 1.0f));

            // 大门（路线二）：围墙南侧，锁着的两扇门板
            var gate = ArtKit.Empty(parent, "Gate_Main", Plan(2.73f, 0f, -5.46f) + Vector3.forward * (T / 2f));
            float leafW = 1.2f * OpeningWidthScale, leafH = Levels[GFence].wallTop;
            ArtKit.Box(gate, "GateDoor_L", new Vector3(-leafW / 2, leafH / 2, 0f), new Vector3(leafW, leafH, 0.1f), pal.WoodDarkA);
            ArtKit.Box(gate, "GateDoor_R", new Vector3(leafW / 2, leafH / 2, 0f), new Vector3(leafW, leafH, 0.1f), pal.WoodDarkA);
            ArtKit.Empty(gate, "InteractPoint", new Vector3(0f, 0f, 0.8f));
            ArtKit.Empty(parent, "EscapeZone_Gate", Plan(2.73f, 0f, -5.46f) - Vector3.forward * 1.5f);
        }

        static void BuildProps(Transform parent, Palette p)
        {
            // Fixtures have one solid footprint. Legs, bedding and tableware stay visual.
            var bench = Table(parent, "Genkan_ShoeBench", new Vector3(6.1f, 0, .48f), 1.25f, .43f, .42f, p);
            Part(bench, "SeatPad", 0,.46f,0, .82f,.06f,.35f, p.WickerA);

            var kitchen = Fixture(parent, "Daidokoro_KamadoAndSink", new Vector3(.5f,0,11.55f), 3.2f,.86f,.74f,90);
            Part(kitchen, "Base", 0,.39f,0, 3.14f,.78f,.7f,p.WoodMidB);
            Part(kitchen, "Worktop", 0,.82f,0, 3.2f,.08f,.74f,p.Stone);
            Part(kitchen, "Firebox", -1.02f,.36f,.356f, .59f,.38f,.018f,p.Interior);
            Round(kitchen,"CookingPot",-1.02f,.98f,0,.46f,.24f,p.Metal);
            Round(kitchen,"PotLid",-1.02f,1.12f,0,.48f,.04f,p.WoodDarkA);
            Part(kitchen, "SinkRim", .98f,.876f,0, .84f,.032f,.54f,p.Metal);
            Part(kitchen, "SinkBasin", .98f,.897f,0, .70f,.012f,.41f,p.Interior);
            Part(kitchen, "Tap", .98f,1.05f,-.29f,.04f,.39f,.04f,p.Metal);
            Part(kitchen, "Spout", .98f,1.23f,-.20f,.04f,.04f,.21f,p.Metal);
            Part(kitchen, "ChoppingBoard", -.02f,.885f,0,.46f,.04f,.28f,p.WoodLightA);
            var dining = Table(parent,"Daidokoro_DiningTable",new Vector3(4.4f,0,11.35f),1.45f,.72f,.8f,p);
            Round(dining,"ServingBowl",.38f,.79f,0,.20f,.13f,p.Lacquer);
            Round(dining,"RiceBowl",-.40f,.765f,-.12f,.14f,.09f,p.Paper);
            Chair(parent,"DiningChair_S",new Vector3(4.4f,0,10.65f),180,p);
            Chair(parent,"DiningChair_N",new Vector3(4.4f,0,12.05f),0,p);

            var tea = Table(parent,"Tsugi_TeaTable",new Vector3(11.339f,.55f,5.738f),.86f,.31f,.56f,p);
            Round(tea,"Teapot",0,.405f,0,.20f,.19f,p.Lacquer);
            Round(tea,"Cup",.28f,.35f,.06f,.09f,.08f,p.Paper);
            var hearth = Table(parent,"Ima_IroriTable",new Vector3(11.65f,.55f,8.12f),1.4f,.34f,.83f,p);
            Part(hearth,"HearthLining",0,.35f,0,.79f,.02f,.52f,p.Interior);
            Round(hearth,"IronKettle",0,.49f,0,.27f,.26f,p.Metal);
            Cushion(parent,"Ima_Zabuton",new Vector3(11.64f,.55f,8.92f),p.Carpet);
            Cushion(parent,"Butsuma_PrayerCushion",new Vector3(16.82f,.55f,5.86f),p.WickerB);

            var alcove = Fixture(parent,"Zashiki_Tokonoma",new Vector3(19.45f,.55f,6.74f),1.88f,.17f,.42f);
            Part(alcove,"DisplayPlatform",0,.085f,0,1.88f,.17f,.42f,p.WoodDarkA);
            Round(alcove,"Vase",-.52f,.35f,0,.18f,.36f,p.Lacquer);
            Part(alcove,"HangingScroll",.23f,1.25f,.23f,.5f,1.10f,.025f,p.FusumaA);
            Part(alcove,"InkPanel",.23f,1.26f,.21f,.28f,.60f,.014f,p.WickerB);
            Bed(parent,"Oku_GuestFuton",new Vector3(22.56f,.55f,10.89f),1.02f,90,p);
            Bed(parent,"Shinshitsu_MainBed",new Vector3(6.37f,3.05f,10.45f),1.50f,0,p);
            Table(parent,"Shinshitsu_Bedside",new Vector3(7.62f,3.05f,11.1f),.46f,.45f,.46f,p);
            Bed(parent,"Kodomo_SingleBed",new Vector3(16.61f,3.05f,17.95f),.92f,90,p);
            var desk = Table(parent,"Kodomo_StudyDesk",new Vector3(16.62f,3.05f,14.72f),1.28f,.72f,.59f,p);
            Part(desk,"OpenNotebook",0,.737f,0,.31f,.026f,.21f,p.Paper);
            Part(desk,"Book",.40f,.76f,-.09f,.22f,.07f,.26f,p.Carpet);
            Chair(parent,"Kodomo_DeskChair",new Vector3(16.62f,3.05f,15.34f),0,p);
            var guest = Table(parent,"Washitsu2F_WritingTable",new Vector3(19.38f,3.05f,10.95f),1.12f,.35f,.62f,p);
            Part(guest,"LetterPaper",0,.365f,0,.31f,.02f,.22f,p.Paper);
            Cushion(parent,"Washitsu2F_Zabuton",new Vector3(19.38f,3.05f,10.22f),p.WickerA);

            var tub = Fixture(parent,"Furo_HinokiTub",new Vector3(16.55f,.55f,19.99f),1.68f,.66f,.98f);
            Part(tub,"TubBody",0,.31f,0,1.68f,.62f,.98f,p.WoodMidA);
            Part(tub,"Inside",0,.622f,0,1.49f,.02f,.80f,p.Interior);
            foreach(float side in new[]{-1f,1f})
            {
                Part(tub,"LongRim",0,.645f,side*.455f,1.68f,.05f,.07f,p.WoodLightA);
                Part(tub,"EndRim",side*.805f,.645f,0,.07f,.05f,.85f,p.WoodLightA);
            }
            Table(parent,"Furo_WashingStool",new Vector3(15.17f,.55f,19.57f),.39f,.26f,.34f,p);
            var vanity = Table(parent,"Furo_Washstand",new Vector3(11.2f,.55f,15.13f),1.06f,.76f,.58f,p);
            Round(vanity,"Basin",0,.84f,0,.40f,.16f,p.Paper);
            var toilet = Fixture(parent,"Benjo_Toilet",new Vector3(9.50f,.55f,19.83f),.48f,.76f,.76f);
            Round(toilet,"Pedestal",0,.19f,-.08f,.34f,.38f,p.Paper);
            Round(toilet,"Bowl",0,.38f,-.10f,.47f,.21f,p.Paper);
            Round(toilet,"SeatOpening",0,.49f,-.10f,.29f,.02f,p.Interior);
            Part(toilet,"Cistern",0,.56f,.26f,.44f,.40f,.20f,p.Paper);
        }

        static Transform Fixture(Transform parent,string name,Vector3 floor,float width,float height,float depth,float yaw = 0)
        {
            var t = ArtKit.Empty(parent,name,floor);
            t.localRotation = Quaternion.Euler(0,yaw,0);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = Vector3.up * (height / 2);
            box.size = new Vector3(width,height,depth);
            return t;
        }

        static void Part(Transform t,string name,float x,float y,float z,float w,float h,float d,Material mat) =>
            ArtKit.Box(t,name,new Vector3(x,y,z),new Vector3(w,h,d),mat,false);

        static void Round(Transform t,string name,float x,float y,float z,float diameter,float height,Material mat) =>
            ArtKit.Cylinder(t,name,new Vector3(x,y,z),new Vector3(diameter,height/2,diameter),Quaternion.identity,mat);

        static Transform Table(Transform parent,string name,Vector3 floor,float w,float h,float d,Palette p)
        {
            var t = Fixture(parent,name,floor,w,h,d);
            Part(t,"Top",0,h-.035f,0,w,.07f,d,p.WoodMidA);
            foreach(float x in new[]{-1f,1f})
                foreach(float z in new[]{-1f,1f})
                    Part(t,"Leg",x*(w/2-.08f),(h-.07f)/2,z*(d/2-.08f),.065f,h-.07f,.065f,p.WoodDarkA);
            return t;
        }

        static void Chair(Transform parent,string name,Vector3 floor,float yaw,Palette p)
        {
            var seat = Table(parent,name,floor,.43f,.43f,.43f,p);
            seat.localRotation = Quaternion.Euler(0,yaw,0);
            Part(seat,"Back",0,.65f,.19f,.42f,.42f,.055f,p.WoodMidA);
        }

        static void Cushion(Transform parent,string name,Vector3 floor,Material mat) =>
            ArtKit.Box(parent,name,floor+Vector3.up*.045f,new Vector3(.50f,.09f,.48f),mat,false);

        static void Bed(Transform parent,string name,Vector3 floor,float width,float yaw,Palette p)
        {
            bool futon = name.Contains("Futon");
            float baseHeight = futon ? .05f : .24f;
            var bed = Fixture(parent,name,floor,width,baseHeight+.18f,2.04f,yaw);
            Part(bed,"Base",0,baseHeight/2,0,width,baseHeight,2.04f,p.WoodDarkB);
            Part(bed,"Mattress",0,baseHeight+.08f,0,width-.04f,.16f,2f,p.Paper);
            Part(bed,"Quilt",0,baseHeight+.18f,-.25f,width-.015f,.075f,1.44f,p.FusumaB);
            Part(bed,"QuiltBorder",0,baseHeight+.22f,.37f,width-.03f,.015f,.12f,p.Carpet);
            Part(bed,"Pillow",0,baseHeight+.22f,.73f,width*.65f,.13f,.36f,p.FusumaA);
            if (!futon) Part(bed,"Headboard",0,.38f,.99f,width,.76f,.08f,p.WoodMidB);
        }
    }
}
