using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第二轮：墙面 / 天花的水渍、烟熏、香灰、血字、拖痕，以及每个房间里的浮尘。
    /// <list type="bullet">
    /// <item>位置都用射线确认看得见：临时给没有碰撞体的网格（Dressing、门扇、家具零件）加 MeshCollider，量完就删，
    ///   场景里不会留下多余的碰撞体（PlayerDoorwayTests 不受影响）。</item>
    /// <item>血字只是气氛，不是线索（2026-11-19 定的四句：ここから出して / 開けるな / みつけた / まだいる）。
    ///   整块要露在一片平的墙面上，不压门窗、钉死的旧门、开门时门扇停靠的位置，不跨柱子；
    ///   也不写在收纳箱上方（免得像在提示箱子里有东西）。南墙、东墙上的字要翻一下，不然是反的。</item>
    /// <item>漏水痕从回り縁下面往下流；和室的長押挡着时放在小壁里或長押下面。至少 3/4 露出来（柱子挡一条没关系）。</item>
    /// </list>
    /// 贴花离墙面 1~2 mm、不投影。想换位置：改这里的规则再重新生成。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>每个房间：漏水痕几道、天花水渍几块（没列的房间不画）。</summary>
        static readonly Dictionary<string, (int leaks, int ceil)> GrimeAmount = new Dictionary<string, (int leaks, int ceil)>
        {
            ["Genkan"] = (2, 1), ["Daidokoro"] = (1, 0), ["Tsugi"] = (1, 1), ["Butsuma"] = (1, 1),
            ["Zashiki"] = (2, 1), ["Ima"] = (1, 0), ["Nando"] = (3, 2), ["Oku"] = (2, 1),
            ["Engawa"] = (2, 0), ["EngawaE"] = (1, 0), ["Rouka"] = (2, 1), ["UraKaidan"] = (1, 0),
            ["Benjo"] = (1, 1), ["Furo"] = (2, 2), ["Kaidan"] = (1, 0), ["Kura"] = (2, 1),
            ["Shinshitsu"] = (2, 2), ["Washitsu2F"] = (1, 2), ["Rouka2F"] = (1, 1), ["Monooki"] = (2, 2),
            ["Kodomo"] = (1, 1), ["Rouka2FN"] = (1, 1),
        };

        /// <summary>已经贴了东西的墙面范围（世界坐标），后放的贴花避开。</summary>
        static readonly List<Bounds> GrimeUsed = new List<Bounds>();

        /// <summary>墙面探测射线从离墙面多远的地方打过去。</summary>
        const float ProbeReach = .9f;

        /// <summary>收纳箱（Containers 下的预制体）的包围盒。血字不写在箱子上方，免得看起来像在提示“箱子里有东西”。</summary>
        static readonly List<Bounds> ContainerBoxes = new List<Bounds>();

        static void BuildGrime(Transform dressing, Transform root)
        {
            if (DM.Leaks == null) return;
            var parent = ArtKit.Empty(dressing, "Grime", Vector3.zero);
            GrimeUsed.Clear();
            CollectContainerBoxes(root);
            var temp = AddProbeColliders(root);
            bool backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            int writing = 0, leaks = 0, ceil = 0, extra = 0;
            try
            {
                // 血字先放（要求最严），其余的贴花避开它们
                if (Writing(parent, "Nando", "DeteSoto", DM.WriteDeteSoto, .85f, .85f * 594f / 1389f, 1.14f, -3f, true)) writing++;
                if (Writing(parent, "Butsuma", "Akeruna", DM.WriteAkeruna, 1f * 551f / 1443f, 1f, 1.45f, 1.5f, true)) writing++;
                if (Writing(parent, "Kodomo", "Mitsuketa", DM.WriteMitsuketa, 1.1f, 1.1f * 804f / 1162f, 1.0f, -4f, false)) writing++;
                if (Writing(parent, "Kura", "Madairu", DM.WriteMadairu, .9f * 480f / 1256f, .9f, 1.3f, 0f, false)) writing++;

                foreach (var room in DRooms)
                {
                    if (!room.hasFloor || room.group == GFence) continue;
                    GrimeAmount.TryGetValue(room.id, out var amt);
                    var mb = new MeshBatch();
                    var rnd = new System.Random(StableHash("grime_" + room.id));
                    extra += RoomGrimeExtras(mb, room, root, rnd);
                    leaks += Leaks(mb, room, amt.leaks, rnd);
                    ceil += CeilingStains(mb, room, amt.ceil, rnd);
                    mb.Build(parent, $"Grime_{room.id}", false);
                }
            }
            finally
            {
                foreach (var c in temp)
                    if (c != null) Object.DestroyImmediate(c);
                Physics.queriesHitBackfaces = backfaces;
                Physics.SyncTransforms();
            }
            Debug.Log($"[Hako Art] 污渍：血字 {writing}/4、漏水痕 {leaks}、天花水渍 {ceil}、烟熏 / 香灰 / 手印 / 抓痕 / 拖痕 {extra}");
        }

        // =====================================================================
        // 射线探测
        // =====================================================================
        /// <summary>给没有碰撞体的可见网格临时加 MeshCollider（室外、庭院、预制体实例除外）。</summary>
        static List<Collider> AddProbeColliders(Transform root)
        {
            var list = new List<Collider>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0 || mf.GetComponent<Collider>() != null) continue;
                if (!mf.TryGetComponent<MeshRenderer>(out var mr) || !mr.enabled) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(mf.gameObject) || mf.GetComponentInParent<Rigidbody>() != null) continue;
                if (UnderName(mf.transform, "Exterior") || UnderName(mf.transform, "Garden")) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                list.Add(mc);
            }
            Physics.SyncTransforms();
            return list;
        }

        static void CollectContainerBoxes(Transform root)
        {
            ContainerBoxes.Clear();
            var parent = root.Find("Containers");
            if (parent == null) return;
            foreach (Transform c in parent)
            {
                var rs = c.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                ContainerBoxes.Add(b);
            }
        }

        /// <summary>墙前 1.2 m 以内、字的左右再各让 0.4 m，从地面到字的上沿有收纳箱。</summary>
        static bool NearContainer(Room room, Edge e, float s0, float s1, float yTop)
        {
            var zone = BoxOnWall(e, s0 - .4f, s1 + .4f, FloorY(room) - .05f, yTop, 0f, T + 1.2f);
            foreach (var b in ContainerBoxes)
                if (b.Intersects(zone)) return true;
            return false;
        }

        /// <summary>DecalOn 的 u 方向固定朝 +x / +z：站在屋里看南墙、东墙时它朝左，字会左右反过来，要翻一下。</summary>
        static bool ReadsMirrored(Edge e)
        {
            var u = e.alongX ? Vector3.right : Vector3.forward;
            return Vector3.Dot(u, Vector3.Cross(Vector3.up, -Inward(e))) < 0f;
        }

        static bool UnderName(Transform t, string name)
        {
            for (; t != null; t = t.parent)
                if (t.name == name) return true;
            return false;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static List<Edge> WallsOf(Room room)
        {
            var list = new List<Edge>();
            foreach (var e in EdgesOf(room))
                if (HasWall(room, e)) list.Add(e);
            return list;
        }

        /// <summary>
        /// 在墙 e 的 [s0,s1]×[y0,y1] 里打 cols×rows 根水平射线（从离墙线 T+0.9 m 处射向墙面）。
        /// 返回“第一下打在离墙线 [dMin,dMax] 处、而且前面什么都没有”的比例；dLo/dHi 是这些点的深度范围。
        /// 起点在家具里面时第一根射线打不到家具，所以再从墙面往外打一根确认。
        /// </summary>
        static float ProbeWall(Edge e, float s0, float s1, float y0, float y1, int cols, int rows, float dMin, float dMax,
            out float dLo, out float dHi)
        {
            var into = -Inward(e);
            int ok = 0;
            dLo = float.MaxValue;
            dHi = float.MinValue;
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    float s = Mathf.Lerp(s0, s1, (i + .5f) / cols), y = Mathf.Lerp(y0, y1, (j + .5f) / rows);
                    if (!Physics.Raycast(OnWall(e, s, y, T + ProbeReach), into, out var hit, ProbeReach + .05f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    float d = T + ProbeReach - hit.distance;
                    if (d < dMin || d > dMax) continue;
                    if (Physics.Raycast(OnWall(e, s, y, d + .002f), -into, ProbeReach - (d - T),
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    dLo = Mathf.Min(dLo, d);
                    dHi = Mathf.Max(dHi, d);
                    ok++;
                }
            return ok / (float)(cols * rows);
        }

        static IEnumerable<Vector2> DiscSamples(float r)
        {
            yield return Vector2.zero;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                yield return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * .5f);
            }
            for (int i = 0; i < 10; i++)
            {
                float a = (i + .5f) * Mathf.PI / 5f;
                yield return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
        }

        /// <summary>水平面（地面 / 天花）上半径 r 的圆里打竖直射线：从高度 yFrom 出发，第一下打在 [yMin,yMax] 而且中间没东西的比例。</summary>
        static float ProbeFlat(Vector3 c, float r, float yFrom, float yMin, float yMax)
        {
            bool down = yFrom > yMax;
            var dir = down ? Vector3.down : Vector3.up;
            float dist = (down ? yFrom - yMin : yMax - yFrom) + .01f;
            int ok = 0, n = 0;
            foreach (var o in DiscSamples(r))
            {
                n++;
                var from = new Vector3(c.x + o.x, yFrom, c.z + o.y);
                if (!Physics.Raycast(from, dir, out var hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.point.y < yMin || hit.point.y > yMax) continue;
                if (Physics.Raycast(hit.point - dir * .002f, -dir, hit.distance - .002f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                ok++;
            }
            return ok / (float)n;
        }

        // =====================================================================
        // 墙面范围
        // =====================================================================
        /// <summary>不压门窗洞、钉死的旧门（血字离旧门 0.45 m，其余 0.3 m）；血字还要避开开门时门扇停靠的位置。</summary>
        static bool WallSpanFree(Room room, Edge e, float s0, float s1, float y0, float y1, bool writing)
        {
            foreach (var g in GapsOn(room, e))
                if (s1 > g.a - .1f && s0 < g.b + .1f) return false;
            float pad = writing ? .45f : .3f;
            foreach (var sp in SealedSpansOn(room, e))
                if (s1 > sp.x - pad && s0 < sp.y + pad) return false;
            if (writing)
            {
                var b = BoxOnWall(e, s0, s1, y0, y1, T - .01f, T + .06f);
                foreach (var z in ParkZones)
                    if (z.Intersects(b)) return false;
            }
            return true;
        }

        /// <summary>土间和土蔵天花的两根大梁压在长边墙上的位置（梁的两头贴着墙，射线看不出来）。</summary>
        static bool BeamAt(Room room, Edge e, float s0, float s1)
        {
            if (!(IsDoma(room) || room.group == GKura) || room.id == "UraKaidan") return false;
            var a = Rect.MinMaxRect(room.r.xMin + T, room.r.yMin + T, room.r.xMax - T, room.r.yMax - T);
            bool longX = a.width >= a.height;
            if (e.alongX != longX) return false;
            float bw = room.group == GKura ? .26f : .22f;
            for (int i = 1; i <= 2; i++)
            {
                float c = longX ? Mathf.Lerp(a.xMin, a.xMax, i / 3f) : Mathf.Lerp(a.yMin, a.yMax, i / 3f);
                if (s1 > c - bw / 2 - .1f && s0 < c + bw / 2 + .1f) return true;
            }
            return false;
        }

        static bool GrimeOverlaps(Edge e, float s, float y, float w, float h, float margin)
        {
            var b = BoxOnWall(e, s - w / 2 - margin, s + w / 2 + margin, y - h / 2 - margin, y + h / 2 + margin, T - .05f, T + .05f);
            foreach (var u in GrimeUsed)
                if (u.Intersects(b)) return true;
            return false;
        }

        static void GrimeRemember(Edge e, float s, float y, float w, float h) =>
            GrimeUsed.Add(BoxOnWall(e, s - w / 2, s + w / 2, y - h / 2, y + h / 2, T - .02f, T + .02f));

        /// <summary>转了 angle 度的 w×h 贴花的外接框。</summary>
        static void RotatedExtent(float w, float h, float angle, out float bw, out float bh)
        {
            float c = Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad)), s = Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad));
            bw = w * c + h * s;
            bh = w * s + h * c;
        }

        /// <summary>小贴花（手印、抓痕）放得下：不压门窗和旧门、不叠别的贴花、整块露在一片平面上（灰泥、腰板、瓷砖都行）。surf = 贴的深度。</summary>
        static bool MarkFits(Room room, Edge e, float s, float y, float bw, float bh, out float surf)
        {
            surf = T;
            InnerSpan(room, e, out float lo, out float hi);
            if (s - bw / 2 < lo + .08f || s + bw / 2 > hi - .08f) return false;
            if (!WallSpanFree(room, e, s - bw / 2, s + bw / 2, y - bh / 2, y + bh / 2, false)) return false;
            if (GrimeOverlaps(e, s, y, bw, bh, .08f)) return false;
            float f = ProbeWall(e, s - bw / 2, s + bw / 2, y - bh / 2, y + bh / 2, 3, 3, T - .003f, T + .03f, out float dLo, out float dHi);
            if (f < 1f || dHi - dLo > .002f) return false;
            surf = dHi;
            return true;
        }

        /// <summary>水平贴花：c 为中心，贴图 u 沿 along，len × wid；up = 朝上（地面）/ 朝下（天花）。</summary>
        static void FlatDecal(MeshBatch mb, Material m, Vector3 c, Vector3 along, float len, float wid, bool up)
        {
            along.y = 0f;
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.right;
            var side = Vector3.Cross(Vector3.up, along);
            Vector3 a = along * (len / 2), b = side * (wid / 2);
            mb.Poly(m, new[] { c - a - b, c + a - b, c + a + b, c - a + b },
                new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, up ? Vector3.up : Vector3.down);
        }

        static float FloorDecalY(Room room) => FloorY(room) + (IsTatami(room) ? .009f : .0045f);

        /// <summary>天花贴花的高度：有棹缘的天花贴在棹缘下面（熏黑、水渍连棹缘一起），土间 / 土蔵贴在板下。</summary>
        static float CeilingDecalY(Room room) => IsDoma(room) || room.group == GKura ? CeilY(room) - .002f : CeilY(room) - .033f;

        // =====================================================================
        // 血字
        // =====================================================================
        /// <summary>
        /// 一句血字：nearSealed = 贴在钉死的旧门旁边（离门框 0.45 m 以外最近的地方），否则贴在从主门口看过去最正对的墙上。
        /// yc 为中心离地高度。单独一个物体 Writing_*，截图工具按名字找。
        /// </summary>
        static bool Writing(Transform parent, string roomId, string name, Material m, float w, float h, float yc, float angle, bool nearSealed)
        {
            var room = RoomById(roomId);
            if (room == null || m == null)
            {
                Debug.LogWarning($"[Hako Art] 血字 {name}：找不到房间 {roomId} 或材质");
                return false;
            }
            float fl = FloorY(room), ys = fl + yc;
            RotatedExtent(w, h, angle, out float bw, out float bh);
            var door = DoorPoint(room);
            float bestScore = float.MaxValue, bestS = 0f, bestD = T;
            Edge best = default;
            foreach (var e in WallsOf(room))
            {
                var spans = SealedSpansOn(room, e);
                if (nearSealed && spans.Count == 0) continue;
                InnerSpan(room, e, out float lo, out float hi);
                for (float s = lo + bw / 2 + .15f; s <= hi - bw / 2 - .15f; s += .05f)
                {
                    float score;
                    if (nearSealed)
                    {
                        score = float.MaxValue;
                        foreach (var sp in spans) score = Mathf.Min(score, Mathf.Max(sp.x - s, s - sp.y));
                    }
                    else
                    {
                        var v = OnWall(e, s, door.y, T) - door;
                        v.y = 0f;
                        float dist = Mathf.Max(v.magnitude, .01f);
                        // 越正对门口越好；离门口 4 m 左右最好
                        score = -Vector3.Dot(v / dist, -Inward(e)) + .05f * Mathf.Abs(dist - 4f);
                    }
                    if (score >= bestScore) continue;
                    if (!WallSpanFree(room, e, s - bw / 2, s + bw / 2, ys - bh / 2, ys + bh / 2, true)) continue;
                    if (NearContainer(room, e, s - bw / 2, s + bw / 2, ys + bh / 2)) continue;
                    if (GrimeOverlaps(e, s, ys, bw, bh, .15f)) continue;
                    // 射线要密：柱子只有 10 cm 宽，稀了会漏掉，字就会跨在柱子上
                    int cols = Mathf.CeilToInt(bw / .035f) + 1, rows = Mathf.CeilToInt(bh / .06f) + 1;
                    float f = ProbeWall(e, s - bw / 2, s + bw / 2, ys - bh / 2, ys + bh / 2, cols, rows, T - .003f, T + .03f, out float dLo, out float dHi);
                    if (f < 1f || dHi - dLo > .002f) continue;
                    bestScore = score;
                    best = e;
                    bestS = s;
                    bestD = dHi;
                }
            }
            if (bestScore == float.MaxValue)
            {
                if (nearSealed)
                {
                    // 納戸的两扇旧门旁边都靠着收纳箱：退一步，贴在正对门口的墙上
                    Debug.Log($"[Hako Art] 血字 {name}：{roomId} 钉死的旧门旁边没有空墙（收纳箱、柱子、门扇挡着），改贴正对门口的墙");
                    return Writing(parent, roomId, name, m, w, h, yc, angle, false);
                }
                Debug.LogWarning($"[Hako Art] 血字 {name}：{roomId} 里找不到一整片露出来的墙，没贴");
                return false;
            }
            var mb = new MeshBatch();
            DecalOn(mb, m, best, bestS, ys, bestD + .0015f, w, h, angle, ReadsMirrored(best));
            mb.Build(parent, "Writing_" + name, false);
            GrimeRemember(best, bestS, ys, bw, bh);
            Debug.Log($"[Hako Art] 血字 {name}：{roomId} {"南北西东"[best.side]}墙，沿墙 {bestS:F2} m，离地 {yc:F2} m");
            return true;
        }

        /// <summary>房间最宽的门洞在室内一侧、视线高度的位置；没有门就用房间中心。</summary>
        static Vector3 DoorPoint(Room room)
        {
            float fl = FloorY(room), bestW = 0f;
            var p = new Vector3(room.r.center.x, fl + 1.5f, room.r.center.y);
            foreach (var e in WallsOf(room))
                foreach (var g in GapsOn(room, e))
                    if (g.door && g.b - g.a > bestW)
                    {
                        bestW = g.b - g.a;
                        p = OnWall(e, (g.a + g.b) / 2, fl + 1.5f, T + .3f);
                    }
            return p;
        }

        // =====================================================================
        // 漏水痕、天花水渍
        // =====================================================================
        static int Leaks(MeshBatch mb, Room room, int want, System.Random rnd)
        {
            if (want <= 0) return 0;
            var walls = WallsOf(room);
            if (walls.Count == 0) return 0;
            float cy = CeilY(room);
            bool tatami = IsTatami(room);
            float kt = room.group == G0 ? .55f + DoorTop : Mathf.Min(Levels[G1].wallTop, Levels[G1].floorTop + DoorTop);
            float nageshiLo = kt + .075f, nageshiHi = nageshiLo + .09f;
            int placed = 0;
            for (int attempt = 0; attempt < 80 && placed < want; attempt++)
            {
                var e = walls[rnd.Next(walls.Count)];
                InnerSpan(room, e, out float lo, out float hi);
                float w = Mathf.Min(Mathf.Lerp(.9f, 2.2f, R01(rnd)), hi - lo - .3f);
                if (w < .6f) continue;
                // 贴图 2:1，水从上沿（回り縁下面）往下流
                float h = w * Mathf.Lerp(.45f, .6f, R01(rnd)), top = cy - .045f;
                if (tatami)
                {
                    // 長押挡在中间：小壁放得下就放小壁，否则从長押下面开始流
                    float band = top - nageshiHi - .02f;
                    if (band >= .35f && R01(rnd) < .5f)
                    {
                        h = Mathf.Min(h, band);
                        w = Mathf.Min(w, h * 2.4f);
                    }
                    else top = nageshiLo - .01f;
                }
                float s = Mathf.Lerp(lo + w / 2 + .1f, hi - w / 2 - .1f, R01(rnd)), y = top - h / 2;
                if (!WallSpanFree(room, e, s - w / 2, s + w / 2, top - h, top, false) || BeamAt(room, e, s - w / 2, s + w / 2)) continue;
                if (GrimeOverlaps(e, s, y, w, h, .3f)) continue;
                if (ProbeWall(e, s - w * .42f, s + w * .42f, top - h * .85f, top - h * .08f, 6, 3, T - .003f, T + .004f, out _, out _) < .75f) continue;
                DecalOn(mb, DM.Leaks[rnd.Next(DM.Leaks.Length)], e, s, y, T + .0012f + placed * .0002f, w, h, 0f, R01(rnd) < .5f);
                GrimeRemember(e, s, y, w, h);
                placed++;
            }
            return placed;
        }

        static int CeilingStains(MeshBatch mb, Room room, int want, System.Random rnd)
        {
            if (want <= 0) return 0;
            float cy = CeilY(room), y = CeilingDecalY(room);
            var a = Rect.MinMaxRect(room.r.xMin + T, room.r.yMin + T, room.r.xMax - T, room.r.yMax - T);
            if (room.id == "Kaidan" || room.id == "UraKaidan") a.yMin = Mathf.Max(a.yMin, 13.195f * PlanScale);
            var used = new List<(Vector3 c, float r)>();
            int placed = 0;
            for (int attempt = 0; attempt < 40 && placed < want; attempt++)
            {
                float size = Mathf.Lerp(.7f, 1.7f, R01(rnd));
                float m = size * .72f + .12f; // 转多少度都碰不到回り縁
                if (a.width < 2 * m || a.height < 2 * m)
                {
                    size = Mathf.Min(a.width, a.height) * .55f;
                    m = size * .72f + .06f;
                    if (size < .4f || a.width < 2 * m || a.height < 2 * m) break;
                }
                var c = new Vector3(Mathf.Lerp(a.xMin + m, a.xMax - m, R01(rnd)), y, Mathf.Lerp(a.yMin + m, a.yMax - m, R01(rnd)));
                bool clash = false;
                foreach (var u in used)
                    if ((u.c - c).magnitude < (u.r + size) * .6f) clash = true;
                if (clash) continue;
                // 大梁、吊着的东西下面不贴
                if (ProbeFlat(c, size * .35f, cy - 1f, cy - .036f, cy + .01f) < .8f) continue;
                FlatDecal(mb, DM.CeilingStain, c, Quaternion.Euler(0f, R01(rnd) * 360f, 0f) * Vector3.right, size, size, false);
                used.Add((c, size));
                placed++;
            }
            return placed;
        }

        // =====================================================================
        // 各房间特有的：烟熏、香灰、手印、抓痕、拖痕
        // =====================================================================
        static int RoomGrimeExtras(MeshBatch mb, Room room, Transform root, System.Random rnd)
        {
            switch (room.id)
            {
                case "Daidokoro": return KitchenSoot(mb, room, root);
                case "Ima": return HearthSoot(mb, room, root);
                case "Butsuma": return IncenseAsh(mb, room, root);
                // 納戸：小孩高度的抓痕和小手印（钉死的旧门背面已经有一些）
                case "Nando":
                    return Marks(mb, room, rnd, DM.Scratch, 2, .16f, .5f, .3f, .85f, 12f) +
                           Marks(mb, room, rnd, DM.Blood, 3, .13f, .155f, .3f, 1.0f, 40f);
                case "Kodomo": return Marks(mb, room, rnd, DM.Scratch, 2, .15f, .45f, .4f, 1.0f, 12f);
                case "Benjo": return Marks(mb, room, rnd, DM.Blood, 1, .13f, .155f, .9f, 1.3f, 30f);
                case "Rouka2F": return DragTrail(mb, room, rnd);
            }
            return 0;
        }

        /// <summary>count 个小贴花，中心离地 [yLo,yHi]，随机转 ±maxAngle 度。</summary>
        static int Marks(MeshBatch mb, Room room, System.Random rnd, Material m, int count, float w, float h, float yLo, float yHi, float maxAngle)
        {
            var walls = WallsOf(room);
            if (walls.Count == 0 || m == null) return 0;
            float fl = FloorY(room);
            int placed = 0;
            for (int attempt = 0; attempt < 150 && placed < count; attempt++)
            {
                var e = walls[rnd.Next(walls.Count)];
                InnerSpan(room, e, out float lo, out float hi);
                float s = Mathf.Lerp(lo + .2f, hi - .2f, R01(rnd)), y = fl + Mathf.Lerp(yLo, yHi, R01(rnd));
                float angle = (R01(rnd) * 2f - 1f) * maxAngle;
                bool flip = R01(rnd) < .5f;
                RotatedExtent(w, h, angle, out float bw, out float bh);
                if (!MarkFits(room, e, s, y, bw, bh, out float surf)) continue;
                DecalOn(mb, m, e, s, y, surf + .0015f + placed * .0002f, w, h, angle, flip);
                GrimeRemember(e, s, y, bw, bh);
                placed++;
            }
            return placed;
        }

        static void TrailHand(float s0, int i, float fl, out float s, out float y, out float angle, out float scale)
        {
            s = s0 + i * .5f + (i % 2) * .06f;
            y = fl + 1.32f - i * .05f - (i % 2) * .03f;
            angle = -12f - i * 9f;
            scale = 1f - i * .04f;
        }

        /// <summary>二楼中廊下：沿长边墙一串往下滑的血手印，下面地上一道拖痕（拖痕浓的一头在手印开始的地方）。</summary>
        static int DragTrail(MeshBatch mb, Room room, System.Random rnd)
        {
            float fl = FloorY(room);
            bool longX = room.r.width >= room.r.height;
            float bestScore = float.MaxValue, bestS0 = 0f;
            Edge best = default;
            var surf = new float[5];
            var bestSurf = new float[5];
            foreach (var e in WallsOf(room))
            {
                if (e.alongX != longX) continue;
                InnerSpan(room, e, out float lo, out float hi);
                float mid = (lo + hi) / 2;
                for (float s0 = lo + .3f; s0 + 2.3f < hi - .3f; s0 += .1f)
                {
                    float score = Mathf.Abs(s0 + 1.1f - mid);
                    if (score >= bestScore) continue;
                    bool ok = true;
                    for (int i = 0; i < 5 && ok; i++)
                    {
                        TrailHand(s0, i, fl, out float s, out float y, out float a, out float sc);
                        RotatedExtent(.2f * sc, .24f * sc, a, out float bw, out float bh);
                        ok = MarkFits(room, e, s, y, bw, bh, out surf[i]);
                    }
                    if (!ok) continue;
                    var c = OnWall(e, s0 + 1.1f, fl, T + .62f);
                    if (ProbeFlat(c, .45f, fl + 1.2f, fl - .01f, fl + .015f) < .85f) continue;
                    bestScore = score;
                    bestS0 = s0;
                    best = e;
                    System.Array.Copy(surf, bestSurf, 5);
                }
            }
            if (bestScore == float.MaxValue)
            {
                Debug.LogWarning($"[Hako Art] {room.id}：找不到放手印和拖痕的一段墙");
                return 0;
            }
            for (int i = 0; i < 5; i++)
            {
                TrailHand(bestS0, i, fl, out float s, out float y, out float a, out float sc);
                DecalOn(mb, DM.Blood, best, s, y, bestSurf[i] + .0015f + i * .0002f, .2f * sc, .24f * sc, a, false);
                RotatedExtent(.2f * sc, .24f * sc, a, out float bw, out float bh);
                GrimeRemember(best, s, y, bw, bh);
            }
            var center = OnWall(best, bestS0 + 1.1f, FloorDecalY(room), T + .62f);
            FlatDecal(mb, DM.BloodDrag, center, best.alongX ? Vector3.right : Vector3.forward, 2.6f, .5f, true);
            return 6;
        }

        /// <summary>台所：かまど火口正上方的墙熏黑到天花，天花上再一大块。</summary>
        static int KitchenSoot(MeshBatch mb, Room room, Transform root)
        {
            var fire = FindDeep(root.Find("Props"), "Firebox");
            if (fire == null)
            {
                Debug.LogWarning("[Hako Art] 找不到かまど的火口（Props/…/Firebox），台所不画烟熏");
                return 0;
            }
            var p = fire.position;
            Edge best = default;
            float bestD = float.MaxValue;
            foreach (var e in WallsOf(room))
            {
                float d = ((e.alongX ? p.z : p.x) - e.line) * e.inward;
                if (d > 0f && d < bestD) { bestD = d; best = e; }
            }
            if (bestD > 2.5f) return 0;
            float fl = FloorY(room), cy = CeilY(room), y0 = fl + .85f, y1 = cy - .045f, h = y1 - y0;
            float s0 = best.alongX ? p.x : p.z;
            InnerSpan(room, best, out float lo, out float hi);
            foreach (float w in new[] { Mathf.Clamp(h * .5f, .9f, 1.6f), 1.1f, .8f })
                foreach (float off in new[] { 0f, .15f, -.15f, .3f, -.3f, .5f, -.5f })
                {
                    float s = s0 + off;
                    if (s - w / 2 < lo + .05f || s + w / 2 > hi - .05f) continue;
                    if (!WallSpanFree(room, best, s - w / 2, s + w / 2, y0, y1, false) || BeamAt(room, best, s - w / 2, s + w / 2)) continue;
                    DecalOn(mb, DM.SootPlume, best, s, (y0 + y1) / 2, T + .0016f, w, h, 0f, false);
                    GrimeRemember(best, s, (y0 + y1) / 2, w, h);
                    FlatDecal(mb, DM.SootCeiling, OnWall(best, s, CeilingDecalY(room), T + .7f), Quaternion.Euler(0f, 17f, 0f) * Vector3.right, 1.5f, 1.5f, false);
                    return 2;
                }
            Debug.LogWarning("[Hako Art] かまど上方的墙被门窗占了，台所不画烟熏");
            return 0;
        }

        /// <summary>居間：囲炉裏正上方的天花熏黑。</summary>
        static int HearthSoot(MeshBatch mb, Room room, Transform root)
        {
            var irori = root.Find("Props/Ima_IroriTable");
            if (irori == null || RoomAt(irori.position + Vector3.up * .3f, room.group) != room)
            {
                Debug.LogWarning("[Hako Art] 囲炉裏不在居間里，天花不画烟熏");
                return 0;
            }
            const float size = 1.5f;
            var a = Rect.MinMaxRect(room.r.xMin + T + .95f, room.r.yMin + T + .95f, room.r.xMax - T - .95f, room.r.yMax - T - .95f);
            var c = new Vector3(Mathf.Clamp(irori.position.x, a.xMin, Mathf.Max(a.xMin, a.xMax)), CeilingDecalY(room),
                Mathf.Clamp(irori.position.z, a.yMin, Mathf.Max(a.yMin, a.yMax)));
            FlatDecal(mb, DM.SootCeiling, c, Quaternion.Euler(0f, 31f, 0f) * Vector3.right, size, size, false);
            return 1;
        }

        /// <summary>仏間：仏壇前面地上一摊香灰（一部分在供桌下面）。</summary>
        static int IncenseAsh(MeshBatch mb, Room room, Transform root)
        {
            var mem = root.Find("Gameplay/MemorialPoint_Butsudan");
            if (mem == null) return 0;
            float y = FloorDecalY(room);
            var c = mem.TransformPoint(0f, 0f, .75f);
            if (RoomAt(c + Vector3.up * .3f, room.group) != room)
            {
                Debug.LogWarning("[Hako Art] 仏壇前面不在仏間里，不画香灰");
                return 0;
            }
            c.y = y;
            FlatDecal(mb, DM.Ash, c, mem.right, .62f, .62f, true);
            var c2 = mem.TransformPoint(.42f, 0f, .58f);
            c2.y = y + .0004f;
            FlatDecal(mb, DM.Ash, c2, Quaternion.AngleAxis(40f, Vector3.up) * mem.right, .3f, .3f, true);
            return 2;
        }

        // =====================================================================
        // 浮尘：每个房间一个粒子系统，只有手电照到才看得见
        // =====================================================================
        static Material DustMaterial()
        {
            string path = $"{DetailMats.Dir}/MX_DustMotes.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ExternalMats.DecalDir + "/T_DustMote.png");
            if (sh == null || tex == null)
            {
                Debug.LogWarning("[Hako Art] 浮尘：缺少 URP Particles/Simple Lit 或 T_DustMote.png");
                return null;
            }
            m = new Material(sh) { name = "MX_DustMotes" };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(.85f, .82f, .76f, .55f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // 贴着镜头的尘埃淡掉，不会一闪一闪地糊在屏幕上
            m.SetFloat("_CameraFadingEnabled", 1f);
            m.SetFloat("_CameraNearFadeDistance", .25f);
            m.SetFloat("_CameraFarFadeDistance", .8f);
            m.SetVector("_CameraFadeParams", new Vector4(.25f, 1f / (.8f - .25f), 0f, 0f));
            m.EnableKeyword("_FADING_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void BuildDust(Transform lighting)
        {
            if (DRooms == null) return;
            var mat = DustMaterial();
            if (mat == null) return;
            var parent = ArtKit.Empty(lighting, "Dust", Vector3.zero);
            int systems = 0, total = 0;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(1f, .8f), new GradientAlphaKey(0f, 1f) });
            foreach (var room in DRooms)
            {
                if (!room.hasFloor || room.group == GFence) continue;
                float fl = FloorY(room), cy = CeilY(room);
                var size = new Vector3(room.r.width - 2 * T - .2f, cy - fl - .25f, room.r.height - 2 * T - .2f);
                if (size.x < .5f || size.y < .5f || size.z < .5f) continue;
                int max = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * size.z * 2.5f), 20, 300);

                var go = new GameObject($"Dust_{room.id}");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(room.r.center.x, fl + .1f + size.y / 2, room.r.center.y);
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 10f;
                main.loop = true;
                main.prewarm = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, .015f);
                main.startSize = new ParticleSystem.MinMaxCurve(.006f, .016f);
                main.startColor = new Color(.9f, .88f, .82f, .75f);
                main.gravityModifier = 0f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Local;
                main.maxParticles = max;
                var em = ps.emission;
                em.enabled = true;
                em.rateOverTime = max / 13f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.position = Vector3.zero;
                shape.rotation = Vector3.zero;
                shape.scale = size;
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = .04f;
                noise.frequency = .3f;
                noise.scrollSpeed = .05f;
                noise.damping = true;
                noise.octaveCount = 1;
                noise.quality = ParticleSystemNoiseQuality.Medium;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = fade;
                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = mat;
                rend.renderMode = ParticleSystemRenderMode.Billboard;
                rend.shadowCastingMode = ShadowCastingMode.Off;
                rend.receiveShadows = false;
                rend.lightProbeUsage = LightProbeUsage.Off;
                rend.reflectionProbeUsage = ReflectionProbeUsage.Off;
                rend.minParticleSize = 0f;
                rend.maxParticleSize = .015f;
                systems++;
                total += max;
            }
            Debug.Log($"[Hako Art] 浮尘：{systems} 个房间，最多 {total} 粒");
        }
    }
}
