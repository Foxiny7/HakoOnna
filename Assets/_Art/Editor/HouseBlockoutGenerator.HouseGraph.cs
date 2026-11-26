using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 给玩法用的三样东西（2026-11-22，转移重做 V1.3 模块 T）：
    /// <list type="bullet">
    /// <item><b>HouseGraph</b>：房间、相邻关系、每个容器在哪个房间（Gameplay/HouseGraph）。</item>
    /// <item><b>RoomVantage</b>：每个有容器的房间一个俯视机位（Gameplay/RoomVantages/Vantage_房间名）。</item>
    /// <item><b>HouseMapProxy</b>：3D 地图用的简化房子（Gameplay/HouseMapProxy，生成后是关着的，放在真房子正下方 300 m）。</item>
    /// </list>
    /// 相邻关系：同层的按房间范围算（共用一面墙，或隔着一段没有容器的走廊正对着）；楼梯和后院是手写的三条。
    /// 算出来应该和程序那边原来手写的 26 条一致，日志里会列出来。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>楼梯、后院这些不能从房间范围推出来的连接。</summary>
        static readonly (string a, string b, HouseGraph.LinkKind kind)[] SpecialLinks =
        {
            ("Daidokoro", "Monooki", HouseGraph.LinkKind.Stairs), // 裏階段
            ("Oku", "Kodomo", HouseGraph.LinkKind.Stairs),        // 主階段
            ("Furo", "Kura", HouseGraph.LinkKind.Yard),           // 隔着后院
            // 2026-11-22 用户定：台所后门出去就是后院和土蔵（不然土蔵只连着風呂，是个死胡同）
            ("Daidokoro", "Kura", HouseGraph.LinkKind.Yard),
        };

        const float MapWallHeight = .7f, MapWallThick = .09f, LinkMinOverlap = .5f;

        static Material MapMat(string name, Color color)
        {
            string path = $"{DetailMats.Dir}/{name}.mat";
            // 自己的纯色着色器（不受雾）；万一没导入成功就退回 URP 的 Unlit
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Art/Shaders/HakoMapUnlit.shader");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>a、b 两个范围是不是贴着同一条边；返回贴边的方向（0 = b 在 a 的北边，1 = 南，2 = 东，3 = 西）和重叠的区间。</summary>
        static bool Touch(Rect a, Rect b, out int side, out Vector2 span)
        {
            const float eps = .02f;
            side = -1;
            span = default;
            if (Mathf.Abs(a.yMax - b.yMin) < eps) side = 0;
            else if (Mathf.Abs(a.yMin - b.yMax) < eps) side = 1;
            else if (Mathf.Abs(a.xMax - b.xMin) < eps) side = 2;
            else if (Mathf.Abs(a.xMin - b.xMax) < eps) side = 3;
            else return false;
            span = side < 2
                ? new Vector2(Mathf.Max(a.xMin, b.xMin), Mathf.Min(a.xMax, b.xMax))
                : new Vector2(Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.yMax, b.yMax));
            return span.y - span.x > 0f;
        }

        static void BuildHouseGraph(Transform root)
        {
            var gameplay = root.Find("Gameplay");
            var visuals = root.Find("Containers").GetComponentsInChildren<ContainerVisual>();
            var rooms = DRooms.Where(r => r.group != GFence).ToList();
            int Index(string id) => rooms.FindIndex(r => r.id == id);

            // ---- 容器 → 房间（按名字前缀；再用位置核对一遍）----
            var containerRoom = new int[visuals.Length];
            var has = new bool[rooms.Count];
            for (int i = 0; i < visuals.Length; i++)
            {
                string name = visuals[i].transform.parent.name;
                int ri = Index(name.Split('_')[0]);
                var c = visuals[i].GetComponentInChildren<Collider>().bounds.center;
                if (ri < 0) Debug.LogWarning($"[Hako Art] HouseGraph：容器 {name} 的名字对不上任何房间");
                else
                {
                    has[ri] = true;
                    if (!rooms[ri].r.Contains(new Vector2(c.x, c.z)))
                        Debug.LogWarning($"[Hako Art] HouseGraph：容器 {name} 的位置不在 {rooms[ri].id} 的范围里");
                }
                containerRoom[i] = ri;
            }

            // ---- 相邻关系 ----
            var links = new List<HouseGraph.Link>();
            void Add(int a, int b, HouseGraph.LinkKind kind)
            {
                if (a < 0 || b < 0 || a == b) return;
                if (links.Any(l => (l.a == a && l.b == b) || (l.a == b && l.b == a))) return;
                links.Add(new HouseGraph.Link { a = Mathf.Min(a, b), b = Mathf.Max(a, b), kind = kind });
            }
            for (int a = 0; a < rooms.Count; a++)
                for (int b = a + 1; b < rooms.Count; b++)
                {
                    if (!has[a] || !has[b] || rooms[a].group != rooms[b].group) continue;
                    // 共用一面墙
                    if (Touch(rooms[a].r, rooms[b].r, out _, out var span) && span.y - span.x >= LinkMinOverlap)
                    {
                        Add(a, b, HouseGraph.LinkKind.Wall);
                        continue;
                    }
                    // 隔着一段没有容器的走廊正对着
                    for (int c = 0; c < rooms.Count; c++)
                    {
                        var mid = rooms[c];
                        if (has[c] || mid.group != rooms[a].group || !mid.hasFloor || IsStairRoom(mid) || mid.id == "Kaidan" || mid.id == "UraKaidan") continue;
                        if (!Touch(rooms[a].r, mid.r, out int sa, out var pa) || !Touch(rooms[b].r, mid.r, out int sb, out var pb)) continue;
                        if ((sa ^ 1) != sb) continue; // 要在走廊的两对面
                        if (Mathf.Min(pa.y, pb.y) - Mathf.Max(pa.x, pb.x) >= LinkMinOverlap) Add(a, b, HouseGraph.LinkKind.Wall);
                    }
                }
            foreach (var (a, b, kind) in SpecialLinks) Add(Index(a), Index(b), kind);

            // ---- 组件 ----
            var node = ArtKit.Empty(gameplay, "HouseGraph", Vector3.zero);
            var graph = node.gameObject.AddComponent<HouseGraph>();
            var vantages = ArtKit.Empty(gameplay, "RoomVantages", Vector3.zero);
            graph.containers = visuals;
            graph.containerRoom = containerRoom;
            graph.links = links.ToArray();
            graph.rooms = new HouseGraph.RoomInfo[rooms.Count];
            // 挑机位要打射线看“真的看得见多少”：临时给没有碰撞体的家具、陈设加 MeshCollider，挑完就删（和污渍那一步同一个办法）
            var probes = AddProbeColliders(root);
            bool backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    var r = rooms[i];
                    graph.rooms[i] = new HouseGraph.RoomInfo
                    {
                        id = r.id, label = r.label,
                        floor = r.group == G1 ? 1 : 0,
                        building = r.group == GKura ? 1 : 0,
                        center = new Vector3(r.r.center.x, FloorY(r), r.r.center.y),
                        size = r.r.size,
                        ceilingY = CeilY(r),
                        hasContainers = has[i],
                        vantage = has[i] ? RoomVantage(vantages, r, visuals, containerRoom, i) : null,
                    };
                }
            }
            finally
            {
                foreach (var c in probes)
                    if (c != null) Object.DestroyImmediate(c);
                Physics.queriesHitBackfaces = backfaces;
                Physics.SyncTransforms();
            }

            BuildMapProxy(gameplay, graph, rooms);

            // ---- 日志：给程序核对 ----
            var names = links.OrderBy(l => l.kind).ThenBy(l => rooms[l.a].id).Select(l =>
                $"{rooms[l.a].id}-{rooms[l.b].id}{(l.kind == HouseGraph.LinkKind.Wall ? "" : l.kind == HouseGraph.LinkKind.Stairs ? "(楼梯)" : "(后院)")}");
            int maxHops = 0;
            for (int a = 0; a < rooms.Count; a++)
                for (int b = 0; b < rooms.Count; b++)
                    if (has[a] && has[b])
                    {
                        int h = graph.Hops(a, b);
                        if (h < 0) Debug.LogWarning($"[Hako Art] HouseGraph：{rooms[a].id} 到 {rooms[b].id} 走不通");
                        maxHops = Mathf.Max(maxHops, h);
                    }
            Debug.Log($"[Hako Art] HouseGraph：房间 {rooms.Count} 个（有容器的 {has.Count(x => x)} 个），容器 {visuals.Length} 个，相邻 {links.Count} 条，最远 {maxHops} 跳：{string.Join(" ", names)}");
        }

        /// <summary>
        /// 房间的俯视机位：四个天花板角里挑一个——尽量正对着容器的正面，而且机位不在高柜子（壁橱、衣柜）里面。朝向房间中心偏下。
        /// </summary>
        static Transform RoomVantage(Transform parent, Room room, ContainerVisual[] visuals, int[] containerRoom, int roomIndex)
        {
            const float inset = .5f;
            float floor = FloorY(room);
            float y = Mathf.Min(CeilY(room) - .4f, floor + 2.7f);
            var r = room.r;
            var target = new Vector3(r.center.x, floor + .7f, r.center.y);
            float x0 = r.xMin + T + inset, x1 = r.xMax - T - inset, z0 = r.yMin + T + inset, z1 = r.yMax - T - inset;

            // 候选机位：四个角 + 四面墙的正中。细长的房间（浴室、缘侧）优先从短边正中顺着长边看，
            // 不然站在角上半个画面都是贴脸的墙。
            bool longX = r.width > r.height * 1.7f, longZ = r.height > r.width * 1.7f;
            var spots = new List<(Vector3 p, float bias)>();
            float corner = longX || longZ ? -1.5f : 0f;
            foreach (float cx in new[] { x0, x1 })
                foreach (float cz in new[] { z0, z1 })
                    spots.Add((new Vector3(cx, y, cz), corner));
            // 细长房间长边的正中不要：离房间中心只有一步远，镜头会几乎垂直朝下看地板
            if (!longZ)
            {
                spots.Add((new Vector3(x0, y, r.center.y), longX ? 0f : -.4f));
                spots.Add((new Vector3(x1, y, r.center.y), longX ? 0f : -.4f));
            }
            if (!longX)
            {
                spots.Add((new Vector3(r.center.x, y, z0), longZ ? 0f : -.4f));
                spots.Add((new Vector3(r.center.x, y, z1), longZ ? 0f : -.4f));
            }

            // 会挡镜头的东西：这个房间里的容器，和会挡路的高家具（Dressing_Blockers）
            var solids = new List<Bounds>();
            for (int i = 0; i < visuals.Length; i++)
                if (containerRoom[i] == roomIndex) solids.Add(visuals[i].GetComponentInChildren<Collider>().bounds);
            if (BlockerRoot != null)
                foreach (var c in BlockerRoot.GetComponentsInChildren<BoxCollider>())
                {
                    var b = BoxWorldBounds(c);
                    if (r.Contains(new Vector2(b.center.x, b.center.z)) && b.min.y < y && b.max.y > floor + .5f) solids.Add(b);
                }

            Vector3 best = default;
            float bestScore = float.NegativeInfinity;
            foreach (var (p, bias) in spots)
            {
                float score = bias;
                var view = target - p;
                view.y = 0f;
                view.Normalize();
                // 尽量正对着容器的正面
                for (int i = 0; i < visuals.Length; i++)
                {
                    if (containerRoom[i] != roomIndex) continue;
                    var front = visuals[i].transform.parent.Find("InteractPoint");
                    var to = p - visuals[i].GetComponentInChildren<Collider>().bounds.center;
                    to.y = 0f;
                    if (front != null && to.sqrMagnitude > .01f) score += Vector3.Dot(front.forward, to.normalized);
                }
                foreach (var b in solids)
                {
                    float tall = b.max.y - floor;
                    if (tall < 1.1f) continue;
                    var d = b.ClosestPoint(new Vector3(p.x, b.center.y, p.z)) - new Vector3(p.x, b.center.y, p.z);
                    float dist = d.magnitude;
                    // 机位落在柜子的占地里（到顶的壁橱、高衣柜）：不能用
                    if (dist < .3f) { score -= 100f; continue; }
                    // 高家具正挡在镜头前面：越近扣得越多
                    if (dist < 1.8f && Vector3.Dot(d / dist, view) > .3f)
                        score -= (1.8f - dist) * (tall > y - floor - .4f ? 3f : 1.5f);
                }
                // 真的看得见多少：往每个容器、往地面上 3×3 个点各打一条射线
                for (int i = 0; i < visuals.Length; i++)
                    if (containerRoom[i] == roomIndex)
                        score += 2f * Sight(p, visuals[i].GetComponentInChildren<Collider>().bounds.center, visuals[i].transform.parent);
                for (int gx = 0; gx < 3; gx++)
                    for (int gz = 0; gz < 3; gz++)
                        score += Sight(p, new Vector3(Mathf.Lerp(r.xMin + 1f, r.xMax - 1f, gx / 2f), floor + .3f,
                            Mathf.Lerp(r.yMin + 1f, r.yMax - 1f, gz / 2f)), null);
                if (score > bestScore) { bestScore = score; best = p; }
            }
            var t = ArtKit.Empty(parent, "Vantage_" + room.id, best);
            t.rotation = Quaternion.LookRotation(target - best, Vector3.up);
            RuinShots.Add(("r5v_" + room.id, best, target));
            return t;
        }

        /// <summary>从机位看 target：看得见 +1；被离镜头不到 1.8 m 的东西挡住 −1.5（屏风、高柜子贴在镜头前）；被远处的东西挡住 0。</summary>
        static float Sight(Vector3 from, Vector3 target, Transform self)
        {
            var d = target - from;
            float dist = d.magnitude;
            if (dist < .5f) return 0f;
            if (!Physics.Raycast(from, d / dist, out var hit, dist - .25f, ~0, QueryTriggerInteraction.Ignore)) return 1f;
            if (self != null && hit.collider.transform.IsChildOf(self)) return 1f;
            return hit.distance < 1.8f ? -1.5f : 0f;
        }

        static void BuildMapProxy(Transform gameplay, HouseGraph graph, List<Room> rooms)
        {
            var floorMat = MapMat("MP_MapFloor", new Color(.27f, .32f, .35f));
            var corridorMat = MapMat("MP_MapCorridor", new Color(.15f, .17f, .19f));
            var stairMat = MapMat("MP_MapStairs", new Color(.44f, .34f, .2f));
            var wallMat = MapMat("MP_MapWall", new Color(.64f, .68f, .72f));
            var markerMat = MapMat("MP_MapMarker", new Color(.88f, .86f, .8f));
            var linkMat = MapMat("MP_MapLink", new Color(.3f, .62f, 1f));

            var rootT = ArtKit.Empty(gameplay, "HouseMapProxy", Vector3.down * HouseMapProxy.Depth);
            var proxy = rootT.gameObject.AddComponent<HouseMapProxy>();
            proxy.graph = graph;
            string[] groupNames = { "Floor1", "Floor2", "Kura" };
            var batches = new MeshBatch[3];
            for (int g = 0; g < 3; g++)
            {
                proxy.groups[g] = ArtKit.Empty(rootT, groupNames[g], Vector3.zero);
                batches[g] = new MeshBatch();
            }
            int Group(Room r) => r.group == G1 ? 1 : r.group == GKura ? 2 : 0;

            proxy.roomAnchors = new Transform[rooms.Count];
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                int g = Group(room);
                var mb = batches[g];
                float fl = FloorY(room);
                var anchor = ArtKit.Empty(proxy.groups[g], "Room_" + room.id, Vector3.zero);
                anchor.localPosition = new Vector3(room.r.center.x, fl, room.r.center.y);
                proxy.roomAnchors[i] = anchor;
                bool stair = room.id == "Kaidan" || room.id == "UraKaidan";
                if (room.hasFloor)
                    mb.Box(stair ? stairMat : graph.rooms[i].hasContainers ? floorMat : corridorMat,
                        new Vector3(room.r.center.x, fl - .03f, room.r.center.y), new Vector3(room.r.width, .06f, room.r.height));
                if (stair)
                {
                    // 一级级往二楼升的踏步，看得出这里是楼梯
                    const int n = 9;
                    float top = Levels[G1].floorTop;
                    for (int k = 0; k < n; k++)
                    {
                        float t = (k + .5f) / n;
                        mb.Box(stairMat, new Vector3(room.r.center.x, Mathf.Lerp(fl, top, t), Mathf.Lerp(room.r.yMin + .3f, room.r.yMax - .3f, t)),
                            new Vector3(room.r.width * .7f, .08f, (room.r.height - .6f) / n * .9f));
                    }
                }
                if (!room.hasFloor) continue;
                foreach (var e in EdgesOf(room))
                {
                    if (!HasWall(room, e)) continue;
                    var cuts = new List<Vector2>();
                    foreach (var gap in GapsOn(room, e))
                        if (!gap.window) cuts.Add(new Vector2(gap.a, gap.b)); // 门和敞开的口子留空；窗和玻璃拉门照样是墙
                    foreach (var run in Minus(e.from, e.to, cuts))
                        WallBox(mb, wallMat, e, run.x, run.y, fl, fl + MapWallHeight, 0f, MapWallThick);
                }
            }
            for (int g = 0; g < 3; g++) batches[g].Build(proxy.groups[g], "MapShell_" + groupNames[g], false);

            // ---- 容器的小方块（一个容器一个物体，程序按下标上色）----
            proxy.containerMarkers = new Renderer[graph.containers.Length];
            for (int i = 0; i < graph.containers.Length; i++)
            {
                int ri = graph.containerRoom[i];
                if (ri < 0) continue;
                var bounds = graph.containers[i].GetComponentInChildren<Collider>().bounds;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.name = $"Marker_{i:00}_{graph.containers[i].transform.parent.name}";
                cube.transform.SetParent(proxy.groups[Group(rooms[ri])], false);
                cube.transform.localPosition = new Vector3(bounds.center.x, FloorY(rooms[ri]) + .28f, bounds.center.z);
                cube.transform.localScale = new Vector3(Mathf.Max(.45f, bounds.size.x * .9f), .56f, Mathf.Max(.45f, bounds.size.z * .9f));
                var mr = cube.GetComponent<MeshRenderer>();
                mr.sharedMaterial = markerMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                proxy.containerMarkers[i] = mr;
            }

            // ---- 楼梯、后院的连接线 ----
            proxy.linkLines = new LineRenderer[graph.links.Length];
            for (int i = 0; i < graph.links.Length; i++)
            {
                var link = graph.links[i];
                if (link.kind == HouseGraph.LinkKind.Wall) continue;
                var go = new GameObject($"Link_{rooms[link.a].id}_{rooms[link.b].id}");
                go.transform.SetParent(rootT, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.sharedMaterial = linkMat;
                line.widthMultiplier = .16f;
                line.numCapVertices = 3;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                proxy.linkLines[i] = line;
            }

            // 土蔵相对主屋的方向（错开摆放时往这个方向推）
            Vector3 main = Vector3.zero, kura = Vector3.zero;
            int nm = 0, nk = 0;
            foreach (var room in rooms)
            {
                var c = new Vector3(room.r.center.x, 0f, room.r.center.y);
                if (room.group == GKura) { kura += c; nk++; }
                else { main += c; nm++; }
            }
            if (nm > 0 && nk > 0)
            {
                var d = kura / nk - main / nm;
                proxy.kuraDirection = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
            }
            // 2026-11-26：探索者视图（你在这里、院子、出口、金庫、仏壇；MapExplorer.cs）
            BuildMapExplorerView(proxy, gameplay, rooms);
            proxy.Refresh();
            rootT.gameObject.SetActive(false);
        }

        /// <summary>审图用：把 3D 地图打开、错开摆好，记两个机位（r5_map_*）。拍完由调用方删掉或关掉；不存进场景。</summary>
        internal static HouseMapProxy ShowMapProxyForReview()
        {
            var proxy = Object.FindFirstObjectByType<HouseMapProxy>(FindObjectsInactive.Include);
            if (proxy == null) return null;
            proxy.Show(true);
            proxy.Explode(5f, 5f);
            var b = new Bounds();
            bool any = false;
            foreach (var r in proxy.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            RuinShots.Add(("r5_map_southwest", b.center + new Vector3(-15f, 19f, -18f), b.center));
            RuinShots.Add(("r5_map_northeast", b.center + new Vector3(17f, 17f, 17f), b.center));
            RuinShots.Add(("r5_map_top", b.center + new Vector3(0f, 34f, -.5f), b.center));
            return proxy;
        }
    }
}
