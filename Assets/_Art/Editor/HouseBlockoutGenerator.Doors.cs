using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 门（2026-11-19 用户确认）：
    ///   · 所有带门扇的门做成能开关的推拉门（HakoOnna.Art.DoorVisual），默认开着。
    ///     门扇挂在门洞一侧墙面的上轨道上（上吊り引き戸），开着时停在门洞旁边的墙面上，门洞全宽可走。
    ///     停在哪一面、往哪边停，在家具搬家时选（ChooseDoorParking）：不碰容器和钉住的陈设，优先走廊那一面；
    ///     选好的停靠区当成禁区，压到的家具、陈设沿墙让开，第三遍补家具也避开。
    ///   · 縁側朝院子的玻璃拉门是锁着的门：不能开，只能晃（Rattle），对应的 Window_Glass 挂 DoorLink。
    ///   · 封掉的门洞里挑 3 处做成钉死的旧门：一面钉木板、贴符纸、有血手印，另一面是抓痕和小孩的血手印。
    /// 碰撞只有能开关的门扇本身（Doors/Door_*/Leaf_*，BoxCollider + 运动学 Rigidbody），封门和轨道没有碰撞（后面是实墙）。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        internal enum DoorKind { None, Fusuma, Shoji, Itado, Koshi, Kura }

        static Opening Door(Opening o, DoorKind kind, int leaves)
        {
            o.kind = kind;
            o.leaves = leaves;
            return o;
        }

        sealed class DoorPlan
        {
            public Opening o;
            public Room neg, pos;      // 墙线两侧的房间（坐标小 / 大的一侧；室外为 null）
            public float floor, top;   // 门扇底所在的地板高、门洞顶
            public float lo, hi;       // 门洞沿墙范围
            public float[] s0, s1;     // 每扇关着时的沿墙范围
            public int[] track;        // 每扇在哪条轨道（0 靠墙，1 外侧）
            public Room face;          // 门扇挂在哪个房间那一面墙上（null = 没选到）
            public Edge edge;          // face 房间的那条墙边
            public int[] side;         // 开着时每扇停在 −1（lo 侧）还是 +1（hi 侧）
            public string name;
        }

        static List<DoorPlan> DoorPlans = new List<DoorPlan>();

        /// <summary>开着的门扇停靠的墙面 + 钉死旧门前面：家具、陈设都不能压（Rescale 的 Conflicts、第三遍补家具都看它）。</summary>
        static readonly List<Bounds> ParkZones = new List<Bounds>();

        static float LeafThickness(DoorKind k) => k switch
        {
            DoorKind.Fusuma => .02f,
            DoorKind.Shoji => .03f,
            DoorKind.Koshi => .035f,
            DoorKind.Kura => .06f,
            _ => .03f,
        };

        /// <summary>第 track 条轨道上门扇靠墙一面离墙线的距离（墙面在 T；门柱、鴨居凸出 2.5 cm，柱子最多 3.5 cm）。</summary>
        static float TrackBack(DoorPlan p, int track) => T + .04f + track * (LeafThickness(p.o.kind) + .008f);

        static float RailFront(DoorPlan p)
        {
            int maxTrack = 0;
            foreach (int t in p.track) maxTrack = Mathf.Max(maxTrack, t);
            return TrackBack(p, maxTrack) + LeafThickness(p.o.kind) + .008f;
        }

        static float DoorTopOf(Opening o, Room room)
        {
            var lv = Levels[room.group];
            float top = Mathf.Min(lv.wallTop, FloorHeight(room) + o.top);
            if (room.group == G0) top = o.top == DoorTop ? .55f + DoorTop : o.top;
            return top;
        }

        static (float open, float close) DoorTiming(DoorKind k) => k switch
        {
            DoorKind.Itado => (.7f, .6f),
            DoorKind.Koshi => (.75f, .65f),
            DoorKind.Kura => (1.4f, 1.2f),
            _ => (.5f, .42f),
        };

        // =====================================================================
        // 规划：两侧房间、门扇排布（家具之前）
        // =====================================================================
        static void PlanDoors()
        {
            DoorPlans = new List<DoorPlan>();
            ParkZones.Clear();
            foreach (var o in DOps)
            {
                if (o.kind == DoorKind.None || o.window || o.glass || o.leaves <= 0) continue;
                var p = new DoorPlan { o = o, lo = o.center - o.width / 2, hi = o.center + o.width / 2 };
                Vector3 Probe(float d) => o.alongX ? new Vector3(o.center, 0f, o.line + d) : new Vector3(o.line + d, 0f, o.center);
                p.neg = RoomAt(Probe(-.25f), o.group);
                p.pos = RoomAt(Probe(.25f), o.group);
                if (p.neg != null && !p.neg.hasFloor) p.neg = null;
                if (p.pos != null && !p.pos.hasFloor) p.pos = null;
                var any = p.neg ?? p.pos;
                if (any == null) continue;
                p.floor = Mathf.Max(p.neg != null ? FloorY(p.neg) : 0f, p.pos != null ? FloorY(p.pos) : 0f);
                p.top = DoorTopOf(o, any);
                p.name = $"Door_{p.neg?.id ?? "Outside"}_{p.pos?.id ?? "Outside"}";
                LeafLayout(p);
                DoorPlans.Add(p);
            }
        }

        /// <summary>关着时的排布：两端各插进门柱 3 cm，相邻门扇交接处压 3 cm、在不同轨道上。</summary>
        static void LeafLayout(DoorPlan p)
        {
            int n = p.o.leaves;
            const float jamb = .03f, overlap = .03f;
            float q = (p.hi - p.lo) / n;
            p.s0 = new float[n];
            p.s1 = new float[n];
            p.track = new int[n];
            for (int i = 0; i < n; i++)
            {
                p.s0[i] = i == 0 ? p.lo - jamb : p.lo + i * q - overlap / 2;
                p.s1[i] = i == n - 1 ? p.hi + jamb : p.lo + (i + 1) * q + overlap / 2;
                p.track[i] = n == 1 ? 0 : (i % 2 == 0 ? 1 : 0);
            }
        }

        static IEnumerable<int[]> SideModes(int n)
        {
            switch (n)
            {
                case 1:
                    yield return new[] { -1 };
                    yield return new[] { 1 };
                    break;
                case 2:
                    yield return new[] { -1, 1 };
                    yield return new[] { -1, -1 };
                    yield return new[] { 1, 1 };
                    break;
                case 4:
                    yield return new[] { -1, -1, 1, 1 };
                    break;
                default:
                    var all = new int[n];
                    for (int i = 0; i < n; i++) all[i] = i < n / 2 ? -1 : 1;
                    yield return all;
                    break;
            }
        }

        static string SidesText(int[] sides)
        {
            var sb = new System.Text.StringBuilder();
            foreach (int s in sides) sb.Append(s < 0 ? '←' : '→');
            return sb.ToString();
        }

        static bool FaceEdge(Room room, Opening o, out Edge edge)
        {
            foreach (var e in EdgesOf(room))
                if (e.alongX == o.alongX && Mathf.Abs(e.line - o.line) < .01f && HasWall(room, e)) { edge = e; return true; }
            edge = default;
            return false;
        }

        /// <summary>开着时停在 sgn 一侧的门扇占的沿墙范围（没有门扇停这边返回 false）。</summary>
        static bool ParkSpan(DoorPlan p, int[] sides, int sgn, out float a, out float b)
        {
            a = float.MaxValue; b = float.MinValue;
            for (int i = 0; i < sides.Length; i++)
            {
                if (sides[i] != sgn) continue;
                float w = p.s1[i] - p.s0[i];
                a = Mathf.Min(a, sgn < 0 ? p.lo - w : p.hi);
                b = Mathf.Max(b, sgn < 0 ? p.lo : p.hi + w);
            }
            return a < b;
        }

        static bool ParkValid(DoorPlan p, Room face, Edge e, int[] sides)
        {
            InnerSpan(face, e, out float lo, out float hi);
            var gaps = GapsOn(face, e);
            var sealedSpans = SealedSpansOn(face, e);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                if (!ParkSpan(p, sides, sgn, out float a, out float b)) continue;
                if (a < lo + .12f || b > hi - .12f) return false;   // 墙角柱
                foreach (var g in gaps)
                {
                    if (Mathf.Abs(g.a - p.lo) < .02f && Mathf.Abs(g.b - p.hi) < .02f) continue;
                    if (a - .08f < g.b && b + .08f > g.a) return false; // 别的门窗（含门柱、窗框）
                }
                foreach (var sc in sealedSpans)
                    if (a - .2f < sc.y && b + .2f > sc.x) return false;
            }
            return true;
        }

        static List<Bounds> ParkBoxes(DoorPlan p, Edge e, int[] sides)
        {
            var list = new List<Bounds>();
            float d1 = RailFront(p) + .03f;
            for (int sgn = -1; sgn <= 1; sgn += 2)
                if (ParkSpan(p, sides, sgn, out float a, out float b))
                    list.Add(BoxOnWall(e, a - .03f, b + .03f, p.floor + .025f, p.top + .08f, T - .01f, d1));
            return list;
        }

        /// <summary>
        /// 家具推回墙边之后（SolveRelocation 里）调用：每扇门在两面、各种停法里选代价最小的。
        /// 钉住的陈设、出生点 = 不行；容器 1000；其他场景物体（代码家具、保险箱、仏坛）60；带碰撞的陈设 8；其余 2。
        /// 走廊一面 −3（走廊一般空）；两扇以上都停在同一侧 +1.5（不对称）。
        /// </summary>
        static void ChooseDoorParking(List<RGroup> groups)
        {
            ParkZones.Clear();
            ParkZones.AddRange(SealedKeepZones());
            if (DoorPlans == null) return;
            var log = new System.Text.StringBuilder();
            int chosen = 0;
            foreach (var p in DoorPlans)
            {
                float best = float.MaxValue;
                Room bestFace = null;
                Edge bestEdge = default;
                int[] bestSides = null;
                string bestHits = "";
                foreach (var face in new[] { p.neg, p.pos })
                {
                    if (face == null || Mathf.Abs(FloorY(face) - p.floor) > .02f) continue;
                    if (!FaceEdge(face, p.o, out var e)) continue;
                    foreach (var sides in SideModes(p.o.leaves))
                    {
                        if (!ParkValid(p, face, e, sides)) continue;
                        var boxes = ParkBoxes(p, e, sides);
                        bool taken = false;
                        foreach (var z in boxes)
                            foreach (var other in ParkZones)
                                if (z.Intersects(other)) taken = true; // 别的门已经停在这里 / 钉死旧门前面
                        if (taken) continue;
                        float cost = 0f;
                        var hits = new List<string>();
                        foreach (var z in boxes)
                            foreach (var g in groups)
                            {
                                if (g.dropped) continue;
                                var b = g.At(g.s);
                                b.Expand(-.01f);
                                if (!b.Intersects(z)) continue;
                                float c = g.pinned || g.spawn || g.room == null ? 1e6f : g.hasBox ? 1000f : g.hasGo ? 60f : g.solid ? 8f : 2f;
                                cost += c;
                                if (c >= 60f) hits.Add(g.label);
                            }
                        if (IsCorridor(face)) cost -= 3f;
                        if (sides.Length >= 2 && sides[0] == sides[sides.Length - 1]) cost += 1.5f;
                        if (cost < best)
                        {
                            best = cost;
                            bestFace = face;
                            bestEdge = e;
                            bestSides = sides;
                            bestHits = string.Join("、", hits);
                        }
                    }
                }
                if (bestFace == null)
                {
                    Debug.LogWarning($"[Hako Art] {p.name}：两面墙都没有能停门扇的地方，这扇门不做门扇");
                    continue;
                }
                p.face = bestFace;
                p.edge = bestEdge;
                p.side = bestSides;
                ParkZones.AddRange(ParkBoxes(p, bestEdge, bestSides));
                chosen++;
                log.Append($" {p.name}→{bestFace.id}[{SidesText(bestSides)}]");
                if (best >= 60f) log.Append($"（挤到 {bestHits}）");
            }
            Debug.Log($"[Hako Art] 门扇停靠：{chosen}/{DoorPlans.Count} 扇门选好停靠面；" + log);
        }

        /// <summary>Rescale.Conflicts 用：组压到门扇停靠区 / 钉死旧门前面就返回原因。</summary>
        static string ParkConflict(Bounds b)
        {
            if (ParkZones.Count == 0) return null;
            b.Expand(-.008f);
            foreach (var z in ParkZones)
                if (b.Intersects(z)) return "压到门扇停靠区" + Rng(z);
            return null;
        }

        // =====================================================================
        // 生成门扇
        // =====================================================================
        static void BuildDoors(Transform root, Transform structure)
        {
            var doors = ArtKit.Empty(root, "Doors", Vector3.zero);
            int built = 0, leaves = 0;
            foreach (var p in DoorPlans)
            {
                if (p.face == null) continue;
                leaves += BuildSlidingDoor(doors, p);
                built++;
            }
            int glass = 0;
            var perRoom = new Dictionary<string, int>();
            foreach (var (room, e, g) in GlassSpecs)
            {
                perRoom.TryGetValue(room.id, out int k);
                perRoom[room.id] = k + 1;
                BuildGlassDoor(doors, structure, room, e, g, k);
                glass++;
            }
            Debug.Log($"[Hako Art] 门：{built} 扇推拉门（{leaves} 片门扇，默认开着），{glass} 组锁着的玻璃拉门");
        }

        /// <summary>会动的门扇不能静态合批（MeshBatch.Build 默认标了 BatchingStatic / OccludeeStatic）。</summary>
        static void ClearStatic(Transform t)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(c.gameObject, 0);
        }

        static int BuildSlidingDoor(Transform parent, DoorPlan p)
        {
            var o = p.o;
            var e = p.edge;
            var rootPos = o.alongX ? new Vector3((p.lo + p.hi) / 2, p.floor, o.line) : new Vector3(o.line, p.floor, (p.lo + p.hi) / 2);
            var door = ArtKit.Empty(parent, p.name, rootPos);
            int n = o.leaves;
            float th = LeafThickness(o.kind);
            float y0 = p.floor + .015f, y1 = p.top + .045f; // 底下留 1.5 cm，顶插进轨道
            var leaves = new HakoOnna.Art.DoorVisual.Leaf[n];
            Bounds closed = default;
            int seed = StableHash(p.name);
            for (int i = 0; i < n; i++)
            {
                var leaf = ArtKit.Empty(door, $"Leaf_{i}", Vector3.zero);
                var mb = new MeshBatch { M = Matrix4x4.Translate(-rootPos) };
                var soft = new MeshBatch { M = Matrix4x4.Translate(-rootPos) };
                float d0 = TrackBack(p, p.track[i]), d1 = d0 + th;
                int handle = p.side[i] < 0 ? 1 : -1; // 引手在靠门洞中间的一边：停好后也在门洞边上
                BuildLeaf(o.kind, mb, soft, e, p.s0[i], p.s1[i], y0, y1, d0, d1, handle, seed + i * 7919, false);
                mb.Build(leaf, "Leaf");
                soft.Build(leaf, "Paper", false);
                ClearStatic(leaf);

                var wb = BoxOnWall(e, p.s0[i] + .015f, p.s1[i] - .015f, y0, y1 - .05f, d0, d1);
                var bc = leaf.gameObject.AddComponent<BoxCollider>();
                bc.center = wb.center - rootPos;
                bc.size = wb.size;
                var rb = leaf.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                var local = new Bounds(wb.center - rootPos, wb.size);
                if (i == 0) closed = local; else closed.Encapsulate(local);

                float ds = p.side[i] < 0 ? p.lo - p.s1[i] : p.hi - p.s0[i];
                leaves[i] = new HakoOnna.Art.DoorVisual.Leaf
                {
                    part = leaf,
                    openOffset = o.alongX ? new Vector3(ds, 0f, 0f) : new Vector3(0f, 0f, ds),
                };
            }

            // 上轨道（付け鴨居）：横跨门洞和两侧停靠的范围，门扇顶插在里面；两端有挡块
            float ra = p.lo, rb2 = p.hi;
            for (int sgn = -1; sgn <= 1; sgn += 2)
                if (ParkSpan(p, p.side, sgn, out float a, out float b)) { ra = Mathf.Min(ra, a); rb2 = Mathf.Max(rb2, b); }
            ra -= .03f; rb2 += .03f;
            var rail = new MeshBatch { M = Matrix4x4.Translate(-rootPos) };
            float rf = RailFront(p);
            var railMat = IsDoma(p.face) || p.face.group == GKura ? D.BeamDark : D.TrimMid;
            WallBox(rail, railMat, e, ra, rb2, p.top - .015f, p.top + .07f, T - .002f, rf, true);
            WallBox(rail, D.BeamDark, e, ra + .01f, rb2 - .01f, p.top - .02f, p.top - .015f, T + .03f, rf - .004f, true); // 轨道底缝的阴影
            foreach (float s in new[] { ra, rb2 })
                WallBox(rail, D.Iron, e, s - .012f, s + .012f, p.top - .035f, p.top - .015f, T + .02f, rf - .004f);
            rail.Build(door, "Rail");

            var dv = door.gameObject.AddComponent<HakoOnna.Art.DoorVisual>();
            float h = p.top - p.floor;
            var opening = o.alongX
                ? new Bounds(new Vector3(0f, h / 2, 0f), new Vector3(p.hi - p.lo, h, 2 * T))
                : new Bounds(new Vector3(0f, h / 2, 0f), new Vector3(2 * T, h, p.hi - p.lo));
            var (open, close) = DoorTiming(o.kind);
            dv.Configure(leaves, opening, closed, false, true, open, close);
            return n;
        }

        // =====================================================================
        // 门扇样式（墙线坐标：s 沿墙，y 高，d 离墙线向 face 房间；d0 靠墙一面，d1 外面）
        // handle = +1：引手在 s1 一边，−1：在 s0 一边。fixedPanel = 钉死旧门的门板（不动、更破）
        // =====================================================================
        static void BuildLeaf(DoorKind kind, MeshBatch mb, MeshBatch soft, Edge e, float s0, float s1, float y0, float y1,
            float d0, float d1, int handle, int seed, bool fixedPanel)
        {
            var rnd = new System.Random(seed);
            switch (kind)
            {
                case DoorKind.Fusuma: FusumaLeaf(mb, e, s0, s1, y0, y1, d0, d1, handle, rnd, fixedPanel); break;
                case DoorKind.Shoji: ShojiLeaf(mb, soft, e, s0, s1, y0, y1, d0, d1, handle, rnd); break;
                case DoorKind.Koshi: KoshiLeaf(mb, soft, e, s0, s1, y0, y1, d0, d1, handle); break;
                case DoorKind.Kura: KuraLeaf(mb, e, s0, s1, y0, y1, d0, d1, handle); break;
                default: ItadoLeaf(mb, e, s0, s1, y0, y1, d0, d1, handle, rnd, fixedPanel); break;
            }
        }

        static float R01(System.Random r) => (float)r.NextDouble();

        /// <summary>襖：黑漆边框、唐纸两面、黄铜圆引手；一半的门扇有破洞（露出里面的木骨）和翻起来的纸片。</summary>
        static void FusumaLeaf(MeshBatch mb, Edge e, float s0, float s1, float y0, float y1, float d0, float d1, int handle,
            System.Random rnd, bool fixedPanel)
        {
            const float w = .022f;
            WallBox(mb, D.Lacquer, e, s0, s1, y0, y0 + w, d0, d1);
            WallBox(mb, D.Lacquer, e, s0, s1, y1 - w, y1, d0, d1);
            WallBox(mb, D.Lacquer, e, s0, s0 + w, y0 + w, y1 - w, d0, d1);
            WallBox(mb, D.Lacquer, e, s1 - w, s1, y0 + w, y1 - w, d0, d1);
            var paper = fixedPanel ? DM.PaperOld : D.Fusuma;
            WallBox(mb, paper, e, s0 + w, s1 - w, y0 + w, y1 - w, d0 + .0015f, d1 - .0015f);
            float hs = handle > 0 ? s1 - .1f : s0 + .1f, hy = y0 + 1.0f;
            Hikite(mb, e, hs, hy, d1, 1);
            if (!fixedPanel) Hikite(mb, e, hs, hy, d0, -1);
            // 下半截发黄的水渍：一条不规则的带子
            if (R01(rnd) < .7f) Stain(mb, e, s0 + w, s1 - w, y0 + w, y0 + .25f + R01(rnd) * .35f, d1 + .0008f, 1, rnd);
            int tears = fixedPanel ? 2 + rnd.Next(2) : (R01(rnd) < .55f ? 1 + rnd.Next(2) : 0);
            for (int t = 0; t < tears; t++)
            {
                bool front = fixedPanel || R01(rnd) < .6f;
                float r = .05f + R01(rnd) * (fixedPanel ? .14f : .1f);
                float lo = s0 + w + r + .03f, hi = s1 - w - r - .03f;
                if (hi <= lo) continue;
                float ts = Mathf.Lerp(lo, hi, R01(rnd));
                float ty = y0 + .25f + R01(rnd) * 1.4f;
                Tear(mb, e, ts, ty, front ? d1 : d0, front ? 1 : -1, r, rnd, paper);
            }
        }

        static void Hikite(MeshBatch mb, Edge e, float s, float y, float d, int dir)
        {
            var n = Inward(e) * dir;
            var rot = Quaternion.FromToRotation(Vector3.up, n);
            var c = OnWall(e, s, y, d);
            mb.Cyl(D.Brass, c, .038f, .038f, .005f, 16, true, rot);
            mb.Cyl(D.Black, c + n * .004f, .025f, .025f, .0025f, 14, true, rot);
        }

        /// <summary>破洞：黑洞（锯齿边，用三角扇拼）+ 洞里露出的两根木骨 + 洞上沿翻出来的一片纸。</summary>
        static void Tear(MeshBatch mb, Edge e, float s, float y, float face, int dir, float r, System.Random rnd, Material paper)
        {
            var n = Inward(e) * dir;
            const int k = 11;
            var ring = new Vector3[k];
            for (int i = 0; i < k; i++)
            {
                float ang = (i + (R01(rnd) - .5f) * .5f) * Mathf.PI * 2f / k;
                float rad = r * (.75f + .35f * R01(rnd));
                ring[i] = OnWall(e, s + Mathf.Cos(ang) * rad, y + Mathf.Sin(ang) * rad * 1.25f, face + dir * .0012f);
            }
            var c = OnWall(e, s, y, face + dir * .0012f);
            var uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            for (int i = 0; i < k; i++)
                mb.Poly(D.RanmaDark, new[] { c, ring[i], ring[(i + 1) % k] }, uv, n);
            // 洞里的木骨（襖的组子）
            float rib = r * .7f;
            WallBox(mb, D.TrimLight, e, s - rib, s + rib, y - .007f, y + .007f, face + dir * .0018f, face + dir * .004f);
            WallBox(mb, D.TrimLight, e, s + r * .15f - .007f, s + r * .15f + .007f, y - rib, y + rib, face + dir * .0018f, face + dir * .004f);
            // 翻出来的纸片
            var a = OnWall(e, s - r * .5f, y + r * .95f, face + dir * .002f);
            var b = OnWall(e, s + r * .45f, y + r * 1.0f, face + dir * .002f);
            var tip = OnWall(e, s + r * .1f, y - r * .25f, face + dir * (.02f + .02f * R01(rnd)));
            mb.Poly(paper, new[] { a, b, tip }, new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(.5f, 0f) }, n);
        }

        /// <summary>水渍：下沿平、上沿起伏的一条带子（DM.Stain 半透明，顶上一道深色水线）。UV：u 沿墙米数，v 0 底 → 1 顶。</summary>
        static void Stain(MeshBatch mb, Edge e, float s0, float s1, float y0, float y1, float d, int dir, System.Random rnd)
        {
            var n = Inward(e) * dir;
            int k = Mathf.Max(3, Mathf.RoundToInt((s1 - s0) / .08f));
            float off = R01(rnd) * 50f;
            float Top(float s) => Mathf.Lerp(y0 + (y1 - y0) * .45f, y1, Mathf.PerlinNoise(s * 4.3f + off, off * .37f));
            for (int i = 0; i < k; i++)
            {
                float a = Mathf.Lerp(s0, s1, i / (float)k), b = Mathf.Lerp(s0, s1, (i + 1f) / k);
                var pts = new[] { OnWall(e, a, y0, d), OnWall(e, b, y0, d), OnWall(e, b, Top(b), d), OnWall(e, a, Top(a), d) };
                mb.Poly(DM.Stain, pts, new[] { new Vector2(a, 0f), new Vector2(b, 0f), new Vector2(b, 1f), new Vector2(a, 1f) }, n);
            }
        }

        /// <summary>障子：杉木框、腰板、靠墙一面的组子；纸贴在外面一格一张，有的发黄、有的破了只剩一条。纸单独一批，不投影。</summary>
        static void ShojiLeaf(MeshBatch mb, MeshBatch soft, Edge e, float s0, float s1, float y0, float y1, float d0, float d1,
            int handle, System.Random rnd)
        {
            const float w = .03f, koshi = .3f;
            var m = D.TrimLight;
            WallBox(mb, m, e, s0, s1, y0, y0 + koshi, d0, d1);
            WallBox(mb, m, e, s0, s1, y1 - w, y1, d0, d1);
            WallBox(mb, m, e, s0, s0 + w, y0 + koshi, y1 - w, d0, d1);
            WallBox(mb, m, e, s1 - w, s1, y0 + koshi, y1 - w, d0, d1);
            WallBox(mb, m, e, s0 + w, s1 - w, y0 + koshi, y0 + koshi + .025f, d0, d1);
            float ga = y0 + koshi + .025f, gb = y1 - w;
            int nv = Mathf.Max(2, Mathf.RoundToInt((s1 - s0 - 2 * w) / .2f));
            int nh = Mathf.Max(3, Mathf.RoundToInt((gb - ga) / .27f));
            float kd1 = d0 + .018f;
            for (int i = 1; i < nv; i++)
            {
                float s = Mathf.Lerp(s0 + w, s1 - w, i / (float)nv);
                WallBox(mb, m, e, s - .006f, s + .006f, ga, gb, d0, kd1);
            }
            for (int j = 1; j < nh; j++)
            {
                float y = Mathf.Lerp(ga, gb, j / (float)nh);
                WallBox(mb, m, e, s0 + w, s1 - w, y - .006f, y + .006f, d0, kd1);
            }
            float pd0 = d1 - .004f, pd1 = d1 - .003f;
            var n = Inward(e);
            for (int i = 0; i < nv; i++)
                for (int j = 0; j < nh; j++)
                {
                    float a = Mathf.Lerp(s0 + w, s1 - w, i / (float)nv), b = Mathf.Lerp(s0 + w, s1 - w, (i + 1f) / nv);
                    float ya = Mathf.Lerp(ga, gb, j / (float)nh), yb = Mathf.Lerp(ga, gb, (j + 1f) / nh);
                    float r = R01(rnd);
                    if (r < .14f)
                    {
                        // 破了：只剩上沿一条锯齿状的碎纸
                        if (R01(rnd) < .65f)
                        {
                            float h1 = (yb - ya) * (.15f + .3f * R01(rnd)), h2 = (yb - ya) * (.05f + .2f * R01(rnd));
                            var pts = new[]
                            {
                                OnWall(e, a, yb, pd1), OnWall(e, b, yb, pd1), OnWall(e, b, yb - h2, pd1),
                                OnWall(e, (a + b) / 2, yb - h1, pd1), OnWall(e, a, yb - h1 * .6f, pd1),
                            };
                            soft.Poly(R01(rnd) < .5f ? DM.ShojiOld : P.Paper, pts,
                                new[] { Vector2.up, Vector2.one, Vector2.right, new Vector2(.5f, 0f), Vector2.zero }, n);
                        }
                        continue;
                    }
                    WallBox(soft, r < .38f ? DM.ShojiOld : P.Paper, e, a, b, ya, yb, pd0, pd1);
                }
            float hs = handle > 0 ? s1 - w / 2 : s0 + w / 2;
            WallBox(mb, D.RanmaDark, e, hs - .009f, hs + .009f, y0 + .95f, y0 + 1.07f, d1, d1 + .0015f);
            WallBox(mb, D.RanmaDark, e, hs - .009f, hs + .009f, y0 + .95f, y0 + 1.07f, d0 - .0015f, d0);
        }

        /// <summary>板戸：框、竖板（板缝）、横桟、铁引手；下框受潮发黑。</summary>
        static void ItadoLeaf(MeshBatch mb, Edge e, float s0, float s1, float y0, float y1, float d0, float d1, int handle,
            System.Random rnd, bool fixedPanel)
        {
            const float sw = .07f, tr = .08f, br = .12f;
            var fm = D.Post;
            WallBox(mb, fm, e, s0, s0 + sw, y0, y1, d0, d1);
            WallBox(mb, fm, e, s1 - sw, s1, y0, y1, d0, d1);
            WallBox(mb, fm, e, s0 + sw, s1 - sw, y1 - tr, y1, d0, d1, true);
            WallBox(mb, D.BeamDark, e, s0 + sw, s1 - sw, y0, y0 + br, d0, d1, true);
            float pa = y0 + br, pb = y1 - tr;
            WallBox(mb, D.TrimMid, e, s0 + sw, s1 - sw, pa, pb, d0 + .007f, d1 - .007f);
            for (float s = s0 + sw + .17f; s < s1 - sw - .05f; s += .17f)
            {
                WallBox(mb, D.RanmaDark, e, s - .0025f, s + .0025f, pa, pb, d1 - .0072f, d1 - .0066f);
                WallBox(mb, D.RanmaDark, e, s - .0025f, s + .0025f, pa, pb, d0 + .0066f, d0 + .0072f);
            }
            for (float y = pa + .35f; y < pb - .1f; y += .45f)
            {
                WallBox(mb, fm, e, s0 + sw, s1 - sw, y - .02f, y + .02f, d1 - .007f, d1 + .004f, true);
                WallBox(mb, fm, e, s0 + sw, s1 - sw, y - .02f, y + .02f, d0 - .004f, d0 + .007f, true);
            }
            float hs = handle > 0 ? s1 - .045f : s0 + .045f;
            WallBox(mb, D.Iron, e, hs - .018f, hs + .018f, y0 + .95f, y0 + 1.1f, d1, d1 + .004f);
            if (!fixedPanel) WallBox(mb, D.Iron, e, hs - .018f, hs + .018f, y0 + .95f, y0 + 1.1f, d0 - .004f, d0);
            if (R01(rnd) < .6f) Stain(mb, e, s0 + sw, s1 - sw, pa, pa + .2f + R01(rnd) * .3f, d1 - .0062f, 1, rnd);
        }

        /// <summary>玄関的格子拉门：深色框、下部腰板、磨砂玻璃（单独一批不投影）、外面一排细格子。</summary>
        static void KoshiLeaf(MeshBatch mb, MeshBatch soft, Edge e, float s0, float s1, float y0, float y1, float d0, float d1, int handle)
        {
            const float sw = .06f;
            var fm = D.BeamDark;
            WallBox(mb, fm, e, s0, s0 + sw, y0, y1, d0, d1);
            WallBox(mb, fm, e, s1 - sw, s1, y0, y1, d0, d1);
            WallBox(mb, fm, e, s0 + sw, s1 - sw, y1 - .06f, y1, d0, d1, true);
            WallBox(mb, fm, e, s0 + sw, s1 - sw, y0, y0 + .15f, d0, d1, true);
            float ka = y0 + .15f, kb = y0 + .5f;
            WallBox(mb, D.Post, e, s0 + sw, s1 - sw, ka, kb, d0 + .006f, d1 - .006f);
            WallBox(mb, fm, e, s0 + sw, s1 - sw, kb, kb + .04f, d0, d1, true);
            float ga = kb + .04f, gb = y1 - .06f, dm = (d0 + d1) / 2;
            WallBox(soft, D.WindowGlass, e, s0 + sw, s1 - sw, ga, gb, dm - .002f, dm + .002f);
            for (float s = s0 + sw + .045f; s < s1 - sw - .01f; s += .045f)
                WallBox(mb, fm, e, s - .009f, s + .009f, ga, gb, d1 - .012f, d1);
            float ym = Mathf.Lerp(ga, gb, .5f);
            WallBox(mb, fm, e, s0 + sw, s1 - sw, ym - .015f, ym + .015f, d0, d0 + .012f, true);
            WallBox(mb, fm, e, s0 + sw, s1 - sw, ym - .015f, ym + .015f, d1 - .012f, d1, true);
            float hs = handle > 0 ? s1 - .03f : s0 + .03f;
            WallBox(mb, D.Brass, e, hs - .012f, hs + .012f, y0 + .95f, y0 + 1.05f, d1, d1 + .003f);
            WallBox(mb, D.Brass, e, hs - .012f, hs + .012f, y0 + .95f, y0 + 1.05f, d0 - .003f, d0);
        }

        /// <summary>土蔵的门：熏黑的厚木板、三道铁箍加铆钉、外面一个铁环拉手。</summary>
        static void KuraLeaf(MeshBatch mb, Edge e, float s0, float s1, float y0, float y1, float d0, float d1, int handle)
        {
            WallBox(mb, D.BeamDark, e, s0, s1, y0, y1, d0, d1);
            for (float s = s0 + .2f; s < s1 - .05f; s += .2f)
            {
                WallBox(mb, D.RanmaDark, e, s - .003f, s + .003f, y0, y1, d1, d1 + .001f);
                WallBox(mb, D.RanmaDark, e, s - .003f, s + .003f, y0, y1, d0 - .001f, d0);
            }
            var n = Inward(e);
            var rot = Quaternion.FromToRotation(Vector3.up, n);
            foreach (float y in new[] { y0 + .35f, y0 + 1.15f, y1 - .35f })
            {
                WallBox(mb, D.Iron, e, s0 + .02f, s1 - .02f, y - .035f, y + .035f, d1, d1 + .005f, true);
                WallBox(mb, D.Iron, e, s0 + .02f, s1 - .02f, y - .035f, y + .035f, d0 - .005f, d0, true);
                for (float s = s0 + .07f; s < s1 - .05f; s += .16f)
                    mb.Cyl(D.Iron, OnWall(e, s, y, d1 + .005f), .009f, .007f, .006f, 8, true, rot);
            }
            float hs = handle > 0 ? s1 - .16f : s0 + .16f, hy = y0 + 1.05f;
            WallBox(mb, D.Iron, e, hs - .05f, hs + .05f, hy - .05f, hy + .05f, d1, d1 + .006f);
            const int seg = 16;
            float rr = .07f;
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2 / seg, a1 = (k + 1) * Mathf.PI * 2 / seg;
                var p0 = OnWall(e, hs + Mathf.Cos(a0) * rr, hy - .08f + Mathf.Sin(a0) * rr, d1 + .016f);
                var p1 = OnWall(e, hs + Mathf.Cos(a1) * rr, hy - .08f + Mathf.Sin(a1) * rr, d1 + .016f);
                var dir = (p1 - p0).normalized;
                mb.Box(D.Iron, (p0 + p1) / 2, new Vector3((p1 - p0).magnitude + .004f, .012f, .012f),
                    Quaternion.LookRotation(n, Vector3.Cross(n, dir)));
            }
        }

        // =====================================================================
        // 縁側的玻璃拉门：锁着，只能晃
        // =====================================================================
        static void BuildGlassDoor(Transform parent, Transform structure, Room room, Edge e, Gap g, int index)
        {
            float a = g.a, b = g.b, y0 = g.bottom, y1 = g.top;
            var rootPos = e.alongX ? new Vector3((a + b) / 2, y0, e.line) : new Vector3(e.line, y0, (a + b) / 2);
            var door = ArtKit.Empty(parent, $"GlassDoor_{room.id}_{index}", rootPos);
            int n = Mathf.Max(2, Mathf.RoundToInt((b - a) / .85f));
            float w = (b - a) / n, p0 = y0 + .015f, p1 = y1 - .03f;
            const float st = .045f;
            var leaves = new HakoOnna.Art.DoorVisual.Leaf[n];
            var along = e.alongX ? Vector3.right : Vector3.forward;
            Bounds closed = default;
            for (int i = 0; i < n; i++)
            {
                var leaf = ArtKit.Empty(door, $"Leaf_{i}", Vector3.zero);
                var mb = new MeshBatch { M = Matrix4x4.Translate(-rootPos) };
                var gm = new MeshBatch { M = Matrix4x4.Translate(-rootPos) };
                // 相邻门扇在不同轨道上，交接处各压 2.5 cm；两端插进门框
                float s0 = a + i * w - (i == 0 ? .015f : .025f), s1 = a + (i + 1) * w + (i == n - 1 ? .015f : .025f);
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
                float hx = i % 2 == 0 ? s0 + .022f : s1 - .022f;
                WallBox(mb, D.Iron, e, hx - .01f, hx + .01f, y0 + .9f, y0 + 1.02f, d1, d1 + .003f);
                if (i == n / 2)
                {
                    // 中间两扇交接处的螺丝锁（ネジ締り錠）
                    float mid = a + (n / 2) * w;
                    WallBox(mb, D.Brass, e, mid - .03f, mid + .03f, y0 + .98f, y0 + 1.02f, .045f + .03f, .045f + .046f);
                }
                mb.Build(leaf, "Frame");
                gm.Build(leaf, "Glass", false);
                ClearStatic(leaf);
                var local = BoxOnWall(e, s0, s1, p0, p1, d0, d1);
                local.center -= rootPos;
                if (i == 0) closed = local; else closed.Encapsulate(local);
                leaves[i] = new HakoOnna.Art.DoorVisual.Leaf { part = leaf, openOffset = along * .001f };
            }
            var dv = door.gameObject.AddComponent<HakoOnna.Art.DoorVisual>();
            float h = y1 - y0;
            var opening = e.alongX
                ? new Bounds(new Vector3(0f, h / 2, 0f), new Vector3(b - a, h, 2 * T))
                : new Bounds(new Vector3(0f, h / 2, 0f), new Vector3(2 * T, h, b - a));
            dv.Configure(leaves, opening, closed, true, false, .5f, .5f);
            // 玩家实际打到的是 Structure 里的 Window_Glass：挂 DoorLink 指回这扇门
            foreach (var c in structure.GetComponentsInChildren<BoxCollider>())
            {
                if (c.name != "Window_Glass") continue;
                var cb = BoxWorldBounds(c);
                float s = e.alongX ? cb.center.x : cb.center.z, perp = e.alongX ? cb.center.z : cb.center.x;
                if (s <= a || s >= b || Mathf.Abs(perp - e.line) >= .2f) continue;
                var link = c.GetComponent<HakoOnna.Art.DoorLink>();
                if (link == null) link = c.gameObject.AddComponent<HakoOnna.Art.DoorLink>();
                link.Bind(dv);
            }
        }

        // =====================================================================
        // 钉死的旧门（封掉的门洞里挑 3 处）
        // =====================================================================
        sealed class SealedSpec
        {
            public int group;
            public bool alongX;
            public float line, center, width; // 和 DX / DZ 一样：平面单位、V3 宽度
            public string boardRoom;          // 钉木板、贴符纸、有血手印的一面
            public string backRoom;           // 另一面：旧门板；scratched = 抓痕 + 小孩的血手印
            public bool scratched;
            public bool shimenawa;            // 钉木板那面挂注连绳
        }

        static readonly SealedSpec[] Sealed =
        {
            // 玄関（出生点）一进门就看得见：钉死的旧门，后面是縁側
            new SealedSpec { group = G0, alongX = false, line = 5.46f, center = .68f, width = .9f, boardRoom = "Genkan", backRoom = "Engawa" },
            // 仏間北墙：挂注连绳、贴满符纸，封着納戸；納戸那面全是抓痕
            new SealedSpec { group = G0, alongX = true, line = 5.005f, center = 10f, width = .9f, boardRoom = "Butsuma", backRoom = "Nando", scratched = true, shimenawa = true },
            // 奥の間西墙：两扇襖钉死，封着納戸
            new SealedSpec { group = G0, alongX = false, line = 12.74f, center = 6.825f, width = 1.8f, boardRoom = "Oku", backRoom = "Nando", scratched = true },
        };

        static void SealedWorld(SealedSpec s, out float line, out float lo, out float hi)
        {
            line = s.line * PlanScale;
            float c = s.center * PlanScale, w = s.width * OpeningWidthScale;
            lo = c - w / 2;
            hi = c + w / 2;
        }

        /// <summary>这条墙边上钉死的旧门（沿墙范围）。</summary>
        static List<Vector2> SealedSpansOn(Room room, Edge e)
        {
            var list = new List<Vector2>();
            foreach (var s in Sealed)
            {
                if (s.group != room.group || s.alongX != e.alongX) continue;
                SealedWorld(s, out float line, out float lo, out float hi);
                if (Mathf.Abs(line - e.line) > .01f || hi <= e.from || lo >= e.to) continue;
                list.Add(new Vector2(lo, hi));
            }
            return list;
        }

        /// <summary>
        /// 钉木板那一面前面留空（木板、注连绳凸出墙面，门框两边墙上有血手印），家具不能挡住：前面 30 cm、两边多留 20 cm。
        /// 另一面不留：留了会把納戸的長持整个挤到对面墙去（2026-11-19 试过），長持贴着旧门框也说得通。
        /// </summary>
        static IEnumerable<Bounds> SealedKeepZones()
        {
            foreach (var s in Sealed)
            {
                var room = RoomById(s.boardRoom);
                if (room == null) continue;
                SealedWorld(s, out float line, out float lo, out float hi);
                var o = new Opening { group = s.group, alongX = s.alongX, line = line };
                if (!FaceEdge(room, o, out var e)) continue;
                float fl = FloorY(room);
                yield return BoxOnWall(e, lo - .2f, hi + .2f, fl + .025f, .55f + DoorTop + .15f, T - .01f, T + .3f);
            }
        }

        static void BuildSealedDoorways(Transform parent)
        {
            int faces = 0;
            foreach (var s in Sealed)
            {
                SealedWorld(s, out float line, out float lo, out float hi);
                var o = new Opening { group = s.group, alongX = s.alongX, line = line };
                float doorFloor = .55f, top = .55f + DoorTop;
                foreach (var (roomId, board) in new[] { (s.boardRoom, true), (s.backRoom, false) })
                {
                    var room = RoomById(roomId);
                    if (room == null || !FaceEdge(room, o, out var e)) continue;
                    var mb = new MeshBatch();
                    var rnd = new System.Random(StableHash(roomId + s.boardRoom + s.backRoom));
                    SealedFace(mb, e, room, lo, hi, doorFloor, top, board, s, rnd);
                    mb.Build(parent, $"Sealed_{s.boardRoom}_{s.backRoom}_{roomId}");
                    faces++;
                }
            }
            Debug.Log($"[Hako Art] 钉死的旧门：{Sealed.Length} 处（{faces} 面）");
        }

        static void SealedFace(MeshBatch mb, Edge e, Room room, float lo, float hi, float doorFloor, float top, bool board,
            SealedSpec spec, System.Random rnd)
        {
            float fl = FloorY(room);
            // 门框：两侧门柱、鴨居、敷居；土间一面是上がり框（门板底下那段竖板）
            if (fl < doorFloor - .01f)
            {
                WallBox(mb, D.BeamDark, e, lo - .06f, hi + .06f, fl, doorFloor - .015f, T - .005f, T + .035f, true);
                WallBox(mb, D.Post, e, lo - .08f, hi + .08f, doorFloor - .06f, doorFloor - .015f, T - .005f, T + .05f, true);
            }
            WallBox(mb, D.Post, e, lo - .055f, lo, doorFloor - .015f, top, T - .005f, T + .025f);
            WallBox(mb, D.Post, e, hi, hi + .055f, doorFloor - .015f, top, T - .005f, T + .025f);
            WallBox(mb, D.TrimMid, e, lo - .055f, hi + .055f, top, top + .075f, T - .005f, T + .025f, true);
            WallBox(mb, D.TrimLight, e, lo, hi, doorFloor - .015f, doorFloor + .012f, T - .005f, T + .03f, true);

            // 旧门板：和室一面是襖，其余是板戸；贴着墙面，不动、更破
            bool fusuma = IsTatami(room);
            int n = hi - lo > 1.3f ? 2 : 1;
            float q = (hi - lo) / n, pd0 = T + .001f, pd1 = T + .016f;
            for (int i = 0; i < n; i++)
            {
                float a = lo + i * q + (i > 0 ? -.01f : 0f), b = lo + (i + 1) * q + (i < n - 1 ? .01f : 0f);
                BuildLeaf(fusuma ? DoorKind.Fusuma : DoorKind.Itado, mb, mb, e, a, b, doorFloor + .012f, top, pd0 + i % 2 * .002f,
                    pd1 + i % 2 * .002f, i == 0 ? 1 : -1, rnd.Next(), true);
            }
            float face = pd1 + .002f;

            if (board)
            {
                // 木板：两条横的、一个叉；两端压到门柱和墙上，钉子露头
                float bt0 = T + .032f, bt1 = T + .056f;
                float h = top - doorFloor;
                var planks = new List<(Vector2 a, Vector2 b)>
                {
                    (new Vector2(lo - .16f, doorFloor + .42f), new Vector2(hi + .14f, doorFloor + .47f)),
                    (new Vector2(lo - .13f, doorFloor + h * .72f), new Vector2(hi + .17f, doorFloor + h * .68f)),
                    (new Vector2(lo - .04f, doorFloor + .15f), new Vector2(hi + .05f, top - .12f)),
                    (new Vector2(lo - .05f, top - .2f), new Vector2(hi + .04f, doorFloor + .22f)),
                };
                if (hi - lo > 1.6f) planks.Add((new Vector2((lo + hi) / 2 - .06f, doorFloor + .05f), new Vector2((lo + hi) / 2 + .05f, top - .05f)));
                for (int i = 0; i < planks.Count; i++)
                {
                    var (pa, pb) = planks[i];
                    float off = i * .0035f; // 交叉的木板上下错开一点
                    Plank(mb, e, pa, pb, .14f - .02f * (i & 1), bt0 + off, bt1 + off, rnd);
                }
                // 符纸：门板、木板、门柱上贴满
                int count = 8 + rnd.Next(4);
                for (int i = 0; i < count; i++)
                {
                    float s = Mathf.Lerp(lo - .02f, hi + .02f, R01(rnd));
                    float y = doorFloor + .35f + R01(rnd) * (h - .6f);
                    float d = R01(rnd) < .45f ? bt1 + .02f : face;
                    float sc = .9f + R01(rnd) * .25f;
                    DecalOn(mb, DM.Ofuda, e, s, y, d + .0015f, .085f * sc, .27f * sc, (R01(rnd) - .5f) * 18f, false);
                }
                DecalOn(mb, DM.Ofuda, e, lo - .028f, top - .45f, T + .025f + .0015f, .05f, .24f, 0f, false);
                DecalOn(mb, DM.Ofuda, e, hi + .028f, top - .55f, T + .025f + .0015f, .05f, .24f, 0f, false);
                // 血手印：门板上两个（大人的，往外推的样子）；门框两边的灰泥墙上再各按一个
                // （土间那面的门板是深色板戸，印在门板上看不见，印在墙上才显眼）
                for (int i = 0; i < 2; i++)
                {
                    float s = Mathf.Lerp(lo + .15f, hi - .15f, R01(rnd));
                    float y = doorFloor + .6f + R01(rnd) * .8f;
                    DecalOn(mb, DM.Blood, e, s, y, face + .001f + i * .0003f, .2f, .24f, (R01(rnd) - .5f) * 30f, R01(rnd) < .5f);
                }
                foreach (int side in new[] { -1, 1 })
                {
                    float s = side < 0 ? lo - .055f - .13f : hi + .055f + .13f;
                    float y = fl + 1.25f + R01(rnd) * .35f;
                    DecalOn(mb, DM.Blood, e, s, y, T + .001f, .2f, .24f, side * (8f + R01(rnd) * 14f), side > 0);
                }
                DecalOn(mb, DM.Scratch, e, (lo + hi) / 2 + .1f, doorFloor + .95f, face + .0015f, .18f, .6f, (R01(rnd) - .5f) * 10f, false);
                if (spec.shimenawa) Shimenawa(mb, e, lo - .12f, hi + .12f, top - .04f, T + .075f, rnd);
            }
            else if (spec.scratched)
            {
                // 里面那一面：小孩高度的抓痕和小血手印，越往下越多
                int scratches = 3 + rnd.Next(2);
                for (int i = 0; i < scratches; i++)
                {
                    float s = Mathf.Lerp(lo + .1f, hi - .1f, R01(rnd));
                    float y = doorFloor + .45f + R01(rnd) * .7f;
                    DecalOn(mb, DM.Scratch, e, s, y, face + .0015f, .16f + R01(rnd) * .06f, .5f + R01(rnd) * .3f, (R01(rnd) - .5f) * 14f, R01(rnd) < .5f);
                }
                int hands = 4 + rnd.Next(3);
                for (int i = 0; i < hands; i++)
                {
                    float s = Mathf.Lerp(lo - .02f, hi + .02f, R01(rnd));
                    float y = doorFloor + .3f + R01(rnd) * .75f;
                    float sc = .9f + R01(rnd) * .2f;
                    DecalOn(mb, DM.Blood, e, s, y, (s < lo || s > hi ? T + .025f : face) + .002f + i * .0003f,
                        .135f * sc, .16f * sc, (R01(rnd) - .5f) * 40f, R01(rnd) < .5f);
                }
            }
            else
            {
                // 縁側一面：只有灰和一道浅的划痕
                DecalOn(mb, DM.Scratch, e, (lo + hi) / 2, doorFloor + .8f, face + .0015f, .12f, .35f, 6f, false);
            }
        }

        /// <summary>风化木板：从 a 到 b（墙线坐标 s, y），带两端各两颗钉子。</summary>
        static void Plank(MeshBatch mb, Edge e, Vector2 a, Vector2 b, float width, float d0, float d1, System.Random rnd)
        {
            var n = Inward(e);
            var pa = OnWall(e, a.x, a.y, (d0 + d1) / 2);
            var pb = OnWall(e, b.x, b.y, (d0 + d1) / 2);
            var dir = (pb - pa).normalized;
            var rot = Quaternion.LookRotation(n, Vector3.Cross(n, dir));
            float len = (pb - pa).magnitude;
            mb.Box(DM.Weathered, (pa + pb) / 2, new Vector3(len, width, d1 - d0), rot, 0);
            var up = Vector3.Cross(n, dir).normalized;
            var nailRot = Quaternion.FromToRotation(Vector3.up, n);
            var streakRot = Quaternion.LookRotation(n, Vector3.up);
            foreach (float t in new[] { .06f, len - .06f })
                foreach (float k in new[] { -.3f, .3f })
                {
                    var c = pa + dir * t + up * (width * k) + n * ((d1 - d0) / 2);
                    mb.Cyl(D.Iron, c, .007f, .006f, .004f, 6, true, nailRot);
                    // 钉子往下流的锈水（竖直往下，不跟着木板斜）
                    if (R01(rnd) < .55f)
                        mb.Box(DM.Rust, c + n * .0006f - Vector3.up * (.02f + .02f * R01(rnd)), new Vector3(.007f, .045f + .03f * R01(rnd), .001f), streakRot);
                }
        }

        /// <summary>贴在墙线坐标 (s, y) 处、离墙线 d 的一张贴花（四边形，UV 0..1），angle 绕法线转（度）。</summary>
        static void DecalOn(MeshBatch mb, Material m, Edge e, float s, float y, float d, float w, float h, float angle, bool flip)
        {
            var n = Inward(e);
            var q = Quaternion.AngleAxis(angle, n);
            var r = q * (e.alongX ? Vector3.right : Vector3.forward);
            var u = q * Vector3.up;
            var c = OnWall(e, s, y, d);
            var pts = new[] { c - r * w / 2 - u * h / 2, c + r * w / 2 - u * h / 2, c + r * w / 2 + u * h / 2, c - r * w / 2 + u * h / 2 };
            float u0 = flip ? 1f : 0f, u1 = flip ? 0f : 1f;
            mb.Poly(m, pts, new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f) }, n);
        }

        /// <summary>注连绳：两根门柱之间下垂的草绳 + 四条之字形的纸垂（紙垂）。</summary>
        static void Shimenawa(MeshBatch mb, Edge e, float a, float b, float y, float d, System.Random rnd)
        {
            const int seg = 14;
            float sag = .22f;
            var prev = OnWall(e, a, y, d);
            var n = Inward(e);
            for (int i = 1; i <= seg; i++)
            {
                float t = i / (float)seg;
                var cur = OnWall(e, Mathf.Lerp(a, b, t), y - sag * 4f * t * (1f - t), d);
                var dir = cur - prev;
                mb.Cyl(DM.Rope, prev, .028f, .026f, dir.magnitude, 8, false, Quaternion.FromToRotation(Vector3.up, dir.normalized));
                prev = cur;
            }
            for (int k = 1; k <= 4; k++)
            {
                float t = k / 5f, s = Mathf.Lerp(a, b, t), yy = y - sag * 4f * t * (1f - t) - .02f;
                // 之字形：四段往下错开的纸条
                for (int j = 0; j < 4; j++)
                {
                    float off = (j % 2 == 0 ? -1f : 1f) * .025f;
                    var p0 = OnWall(e, s + off - .028f, yy - j * .07f, d + .01f);
                    var p1 = OnWall(e, s + off + .028f, yy - j * .07f, d + .01f);
                    var p2 = OnWall(e, s + off + .028f, yy - (j + 1) * .07f, d + .012f);
                    var p3 = OnWall(e, s + off - .028f, yy - (j + 1) * .07f, d + .012f);
                    mb.Poly(R01(rnd) < .4f ? DM.ShojiOld : P.Paper, new[] { p0, p1, p2, p3 },
                        new[] { Vector2.up, Vector2.one, Vector2.right, Vector2.zero }, n);
                }
            }
        }
    }
}
