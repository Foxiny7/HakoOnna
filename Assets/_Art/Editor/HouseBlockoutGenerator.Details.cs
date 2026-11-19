using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 场景细节（第二遍）：室内建筑细部、外墙、屋顶、围墙、庭院、家具陈设。
    /// 视觉网格全部没有碰撞体，按材质合并后放在 Dressing 下；
    /// 会挡路的家具另在 Props/Dressing_Blockers 下放一个 BoxCollider（Blocker_*），
    /// 这样 PlayerDoorwayTests 能检查它们不挡门、不压墙、不占交互点。
    /// 注意：不要往 Structure/Room_* 下面加碰撞体，测试会把它们当成墙。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        const string DetailMeshPath = "Assets/_Art/Generated/HouseDetail_Meshes.asset";
        static DetailMats D;
        static Palette P;
        static List<Room> DRooms;
        static List<Opening> DOps;
        static Transform BlockerRoot;

        static void BuildDetails(Transform root, Transform structure, Transform props, List<Room> rooms, List<Opening> openings, Palette pal)
        {
            MeshBatch.Created.Clear();
            D = DetailMats.Load();
            P = pal; DRooms = rooms; DOps = openings;
            var dressing = ArtKit.Empty(root, "Dressing", Vector3.zero);
            var arch = ArtKit.Empty(dressing, "Architecture", Vector3.zero);
            var ext = ArtKit.Empty(dressing, "Exterior", Vector3.zero);
            var garden = ArtKit.Empty(dressing, "Garden", Vector3.zero);
            var furn = ArtKit.Empty(dressing, "Furnishings", Vector3.zero);
            BlockerRoot = ArtKit.Empty(props, "Dressing_Blockers", Vector3.zero);

            foreach (var room in rooms)
                if (room.hasFloor && room.group != GFence) InteriorArchitecture(arch, room);
            DecorateSlidingPanels(arch, structure);
            ExteriorWalls(ext);
            BuildRoofs(ext);
            FenceAndGate(ext);
            BuildGarden(garden, structure);
            FurnishRooms(furn, root);
            SaveDetailMeshes();
        }

        static void SaveDetailMeshes()
        {
            ArtKit.EnsureFolder("Assets/_Art/Generated");
            AssetDatabase.DeleteAsset(DetailMeshPath);
            if (MeshBatch.Created.Count > 0)
            {
                AssetDatabase.CreateAsset(MeshBatch.Created[0], DetailMeshPath);
                for (int i = 1; i < MeshBatch.Created.Count; i++)
                    AssetDatabase.AddObjectToAsset(MeshBatch.Created[i], DetailMeshPath);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[Hako Art] 细节网格 {MeshBatch.Created.Count} 个 → {DetailMeshPath}");
            MeshBatch.Created.Clear();
        }

        // =====================================================================
        // 通用工具
        // =====================================================================
        struct Edge
        {
            public bool alongX;
            public float line, from, to, inward;
            public int side; // 0 南 1 北 2 西 3 东
        }

        static Edge[] EdgesOf(Room room)
        {
            var r = room.r;
            return new[]
            {
                new Edge { alongX = true, line = r.yMin, from = r.xMin, to = r.xMax, inward = 1, side = 0 },
                new Edge { alongX = true, line = r.yMax, from = r.xMin, to = r.xMax, inward = -1, side = 1 },
                new Edge { alongX = false, line = r.xMin, from = r.yMin + T, to = r.yMax - T, inward = 1, side = 2 },
                new Edge { alongX = false, line = r.xMax, from = r.yMin + T, to = r.yMax - T, inward = -1, side = 3 },
            };
        }

        static bool HasWall(Room room, Edge e) => !IsStairRoom(room) || e.side == (room.id == "StairW2F" ? 2 : 3);

        /// <summary>墙面在室内可见的范围（扣掉两端垂直墙的厚度）。</summary>
        static void InnerSpan(Room room, Edge e, out float lo, out float hi)
        {
            lo = (e.alongX ? room.r.xMin : room.r.yMin) + T;
            hi = (e.alongX ? room.r.xMax : room.r.yMax) - T;
        }

        sealed class Gap
        {
            public float a, b, bottom, top;
            public bool door, window, glass;
            public Room room;
        }

        /// <summary>和 BuildEdge 相同的规则算出这条边上的门窗洞。</summary>
        static List<Gap> GapsOn(Room room, Edge e)
        {
            var lv = Levels[room.group];
            float fl = FloorHeight(room);
            var list = new List<Gap>();
            foreach (var o in DOps)
            {
                if (o.group != room.group || o.alongX != e.alongX || Mathf.Abs(o.line - e.line) > .01f) continue;
                if (o.center + o.width / 2 <= e.from || o.center - o.width / 2 >= e.to) continue;
                var g = new Gap
                {
                    a = Mathf.Max(e.from, o.center - o.width / 2),
                    b = Mathf.Min(e.to, o.center + o.width / 2),
                    window = o.window,
                    glass = o.glass,
                    door = !o.window && o.sill <= 0,
                    room = room,
                };
                g.bottom = o.window || o.sill > 0 ? fl + o.sill : lv.wallBase;
                g.top = Mathf.Min(lv.wallTop, fl + o.top);
                if (!o.window && room.group == G0) g.top = o.top == DoorTop ? .55f + DoorTop : o.top;
                list.Add(g);
            }
            list.Sort((x, y) => x.a.CompareTo(y.a));
            return list;
        }

        static List<Vector2> Minus(float lo, float hi, IEnumerable<Vector2> cuts)
        {
            var res = new List<Vector2> { new Vector2(lo, hi) };
            foreach (var c in cuts)
            {
                var next = new List<Vector2>();
                foreach (var s in res)
                {
                    if (c.y <= s.x || c.x >= s.y) { next.Add(s); continue; }
                    if (c.x > s.x) next.Add(new Vector2(s.x, c.x));
                    if (c.y < s.y) next.Add(new Vector2(c.y, s.y));
                }
                res = next;
            }
            res.RemoveAll(s => s.y - s.x < .01f);
            return res;
        }

        /// <summary>墙线坐标系：s 沿墙，d 为离墙线向室内的距离（负值 = 室外）。</summary>
        static Vector3 OnWall(Edge e, float s, float y, float d)
        {
            float w = e.line + e.inward * d;
            return e.alongX ? new Vector3(s, y, w) : new Vector3(w, y, s);
        }

        static Vector3 Inward(Edge e) => e.alongX ? new Vector3(0, 0, e.inward) : new Vector3(e.inward, 0, 0);

        static void WallBox(MeshBatch mb, Material m, Edge e, float s0, float s1, float y0, float y1, float d0, float d1, bool uAlongWall = false)
        {
            if (s1 - s0 < .002f || y1 - y0 < .001f || Mathf.Abs(d1 - d0) < .0005f) return;
            var c = OnWall(e, (s0 + s1) / 2, (y0 + y1) / 2, (d0 + d1) / 2);
            float dd = Mathf.Abs(d1 - d0);
            mb.Box(m, c, e.alongX ? new Vector3(s1 - s0, y1 - y0, dd) : new Vector3(dd, y1 - y0, s1 - s0), null,
                uAlongWall ? (e.alongX ? 0 : 2) : -1);
        }

        static bool IsTatami(Room r) => r.floor != null && r.floor == P.Tatami;
        static bool IsDoma(Room r) => r.group == G0 && FloorHeight(r) <= 0f;
        static bool IsTileRoom(Room r) => r.id == "Furo" || r.id == "Benjo";
        static bool IsStairRoom(Room r) => r.id.StartsWith("Stair");
        static bool IsCorridor(Room r) => r.id.StartsWith("Rouka") || r.id.StartsWith("Engawa") || r.id == "Kaidan";
        static float FloorY(Room r) => r.group == G1 ? Levels[G1].floorTop + .003f : FloorHeight(r);
        static float CeilY(Room r) => Levels[r.group].wallTop;

        static Room RoomAt(Vector3 p, int group)
        {
            foreach (var r in DRooms)
                if (r.group == group && r.r.Contains(new Vector2(p.x, p.z))) return r;
            return null;
        }

        static Room RoomById(string id) => DRooms.Find(r => r.id == id);

        static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        static void TopQuad(MeshBatch mb, Material m, Rect r, float y, bool uAlongZ)
        {
            var pts = new[]
            {
                new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMin, y, r.yMax),
                new Vector3(r.xMax, y, r.yMax), new Vector3(r.xMax, y, r.yMin),
            };
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++) uv[i] = uAlongZ ? new Vector2(pts[i].z, pts[i].x) : new Vector2(pts[i].x, pts[i].z);
            mb.Poly(m, pts, uv, Vector3.up);
        }

        /// <summary>矩形扣掉一个洞（秘密出口地毯下面要露出暗门）。</summary>
        static List<Rect> CutHole(Rect r, Rect? hole)
        {
            var list = new List<Rect>();
            if (hole == null || !r.Overlaps(hole.Value)) { list.Add(r); return list; }
            var h = hole.Value;
            float x0 = Mathf.Max(r.xMin, h.xMin), x1 = Mathf.Min(r.xMax, h.xMax);
            float y0 = Mathf.Max(r.yMin, h.yMin), y1 = Mathf.Min(r.yMax, h.yMax);
            if (h.yMin > r.yMin) list.Add(Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, y0));
            if (h.yMax < r.yMax) list.Add(Rect.MinMaxRect(r.xMin, y1, r.xMax, r.yMax));
            if (x0 > r.xMin) list.Add(Rect.MinMaxRect(r.xMin, y0, x0, y1));
            if (x1 < r.xMax) list.Add(Rect.MinMaxRect(x1, y0, r.xMax, y1));
            list.RemoveAll(q => q.width < .002f || q.height < .002f);
            return list;
        }

        static void FloorRect(MeshBatch mb, Material m, Rect r, float y, bool uAlongZ, Rect? hole)
        {
            foreach (var piece in CutHole(r, hole)) TopQuad(mb, m, piece, y, uAlongZ);
        }

        // =====================================================================
        // 室内：地面、天花、墙面细部
        // =====================================================================
        static void InteriorArchitecture(Transform parent, Room room)
        {
            var mb = new MeshBatch();
            float fl = FloorY(room);
            var r = room.r;
            var inner = Rect.MinMaxRect(r.xMin + T, r.yMin + T, r.xMax - T, r.yMax - T);
            Rect? hole = null;
            if (room.id == "Ima" || room.id == "Zashiki" || room.id == "Nando" || room.id == "Kura")
                hole = new Rect(r.center.x - 1f, r.center.y - .75f, 2f, 1.5f);

            if (IsTatami(room)) TatamiFloor(mb, inner, fl, hole);
            else
            {
                var fm = IsDoma(room) ? D.Tataki : IsTileRoom(room) ? D.BathTile : room.group == GKura ? D.KuraFloor : D.FloorWood;
                bool planks = fm == D.FloorWood || fm == D.KuraFloor;
                FloorRect(mb, fm, inner, fl + .002f, planks && inner.height > inner.width, hole);
            }
            Ceiling(mb, room, inner);
            GlassMB = new MeshBatch();
            foreach (var e in EdgesOf(room))
                if (HasWall(room, e)) WallDetails(mb, room, e);
            mb.Build(parent, $"Arch_{room.id}");
            GlassMB.Build(parent, $"Glass_{room.id}", false); // 透明玻璃单独一批，不投影
            GlassMB = null;
        }

        static void TatamiFloor(MeshBatch mb, Rect a, float fl, Rect? hole)
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt(a.width / .955f));
            int nz = Mathf.Max(1, Mathf.RoundToInt(a.height / .955f));
            float cw = a.width / nx, cd = a.height / nz;
            FloorRect(mb, D.TatamiSeam, a, fl + .003f, false, hole);
            const float inset = .0025f, heri = .028f;
            float y = fl + .0065f;
            foreach (var m in SolveTatami(nx, nz))
            {
                var r = Rect.MinMaxRect(a.xMin + m.xMin * cw + inset, a.yMin + m.yMin * cd + inset,
                    a.xMin + m.xMax * cw - inset, a.yMin + m.yMax * cd - inset);
                bool longX = r.width >= r.height;
                var body = longX
                    ? Rect.MinMaxRect(r.xMin, r.yMin + heri, r.xMax, r.yMax - heri)
                    : Rect.MinMaxRect(r.xMin + heri, r.yMin, r.xMax - heri, r.yMax);
                // 草席纹理 v 沿榻榻米长边
                FloorRect(mb, D.Tatami, body, y, longX, hole);
                var h1 = longX ? Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, r.yMin + heri) : Rect.MinMaxRect(r.xMin, r.yMin, r.xMin + heri, r.yMax);
                var h2 = longX ? Rect.MinMaxRect(r.xMin, r.yMax - heri, r.xMax, r.yMax) : Rect.MinMaxRect(r.xMax - heri, r.yMin, r.xMax, r.yMax);
                FloorRect(mb, D.Heri, h1, y + .0005f, !longX, hole);
                FloorRect(mb, D.Heri, h2, y + .0005f, !longX, hole);
            }
        }

        /// <summary>榻榻米铺法：2×1 格的整张和 1×1 的半张，不允许四角交汇成十字（不祝儀敷き）。</summary>
        static List<RectInt> SolveTatami(int nx, int nz)
        {
            var id = new int[nx, nz];
            int total = nx * nz;
            for (int halves = total % 2; halves <= total; halves += 2)
            {
                System.Array.Clear(id, 0, id.Length);
                var mats = new List<RectInt>();
                int budget = 400000;
                if (TatamiDfs(id, nx, nz, 0, halves, mats, ref budget)) return mats;
            }
            var all = new List<RectInt>();
            for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++) all.Add(new RectInt(x, z, 1, 1));
            return all;
        }

        static bool TatamiDfs(int[,] id, int nx, int nz, int idx, int halves, List<RectInt> mats, ref int budget)
        {
            if (--budget < 0) return false;
            int n = nx * nz;
            while (idx < n && id[idx % nx, idx / nx] != 0) idx++;
            if (idx >= n) return halves == 0;
            int x = idx % nx, z = idx / nx, k = mats.Count + 1;
            for (int opt = 0; opt < 3; opt++)
            {
                RectInt m;
                if (opt == 0) { if (x + 1 >= nx || id[x + 1, z] != 0) continue; m = new RectInt(x, z, 2, 1); }
                else if (opt == 1) { if (z + 1 >= nz || id[x, z + 1] != 0) continue; m = new RectInt(x, z, 1, 2); }
                else { if (halves == 0) continue; m = new RectInt(x, z, 1, 1); }
                Fill(id, m, k);
                mats.Add(m);
                if (NoCross(id, nx, nz, m) && TatamiDfs(id, nx, nz, idx + 1, opt == 2 ? halves - 1 : halves, mats, ref budget)) return true;
                mats.RemoveAt(mats.Count - 1);
                Fill(id, m, 0);
                if (budget < 0) return false;
            }
            return false;
        }

        static void Fill(int[,] id, RectInt m, int v)
        {
            for (int x = m.xMin; x < m.xMax; x++)
                for (int z = m.yMin; z < m.yMax; z++) id[x, z] = v;
        }

        static bool NoCross(int[,] id, int nx, int nz, RectInt m)
        {
            for (int vx = m.xMin; vx <= m.xMax; vx++)
                for (int vz = m.yMin; vz <= m.yMax; vz++)
                {
                    if (vx <= 0 || vz <= 0 || vx >= nx || vz >= nz) continue;
                    int a = id[vx - 1, vz - 1], b = id[vx, vz - 1], c = id[vx - 1, vz], d = id[vx, vz];
                    if (a == 0 || b == 0 || c == 0 || d == 0) continue;
                    if (a != b && a != c && b != d && c != d) return false;
                }
            return true;
        }

        static void Ceiling(MeshBatch mb, Room room, Rect a)
        {
            // 楼梯间只有楼板下面那一段有天花
            if (room.id == "Kaidan" || room.id == "UraKaidan") a.yMin = Mathf.Max(a.yMin, 13.195f * PlanScale);
            if (a.height < .2f || a.width < .2f) return;
            float cy = CeilY(room);
            bool dark = IsDoma(room) || room.group == GKura;
            bool longX = a.width >= a.height;
            var pts = new[]
            {
                new Vector3(a.xMin, cy - .001f, a.yMin), new Vector3(a.xMax, cy - .001f, a.yMin),
                new Vector3(a.xMax, cy - .001f, a.yMax), new Vector3(a.xMin, cy - .001f, a.yMax),
            };
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++) uv[i] = longX ? new Vector2(pts[i].x, pts[i].z) : new Vector2(pts[i].z, pts[i].x);
            mb.Poly(dark ? D.CeilingDark : D.Ceiling, pts, uv, Vector3.down);

            var trim = dark ? D.BeamDark : D.TrimMid;
            const float mw = .05f, mh = .04f; // 回り縁
            mb.Box(trim, new Vector3(a.center.x, cy - mh / 2, a.yMin + mw / 2), new Vector3(a.width, mh, mw));
            mb.Box(trim, new Vector3(a.center.x, cy - mh / 2, a.yMax - mw / 2), new Vector3(a.width, mh, mw));
            mb.Box(trim, new Vector3(a.xMin + mw / 2, cy - mh / 2, a.center.y), new Vector3(mw, mh, a.height));
            mb.Box(trim, new Vector3(a.xMax - mw / 2, cy - mh / 2, a.center.y), new Vector3(mw, mh, a.height));

            if (dark)
            {
                if (room.id == "UraKaidan") return;
                // 土间和土蔵：两根横跨短边的熏黑大梁
                bool kura = room.group == GKura;
                float bw = kura ? .26f : .22f, bh = kura ? .30f : .28f;
                for (int i = 1; i <= 2; i++)
                {
                    float t = i / 3f;
                    if (longX) mb.Box(D.BeamDark, new Vector3(Mathf.Lerp(a.xMin, a.xMax, t), cy - bh / 2, a.center.y), new Vector3(bw, bh, a.height));
                    else mb.Box(D.BeamDark, new Vector3(a.center.x, cy - bh / 2, Mathf.Lerp(a.yMin, a.yMax, t)), new Vector3(a.width, bh, bw));
                }
                return;
            }
            // 棹缘：沿长边约 0.45 m 一根，与天花板木纹垂直
            float len = longX ? a.width : a.height;
            int n = Mathf.FloorToInt(len / .45f);
            float step = len / (n + 1);
            for (int i = 1; i <= n; i++)
            {
                float s = (longX ? a.xMin : a.yMin) + i * step;
                if (longX) mb.Box(trim, new Vector3(s, cy - .016f, a.center.y), new Vector3(.032f, .03f, a.height - 2 * mw));
                else mb.Box(trim, new Vector3(a.center.x, cy - .016f, s), new Vector3(a.width - 2 * mw, .03f, .032f));
            }
        }

        static void WallDetails(MeshBatch mb, Room room, Edge e)
        {
            float fl = FloorY(room), cy = CeilY(room);
            var gaps = GapsOn(room, e);
            InnerSpan(room, e, out float lo, out float hi);
            bool tatami = IsTatami(room), doma = IsDoma(room), kura = room.group == GKura, tile = IsTileRoom(room);
            var doorCuts = new List<Vector2>();
            foreach (var g in gaps) if (g.door) doorCuts.Add(new Vector2(g.a, g.b));

            // 敷居（土间一侧有踏步，不放）
            if (!doma)
                foreach (var g in gaps)
                    if (g.door) WallBox(mb, D.TrimLight, e, g.a, g.b, fl - .002f, fl + .012f, 0f, T);

            // 腰板 / 踢脚
            float wain = tile ? 1.2f : (IsCorridor(room) || room.id == "Nando" || room.id == "Monooki") ? .9f : doma ? 1.0f : 0f;
            var wm = tile ? D.BathTile : doma ? D.WainscotDark : D.Wainscot;
            foreach (var seg in Minus(lo, hi, doorCuts))
            {
                if (kura) { WallBox(mb, D.BeamDark, e, seg.x, seg.y, fl, fl + .12f, T - .002f, T + .015f); continue; }
                if (wain <= 0f) { WallBox(mb, D.Post, e, seg.x, seg.y, fl, fl + .06f, T - .002f, T + .012f); continue; }
                var cuts = new List<float> { seg.x, seg.y };
                foreach (var g in gaps) if (!g.door) { cuts.Add(g.a); cuts.Add(g.b); }
                cuts.Sort();
                for (int i = 0; i < cuts.Count - 1; i++)
                {
                    float s0 = Mathf.Max(seg.x, cuts[i]), s1 = Mathf.Min(seg.y, cuts[i + 1]);
                    if (s1 - s0 < .005f) continue;
                    float mid = (s0 + s1) / 2, h = wain;
                    foreach (var g in gaps) if (!g.door && mid > g.a && mid < g.b) h = Mathf.Min(h, g.bottom - fl);
                    if (h < .05f) continue;
                    WallBox(mb, wm, e, s0, s1, fl, fl + h, T - .002f, T + .012f);
                    if (!tile) WallBox(mb, doma ? D.BeamDark : D.TrimMid, e, s0, s1, fl + h - .03f, fl + h, T - .002f, T + .022f);
                }
            }

            // 柱：转角、约 1.82 m 一根、窗两侧；门边已有门框柱
            if (!kura)
            {
                float pw = doma ? .15f : .105f, pp = doma ? .035f : .02f;
                var posts = new List<float> { lo + pw / 2, hi - pw / 2 };
                int n = Mathf.Max(1, Mathf.RoundToInt((hi - lo) / 1.82f));
                for (int i = 1; i < n; i++) posts.Add(Mathf.Lerp(lo, hi, i / (float)n));
                foreach (var g in gaps) if (!g.door) { posts.Add(g.a - pw / 2); posts.Add(g.b + pw / 2); }
                posts.Sort();
                float last = -999f;
                foreach (float s in posts)
                {
                    if (s - pw / 2 < lo - .001f || s + pw / 2 > hi + .001f || s - last < pw) continue;
                    bool blocked = false;
                    foreach (var g in gaps)
                    {
                        float pad = g.door ? .07f : 0f;
                        if (s + pw / 2 > g.a - pad + .002f && s - pw / 2 < g.b + pad - .002f) blocked = true;
                    }
                    if (blocked) continue;
                    WallBox(mb, doma ? D.BeamDark : D.Post, e, s - pw / 2, s + pw / 2, fl, cy, T - .005f, T + pp);
                    last = s;
                }
            }

            // 长押 + 欄間
            if (tatami)
            {
                float kt = room.group == G0 ? .55f + DoorTop : Mathf.Min(Levels[G1].wallTop, Levels[G1].floorTop + DoorTop);
                float ny = kt + .075f;
                WallBox(mb, D.TrimMid, e, lo, hi, ny, ny + .09f, T - .005f, T + .05f);
                foreach (var g in gaps)
                {
                    if (!g.door || g.top > kt + .01f) continue;
                    var other = RoomAt(OnWall(e, (g.a + g.b) / 2, fl + 1f, -.1f), room.group);
                    if (other == null || !IsTatami(other)) continue;
                    Ranma(mb, e, g.a, g.b, ny + .09f, cy - .06f);
                }
            }

            foreach (var g in gaps)
            {
                if (g.glass) GlassDoors(mb, e, g);
                else if (g.window)
                {
                    float y0 = g.bottom, y1 = g.top;
                    var fm = doma ? D.BeamDark : D.TrimMid;
                    WallBox(mb, fm, e, g.a - .04f, g.a, y0 - .05f, y1 + .05f, T - .005f, T + .03f);
                    WallBox(mb, fm, e, g.b, g.b + .04f, y0 - .05f, y1 + .05f, T - .005f, T + .03f);
                    WallBox(mb, fm, e, g.a - .04f, g.b + .04f, y1, y1 + .05f, T - .005f, T + .03f);
                    WallBox(mb, fm, e, g.a - .06f, g.b + .06f, y0 - .03f, y0, T - .005f, T + .06f);
                    for (float y = y0 + .26f; y < y1 - .05f; y += .26f)
                        WallBox(mb, D.TrimLight, e, g.a, g.b, y - .012f, y + .012f, .02f, .09f);
                }
                else if (!g.door)
                    WallBox(mb, D.TrimMid, e, g.a, g.b, g.bottom - .01f, g.bottom + .04f, -.01f, T + .02f); // 二楼栏杆扶手
            }
        }

        static MeshBatch GlassMB;

        /// <summary>
        /// 縁側朝院子的落地玻璃拉门（ガラス戸），关着：鴨居 + 敷居，门扇在两条轨道上交错排，
        /// 木框、下部腰板、上面 3×2 格玻璃，中间两扇的交接处有一个黄铜螺丝锁。
        /// </summary>
        static void GlassDoors(MeshBatch mb, Edge e, Gap g)
        {
            var gm = GlassMB ?? mb;
            float y0 = g.bottom, y1 = g.top;
            // 室内侧的门框（和窗框一样凸出墙面 3 cm），鴨居、敷居横穿整个墙厚
            WallBox(mb, D.TrimMid, e, g.a - .04f, g.a, y0, y1 + .05f, T - .005f, T + .03f);
            WallBox(mb, D.TrimMid, e, g.b, g.b + .04f, y0, y1 + .05f, T - .005f, T + .03f);
            WallBox(mb, D.TrimMid, e, g.a - .04f, g.b + .04f, y1, y1 + .05f, T - .005f, T + .03f);
            WallBox(mb, D.TrimMid, e, g.a, g.b, y1 - .045f, y1, -.005f, T + .005f);
            WallBox(mb, D.TrimLight, e, g.a - .04f, g.b + .04f, y0 - .02f, y0 + .015f, -.005f, T + .03f);
            int n = Mathf.Max(2, Mathf.RoundToInt((g.b - g.a) / .85f));
            float w = (g.b - g.a) / n, p0 = y0 + .015f, p1 = y1 - .03f;
            const float st = .045f;
            for (int i = 0; i < n; i++)
            {
                // 相邻门扇在不同轨道上，交接处各压 2.5 cm；两端插进门框
                float s0 = g.a + i * w - (i == 0 ? .015f : .025f), s1 = g.a + (i + 1) * w + (i == n - 1 ? .015f : .025f);
                float d0 = i % 2 == 0 ? .012f : .045f, d1 = d0 + .03f;
                WallBox(mb, D.TrimMid, e, s0, s0 + st, p0, p1, d0, d1);
                WallBox(mb, D.TrimMid, e, s1 - st, s1, p0, p1, d0, d1);
                WallBox(mb, D.TrimMid, e, s0 + st, s1 - st, p1 - .06f, p1, d0, d1);
                WallBox(mb, D.TrimMid, e, s0 + st, s1 - st, p0, p0 + .08f, d0, d1);
                WallBox(mb, D.Wainscot, e, s0 + st, s1 - st, p0 + .08f, p0 + .3f, d0 + .009f, d1 - .009f);
                WallBox(mb, D.TrimMid, e, s0 + st, s1 - st, p0 + .3f, p0 + .335f, d0, d1);
                float g0 = p0 + .335f, g1 = p1 - .06f, sm = (s0 + s1) / 2;
                for (int k = 1; k < 3; k++)
                {
                    float y = Mathf.Lerp(g0, g1, k / 3f);
                    WallBox(mb, D.TrimMid, e, s0 + st, s1 - st, y - .011f, y + .011f, d0 + .006f, d1 - .006f);
                }
                WallBox(mb, D.TrimMid, e, sm - .01f, sm + .01f, g0, g1, d0 + .006f, d1 - .006f);
                float dm = (d0 + d1) / 2;
                WallBox(gm, D.WindowGlass, e, s0 + st, s1 - st, g0, g1, dm - .002f, dm + .002f);
                // 引手：门扇外端的竖框上一小块铁片
                float hx = i % 2 == 0 ? s0 + .022f : s1 - .022f;
                WallBox(mb, D.Iron, e, hx - .01f, hx + .01f, y0 + .9f, y0 + 1.02f, d1, d1 + .003f);
            }
            // 中间两扇交接处的螺丝锁（ネジ締り錠）
            float mid = g.a + (n / 2) * w;
            WallBox(mb, D.Brass, e, mid - .03f, mid + .03f, y0 + .98f, y0 + 1.02f, .045f + .03f, .045f + .046f);
        }

        static void Ranma(MeshBatch mb, Edge e, float a, float b, float y0, float y1)
        {
            if (y1 - y0 < .12f) return;
            WallBox(mb, D.RanmaDark, e, a, b, y0, y1, T - .002f, T + .004f);
            WallBox(mb, D.TrimMid, e, a, b, y0, y0 + .03f, T - .002f, T + .03f);
            WallBox(mb, D.TrimMid, e, a, b, y1 - .03f, y1, T - .002f, T + .03f);
            WallBox(mb, D.TrimMid, e, a, a + .03f, y0, y1, T - .002f, T + .03f);
            WallBox(mb, D.TrimMid, e, b - .03f, b, y0, y1, T - .002f, T + .03f);
            for (float s = a + .055f; s < b - .04f; s += .055f)
                WallBox(mb, D.TrimLight, e, s - .007f, s + .007f, y0 + .03f, y1 - .03f, T, T + .02f);
            float m1 = Mathf.Lerp(y0, y1, .33f), m2 = Mathf.Lerp(y0, y1, .67f);
            WallBox(mb, D.TrimLight, e, a, b, m1 - .008f, m1 + .008f, T, T + .022f);
            WallBox(mb, D.TrimLight, e, a, b, m2 - .008f, m2 + .008f, T, T + .022f);
        }

        // =====================================================================
        // 收在墙里的推拉门：和室之间 = 襖，朝縁側 = 障子，玄関 = 格子门
        // =====================================================================
        static void DecorateSlidingPanels(Transform parent, Transform structure)
        {
            var mb = new MeshBatch();
            foreach (var mr in structure.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.name != "ParkedSlidingPanel") continue;
                var roomT = mr.transform.parent;
                Room room = null;
                foreach (var r in DRooms) if (roomT.name.StartsWith($"Room_{r.id}_")) room = r;
                if (room == null) continue;
                var b = mr.bounds;
                bool alongX = b.size.x > b.size.z;
                Edge e = default;
                float best = 999f;
                foreach (var ed in EdgesOf(room))
                {
                    if (ed.alongX != alongX) continue;
                    float dist = Mathf.Abs((alongX ? b.center.z : b.center.x) - ed.line);
                    if (dist < best) { best = dist; e = ed; }
                }
                float s0 = alongX ? b.min.x : b.min.z, s1 = alongX ? b.max.x : b.max.z;
                float y0 = b.min.y, y1 = b.max.y, face = T + .012f;
                if (mr.sharedMaterial == P.WoodDarkA) { KoshiDoor(mb, e, s0, s1, y0, y1, face); continue; }
                var other = RoomAt(OnWall(e, (s0 + s1) / 2, y0 + 1f, -.1f), room.group);
                if (other != null && other.id.StartsWith("Engawa")) ShojiPanel(mb, e, s0, s1, y0, y1, face);
                else
                {
                    mr.sharedMaterial = D.Fusuma;
                    FusumaPanel(mb, e, s0, s1, y0, y1, face);
                }
            }
            mb.Build(parent, "SlidingPanels_Detail");
        }

        static void FusumaPanel(MeshBatch mb, Edge e, float s0, float s1, float y0, float y1, float f)
        {
            const float w = .022f;
            WallBox(mb, D.Lacquer, e, s0, s1, y0, y0 + w, f - .004f, f + .006f);
            WallBox(mb, D.Lacquer, e, s0, s1, y1 - w, y1, f - .004f, f + .006f);
            WallBox(mb, D.Lacquer, e, s0, s0 + w, y0, y1, f - .004f, f + .006f);
            WallBox(mb, D.Lacquer, e, s1 - w, s1, y0, y1, f - .004f, f + .006f);
            var n = Inward(e);
            var rot = Quaternion.FromToRotation(Vector3.up, n);
            var c = OnWall(e, s1 - .09f, y0 + .82f, f - .002f);
            mb.Cyl(D.Brass, c, .036f, .036f, .007f, 14, true, rot);
            mb.Cyl(D.Black, c + n * .005f, .024f, .024f, .004f, 12, true, rot);
        }

        static void ShojiPanel(MeshBatch mb, Edge e, float s0, float s1, float y0, float y1, float f)
        {
            const float w = .03f, koshi = .28f;
            var m = D.TrimLight;
            WallBox(mb, m, e, s0, s1, y0, y0 + koshi, f - .004f, f + .004f);
            WallBox(mb, m, e, s0, s1, y0 + koshi, y0 + koshi + w, f - .004f, f + .01f);
            WallBox(mb, m, e, s0, s1, y1 - w, y1, f - .004f, f + .01f);
            WallBox(mb, m, e, s0, s0 + w, y0, y1, f - .004f, f + .01f);
            WallBox(mb, m, e, s1 - w, s1, y0, y1, f - .004f, f + .01f);
            float n = Mathf.Max(1f, Mathf.Round((s1 - s0 - 2 * w) / .19f));
            for (int i = 1; i < n; i++)
            {
                float s = Mathf.Lerp(s0 + w, s1 - w, i / n);
                WallBox(mb, m, e, s - .006f, s + .006f, y0 + koshi + w, y1 - w, f - .002f, f + .008f);
            }
            for (float y = y0 + koshi + w + .24f; y < y1 - w - .05f; y += .24f)
                WallBox(mb, m, e, s0 + w, s1 - w, y - .006f, y + .006f, f - .002f, f + .008f);
        }

        static void KoshiDoor(MeshBatch mb, Edge e, float s0, float s1, float y0, float y1, float f)
        {
            const float w = .045f;
            var m = D.Post;
            WallBox(mb, m, e, s0, s1, y0, y0 + .12f, f - .004f, f + .01f);
            WallBox(mb, m, e, s0, s1, y1 - w, y1, f - .004f, f + .01f);
            WallBox(mb, m, e, s0, s0 + w, y0, y1, f - .004f, f + .01f);
            WallBox(mb, m, e, s1 - w, s1, y0, y1, f - .004f, f + .01f);
            WallBox(mb, m, e, s0, s1, y0 + .9f, y0 + .93f, f - .004f, f + .012f);
            for (float s = s0 + w + .03f; s < s1 - w - .01f; s += .05f)
                WallBox(mb, m, e, s - .01f, s + .01f, y0 + .12f, y1 - w, f - .002f, f + .008f);
        }
    }
}
