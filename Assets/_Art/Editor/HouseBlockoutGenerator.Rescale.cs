using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 从 V3 旧尺寸（平面 ×√2、净高 2.3 m、门高 1.76 m）搬到新尺寸，并把家具放大到和建筑协调。
    /// 建筑本身按新常量直接生成；手摆的家具、陈设、容器、出生点仍按旧坐标书写，
    /// 生成后在这里统一搬家：
    ///   · 互相挨着（间隙 &lt; 0.2 m）的东西归成一组，整组平移，组内相对位置不变；
    ///   · 贴墙的组保持离墙距离，房间中部的组按比例拉开（离墙 1.2 m 以内算贴墙）；
    ///   · 垂直方向：落地的跟地板，吊在天花下的跟天花，长押以上的跟门楣；
    ///   · 门洞正中的东西（匾额、暖帘、门口脚垫）沿墙方向跟着门走。
    /// 然后所有室内家具统一放大 FurnitureScale（大小件同一个比例，和放大后的建筑一致）：
    /// 贴墙的一侧、落地的底面、吊顶的顶面不动，往房间里长。
    /// 放大后离墙 0.65 m 以内的组推回墙边（落地的离墙面 4 cm，挂墙的贴住），
    /// 旧坐标是按平面 ×√2 摆的，容器背后原来普遍空着 10–50 cm。
    /// 推完如果比原来更挤（碰到别的组、门口、交互站位、步行路线、墙或天花），
    /// 只平移让开、不缩小：贴墙的组只沿墙滑；实在放不下的纯陈设撤掉，场景物体保留并在日志里报出来。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        const float OldPS = 1.41421356f;
        const float OldDoorTop = 1.76f;
        const float FitMargin = 1.2f;

        /// <summary>所有家具相对真实尺寸的统一放大倍数（容器、手摆陈设、代码家具、第三遍补的家具都用它）。</summary>
        internal const float FurnitureScale = 1.25f;

        /// <summary>落地家具背面离墙面（和第三遍补的家具 FillGap 一致，躲开柱子和腰板压条）。</summary>
        const float SnapGap = .04f;
        /// <summary>放大后离墙面这么近的组算“靠墙摆的”，推回墙边。</summary>
        const float SnapReach = .65f;

        static readonly Level[] OldLevels =
        {
            new Level(0f, 0.55f, 2.85f),
            new Level(3.05f, 3.05f, 5.35f),
            new Level(0f, 0.30f, 3.0f),
            new Level(0f, 0f, 2.4f),
        };

        static Vector3 OldPlan(float x, float y, float z) => new Vector3(x * OldPS, y, z * OldPS);

        static float OldFloorHeight(Room room) =>
            room.id == "Genkan" || room.id == "Daidokoro" || room.id == "UraKaidan" ? 0 : OldLevels[room.group].floorTop;

        /// <summary>一维映射：离两端 m 以内保持离端距离，中段线性拉伸。</summary>
        static float Stretch(float v, float a0, float b0, float a1, float b1)
        {
            float m = Mathf.Min(FitMargin, (b0 - a0) * .35f);
            if (v <= a0 + m) return a1 + (v - a0);
            if (v >= b0 - m) return b1 - (b0 - v);
            return Mathf.Lerp(a1 + m, b1 - m, (v - a0 - m) / (b0 - a0 - 2 * m));
        }

        static bool Inside(Rect r, Vector2 q) =>
            q.x >= r.xMin - .001f && q.x <= r.xMax + .001f && q.y >= r.yMin - .001f && q.y <= r.yMax + .001f;

        static List<Room> layoutRooms;
        static List<Room> LayoutRooms => DRooms ?? (layoutRooms ?? (layoutRooms = DefineRooms(Palette.Load())));

        /// <summary>旧坐标点落在哪个房间（null = 室外）。</summary>
        static Room OldRoomAt(Vector3 p)
        {
            var q = new Vector2(p.x, p.z);
            foreach (var r in LayoutRooms) if (r.group == GKura && Inside(r.r0, q)) return r;
            int g = p.y > OldLevels[G1].floorTop - .1f ? G1 : G0;
            foreach (var r in LayoutRooms) if (r.group == g && Inside(r.r0, q)) return r;
            return null;
        }

        static float OutdoorScale => PlanScale / OldPS;

        /// <summary>新平面相对 V3 的倍数（截图工具用）。</summary>
        internal static float PlanStretch => OutdoorScale;

        /// <summary>俯视平面图的相机高度：楼板 / 屋顶下方一点。</summary>
        internal static float PlanCutHeight(bool upstairs) => upstairs ? Levels[G1].wallTop - .35f : Levels[G0].wallTop - .45f;

        /// <summary>单个旧坐标点搬到新尺寸（截图机位、预览相机用）；高度保持离地板距离。</summary>
        internal static Vector3 MapOldPoint(Vector3 p)
        {
            var room = OldRoomAt(p);
            if (room == null) return new Vector3(p.x * OutdoorScale, p.y, p.z * OutdoorScale);
            return new Vector3(
                Stretch(p.x, room.r0.xMin, room.r0.xMax, room.r.xMin, room.r.xMax),
                p.y - OldFloorHeight(room) + FloorHeight(room),
                Stretch(p.z, room.r0.yMin, room.r0.yMax, room.r.yMin, room.r.yMax));
        }

        // =====================================================================
        // 分组搬家 + 放大
        // =====================================================================
        sealed class RItem
        {
            public Bounds b;
            public bool empty, pinned, solid, spawn;
            public Room room;
            public int up;
            public Transform go; // 场景物体；null = Kit 网格件 / Blocker
        }

        sealed class RGroup
        {
            public Bounds b0, b;   // 旧尺寸 / 搬家后（放大前）
            public Room room;
            public bool pinned, spawn, solid;
            /// <summary>组里有场景物体（容器、代码家具、出生点、保险箱、仏坛），放不下也不能撤。</summary>
            public bool hasGo;
            /// <summary>组里有容器（选门扇停靠的一侧时尽量不碰）。</summary>
            public bool hasBox;
            /// <summary>贴着 x / z 方向的墙：避让时这个方向不动，只沿墙滑。</summary>
            public bool lockX, lockZ;
            public bool dropped;
            public float vol = -1f, s = 1f;
            public Vector3 d, pivot;
            /// <summary>放大之后的平移（推回墙边 + 避让）。</summary>
            public Vector3 o;
            public string label = "";
            /// <summary>推墙 / 避让过程的记录（诊断用）。</summary>
            public string note = "";
            /// <summary>组里实心件（容器、带碰撞体的家具、Blocker）的包围盒：旧尺寸 / 搬家后（放大前）。</summary>
            public Bounds sb0, sb;
            public bool hasSolid;
            /// <summary>
            /// 组里“定位件”（实心的、高 0.25 m 以上的、离地挂着的）的包围盒：旧尺寸 / 搬家后（放大前）。
            /// 算这组是不是靠墙摆的只看它——桌边的坐垫离墙近，不能把整套桌椅拉到墙边。
            /// </summary>
            public Bounds kb0, kb;
            public bool hasKey;
            /// <summary>绕竖轴转 90°×rot（换到别的墙时背面朝新墙；0 = 不转）。</summary>
            public int rot;
            /// <summary>推回墙边之后、避让之前的平移：避让把靠墙件推远了，也还按靠墙件找地方。</summary>
            public Vector3 snapO;
            /// <summary>最后再靠墙时背后的薄件允许嵌进墙面的深度，之后的冲突检查也按这个算。</summary>
            public float inWall;
            public Vector3 Turn(Vector3 v) => rot switch
            {
                1 => new Vector3(v.z, v.y, -v.x),
                2 => new Vector3(-v.x, v.y, -v.z),
                3 => new Vector3(-v.z, v.y, v.x),
                _ => v,
            };
            Bounds Place(Bounds src, float sc) =>
                new Bounds(pivot + Turn((src.center - pivot) * sc) + o, (rot & 1) == 1 ? new Vector3(src.size.z, src.size.y, src.size.x) * sc : src.size * sc);
            public Bounds At(float sc) => Place(b, sc);
            public Bounds SolidAt(float sc) => Place(sb, sc);
            public Bounds KeyAt(float sc) => Place(kb, sc);
            /// <summary>组里一件（旧坐标包围盒）搬家 + 放大之后的位置（诊断用）。</summary>
            public Bounds Map(Bounds old) => Place(new Bounds(old.center + d, old.size), s);
            /// <summary>旧坐标 → 新坐标：先平移 d，再以 pivot 为中心放大 s 倍、转 90°×rot，最后平移 o。</summary>
            public Matrix4x4 M => dropped ? Matrix4x4.zero
                : Matrix4x4.Translate(pivot + o) * Matrix4x4.Rotate(Quaternion.Euler(0f, 90f * rot, 0f))
                  * Matrix4x4.Scale(Vector3.one * s) * Matrix4x4.Translate(d - pivot);
        }

        struct RMarker
        {
            public int g;
            public Vector3 p0;   // 旧坐标
            public bool tested;  // PlayerDoorwayTests 会检查站位（出生点、保险箱、仏坛）
        }

        /// <summary>搬家 + 放大之后各组占的空间（第三遍按房间补家具时避让）。</summary>
        static List<Bounds> Occupied;
        /// <summary>和 Occupied 一一对应：整组没有碰撞体（坐垫、散放的玩具这类能踩过去的），房间中部家具组不用和它隔开走道宽。</summary>
        static List<bool> OccupiedSoft;
        /// <summary>必须留空的站位：交互点、出生点、暗门地毯。</summary>
        static List<Bounds> KeepOut;
        /// <summary>禁区列表里前这么多个是门口留空区（只管挡路的定位件，容 5 cm），后面的是暗门地毯（整组都不能压）。</summary>
        static int DoorZoneCount;
        const float DoorZoneSlack = .05f;

        static Bounds StandZone(Vector3 p) => new Bounds(p + Vector3.up * .83f, new Vector3(.48f, 1.64f, .48f));

        /// <summary>
        /// 必须保持畅通的步行路线（两两一组：起点、终点），和 PlayerDoorwayTests 的路线一致，
        /// 另加两条楼梯侧面的路线。家具放大、补家具都不能往上面压。
        /// 縁側朝院子的四个开口改成关着的玻璃拉门后，穿过它们的路线已删掉（测试那边等 Codex 改）。
        /// 2026-11-19 封掉 11 处门洞后，穿过它们的路线也删掉了（Door_3/5/8/10/11/12/13/14/18/28/41，测试等 Codex 删）。
        /// </summary>
        static readonly Vector3[] WalkLanes =
        {
            new Vector3(5.46f, 0f, -1.3999f), new Vector3(5.46f, 0f, .9899f),
            new Vector3(5.46f, 0f, 9.0201f), new Vector3(5.46f, 0f, 11f),
            new Vector3(9.1054f, 0f, 6.37f), new Vector3(12.8018f, .55f, 6.37f),
            new Vector3(9.1054f, 0f, 13.65f), new Vector3(12.8018f, .55f, 13.65f),
            new Vector3(3.64f, 0f, 19.03f), new Vector3(3.64f, 0f, 21.42f),
            new Vector3(24.49f, .55f, 6.37f), new Vector3(26.4699f, .55f, 6.37f),
            new Vector3(14.56f, .55f, 1.3062f), new Vector3(14.56f, .55f, 3.72f),
            new Vector3(21.84f, .55f, 1.3062f), new Vector3(21.84f, .55f, 3.72f),
            new Vector3(30f, .55f, 1.3062f), new Vector3(30f, .55f, 3.72f),
            new Vector3(33.59f, .55f, 13.65f), new Vector3(36.0039f, .55f, 13.65f),
            new Vector3(14.56f, .55f, 16.3f), new Vector3(14.56f, .55f, 18.7139f),
            new Vector3(21.84f, .55f, 16.3f), new Vector3(21.84f, .55f, 18.7139f),
            new Vector3(30f, .55f, 16.3f), new Vector3(30f, .55f, 18.7139f),
            new Vector3(33.59f, .55f, 18.66f), new Vector3(36.0039f, .55f, 18.66f),
            new Vector3(33.59f, .55f, 1.365f), new Vector3(36.0039f, .55f, 1.365f),
            new Vector3(12.7399f, .55f, 18.596f), new Vector3(12.7399f, .55f, 21.0099f),
            new Vector3(19.9999f, .55f, 18.596f), new Vector3(19.9999f, .55f, 21.0099f),
            new Vector3(16.38f, 0f, 32.34f), new Vector3(16.38f, .3f, 36.4176f),
            new Vector3(20.0253f, .3f, 39.2f), new Vector3(24.08f, 0f, 39.2f),
            new Vector3(5.46f, 0f, -12.3201f), new Vector3(5.46f, 0f, -9.5201f),
            new Vector3(12.7399f, 4.15f, 16.3f), new Vector3(12.7399f, 4.15f, 18.7139f),
            new Vector3(23.6f, 4.15f, 16.3f), new Vector3(23.6f, 4.15f, 18.7139f),
            new Vector3(17.2101f, 4.15f, 13.65f), new Vector3(19.19f, 4.15f, 13.65f),
            new Vector3(13.6001f, 4.15f, 18.596f), new Vector3(13.6001f, 4.15f, 21.0099f),
            new Vector3(20.9999f, 4.15f, 18.596f), new Vector3(20.9999f, 4.15f, 21.0099f),
            new Vector3(13.6001f, 4.15f, 25.4001f), new Vector3(13.6001f, 4.15f, 27.8139f),
            new Vector3(20.9999f, 4.15f, 25.4001f), new Vector3(20.9999f, 4.15f, 27.8139f),
            new Vector3(9.08f, 0f, 18.4485f), new Vector3(9.08f, 4.15f, 27.8308f),
            new Vector3(27.3f, .55f, 18.5624f), new Vector3(27.3f, 4.15f, 27.8308f),
            new Vector3(6.0713f, 0f, 6.37f), new Vector3(13.8567f, .55f, 6.37f),
            new Vector3(6.0713f, 0f, 13.65f), new Vector3(13.8567f, .55f, 13.65f),
            new Vector3(13.6047f, 4.15f, 20.7137f), new Vector3(13.6047f, 4.15f, 25.7795f),
            new Vector3(2.5609f, 0f, 12.5469f), new Vector3(2.5609f, 0f, 19.0137f),
            new Vector3(8.7248f, 0f, 12.5469f), new Vector3(8.7248f, 0f, 19.0137f),
            new Vector3(21.0011f, 4.15f, 20.7137f), new Vector3(21.0011f, 4.15f, 25.7795f),
            new Vector3(25.1f, .55f, 23.205f), new Vector3(27.3f, .55f, 23.205f),
            new Vector3(6.88f, 0f, 23.205f), new Vector3(9.08f, 0f, 23.205f),
        };

        static int FindRoot(List<RItem> items, int i)
        {
            while (items[i].up != i) { items[i].up = items[items[i].up].up; i = items[i].up; }
            return i;
        }

        static bool Touch(RItem a, RItem c, float g = .2f)
        {
            if (a.empty || c.empty || a.room != c.room) return false;
            const float gy = .1f;
            return a.b.min.y <= c.b.max.y + gy && c.b.min.y <= a.b.max.y + gy
                && a.b.min.x <= c.b.max.x + g && c.b.min.x <= a.b.max.x + g
                && a.b.min.z <= c.b.max.z + g && c.b.min.z <= a.b.max.z + g;
        }

        /// <summary>
        /// 物体的世界包围盒：渲染器 + 实心 BoxCollider。容器金具（Fit_*）不算——
        /// 它们只是贴在箱面上的薄片，算进去会让搬家求解把容器挪 1–2 cm。
        /// 碰撞体按自己的 transform 算——刚实例化再挪位置的 Collider.bounds 还停在原点
        /// （autoSyncTransforms 关着），直接用会把整屋的东西连成一组。
        /// </summary>
        static Bounds? WorldBounds(Transform t)
        {
            Bounds? acc = null;
            void Add(Bounds b)
            {
                if (b.size == Vector3.zero) return;
                if (acc == null) acc = b; else { var x = acc.Value; x.Encapsulate(b); acc = x; }
            }
            foreach (var r in t.GetComponentsInChildren<Renderer>())
                if (r.enabled && !ContainerPrefabBuilder.IsFitting(r)) Add(r.bounds);
            foreach (var c in t.GetComponentsInChildren<BoxCollider>()) if (c.enabled && !c.isTrigger) Add(BoxWorldBounds(c));
            return acc;
        }

        static Bounds BoxWorldBounds(BoxCollider c)
        {
            var tr = c.transform;
            var e = c.size * .5f;
            var b = new Bounds(tr.TransformPoint(c.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(tr.TransformPoint(c.center + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
            return b;
        }

        /// <summary>
        /// 东西属于哪个房间：平面看包围盒中心，楼层看底面。
        /// 钉住的件（暖帘、床の間）高度已经按新尺寸写，楼层按新标高判断。
        /// </summary>
        static Room ItemRoom(Bounds b, bool pinned)
        {
            var q = new Vector2(b.center.x, b.center.z);
            foreach (var r in LayoutRooms) if (r.group == GKura && Inside(r.r0, q)) return r;
            float split = (pinned ? Levels[G1].floorTop : OldLevels[G1].floorTop) - .1f;
            int g = b.min.y + .01f > split ? G1 : G0;
            foreach (var r in LayoutRooms) if (r.group == g && Inside(r.r0, q)) return r;
            return null;
        }

        static Bounds BlockerBounds(Vector3 floorPos, float yaw, Vector3 size)
        {
            float c = Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)), s = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad));
            var ext = new Vector3(c * size.x + s * size.z, size.y, s * size.x + c * size.z);
            return new Bounds(floorPos + Vector3.up * size.y / 2, ext);
        }

        /// <summary>
        /// 给 Kit 件（按登记顺序）算变换矩阵（平移 + 放大）；场景里代码生成的家具、容器、出生点、
        /// 保险箱、仏坛直接移动、缩放。顺带算出 Occupied / KeepOut 给第三遍补家具用。
        /// </summary>
        static List<Matrix4x4> SolveRelocation(List<Bounds> kitBounds, List<bool> kitPinned, List<bool> kitSolid, Transform root)
        {
            Physics.SyncTransforms();
            var items = new List<RItem>();
            for (int i = 0; i < kitBounds.Count; i++)
            {
                var b = kitBounds[i];
                items.Add(new RItem
                {
                    b = b, pinned = kitPinned[i], solid = i < kitSolid.Count && kitSolid[i],
                    empty = b.size == Vector3.zero && b.center == Vector3.zero,
                });
            }
            int kitCount = items.Count;
            var markerItems = new List<(int item, Vector3 p, bool tested)>();

            void AddGo(Transform t, Bounds? forced = null, bool spawn = false, bool tested = false)
            {
                if (t == null) return;
                var b = forced ?? WorldBounds(t);
                bool solid = false;
                foreach (var c in t.GetComponentsInChildren<BoxCollider>()) solid |= c.enabled && !c.isTrigger;
                items.Add(new RItem { go = t, b = b ?? new Bounds(t.position, Vector3.zero), solid = solid, spawn = spawn });
                if (spawn) markerItems.Add((items.Count - 1, t.position, true));
                var ip = t.Find("InteractPoint");
                if (ip != null) markerItems.Add((items.Count - 1, ip.position, tested));
            }
            var props = root.Find("Props");
            foreach (Transform t in props) if (t.name != "Dressing_Blockers") AddGo(t);
            foreach (Transform t in root.Find("Containers")) AddGo(t);
            var gp = root.Find("Gameplay");
            foreach (Transform t in gp.Find("ExplorerSpawnPoints")) AddGo(t, StandZone(t.position), true);
            AddGo(gp.Find("Safe_Kinko"), null, false, true);
            AddGo(gp.Find("MemorialPoint_Butsudan"), null, false, true);

            for (int i = 0; i < items.Count; i++)
            {
                items[i].up = i;
                if (!items[i].empty) items[i].room = ItemRoom(items[i].b, items[i].pinned);
            }
            // 先并容器以外的东西（床の間的台面、香炉跟着钉住的床柱、落とし掛け走，保持原尺寸）；
            // 再并容器：容器不并进有钉住件的组——钉住的组不放大，并进去的容器也就不放大了；
            // 容器只和真正挨着的（5 cm 以内、上面摆的）并，旁边隔着一点的桌椅、架子各搬各的。
            bool IsBox(int idx) => items[idx].go != null && items[idx].go.parent != null && items[idx].go.parent.name == "Containers";
            var groupPinned = new bool[items.Count];
            for (int i = 0; i < items.Count; i++) groupPinned[i] = items[i].pinned;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < items.Count; i++)
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        if ((IsBox(i) || IsBox(j)) != (pass == 1) || !Touch(items[i], items[j], pass == 1 ? .05f : .2f)) continue;
                        int a = FindRoot(items, i), c = FindRoot(items, j);
                        if (a == c || pass == 1 && (groupPinned[a] || groupPinned[c])) continue;
                        items[c].up = a;
                        groupPinned[a] |= groupPinned[c];
                    }

            // 每组：合并包围盒，房间取体积最大的那件所在房间
            var groups = new List<RGroup>();
            var groupOf = new int[items.Count];
            var rootToGroup = new Dictionary<int, int>();
            for (int i = 0; i < items.Count; i++)
            {
                groupOf[i] = -1;
                if (items[i].empty) continue;
                int r = FindRoot(items, i);
                if (!rootToGroup.TryGetValue(r, out int gi))
                {
                    gi = groups.Count;
                    rootToGroup[r] = gi;
                    groups.Add(new RGroup { b0 = items[i].b });
                }
                groupOf[i] = gi;
                var g = groups[gi];
                var b = items[i].b;
                g.b0.Encapsulate(b);
                g.pinned |= items[i].pinned;
                g.spawn |= items[i].spawn;
                g.solid |= items[i].solid;
                if (items[i].solid)
                {
                    if (!g.hasSolid) { g.sb0 = b; g.hasSolid = true; }
                    else g.sb0.Encapsulate(b);
                }
                var ir = items[i].room;
                bool key = items[i].solid || b.size.y >= .25f
                    || ir != null && b.min.y > (items[i].pinned ? FloorHeight(ir) : OldFloorHeight(ir)) + .12f;
                if (key)
                {
                    if (!g.hasKey) { g.kb0 = b; g.hasKey = true; }
                    else g.kb0.Encapsulate(b);
                }
                if (items[i].go != null)
                {
                    g.hasGo = true;
                    if (IsBox(i)) g.hasBox = true;
                    if (g.label.Length < 60) g.label += (g.label.Length > 0 ? "+" : "") + items[i].go.name;
                }
                float vol = (b.size.x + .01f) * (b.size.y + .01f) * (b.size.z + .01f);
                if (vol > g.vol) { g.vol = vol; g.room = items[i].room; }
            }
            foreach (var g in groups)
            {
                g.d = GroupDelta(g.b0, g.room, g.pinned);
                g.b = new Bounds(g.b0.center + g.d, g.b0.size);
                g.sb = new Bounds(g.sb0.center + g.d, g.sb0.size);
                g.kb = g.hasKey ? new Bounds(g.kb0.center + g.d, g.kb0.size) : g.b;
                g.pivot = GroupPivot(g);
                // 统一比例；钉住的（已按新尺寸写）、出生点、室外的保持原大
                g.s = g.pinned || g.spawn || g.room == null ? 1f : FurnitureScale;
                if (g.label.Length == 0) g.label = $"{g.room?.id ?? "室外"}陈设({g.b.center.x:0.0},{g.b.center.z:0.0})";
            }

            // 固定的禁区：门口两侧、暗门地毯（放大后不能比原来压得更多）
            var zones = new List<Bounds>();
            foreach (var o in DOps)
                if (!o.window && o.sill <= 0 && o.group != GFence) zones.Add(DoorZone(o, .3f, 1.0f));
            DoorZoneCount = zones.Count; // 后面加的是暗门地毯
            // 门口正前方、楼梯井扶手墙上方的开口前：PlayerDoorwayTests 在这里查通行空间，实心部分一点都不能压
            var strict = new List<Bounds>();
            foreach (var o in DOps)
            {
                if (o.window || o.group == GFence) continue;
                strict.Add(o.sill <= 0 ? DoorFront(o) : GuardZone(o));
            }
            var keep = new List<Bounds>();
            var cand = gp.Find("SecretExitCandidates");
            if (cand != null)
                foreach (Transform c in cand)
                {
                    zones.Add(new Bounds(c.position + Vector3.up, new Vector3(2.1f, 2f, 1.6f)));
                    keep.Add(new Bounds(c.position + Vector3.up, new Vector3(2.5f, 2f, 2.0f)));
                }
            var markers = new List<RMarker>();
            foreach (var (item, p, tested) in markerItems)
                if (groupOf[item] >= 0) markers.Add(new RMarker { g = groupOf[item], p0 = p, tested = tested });
            // 墙体（PlayerDoorwayTests 查容器 / 家具碰撞体不能压进 Wall* 碰撞体）
            var wallBoxes = new List<Bounds>();
            var structure = root.Find("Structure");
            if (structure != null)
                foreach (var c in structure.GetComponentsInChildren<BoxCollider>())
                    if (c.name.StartsWith("Wall")) wallBoxes.Add(BoxWorldBounds(c));

            int n = groups.Count;
            var baseB = new Bounds[n];
            var baseLane = new float[n];
            for (int i = 0; i < n; i++) { baseB[i] = groups[i].b; baseLane[i] = LaneClearance(baseB[i]); }
            var baseZone = new Bounds[markers.Count];
            for (int m = 0; m < markers.Count; m++) baseZone[m] = StandZone(markers[m].p0 + groups[markers[m].g].d);

            // 靠墙摆的推回墙边
            int snapped = 0;
            foreach (var g in groups)
            {
                if (SnapToWalls(g, zones, strict)) snapped++;
                g.snapO = g.o;
            }

            // 推拉门开着时门扇停在门洞旁边的墙面上：按推回墙边后的位置选停靠的一面和方向（避开容器、钉住的陈设），
            // 选好的停靠区之后当成禁区，压到的家具、陈设沿墙让开（见 Conflicts）
            ChooseDoorParking(groups);

            // 还比原来挤的组平移让开（不缩小）
            var now = new Bounds[n];
            void Refresh() { for (int i = 0; i < n; i++) now[i] = groups[i].dropped ? new Bounds(Vector3.one * -999f, Vector3.zero) : groups[i].At(groups[i].s); }

            // 容器换位置时可以先挤开的：落地的纯陈设（不是场景物体、没钉住），之后再给它们另找地方，找不到就撤掉
            bool skipDressing = false;
            bool Evictable(int j)
            {
                var gj = groups[j];
                return skipDressing && !gj.hasGo && !gj.pinned && !gj.spawn && gj.room != null && gj.s > 1f
                       && VerticalClass(gj.b0, gj.room) == 0;
            }

            // 组 i 放在 now[i] 时和别的组、门口、路线、墙、站位还有没有冲突：null = 没有，否则返回原因
            string Conflicts(int i)
            {
                var g = groups[i];
                if (g.dropped) return null;
                string park = ParkConflict(now[i]);
                if (park != null) return park;
                if (g.hasSolid && !g.pinned && g.room != null)
                {
                    var sNow = g.SolidAt(g.s);
                    sNow.Expand(-.01f);
                    foreach (var z in strict)
                        if (sNow.Intersects(z)) return $"压到门口正前方({z.center.x:0.0},{z.center.z:0.0})";
                    if (g.s > 1f)
                    {
                        sNow.Expand(.006f); // 测试只容 0.4 cm
                        foreach (var w in wallBoxes)
                            if (sNow.Intersects(w)) return $"压进墙({w.center.x:0.0},{w.center.z:0.0})";
                    }
                }
                if (g.s > 1f)
                {
                    string e = EscapeReason(g, now[i], baseB[i], zones, baseLane[i], g.inWall);
                    if (e != null) return e;
                }
                for (int j = 0; j < n; j++)
                {
                    if (j == i || groups[j].dropped || (g.s <= 1f && groups[j].s <= 1f) || Evictable(j)) continue;
                    if (Closer(now[i], now[j], baseB[i], baseB[j], .03f)) return "挤到 " + groups[j].label;
                }
                for (int m = 0; m < markers.Count; m++)
                {
                    var mk = markers[m];
                    if (groups[mk.g].dropped) continue;
                    if (mk.g == i)
                    {
                        var zNow = StandZone(g.M.MultiplyPoint3x4(mk.p0));
                        for (int j = 0; j < n; j++)
                        {
                            if (j == i || groups[j].dropped || (g.s <= 1f && groups[j].s <= 1f) || Evictable(j)) continue;
                            float margin = mk.tested && groups[j].solid ? 0f : .05f;
                            if (Closer(zNow, now[j], baseZone[m], baseB[j], margin)) return "交互站位被 " + groups[j].label + " 挡住";
                        }
                    }
                    else if (g.s > 1f || groups[mk.g].s > 1f)
                    {
                        var zNow = StandZone(groups[mk.g].M.MultiplyPoint3x4(mk.p0));
                        float margin = mk.tested && g.solid ? 0f : .05f;
                        if (Closer(zNow, now[i], baseZone[m], baseB[i], margin)) return "挡住 " + groups[mk.g].label + " 的交互站位";
                    }
                }
                return null;
            }

            float[] steps = { .03f, .06f, .1f, .15f, .2f, .25f, .3f, .4f, .5f, .6f, .75f, .9f, 1.1f, 1.3f };
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, c) => groups[a].vol.CompareTo(groups[c].vol)); // 小件先让
            int rounds = 0, nudged = 0;
            var bad = new List<int>();
            for (; rounds < 8; rounds++)
            {
                Refresh();
                bad.Clear();
                foreach (int i in order) if (Conflicts(i) != null) bad.Add(i);
                if (bad.Count == 0) break;
                foreach (int i in bad)
                {
                    var g = groups[i];
                    if (g.pinned || g.spawn || g.room == null || g.s <= 1f) continue;
                    if (Conflicts(i) == null) continue; // 别的组已经让开了
                    var keepO = g.o;
                    bool ok = false;
                    var went = Vector3.zero;
                    foreach (float st in steps)
                    {
                        foreach (var dir in NudgeDirs(g))
                        {
                            g.o = keepO + dir * st;
                            now[i] = g.At(g.s);
                            if (Conflicts(i) == null) { ok = true; went = dir; break; }
                        }
                        if (ok) break;
                    }
                    if (ok)
                    {
                        nudged++;
                        g.note += $"让({g.o.x - keepO.x:0.00},{g.o.z - keepO.z:0.00}) ";
                        LeaveCorner(g, went);
                    }
                    else { g.o = keepO; now[i] = g.At(g.s); }
                }
            }

            // 小步让不开的：沿墙（不贴墙的在附近平面上）找最近的空位，最远 4 m
            int relocated = 0;
            Refresh();
            foreach (int i in order)
            {
                var g = groups[i];
                if (g.pinned || g.spawn || g.room == null || g.s <= 1f || Conflicts(i) == null) continue;
                var keepO = g.o;
                bool ok = false;
                var went = Vector3.zero;
                foreach (var off in WideOffsets(g))
                {
                    g.o = keepO + off;
                    now[i] = g.At(g.s);
                    if (Conflicts(i) == null) { ok = true; went = off; break; }
                }
                if (ok) { relocated++; g.note += $"另找位置({went.x:0.00},{went.z:0.00}) "; LeaveCorner(g, went); }
                else { g.o = keepO; now[i] = g.At(g.s); }
            }

            // 换位置时比 Conflicts 更严：和任何组都隔开 2 cm，不压暗门地毯，自己的交互站位不被占、也不占别人的
            bool RehomeClear(int i)
            {
                var bi = now[i];
                foreach (var kz in keep) if (bi.Intersects(kz)) return false;
                for (int j = 0; j < n; j++)
                    if (j != i && !groups[j].dropped && !Evictable(j) && Sep(bi, now[j]) < .02f) return false;
                for (int m = 0; m < markers.Count; m++)
                {
                    var mk = markers[m];
                    if (groups[mk.g].dropped) continue;
                    var z = StandZone(groups[mk.g].M.MultiplyPoint3x4(mk.p0));
                    if (mk.g != i) { if (z.Intersects(bi)) return false; continue; }
                    for (int j = 0; j < n; j++)
                        if (j != i && !groups[j].dropped && !Evictable(j) && z.Intersects(now[j])) return false;
                }
                return true;
            }
            bool HasTestedMarker(int i)
            {
                foreach (var mk in markers) if (mk.g == i && mk.tested) return true;
                return false;
            }
            // 背面朝哪面墙：有交互点的（容器）按交互点在哪边定正面；其余按实心部分离哪面墙最近
            int BackSide(int i, out float gap)
            {
                var g = groups[i];
                var gaps = SolidWallGaps(g);
                foreach (var mk in markers)
                {
                    if (mk.g != i || mk.tested) continue;
                    var face = g.M.MultiplyPoint3x4(mk.p0) - g.SolidAt(g.s).center;
                    int side = Mathf.Abs(face.x) >= Mathf.Abs(face.z) ? (face.x > 0f ? 0 : 1) : (face.z > 0f ? 2 : 3);
                    gap = gaps[side];
                    return side;
                }
                return NearestWall(g, out gap);
            }
            bool NeedsHome(int i, out int back, out float gapNow, out bool stuck)
            {
                var g = groups[i];
                back = 0; gapNow = 0f; stuck = false;
                if (g.dropped || g.pinned || g.spawn || g.room == null || g.s <= 1f || !g.hasSolid) return false;
                if (VerticalClass(g.b0, g.room) != 0 || HasTestedMarker(i)) return false;
                back = BackSide(i, out gapNow);
                // 避让把靠墙件推远了的，按推回墙边时离墙多远算它是不是靠墙的
                var keepO = g.o;
                g.o = g.snapO;
                float gapSnap = SolidWallGaps(g)[back];
                g.o = keepO;
                if (Mathf.Min(gapNow, gapSnap) >= SnapReach) return false;
                stuck = Conflicts(i) != null;
                if (stuck) return true;
                // 整组已经贴着墙（背后的镜子、搁板、薄件顶着墙）：实心部分离墙远是摆法本身（镜子前的矮凳），近的留给最后再靠墙
                return gapNow > .065f && WallGaps(g, g.At(g.s))[back] > .065f;
            }

            // 推不到墙边的靠墙件（那面墙大半是门洞，放大后塞不进门洞两侧的墙垛），以及上面怎么让都让不开的靠墙件：
            // 在本房间四面墙上找整组能贴墙放下的位置，背面朝墙（换墙就整组转 90° / 180°），离原位近的优先。
            // 先放容器、代码家具（大的先放；先找不用挤开别的东西的空位，没有才挤开落地的纯陈设），再给纯陈设找地方。
            // 带保险箱、仏坛交互点的组（玩法标记）不动。
            int rehomed = 0;
            var homeless = new List<(int i, string why)>();
            Refresh();
            var homeOrder = new List<int>(order);
            homeOrder.Sort((a, c) => groups[a].hasGo != groups[c].hasGo ? (groups[a].hasGo ? -1 : 1) : groups[c].vol.CompareTo(groups[a].vol));
            foreach (int i in homeOrder)
            {
                var g = groups[i];
                if (!NeedsHome(i, out int back, out float gapNow, out bool stuck)) continue;
                var keepO = g.o;
                int keepRot = g.rot;
                var start = g.SolidAt(g.s).center;
                var r = g.room.r;
                var cands = new List<(float dist, int rot, Vector3 o, int side)>();
                for (int k = 0; k < 4; k++)
                {
                    g.rot = k;
                    g.o = Vector3.zero;
                    Bounds B = g.At(g.s), S = g.SolidAt(g.s);
                    int w = back;
                    for (int t = 0; t < k; t++) w = Turn90[w];
                    bool alongZ = w < 2; // ±x 墙上沿 z 滑
                    float perp = w switch
                    {
                        0 => r.xMin + T + SnapGap - B.min.x,
                        1 => r.xMax - T - SnapGap - B.max.x,
                        2 => r.yMin + T + SnapGap - B.min.z,
                        _ => r.yMax - T - SnapGap - B.max.z,
                    };
                    float a0 = alongZ ? r.yMin + T - B.min.z : r.xMin + T - B.min.x;
                    float a1 = alongZ ? r.yMax - T - B.max.z : r.xMax - T - B.max.x;
                    for (float a = a0; a <= a1 + 1e-4f; a += .05f)
                    {
                        var off = alongZ ? new Vector3(perp, 0f, a) : new Vector3(a, 0f, perp);
                        cands.Add(((S.center + off - start).sqrMagnitude, k, off, w));
                    }
                }
                cands.Sort((p, q) => p.dist.CompareTo(q.dist));
                bool ok = false;
                // 容器、代码家具先找不用挤开陈设的空位，实在没有才挤
                for (int pass = g.hasGo ? 0 : 1; pass < 2 && !ok; pass++)
                {
                    skipDressing = pass == 1 && g.hasGo;
                    foreach (var cnd in cands)
                    {
                        g.rot = cnd.rot;
                        g.o = cnd.o;
                        now[i] = g.At(g.s);
                        if (SolidWallGaps(g)[cnd.side] > .065f || Conflicts(i) != null || !RehomeClear(i)) continue;
                        ok = true;
                        rehomed++;
                        g.lockX = cnd.side < 2;
                        g.lockZ = cnd.side >= 2;
                        g.note += $"换到{SideNames[cnd.side]}（转 {90 * cnd.rot}°，原来{SideNames[back]}离墙 {gapNow:0.00}{(stuck ? "、有冲突" : "")}{(skipDressing ? "、挤开陈设" : "")}） ";
                        break;
                    }
                }
                skipDressing = false;
                if (!ok)
                {
                    homeless.Add((i, $"{g.label}（{SideNames[back]}离墙 {gapNow:0.00} m）"));
                    g.rot = keepRot;
                    g.o = keepO;
                    now[i] = g.At(g.s);
                    g.note += $"四面墙都没有能贴墙的空位（{SideNames[back]}离墙 {gapNow:0.00}） ";
                }
            }

            // 实心件离墙还差一点（10–20 cm），是因为背后挂着薄的东西（锅铲、管子、烟道）把整组顶住了：
            // 整组再往墙靠，背后的薄件嵌进墙面 7 cm 以内（从屋里看不出来），实心件和别的东西不能进墙。
            // 四面都看：墙角里的组（一面靠着，另一面被烟道顶住）两面都要贴上
            int closed = 0;
            foreach (int i in order)
            {
                var g = groups[i];
                if (g.dropped || g.pinned || g.spawn || g.room == null || g.s <= 1f || !g.hasSolid) continue;
                if (VerticalClass(g.b0, g.room) != 0 || HasTestedMarker(i)) continue;
                var r = g.room.r;
                bool any = false;
                for (int side = 0; side < 4; side++)
                {
                    float sg = SolidWallGaps(g)[side];
                    if (sg <= .065f || sg >= .25f) continue;
                    var push = (sg - SnapGap) * (side switch { 0 => Vector3.left, 1 => Vector3.right, 2 => Vector3.back, _ => Vector3.forward });
                    var keepO = g.o;
                    float keepIn = g.inWall;
                    g.o += push;
                    bool ok = true;
                    for (int k = 0; k < items.Count && ok; k++)
                    {
                        if (groupOf[k] != i) continue;
                        var mb = g.Map(items[k].b);
                        float into = side switch
                        {
                            0 => r.xMin + T - mb.min.x,
                            1 => mb.max.x - (r.xMax - T),
                            2 => r.yMin + T - mb.min.z,
                            _ => mb.max.z - (r.yMax - T),
                        };
                        if (into > (items[k].solid ? 0f : .07f)) ok = false;
                    }
                    now[i] = g.At(g.s);
                    g.inWall = .075f;
                    if (ok && Conflicts(i) == null) { any = true; g.note += $"{SideNames[side]}再靠墙 {sg - SnapGap:0.00} "; }
                    else { g.o = keepO; g.inWall = keepIn; now[i] = g.At(g.s); }
                }
                if (any) closed++;
            }

            // 实在让不开：纯陈设撤掉，场景物体留着报出来
            var droppedNames = new List<string>();
            var stuckNames = new List<string>();
            Refresh();
            // 先撤能撤的（被容器挤开又没地方去的纯陈设），再看场景物体还有没有冲突
            for (int pass = 0; pass < 2; pass++)
                foreach (int i in order)
                {
                    var g = groups[i];
                    bool droppable = !g.hasGo && !g.pinned && g.s > 1f;
                    if (droppable != (pass == 0)) continue;
                    string why = Conflicts(i);
                    if (why == null) continue;
                    if (droppable)
                    {
                        g.dropped = true;
                        now[i] = new Bounds(Vector3.one * -999f, Vector3.zero);
                        droppedNames.Add($"{g.label}（{why}）");
                    }
                    else stuckNames.Add($"{g.label}（{why}）");
                }
            // 换不了墙、最后也没撤掉的：留在原处，日志里写明
            var stay = new List<string>();
            foreach (var (hi, why) in homeless) if (!groups[hi].dropped) stay.Add(why);

            var xform = new List<Matrix4x4>(kitCount);
            int moved = 0, grown = 0;
            foreach (var g in groups) if (g.s > 1.001f && !g.dropped) grown++;
            for (int i = 0; i < items.Count; i++)
            {
                var M = groupOf[i] < 0 ? Matrix4x4.identity : groups[groupOf[i]].M;
                if (i < kitCount) { xform.Add(M); continue; }
                if (groupOf[i] < 0) continue;
                var t = items[i].go;
                var gi = groups[groupOf[i]];
                t.position = M.MultiplyPoint3x4(t.position);
                t.rotation = Quaternion.Euler(0f, 90f * gi.rot, 0f) * t.rotation;
                t.localScale *= gi.s;
                moved++;
            }

            Occupied = new List<Bounds>(n);
            OccupiedSoft = new List<bool>(n);
            foreach (var g in groups)
            {
                if (g.dropped) continue;
                var b = g.At(g.s);
                Occupied.Add(b);
                OccupiedSoft.Add(!g.solid && b.size.y < .5f);
            }
            KeepOut = keep;
            foreach (var mk in markers) KeepOut.Add(StandZone(groups[mk.g].M.MultiplyPoint3x4(mk.p0)));
            Debug.Log($"[Hako Art] 搬家：{kitCount} 件陈设 + {moved} 个场景物体，{n} 组；统一放大 ×{FurnitureScale:0.00} {grown} 组，" +
                      $"推回墙边 {snapped} 组，平移避让 {nudged} 次（{rounds + 1} 轮），沿墙另找位置 {relocated} 组，" +
                      $"换墙贴放 {rehomed} 组" + (stay.Count > 0 ? $"（{stay.Count} 组本房间四面墙都放不下，留在原处：{string.Join(" ", stay)}）" : "") +
                      $"，背后薄件嵌墙再靠墙 {closed} 组" +
                      (droppedNames.Count > 0 ? $"；放不下撤掉 {droppedNames.Count} 组：{string.Join(" ", droppedNames)}" : "") +
                      (stuckNames.Count > 0 ? $"；仍有冲突 {stuckNames.Count} 组：{string.Join(" ", stuckNames)}" : ""));
            // 容器挪了位置要问用户：把有容器的组的搬家记录打出来
            var boxNotes = new List<string>();
            foreach (var g in groups)
                if (g.hasBox && !string.IsNullOrEmpty(g.note)) boxNotes.Add($"{g.label}：{g.note.Trim()}");
            if (boxNotes.Count > 0) Debug.Log("[Hako Art] 有容器的组的搬家记录：" + string.Join("；", boxNotes));
            return xform;
        }

        /// <summary>墙角里的组沿一面墙滑开之后就离开了另一面墙：那个轴解锁，之后只沿还贴着的墙滑（不会两面都离开、漏出缝）。</summary>
        static void LeaveCorner(RGroup g, Vector3 moved)
        {
            if (!g.lockX || !g.lockZ) return;
            if (Mathf.Abs(moved.x) > Mathf.Abs(moved.z)) g.lockX = false;
            else g.lockZ = false;
        }

        /// <summary>组里实心部分离本房间四面墙面（−x、+x、−z、+z）的距离。</summary>
        static float[] SolidWallGaps(RGroup g) => WallGaps(g, g.SolidAt(g.s));

        /// <summary>包围盒 s 离组所在房间四面墙面（−x、+x、−z、+z）的距离。</summary>
        static float[] WallGaps(RGroup g, Bounds s)
        {
            var r = g.room.r;
            return new[] { s.min.x - (r.xMin + T), r.xMax - T - s.max.x, s.min.z - (r.yMin + T), r.yMax - T - s.max.z };
        }

        /// <summary>实心部分离哪面墙最近（这组的背面）。</summary>
        static int NearestWall(RGroup g, out float gap)
        {
            var ds = SolidWallGaps(g);
            int best = 0;
            for (int k = 1; k < 4; k++) if (ds[k] < ds[best]) best = k;
            gap = ds[best];
            return best;
        }

        /// <summary>整组转 90°（RGroup.rot + 1）后，原来朝 −x / +x / −z / +z 的那一面改朝哪边。</summary>
        static readonly int[] Turn90 = { 3, 2, 0, 1 };

        static List<Vector3> planeOffsets;

        /// <summary>大范围另找位置的候选平移（近的在前）：贴墙的只沿墙，墙角里的沿任一面墙，不贴墙的在 2.5 m 内的平面上。</summary>
        static IEnumerable<Vector3> WideOffsets(RGroup g)
        {
            if (g.lockX || g.lockZ)
            {
                for (float d = .1f; d <= 4.001f; d += .1f)
                {
                    if (!g.lockX || g.lockZ && g.lockX) { yield return new Vector3(d, 0, 0); yield return new Vector3(-d, 0, 0); }
                    if (!g.lockZ || g.lockZ && g.lockX) { yield return new Vector3(0, 0, d); yield return new Vector3(0, 0, -d); }
                }
                yield break;
            }
            if (planeOffsets == null)
            {
                planeOffsets = new List<Vector3>();
                for (float x = -2.5f; x <= 2.501f; x += .15f)
                    for (float z = -2.5f; z <= 2.501f; z += .15f)
                    {
                        var v = new Vector3(x, 0, z);
                        if (v.sqrMagnitude > 1e-4f && v.magnitude <= 2.5f) planeOffsets.Add(v);
                    }
                planeOffsets.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
            }
            foreach (var v in planeOffsets) yield return v;
        }

        /// <summary>
        /// 避让方向：贴墙的轴不动，只沿墙滑；墙角里的（两个轴都贴墙）可以离开其中一面墙、沿另一面滑；
        /// 两个轴都没贴墙的再试斜向。
        /// </summary>
        static IEnumerable<Vector3> NudgeDirs(RGroup g)
        {
            bool corner = g.lockX && g.lockZ;
            if (!g.lockX || corner) { yield return Vector3.right; yield return Vector3.left; }
            if (!g.lockZ || corner) { yield return Vector3.forward; yield return Vector3.back; }
            if (!g.lockX && !g.lockZ)
            {
                const float k = .7071f;
                yield return new Vector3(k, 0, k);
                yield return new Vector3(-k, 0, k);
                yield return new Vector3(k, 0, -k);
                yield return new Vector3(-k, 0, -k);
            }
        }

        /// <summary>
        /// 放大后离墙面 SnapReach 以内的组推回墙边：落地的离墙面 SnapGap，挂墙中段的贴住。
        /// 吊顶的、长押以上的、钉住的、出生点、室外的不动；跨过墙中线的（门口的东西）那个方向不动。
        /// </summary>
        static bool SnapToWalls(RGroup g, List<Bounds> zones, List<Bounds> strict)
        {
            if (g.pinned || g.spawn || g.room == null) return false;
            int v = VerticalClass(g.b0, g.room);
            if (v == 1 || v == 2) return false;
            float gap = v == 0 ? SnapGap : .005f;
            var r = g.room.r;
            var cur = g.At(g.s);
            var key = g.KeyAt(g.s);
            float dx = 0f, dz = 0f;
            if (cur.min.x > r.xMin - .01f && cur.max.x < r.xMax + .01f)
                dx = AxisSnap(cur.min.x - (r.xMin + T), (r.xMax - T) - cur.max.x,
                    key.min.x - (r.xMin + T), (r.xMax - T) - key.max.x, gap, out g.lockX);
            else g.note += "x 超出房间 ";
            if (cur.min.z > r.yMin - .01f && cur.max.z < r.yMax + .01f)
                dz = AxisSnap(cur.min.z - (r.yMin + T), (r.yMax - T) - cur.max.z,
                    key.min.z - (r.yMin + T), (r.yMax - T) - key.max.z, gap, out g.lockZ);
            else g.note += "z 超出房间 ";
            // 推过去会压进门口：先沿墙滑开（最远 1.5 m）再推；滑不开这个方向就不推，也不锁
            var shift = Vector3.zero;
            if (dx != 0f)
            {
                var sx = new Vector3(dx, 0f, 0f);
                string why = SnapBlockedWhy(g, cur, sx, zones, strict);
                if (why == null) shift += sx;
                else if (!g.lockZ && TrySlide(g, cur, sx, Vector3.forward, zones, strict, out var slide)) { shift += sx + slide; g.note += $"x 推墙前沿墙滑 {slide.z:0.00} "; }
                else { g.lockX = false; g.note += $"x 推墙 {dx:0.00} 被挡：{why} "; }
            }
            if (dz != 0f)
            {
                var sz = shift + new Vector3(0f, 0f, dz);
                string why = SnapBlockedWhy(g, cur, sz, zones, strict);
                if (why == null) shift = sz;
                else if (!g.lockX && TrySlide(g, cur, sz, Vector3.right, zones, strict, out var slide)) { shift = sz + slide; g.note += $"z 推墙前沿墙滑 {slide.x:0.00} "; }
                else { g.lockZ = false; g.note += $"z 推墙 {dz:0.00} 被挡：{why} "; }
            }
            g.o += shift;
            return shift != Vector3.zero || g.lockX || g.lockZ;
        }

        /// <summary>推墙会压进门口时，沿墙（along 方向）滑多远能让开；滑完不能出房间。</summary>
        static bool TrySlide(RGroup g, Bounds cur, Vector3 snap, Vector3 along, List<Bounds> zones, List<Bounds> strict, out Vector3 slide)
        {
            var r = g.room.r;
            for (float st = .1f; st <= 1.501f; st += .1f)
                for (int sg = 1; sg >= -1; sg -= 2)
                {
                    slide = along * (st * sg);
                    var b = new Bounds(cur.center + snap + slide, cur.size);
                    if (b.min.x < r.xMin + T || b.max.x > r.xMax - T || b.min.z < r.yMin + T || b.max.z > r.yMax - T) continue;
                    if (!SnapBlocked(g, cur, snap + slide, zones, strict)) return true;
                }
            slide = Vector3.zero;
            return false;
        }

        /// <summary>这一步推墙会不会压进门口：进入原来不在里面的门口禁区，或者实心部分压到门口正前方 / 楼梯井开口前。</summary>
        static bool SnapBlocked(RGroup g, Bounds cur, Vector3 shift, List<Bounds> zones, List<Bounds> strict) =>
            SnapBlockedWhy(g, cur, shift, zones, strict) != null;

        static string SnapBlockedWhy(RGroup g, Bounds cur, Vector3 shift, List<Bounds> zones, List<Bounds> strict)
        {
            var moved = new Bounds(cur.center + shift, cur.size);
            var key = g.KeyAt(g.s);
            var keyMoved = new Bounds(key.center + shift, key.size);
            for (int zi = 0; zi < zones.Count; zi++)
            {
                var z = zones[zi];
                if (zi < DoorZoneCount)
                {
                    // 门口留空区：坐垫、脚垫能踩过去，只看定位件；容 5 cm
                    z.Expand(-2f * DoorZoneSlack);
                    if (g.hasKey && keyMoved.Intersects(z) && !key.Intersects(z)) return "门口留空区" + Rng(z) + " 定位件" + Rng(keyMoved);
                }
                else if (moved.Intersects(z) && !cur.Intersects(z)) return "暗门地毯" + Rng(z) + " 组" + Rng(moved);
            }
            if (!g.hasSolid) return null;
            var s = g.SolidAt(g.s);
            s.center += shift;
            s.Expand(-.01f);
            foreach (var z in strict) if (s.Intersects(z)) return "门洞正前方" + Rng(z) + " 实心" + Rng(s);
            return null;
        }

        static string Rng(Bounds b) => $"(x {b.min.x:0.00}..{b.max.x:0.00}, z {b.min.z:0.00}..{b.max.z:0.00})";

        /// <summary>门洞正前方：门宽 + 两侧各 3 cm，离墙线 0.45 m 以内（PlayerDoorwayTests 在离地板边 0.34 m 以内查通行空间）。</summary>
        static Bounds DoorFront(Opening o)
        {
            float y0 = Levels[o.group].wallBase - .1f, y1 = Levels[o.group].floorTop + DoorTop;
            return LineBox(o.alongX, o.line, o.center - o.width / 2 - .03f, o.center + o.width / 2 + .03f, y0, y1, .45f);
        }

        /// <summary>楼梯井扶手墙上方的开口：离墙线 0.45 m 以内不放实心家具（PlayerDoorwayTests 把它当出口检查通行空间）。</summary>
        static Bounds GuardZone(Opening o)
        {
            float y0 = Levels[o.group].wallBase - .1f, y1 = Levels[o.group].wallBase + 2.7f;
            return LineBox(o.alongX, o.line, o.center - o.width / 2 - .05f, o.center + o.width / 2 + .05f, y0, y1, .45f);
        }

        /// <summary>
        /// lo / hi 为整组离两侧墙面的距离，klo / khi 为组里定位件的；返回沿该轴的平移（贴近的那面墙）。
        /// 定位件离墙远的（只是桌边的坐垫这类矮件靠近墙）不推。
        /// </summary>
        static float AxisSnap(float lo, float hi, float klo, float khi, float gap, out bool locked)
        {
            bool toLo = lo <= hi;
            float d = toLo ? lo : hi;
            locked = d < SnapReach && (toLo ? klo : khi) < SnapReach;
            if (!locked) return 0f;
            // 正数 = 离开这面墙，负数 = 靠过去
            float move;
            if (d < 0f) move = gap - d;                 // 嵌进墙里：推出来
            else if (d > gap + .005f) move = gap - d;   // 离墙太远：推过去
            else return 0f;                            // 已经贴着
            return toLo ? move : -move;
        }

        /// <summary>门洞两侧的通行区：沿墙比门宽 alongPad，向两侧各 depth，高到门顶上一点。</summary>
        static Bounds DoorZone(Opening o, float alongPad, float depth)
        {
            float y0 = Levels[o.group].wallBase - .1f, y1 = Levels[o.group].wallBase + 2.7f;
            return LineBox(o.alongX, o.line, o.center - o.width / 2 - alongPad, o.center + o.width / 2 + alongPad, y0, y1, depth);
        }

        /// <summary>以墙线为中心、向两侧各 depth 的盒子；s0..s1 沿墙。</summary>
        static Bounds LineBox(bool alongX, float line, float s0, float s1, float y0, float y1, float depth) =>
            alongX
                ? new Bounds(new Vector3((s0 + s1) / 2, (y0 + y1) / 2, line), new Vector3(s1 - s0, y1 - y0, 2 * depth))
                : new Bounds(new Vector3(line, (y0 + y1) / 2, (s0 + s1) / 2), new Vector3(2 * depth, y1 - y0, s1 - s0));

        /// <summary>两个包围盒的间距（分离时为正，相交时为负的最小穿透）。</summary>
        static float Sep(Bounds a, Bounds b) => Mathf.Max(
            Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x),
            Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y),
            Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));

        /// <summary>放大后两者的间距比 min(原间距, margin) 还小。</summary>
        static bool Closer(Bounds a, Bounds b, Bounds a0, Bounds b0, float margin) =>
            Sep(a, b) < Mathf.Min(Sep(a0, b0), margin) - 1e-4f;

        static readonly string[] SideNames = { "-x 墙", "+x 墙", "-z 墙", "+z 墙", "天花" };

        /// <summary>放大后碰到门口 / 暗门禁区、离步行路线更近、或者伸出房间 / 顶到天花：返回原因，没有就是 null。</summary>
        static string EscapeReason(RGroup g, Bounds cur, Bounds bas, List<Bounds> zones, float baseLane, float outSlack = 0f)
        {
            var key = g.KeyAt(g.s);
            for (int zi = 0; zi < zones.Count; zi++)
            {
                var z = zones[zi];
                if (zi < DoorZoneCount)
                {
                    // 门口留空区只管挡路的定位件（坐垫、脚垫能踩过去），比原来多压 5 cm 以内不算
                    if (g.hasKey && Sep(key, z) < Mathf.Min(Sep(g.kb, z), 0f) - DoorZoneSlack)
                        return $"门口留空区({z.center.x:0.0},{z.center.z:0.0})";
                }
                else if (Closer(cur, z, bas, z, 0f)) return $"暗门地毯({z.center.x:0.0},{z.center.z:0.0})";
            }
            float lane = LaneClearance(cur), need = Mathf.Min(baseLane, .45f);
            if (lane < need - 1e-4f) return $"离步行路线 {lane:0.00} m（要 {need:0.00}）";
            var r = g.room.r;
            float ceil = Levels[g.room.group].wallTop - .02f;
            for (int side = 0; side < 5; side++)
                if (Over(cur, side) > Mathf.Max(0f, Over(bas, side)) + .002f + (side < 4 ? outSlack : 0f)) return $"伸出{SideNames[side]} {Over(cur, side):0.00} m";
            return null;

            float Over(Bounds b, int side) => side switch
            {
                0 => r.xMin + T - b.min.x,
                1 => b.max.x - (r.xMax - T),
                2 => r.yMin + T - b.min.z,
                3 => b.max.z - (r.yMax - T),
                _ => b.max.y - ceil,
            };
        }

        /// <summary>包围盒在平面上离最近一条（高度相关的）步行路线多远。</summary>
        static float LaneClearance(Bounds b)
        {
            float best = 99f;
            var rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
            for (int i = 0; i + 1 < WalkLanes.Length; i += 2)
            {
                Vector3 a = WalkLanes[i], c = WalkLanes[i + 1];
                if (b.max.y < Mathf.Min(a.y, c.y) - .1f || b.min.y > Mathf.Max(a.y, c.y) + 1.75f) continue;
                best = Mathf.Min(best, SegRectDist(new Vector2(a.x, a.z), new Vector2(c.x, c.z), rect));
            }
            return best;
        }

        static float SegRectDist(Vector2 a, Vector2 b, Rect r)
        {
            if (SegHitsRect(a, b, r)) return 0f;
            float d = Mathf.Min(PtRectDist(a, r), PtRectDist(b, r));
            d = Mathf.Min(d, PtSegDist(new Vector2(r.xMin, r.yMin), a, b));
            d = Mathf.Min(d, PtSegDist(new Vector2(r.xMax, r.yMin), a, b));
            d = Mathf.Min(d, PtSegDist(new Vector2(r.xMin, r.yMax), a, b));
            return Mathf.Min(d, PtSegDist(new Vector2(r.xMax, r.yMax), a, b));
        }

        static bool SegHitsRect(Vector2 a, Vector2 b, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            var d = b - a;
            bool Clip(float p, float q)
            {
                if (Mathf.Abs(p) < 1e-7f) return q >= 0f;
                float t = q / p;
                if (p < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
                return true;
            }
            return Clip(-d.x, a.x - r.xMin) && Clip(d.x, r.xMax - a.x) && Clip(-d.y, a.y - r.yMin) && Clip(d.y, r.yMax - a.y);
        }

        static float PtRectDist(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float PtSegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// 放大中心：贴墙的一侧（离内墙面 1.2 m 以内）不动，否则取中心；
        /// 垂直方向落地的底面不动、吊顶的顶面不动、墙面中段的取中心。
        /// </summary>
        static Vector3 GroupPivot(RGroup g)
        {
            var b = g.b;
            if (g.room == null) return b.center;
            var r = g.room.r;
            float x = PivotAxis(b.min.x, b.max.x, b.center.x, r.xMin + T, r.xMax - T);
            float z = PivotAxis(b.min.z, b.max.z, b.center.z, r.yMin + T, r.yMax - T);
            int v = VerticalClass(g.b0, g.room);
            float y = v == 1 ? b.max.y : v == 3 ? b.center.y : b.min.y;
            return new Vector3(x, y, z);
        }

        static float PivotAxis(float lo, float hi, float c, float wallLo, float wallHi)
        {
            float dLo = lo - wallLo, dHi = wallHi - hi;
            if (Mathf.Min(dLo, dHi) > FitMargin) return c;
            return dLo <= dHi ? lo : hi;
        }

        static Vector3 GroupDelta(Bounds b, Room room, bool pinned)
        {
            if (room == null)
            {
                float k = OutdoorScale - 1f;
                float lift = !pinned && b.min.y > 1.5f ? Levels[G0].wallTop - OldLevels[G0].wallTop : 0f;
                return new Vector3(b.center.x * k, lift, b.center.z * k);
            }
            float dx = AxisDelta(b.min.x, b.max.x, b.center.x, room.r0.xMin, room.r0.xMax, room.r.xMin, room.r.xMax);
            float dz = AxisDelta(b.min.z, b.max.z, b.center.z, room.r0.yMin, room.r0.yMax, room.r.yMin, room.r.yMax);

            // 门洞正中的东西沿墙方向跟门走
            if (DOps != null)
                foreach (var o in DOps)
                {
                    if (o.group != room.group || o.window) continue;
                    float perp = o.alongX ? b.center.z : b.center.x, along = o.alongX ? b.center.x : b.center.z;
                    if (Mathf.Abs(perp - o.line0) > .9f || Mathf.Abs(along - o.center0) > .12f) continue;
                    if (o.alongX) dx = o.center - o.center0;
                    else dz = o.center - o.center0;
                }
            float dy = pinned ? FloorHeight(room) - OldFloorHeight(room) : VerticalDelta(b, room);
            return new Vector3(dx, dy, dz);
        }

        static float AxisDelta(float lo, float hi, float c, float a0, float b0, float a1, float b1)
        {
            float m = Mathf.Min(FitMargin, (b0 - a0) * .35f);
            float dLo = lo - a0, dHi = b0 - hi;
            float anchor = dLo <= m || dHi <= m ? (dLo <= dHi ? lo : hi) : c;
            return Stretch(anchor, a0, b0, a1, b1) - anchor;
        }

        /// <summary>0 落地，1 吊在天花下，2 长押以上，3 墙面中段（垂直方向仍跟地板）。</summary>
        static int VerticalClass(Bounds b, Room room)
        {
            int g = room.group;
            float f0 = OldFloorHeight(room), c0 = OldLevels[g].wallTop;
            float k0 = (g == G0 ? .55f : OldLevels[g].floorTop) + OldDoorTop;
            if (b.min.y <= f0 + .12f) return 0;
            if (b.max.y >= c0 - .35f) return 1;
            if (b.min.y >= k0 - .2f) return 2;
            return 3;
        }

        /// <summary>落地的跟地板；吊在天花下的跟天花；长押以上的跟门楣。</summary>
        static float VerticalDelta(Bounds b, Room room)
        {
            int g = room.group;
            switch (VerticalClass(b, room))
            {
                case 1: return Levels[g].wallTop - OldLevels[g].wallTop;
                case 2:
                    float k0 = (g == G0 ? .55f : OldLevels[g].floorTop) + OldDoorTop;
                    float k1 = (g == G0 ? .55f : Levels[g].floorTop) + DoorTop;
                    return k1 - k0;
                default: return FloorHeight(room) - OldFloorHeight(room);
            }
        }
    }
}
