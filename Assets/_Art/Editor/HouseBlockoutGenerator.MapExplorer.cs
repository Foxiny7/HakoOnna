using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 3D 地图的探索者视图（方案见 Docs/HakoOnna_ExplorerMap_Proposal.md，2026-11-26 交付），挂在 HouseMapProxy 上：
    /// <list type="bullet">
    /// <item>“你在这里”：每个有地板的房间一块红色高亮地板（roomHighlights），平时关着。</item>
    /// <item>一楼那一组下面的 ExplorerOnly 节点，平时关着：院子地面、围墙（大门处留口）、三道竹篱笆、
    ///   三个出口（门洞里一条白色门槛 + 门外一个尖朝外的白箭头）、金庫、仏壇。</item>
    /// <item>出口、金庫、仏壇的名字挂点（landmarks / landmarkLabels），和房间锚点一样在地面高度。</item>
    /// </list>
    /// 全部用内置立方体，不新增网格，细节网格文件的内容不会跟着变。位置都从已经生成好的场景里取：
    /// 地图在 BuildDetails 的最后才建，这时家具搬家、保险箱模型都已经到位。
    /// 容器的小方块不在这里改：探索者视图里的黄色 / 绿色由 HouseMapProxy.SetExplorerView 和玩法上色。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>院子地面的顶面高度、围墙和竹篱笆在地图里的高度和厚度（比房子的矮墙厚：远看也要看得出来）。</summary>
        const float MapGroundTop = -.08f, MapFenceHeight = .45f, MapYardWallThick = .16f, MapBambooThick = .2f;

        static int MapGroup(Room r) => r.group == G1 ? 1 : r.group == GKura ? 2 : 0;

        static Renderer MapCube(Transform parent, string name, Vector3 localPos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = name;
            var t = cube.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = rot ?? Quaternion.identity;
            t.localScale = scale;
            var mr = cube.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        /// <summary>沿墙线的一段矮墙。坐标系同 WallBox：s 沿墙，d 是离墙线往室内的距离（负值在室外）。</summary>
        static void MapWallRun(Transform parent, string name, Edge e, float s0, float s1, float y0, float y1, float d0, float d1, Material m)
        {
            if (s1 - s0 < .01f) return;
            var c = OnWall(e, (s0 + s1) / 2, (y0 + y1) / 2, (d0 + d1) / 2);
            float dd = Mathf.Abs(d1 - d0);
            MapCube(parent, name, c, e.alongX ? new Vector3(s1 - s0, y1 - y0, dd) : new Vector3(dd, y1 - y0, s1 - s0), m);
        }

        static bool TryRendererBounds(Transform t, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }

        static void BuildMapExplorerView(HouseMapProxy proxy, Transform gameplay, List<Room> rooms)
        {
            // 颜色避开探索者视图里已经有意思的颜色：容器黄 / 绿、你在这里红、楼梯线蓝
            var hereMat = MapMat("MP_MapHere", new Color(.86f, .24f, .2f));
            var groundMat = MapMat("MP_MapGround", new Color(.09f, .1f, .095f));
            var fenceMat = MapMat("MP_MapFence", new Color(.42f, .45f, .48f));
            var bambooMat = MapMat("MP_MapBamboo", new Color(.6f, .5f, .3f));
            var exitMat = MapMat("MP_MapExit", new Color(.95f, .95f, .9f));
            var safeMat = MapMat("MP_MapSafe", new Color(.25f, .8f, .9f));
            var altarMat = MapMat("MP_MapAltar", new Color(.78f, .5f, .98f));

            // ---- 你在这里：每个有地板的房间一块高亮地板（盖在地板上 2 cm 厚，四边让出矮墙）----
            proxy.roomHighlights = new Renderer[rooms.Count];
            float inset = MapWallThick + .02f;
            int plates = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (!room.hasFloor) continue;
                var plate = MapCube(proxy.groups[MapGroup(room)], "Here_" + room.id,
                    new Vector3(room.r.center.x, FloorY(room) + .012f, room.r.center.y),
                    new Vector3(Mathf.Max(.2f, room.r.width - 2 * inset), .02f, Mathf.Max(.2f, room.r.height - 2 * inset)), hereMat);
                plate.gameObject.SetActive(false);
                proxy.roomHighlights[i] = plate;
                plates++;
            }

            var extra = ArtKit.Empty(proxy.groups[0], "ExplorerOnly", Vector3.zero);
            var landmarks = new List<Transform>();
            var labels = new List<string>();
            void Landmark(string label, Vector3 p)
            {
                landmarks.Add(ArtKit.Empty(extra, "Label_" + label, p));
                labels.Add(label);
            }

            // ---- 院子地面 + 围墙（大门处留口；和 FenceAndGate 用同一套边和开口）----
            var fence = RoomById("Fence");
            int fenceRuns = 0;
            if (fence != null)
            {
                var fr = fence.r;
                MapCube(extra, "Yard_Ground", new Vector3(fr.center.x, MapGroundTop - .02f, fr.center.y), new Vector3(fr.width, .04f, fr.height), groundMat);
                foreach (var e in EdgesOf(fence))
                {
                    var cuts = GapsOn(fence, e).Select(g => new Vector2(g.a, g.b)).ToList();
                    foreach (var run in Minus(e.from, e.to, cuts))
                        MapWallRun(extra, $"Yard_Wall_{fenceRuns++:00}", e, run.x, run.y, MapGroundTop, MapGroundTop + MapFenceHeight, 0f, MapYardWallThick, fenceMat);
                }
            }
            else Debug.LogWarning("[Hako Art] 3D 地图：找不到围墙（Fence），探索者视图里没有院子");

            // ---- 竹篱笆：把院子分成门前、后院、只能看的主庭园（范围取真篱笆的碰撞体）----
            int bamboo = 0;
            var fenceColliders = gameplay.parent.Find("Structure/GardenFences");
            if (fenceColliders != null)
                foreach (var c in fenceColliders.GetComponentsInChildren<BoxCollider>())
                {
                    var b = BoxWorldBounds(c);
                    var size = b.size.x >= b.size.z
                        ? new Vector3(b.size.x, MapFenceHeight, MapBambooThick)
                        : new Vector3(MapBambooThick, MapFenceHeight, b.size.z);
                    MapCube(extra, $"Yard_Bamboo_{bamboo++}", new Vector3(b.center.x, MapGroundTop + MapFenceHeight / 2, b.center.z), size, bambooMat);
                }
            if (bamboo == 0) Debug.LogWarning("[Hako Art] 3D 地图：找不到竹篱笆的碰撞体（Structure/GardenFences）");

            // ---- 出口：一楼不是窗的开口里，只有一个房间挨着的（另一边是室外），再加上围墙的大门 ----
            // 钉死的旧门不在 DOps 里，玻璃拉门和窗是 window，所以这里只会找到真能走出去的门
            var exitNames = new List<string>();
            foreach (var o in DOps)
            {
                if (o.window || (o.group != G0 && o.group != GFence)) continue;
                Room owner = null;
                Edge edge = default;
                int faced = 0;
                foreach (var r in DRooms)
                {
                    if (r.group != o.group) continue;
                    foreach (var e in EdgesOf(r))
                        if (e.alongX == o.alongX && Mathf.Abs(e.line - o.line) < .01f && o.center > e.from && o.center < e.to)
                        {
                            faced++;
                            owner = r;
                            edge = e;
                        }
                }
                if (faced != 1) continue;
                string label = owner.id == "Genkan" ? "玄関口" : owner.id == "Daidokoro" ? "勝手口" : owner.group == GFence ? "表門" : "出口";
                exitNames.Add($"{label}({owner.id})");
                float half = o.width / 2;
                bool gate = owner.group == GFence;
                float fl = gate ? MapGroundTop : FloorY(owner);
                // 门槛：门洞里横一条白条（和两边的墙一样厚）
                MapWallRun(extra, $"Exit_{owner.id}_Sill", edge, o.center - half + .05f, o.center + half - .05f, fl, fl + .05f,
                    -.03f, (gate ? MapYardWallThick : MapWallThick) + .03f, exitMat);
                // 门外一个尖朝外的箭头（两根斜放的条）
                float y = MapGroundTop + .04f;
                var tip = OnWall(edge, o.center, y, -2f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var end = OnWall(edge, o.center + side * Mathf.Min(half * .9f, 1.1f), y, -.8f);
                    var dir = end - tip;
                    MapCube(extra, $"Exit_{owner.id}_Arrow{(side < 0 ? "L" : "R")}", (tip + end) / 2,
                        new Vector3(.26f, .08f, dir.magnitude + .13f), exitMat, Quaternion.LookRotation(dir.normalized, Vector3.up));
                }
                Landmark(label, OnWall(edge, o.center, MapGroundTop, -1.4f));
            }
            if (exitNames.Count != 3)
                Debug.LogWarning($"[Hako Art] 3D 地图：出口应该是 3 个（玄関口、勝手口、表門），找到 {exitNames.Count} 个：{string.Join(" ", exitNames)}");

            // ---- 金庫（居間北墙）----
            string safeInfo = "没找到";
            var safe = gameplay.Find("Safe_Kinko");
            if (safe != null && TryRendererBounds(safe, out var sb))
            {
                var room = RoomAt(sb.center, G0);
                float fl = room != null ? FloorY(room) : sb.min.y;
                MapCube(extra, "Map_Safe", new Vector3(sb.center.x, fl + .4f, sb.center.z),
                    new Vector3(Mathf.Max(.55f, sb.size.x), .8f, Mathf.Max(.55f, sb.size.z)), safeMat);
                Landmark("金庫", new Vector3(sb.center.x, fl, sb.center.z));
                safeInfo = $"{room?.id} ({sb.center.x:F2}, {sb.center.z:F2})";
            }
            else Debug.LogWarning("[Hako Art] 3D 地图：找不到保险箱（Gameplay/Safe_Kinko）");

            // ---- 仏壇（仏間东墙）：只写“仏壇”，超度怎么做还没定（V1.3 待定第 8 项）----
            string altarInfo = "没找到";
            var altar = gameplay.Find("MemorialPoint_Butsudan");
            if (altar != null)
            {
                Vector3 c, size;
                if (TryRendererBounds(altar, out var ab)) { c = ab.center; size = ab.size; }
                else { c = altar.position; size = new Vector3(1.2f, 1.6f, .6f); }
                var room = RoomAt(c, G0);
                float fl = room != null ? FloorY(room) : c.y;
                MapCube(extra, "Map_Altar", new Vector3(c.x, fl + .5f, c.z),
                    new Vector3(Mathf.Max(.6f, size.x * .9f), 1f, Mathf.Max(.45f, size.z * .9f)), altarMat);
                Landmark("仏壇", new Vector3(c.x, fl, c.z));
                altarInfo = $"{room?.id} ({c.x:F2}, {c.z:F2})";
            }
            else Debug.LogWarning("[Hako Art] 3D 地图：找不到仏壇（Gameplay/MemorialPoint_Butsudan）");

            proxy.explorerOnly = new[] { extra.gameObject };
            proxy.landmarks = landmarks.ToArray();
            proxy.landmarkLabels = labels.ToArray();
            extra.gameObject.SetActive(false);
            Debug.Log($"[Hako Art] 3D 地图（探索者视图）：高亮地板 {plates} 块，围墙 {fenceRuns} 段，竹篱笆 {bamboo} 段，" +
                      $"出口 {exitNames.Count} 个（{string.Join(" ", exitNames)}），金庫 {safeInfo}，仏壇 {altarInfo}");
        }
    }
}
