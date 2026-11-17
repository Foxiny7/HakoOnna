using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 生成日式老宅灰盒场景：两层主屋 + 土蔵（仓库）+ 围墙庭院。
    /// 坐标：1 单位 = 1 米，x 向东，z 向北，原点 = 主屋西南角。
    /// 布局为美术提案（地图布局是待定项），确认后再细化。
    /// </summary>
    public static class HouseBlockoutGenerator
    {
        const string ScenePath = "Assets/_Art/Scenes/House_Blockout.unity";
        const float T = 0.08f;       // 单侧墙厚（相邻房间各建一层，内墙总厚 0.16）
        const float DoorTop = 2.0f;  // 普通门洞高度

        // ---------- 楼层 ----------
        struct Level
        {
            public float wallBase, floorTop, wallTop;
            public Level(float b, float f, float t) { wallBase = b; floorTop = f; wallTop = t; }
        }

        const int G0 = 0, G1 = 1, GKura = 2, GFence = 3;
        static readonly Level[] Levels =
        {
            new Level(0f, 0f, 2.7f),    // 一层
            new Level(2.7f, 2.9f, 5.3f),// 二层（墙从楼板底部开始，避免缝隙）
            new Level(0f, 0f, 3.0f),    // 土蔵
            new Level(0f, 0f, 2.4f),    // 围墙
        };

        // ---------- 房间与开口 ----------
        class Room
        {
            public string id, label;
            public int group;
            public Rect r; // x, y(=z), width, depth
            public Material floor;
            public bool hasFloor = true;
        }

        class Opening
        {
            public int group;
            public bool alongX; // true: 墙沿 x 方向，位于 z = line；false: 墙沿 z 方向，位于 x = line
            public float line, center, width, sill, top;
            public bool window;
        }

        static Room R(string id, string label, int g, float x0, float z0, float x1, float z1, Material floor, bool hasFloor = true) =>
            new Room { id = id, label = label, group = g, r = Rect.MinMaxRect(x0, z0, x1, z1), floor = floor, hasFloor = hasFloor };

        static Opening DX(int g, float z, float x, float w, float top = DoorTop) =>
            new Opening { group = g, alongX = true, line = z, center = x, width = w, sill = 0f, top = top };

        static Opening DZ(int g, float x, float z, float w, float top = DoorTop) =>
            new Opening { group = g, alongX = false, line = x, center = z, width = w, sill = 0f, top = top };

        static Opening WX(int g, float z, float x, float w, float sill, float top) =>
            new Opening { group = g, alongX = true, line = z, center = x, width = w, sill = sill, top = top, window = true };

        static Opening WZ(int g, float x, float z, float w, float sill, float top) =>
            new Opening { group = g, alongX = false, line = x, center = z, width = w, sill = sill, top = top, window = true };

        // ---------- 容器摆放 ----------
        struct Place
        {
            public string room;
            public ContainerKind kind;
            public char wall;     // N/S/E/W：靠哪面墙，正面朝向房间内
            public float offset;  // 沿墙方向，从房间 min 角到容器中心的距离
            public Place(string room, ContainerKind kind, char wall, float offset) { this.room = room; this.kind = kind; this.wall = wall; this.offset = offset; }
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
            var roomById = new Dictionary<string, Room>();
            foreach (var room in rooms) roomById[room.id] = room;

            foreach (var room in rooms) BuildRoom(structure, room, openings, pal);
            BuildSlabs(structure, pal);
            BuildStairs(structure, pal);
            var ground = ArtKit.Box(structure, "Ground_Garden", new Vector3(10f, -0.06f, 9.5f), new Vector3(36f, 0.1f, 35f), pal.Ground);
            // 地面很大，降低光照贴图精度，避免超出图集大小的警告
            var so = new SerializedObject(ground.GetComponent<MeshRenderer>());
            so.FindProperty("m_ScaleInLightmap").floatValue = 0.15f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ArtKit.MarkStaticRecursive(structure.gameObject);

            PlaceContainers(containers, roomById);
            BuildGameplayMarkers(gameplay, roomById, pal);
            BuildProps(props, pal);
            AtmosphereBuilder.Build(lighting);

            ArtKit.EnsureFolder("Assets/_Art/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Hako Art] 灰盒场景已生成：{ScenePath}");
        }

        // =====================================================================
        // 布局数据
        // =====================================================================
        static List<Room> DefineRooms(Palette p) => new List<Room>
        {
            // ---- 一层 (主屋 20 × 13 m) ----
            R("Genkan", "玄関", G0, 0, 0, 4, 8, p.Stone),
            R("Engawa", "縁側", G0, 4, 0, 20, 2, p.WoodFloor),
            R("Ima", "居間", G0, 4, 2, 10, 8, p.Tatami),
            R("Zashiki", "座敷", G0, 10, 2, 16, 8, p.Tatami),
            R("Butsuma", "仏間", G0, 16, 2, 20, 8, p.Tatami),
            R("Rouka", "廊下", G0, 0, 8, 20, 9.5f, p.WoodFloor),
            R("Daidokoro", "台所", G0, 0, 9.5f, 6, 13, p.WoodFloor),
            R("Furo", "風呂", G0, 6, 9.5f, 9, 13, p.Tile),
            R("Benjo", "便所", G0, 9, 9.5f, 11, 13, p.Tile),
            R("Kaidan", "階段", G0, 11, 9.5f, 14, 13, p.WoodFloor),
            R("Nando", "納戸", G0, 14, 9.5f, 20, 13, p.WoodFloor),

            // ---- 二层 (14 × 8 m，位于主屋西北部) ----
            R("Rouka2F", "二階廊下", G1, 0, 8.2f, 14, 9.6f, p.WoodFloor),
            R("StairW2F", "裏階段口", G1, 0, 9.6f, 1.5f, 13, null, hasFloor: false),
            R("Kodomo", "子供部屋", G1, 1.5f, 9.6f, 7, 13, p.Tatami),
            R("Shinshitsu", "寝室", G1, 7, 9.6f, 12.3f, 13, p.Tatami),
            R("StairE2F", "階段口", G1, 12.3f, 9.6f, 14, 13, null, hasFloor: false),
            R("Monooki", "物置", G1, 0, 5, 5, 8.2f, p.WoodFloor),
            R("Washitsu2F", "二階和室", G1, 5, 5, 14, 8.2f, p.Tatami),

            // ---- 土蔵 ----
            R("Kura", "土蔵", GKura, 6, 16, 12, 21, p.Stone),

            // ---- 围墙（不建地板） ----
            R("Fence", "塀", GFence, -5, -5, 25, 24, null, hasFloor: false),
        };

        static List<Opening> DefineOpenings() => new List<Opening>
        {
            // ---- 一层门 ----
            DX(G0, 0, 2, 1.6f),          // 玄関 正门（通往庭院）
            DZ(G0, 4, 5, 1.0f),          // 玄関 ↔ 居間
            DZ(G0, 4, 1, 1.0f),          // 玄関 ↔ 縁側
            DX(G0, 8, 2, 1.2f),          // 玄関 ↔ 廊下
            DX(G0, 2, 7, 2.0f),          // 居間 ↔ 縁側
            DZ(G0, 10, 5, 1.8f),         // 居間 ↔ 座敷
            DX(G0, 8, 8, 1.0f),          // 居間 ↔ 廊下
            DX(G0, 2, 13, 3.0f),         // 座敷 ↔ 縁側
            DZ(G0, 16, 5, 1.8f),         // 座敷 ↔ 仏間
            DX(G0, 2, 18, 1.5f),         // 仏間 ↔ 縁側
            DX(G0, 8, 18, 1.0f),         // 仏間 ↔ 廊下
            DX(G0, 0, 10, 4.0f, 2.2f),   // 縁側 → 庭院
            DX(G0, 0, 17, 2.0f, 2.2f),   // 縁側 → 庭院
            DZ(G0, 20, 1, 1.5f, 2.2f),   // 縁側 东端 → 庭院
            DX(G0, 9.5f, 3, 1.2f),       // 廊下 ↔ 台所
            DX(G0, 9.5f, 7.5f, 0.9f),    // 廊下 ↔ 風呂
            DX(G0, 9.5f, 10, 0.8f),      // 廊下 ↔ 便所
            DX(G0, 9.5f, 11.6f, 1.0f),   // 廊下 ↔ 階段
            DX(G0, 9.5f, 17, 1.0f),      // 廊下 ↔ 納戸
            DX(G0, 13, 2, 1.0f),         // 台所 后门 → 庭院（通往土蔵）
            DZ(G0, 6, 11, 0.8f),         // 台所 ↔ 風呂
            DZ(G0, 11, 11.5f, 0.8f),     // 便所 ↔ 階段
            DZ(G0, 20, 11, 1.0f),        // 納戸 后门 → 庭院

            // ---- 一层窗（装障子纸，不可通行） ----
            WZ(G0, 0, 6, 1.2f, 0.9f, 1.8f),      // 玄関 西
            WX(G0, 13, 4.5f, 1.0f, 1.2f, 1.9f),  // 台所 北
            WX(G0, 13, 7.5f, 0.6f, 1.2f, 1.8f),  // 風呂 北
            WZ(G0, 20, 12.3f, 0.8f, 1.0f, 1.8f), // 納戸 东
            WZ(G0, 20, 3, 1.0f, 1.0f, 1.8f),     // 仏間 东

            // ---- 二层门 ----
            DX(G1, 9.6f, 0.75f, 1.5f),   // 廊下 ↔ 裏階段
            DX(G1, 9.6f, 13.15f, 1.7f),  // 廊下 ↔ 主階段
            DX(G1, 9.6f, 4, 0.9f),       // 廊下 ↔ 子供部屋
            DX(G1, 9.6f, 9.5f, 0.9f),    // 廊下 ↔ 寝室
            DX(G1, 8.2f, 2.5f, 0.9f),    // 廊下 ↔ 物置
            DX(G1, 8.2f, 10, 1.8f),      // 廊下 ↔ 二階和室
            DZ(G1, 5, 6.5f, 0.9f),       // 物置 ↔ 二階和室
            DZ(G1, 7, 11.5f, 0.9f),      // 子供部屋 ↔ 寝室

            // ---- 二层窗 ----
            WX(G1, 13, 6, 0.8f, 1.2f, 1.8f),
            WX(G1, 13, 9.9f, 0.6f, 0.9f, 1.8f),
            WX(G1, 5, 8.45f, 0.8f, 1.2f, 1.8f),
            WX(G1, 5, 12.9f, 1.0f, 0.9f, 1.8f),
            WZ(G1, 0, 6.6f, 0.8f, 1.2f, 1.8f),
            WZ(G1, 14, 8.9f, 0.6f, 1.0f, 1.8f),

            // ---- 土蔵 ----
            DX(GKura, 16, 9, 1.4f, 2.2f),
            DZ(GKura, 12, 18.5f, 1.0f),
            WX(GKura, 21, 9, 0.6f, 1.8f, 2.3f),

            // ---- 围墙大门（路线二的出口） ----
            DX(GFence, -5, 2, 2.4f, 2.4f),
        };

        static readonly Place[] Placements =
        {
            new Place("Genkan", ContainerKind.GetaBako, 'W', 2.0f),
            new Place("Genkan", ContainerKind.GetaBako, 'W', 3.2f),
            new Place("Genkan", ContainerKind.Tsuzura, 'W', 6.5f),
            new Place("Genkan", ContainerKind.GetaBako, 'E', 3.0f),

            new Place("Ima", ContainerKind.ChaTansu, 'N', 1.5f),
            new Place("Ima", ContainerKind.Tansu, 'N', 3.0f),
            new Place("Ima", ContainerKind.Tansu, 'W', 5.0f),

            new Place("Zashiki", ContainerKind.Nagamochi, 'N', 1.4f),
            new Place("Zashiki", ContainerKind.Oshiire, 'N', 4.2f),
            new Place("Zashiki", ContainerKind.Tansu, 'W', 1.2f),

            new Place("Butsuma", ContainerKind.Tsuzura, 'E', 1.0f),
            new Place("Butsuma", ContainerKind.ChaTansu, 'E', 5.0f),

            new Place("Engawa", ContainerKind.Nagamochi, 'S', 2.0f),
            new Place("Engawa", ContainerKind.Tsuzura, 'S', 10.0f),

            new Place("Daidokoro", ContainerKind.ChaTansu, 'N', 4.5f),
            new Place("Daidokoro", ContainerKind.Komebitsu, 'S', 4.8f),
            new Place("Daidokoro", ContainerKind.Tsuzura, 'S', 1.6f),

            new Place("Furo", ContainerKind.GetaBako, 'W', 3.0f),
            new Place("Benjo", ContainerKind.GetaBako, 'N', 1.0f),

            new Place("Nando", ContainerKind.Nagamochi, 'N', 1.2f),
            new Place("Nando", ContainerKind.YofukuTansu, 'N', 3.3f),
            new Place("Nando", ContainerKind.Tsuzura, 'N', 5.2f),
            new Place("Nando", ContainerKind.Tansu, 'W', 1.75f),
            new Place("Nando", ContainerKind.Komebitsu, 'S', 1.0f),

            new Place("Kodomo", ContainerKind.Oshiire, 'N', 2.0f),
            new Place("Kodomo", ContainerKind.Tansu, 'N', 4.5f),
            new Place("Kodomo", ContainerKind.Tsuzura, 'W', 1.5f),

            new Place("Shinshitsu", ContainerKind.Oshiire, 'N', 1.5f),
            new Place("Shinshitsu", ContainerKind.YofukuTansu, 'N', 3.8f),
            new Place("Shinshitsu", ContainerKind.Tansu, 'E', 1.7f),

            new Place("Monooki", ContainerKind.Nagamochi, 'S', 1.2f),
            new Place("Monooki", ContainerKind.Tsuzura, 'S', 3.0f),
            new Place("Monooki", ContainerKind.Tansu, 'S', 4.3f),
            new Place("Monooki", ContainerKind.Komebitsu, 'W', 2.5f),

            new Place("Washitsu2F", ContainerKind.Oshiire, 'S', 2.0f),
            new Place("Washitsu2F", ContainerKind.Tansu, 'S', 4.5f),
            new Place("Washitsu2F", ContainerKind.ChaTansu, 'S', 6.5f),
            new Place("Washitsu2F", ContainerKind.Nagamochi, 'E', 1.6f),

            new Place("Kura", ContainerKind.Nagamochi, 'N', 1.2f),
            new Place("Kura", ContainerKind.Nagamochi, 'N', 3.0f),
            new Place("Kura", ContainerKind.Tsuzura, 'N', 4.8f),
            new Place("Kura", ContainerKind.Tansu, 'W', 2.5f),
            new Place("Kura", ContainerKind.YofukuTansu, 'W', 3.6f),
            new Place("Kura", ContainerKind.Komebitsu, 'S', 1.0f),
            new Place("Kura", ContainerKind.Tsuzura, 'E', 1.2f),
        };

        // =====================================================================
        // 构建
        // =====================================================================
        static void BuildRoom(Transform parent, Room room, List<Opening> openings, Palette pal)
        {
            var lv = Levels[room.group];
            var t = ArtKit.Empty(parent, $"Room_{room.id}_{room.label}", Vector3.zero);
            var r = room.r;
            var wallMat = room.group == GFence ? pal.Fence : pal.Plaster;

            if (room.hasFloor && room.floor != null)
            {
                if (room.group == G1)
                    ArtKit.Box(t, "Floor", new Vector3(r.center.x, lv.floorTop + 0.005f, r.center.y), new Vector3(r.width, 0.01f, r.height), room.floor, false);
                else
                    ArtKit.Box(t, "Floor", new Vector3(r.center.x, -0.05f, r.center.y), new Vector3(r.width, 0.1f, r.height), room.floor);
            }

            BuildEdge(t, room, lv, true, r.yMin, r.xMin, r.xMax, +1f, wallMat, openings, pal);
            BuildEdge(t, room, lv, true, r.yMax, r.xMin, r.xMax, -1f, wallMat, openings, pal);
            BuildEdge(t, room, lv, false, r.xMin, r.yMin + T, r.yMax - T, +1f, wallMat, openings, pal);
            BuildEdge(t, room, lv, false, r.xMax, r.yMin + T, r.yMax - T, -1f, wallMat, openings, pal);
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
                float bottom = lv.floorTop + o.sill;
                float top = Mathf.Min(lv.wallTop, lv.floorTop + o.top);
                if (bottom > lv.wallBase + 0.001f) WallPiece(parent, "Wall_Below", alongX, wallLine, a, b, lv.wallBase, bottom, mat);
                if (top < lv.wallTop - 0.001f) WallPiece(parent, "Wall_Lintel", alongX, wallLine, a, b, top, lv.wallTop, mat);
                if (o.window)
                {
                    // 障子窗：可透月光（不投射阴影），但有碰撞，玩家不能翻出去
                    var shoji = WallPiece(parent, "Window_Shoji", alongX, wallLine, a, b, bottom, top, pal.Paper);
                    var s = shoji.transform.localScale;
                    shoji.transform.localScale = alongX ? new Vector3(s.x, s.y, 0.02f) : new Vector3(0.02f, s.y, s.z);
                    shoji.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

        static void BuildSlabs(Transform parent, Palette pal)
        {
            var t = ArtKit.Empty(parent, "Slabs", Vector3.zero);
            // 一层天花板 / 二层楼板（留出两个楼梯井）
            var holes = new[] { Rect.MinMaxRect(0f, 9.6f, 1.5f, 13f), Rect.MinMaxRect(12.3f, 9.6f, 14f, 13f) };
            foreach (var r in SubtractRects(Rect.MinMaxRect(0f, 0f, 20f, 13f), holes))
                ArtKit.Box(t, "Slab_1F", new Vector3(r.center.x, 2.8f, r.center.y), new Vector3(r.width, 0.2f, r.height), pal.Ceiling);
            // 二层屋顶
            ArtKit.Box(t, "Roof_2F", new Vector3(7f, 5.375f, 9f), new Vector3(14f, 0.15f, 8f), pal.Roof);
            // 土蔵屋顶
            ArtKit.Box(t, "Roof_Kura", new Vector3(9f, 3.075f, 18.5f), new Vector3(6f, 0.15f, 5f), pal.Roof);
        }

        /// <summary>从矩形中挖掉若干矩形，返回剩余部分（按行合并）。</summary>
        static List<Rect> SubtractRects(Rect outer, IList<Rect> holes)
        {
            var xs = new SortedSet<float> { outer.xMin, outer.xMax };
            var zs = new SortedSet<float> { outer.yMin, outer.yMax };
            foreach (var h in holes)
            {
                xs.Add(Mathf.Clamp(h.xMin, outer.xMin, outer.xMax)); xs.Add(Mathf.Clamp(h.xMax, outer.xMin, outer.xMax));
                zs.Add(Mathf.Clamp(h.yMin, outer.yMin, outer.yMax)); zs.Add(Mathf.Clamp(h.yMax, outer.yMin, outer.yMax));
            }
            var xl = new List<float>(xs);
            var zl = new List<float>(zs);
            var result = new List<Rect>();
            for (int j = 0; j < zl.Count - 1; j++)
            {
                float runStart = float.NaN;
                for (int i = 0; i <= xl.Count - 1; i++)
                {
                    bool filled = false;
                    if (i < xl.Count - 1)
                    {
                        var c = new Vector2((xl[i] + xl[i + 1]) / 2f, (zl[j] + zl[j + 1]) / 2f);
                        filled = true;
                        foreach (var h in holes) if (h.Contains(c)) { filled = false; break; }
                    }
                    if (filled && float.IsNaN(runStart)) runStart = xl[i];
                    if (!filled && !float.IsNaN(runStart))
                    {
                        result.Add(Rect.MinMaxRect(runStart, zl[j], xl[i], zl[j + 1]));
                        runStart = float.NaN;
                    }
                }
            }
            return result;
        }

        static void BuildStairs(Transform parent, Palette pal)
        {
            var t = ArtKit.Empty(parent, "Stairs", Vector3.zero);
            // 两座楼梯都从北端（z=12.9）向南爬升到二层（z=9.6）。日式老宅楼梯很陡，约 41°。
            BuildStair(t, "Stair_Main", 12.6f, 13.9f, 12.9f, 9.6f, 2.9f, 13, pal.WoodDarkA);
            BuildStair(t, "Stair_Kitchen", 0.2f, 1.2f, 12.9f, 9.6f, 2.9f, 13, pal.WoodDarkB);
        }

        static void BuildStair(Transform parent, string name, float x0, float x1, float zBottom, float zTop, float rise, int steps, Material mat)
        {
            var t = ArtKit.Empty(parent, name, Vector3.zero);
            float run = (zBottom - zTop) / steps, stepRise = rise / steps, w = x1 - x0, xm = (x0 + x1) / 2f;
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * stepRise;
                float zc = zBottom - (i + 0.5f) * run;
                ArtKit.Box(t, $"Step_{i:00}", new Vector3(xm, h / 2f, zc), new Vector3(w, h, run), mat, false);
            }
            // 行走用的斜坡碰撞体（台阶本身无碰撞，避免角色控制器卡台阶）
            float len = Mathf.Sqrt(run * steps * run * steps + rise * rise);
            float angle = Mathf.Atan2(rise, zBottom - zTop) * Mathf.Rad2Deg;
            var ramp = new GameObject("WalkRamp");
            ramp.transform.SetParent(t, false);
            ramp.transform.localPosition = new Vector3(xm, rise / 2f + stepRise * 0.5f, (zBottom + zTop) / 2f);
            ramp.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            ramp.AddComponent<BoxCollider>().size = new Vector3(w, 0.05f, len);
        }

        static void PlaceContainers(Transform parent, Dictionary<string, Room> rooms)
        {
            var rng = new System.Random(4521); // 固定种子：每次生成的变体分布一致
            var counter = new Dictionary<string, int>();
            foreach (var p in Placements)
            {
                if (!rooms.TryGetValue(p.room, out var room)) { Debug.LogError($"[Hako Art] 未知房间 {p.room}"); continue; }
                var size = ContainerPrefabBuilder.Sizes[p.kind];
                string variant = ContainerPrefabBuilder.Variants[rng.Next(ContainerPrefabBuilder.Variants.Length)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContainerPrefabBuilder.PathFor(p.kind, variant));
                if (prefab == null) { Debug.LogError($"[Hako Art] 缺少预制体 {p.kind}_{variant}"); continue; }

                var r = room.r;
                float y = Levels[room.group].floorTop;
                float half = size.z / 2f;
                Vector3 pos;
                float yaw;
                switch (p.wall)
                {
                    case 'N': pos = new Vector3(r.xMin + p.offset, y, r.yMax - T - half); yaw = 180f; break;
                    case 'S': pos = new Vector3(r.xMin + p.offset, y, r.yMin + T + half); yaw = 0f; break;
                    case 'W': pos = new Vector3(r.xMin + T + half, y, r.yMin + p.offset); yaw = 90f; break;
                    default: pos = new Vector3(r.xMax - T - half, y, r.yMin + p.offset); yaw = -90f; break;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                counter.TryGetValue(p.room, out int n);
                counter[p.room] = n + 1;
                go.name = $"{p.room}_{p.kind}_{n + 1:00}";
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            }
        }

        static void BuildGameplayMarkers(Transform parent, Dictionary<string, Room> rooms, Palette pal)
        {
            // 探索者出生点（玄関）
            var spawns = ArtKit.Empty(parent, "ExplorerSpawnPoints", Vector3.zero);
            Vector3[] sp = { new Vector3(1.2f, 0, 2.5f), new Vector3(2.8f, 0, 2.5f), new Vector3(1.2f, 0, 4.5f), new Vector3(2.8f, 0, 4.5f), new Vector3(2f, 0, 6.3f) };
            for (int i = 0; i < sp.Length; i++)
            {
                var s = ArtKit.Empty(spawns, $"SpawnPoint_{i}", sp[i]);
                s.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            // 秘密出口候选点：地毯 + HatchAnchor。暗门只在被发现时由程序生成。
            var candidates = ArtKit.Empty(parent, "SecretExitCandidates", Vector3.zero);
            Vector3[] cp = { new Vector3(7f, 0, 4.8f), new Vector3(13f, 0, 4.8f), new Vector3(17.2f, 0, 11.1f), new Vector3(9f, 0, 18.4f) };
            string[] cn = { "Ima", "Zashiki", "Nando", "Kura" };
            for (int i = 0; i < cp.Length; i++)
            {
                var c = ArtKit.Empty(candidates, $"SecretExitCandidate_{i}_{cn[i]}", cp[i]);
                ArtKit.Box(c, "Carpet", new Vector3(0f, 0.006f, 0f), new Vector3(2.0f, 0.012f, 1.5f), pal.Carpet, false);
                ArtKit.Empty(c, "HatchAnchor", Vector3.zero);
            }

            // 保险箱（居間北墙）
            var safe = ArtKit.Empty(parent, "Safe_Kinko", new Vector3(9.2f, 0f, 8f - T - 0.275f));
            safe.rotation = Quaternion.Euler(0f, 180f, 0f);
            ArtKit.Box(safe, "Body", new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.55f), pal.Metal);
            ArtKit.Cylinder(safe, "Dial", new Vector3(0f, 0.5f, 0.285f), new Vector3(0.12f, 0.01f, 0.12f), Quaternion.Euler(90f, 0f, 0f), pal.Rust);
            ArtKit.Empty(safe, "InteractPoint", new Vector3(0f, 0f, 0.9f));
            ArtKit.Empty(safe, "KeyAnchor", new Vector3(0f, 0.3f, 0f));

            // 超度地点：仏壇（仏間东墙）
            var memorial = ArtKit.Empty(parent, "MemorialPoint_Butsudan", new Vector3(20f - T - 0.3f, 0f, 5f));
            memorial.rotation = Quaternion.Euler(0f, -90f, 0f);
            ArtKit.Box(memorial, "Butsudan", new Vector3(0f, 0.8f, 0f), new Vector3(1.2f, 1.6f, 0.6f), pal.Lacquer);
            ArtKit.Box(memorial, "Altar", new Vector3(0f, 0.45f, 0.45f), new Vector3(1.0f, 0.05f, 0.3f), pal.WoodDarkA);
            ArtKit.Empty(memorial, "InteractPoint", new Vector3(0f, 0f, 1.0f));

            // 大门（路线二）：围墙南侧，锁着的两扇门板
            var gate = ArtKit.Empty(parent, "Gate_Main", new Vector3(2f, 0f, -5f + T / 2f));
            ArtKit.Box(gate, "GateDoor_L", new Vector3(-0.6f, 1.2f, 0f), new Vector3(1.2f, 2.4f, 0.1f), pal.WoodDarkA);
            ArtKit.Box(gate, "GateDoor_R", new Vector3(0.6f, 1.2f, 0f), new Vector3(1.2f, 2.4f, 0.1f), pal.WoodDarkA);
            ArtKit.Empty(gate, "InteractPoint", new Vector3(0f, 0f, 0.8f));
            ArtKit.Empty(parent, "EscapeZone_Gate", new Vector3(2f, 0f, -6.5f));
        }

        static void BuildProps(Transform parent, Palette pal)
        {
            // 少量非容器道具，只用于判断空间比例
            ArtKit.Box(parent, "Bathtub", new Vector3(8.4f, 0.3f, 11.8f), new Vector3(1.0f, 0.6f, 0.8f), pal.WoodMidA);
            ArtKit.Box(parent, "Chabudai_Ima", new Vector3(6.2f, 0.17f, 3.3f), new Vector3(0.9f, 0.34f, 0.9f), pal.WoodDarkB);
            ArtKit.Box(parent, "Kamado_Daidokoro", new Vector3(5.45f, 0.4f, 12.35f), new Vector3(0.9f, 0.8f, 0.7f), pal.Stone);
        }
    }
}
