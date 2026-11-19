using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>外墙饰面、屋顶（寄栋 / 入母屋 / 切妻）、围墙瓦顶和冠木门。全部只有视觉，没有碰撞体。</summary>
    public static partial class HouseBlockoutGenerator
    {
        // =====================================================================
        // 外墙：一楼 石基 + 焼杉下见板 + 白漆喰；二楼 白漆喰；土蔵 なまこ壁 + 漆喰
        // =====================================================================
        struct Zone
        {
            public float y0, y1, d;
            public Material m;
            public Zone(float y0, float y1, float d, Material m) { this.y0 = y0; this.y1 = y1; this.d = d; this.m = m; }
        }

        static Zone[] ZonesFor(int group)
        {
            float t0 = Levels[G0].wallTop, f1 = Levels[G1].floorTop, t1 = Levels[G1].wallTop, tk = Levels[GKura].wallTop;
            if (group == G0)
                return new[]
                {
                    new Zone(0f, .32f, .035f, D.StoneWall),
                    new Zone(.32f, 2.6f, .014f, D.Siding),
                    new Zone(2.6f, 2.66f, .03f, D.Post),
                    new Zone(2.66f, t0 - .05f, .008f, D.PlasterWhite),
                    new Zone(t0 - .05f, t0 + .27f, .04f, D.Post),
                };
            if (group == G1)
                return new[]
                {
                    new Zone(f1 - .1f, f1 + .1f, .03f, D.Post),
                    new Zone(f1 + .1f, t1 - .05f, .008f, D.PlasterWhite),
                    new Zone(t1 - .05f, t1 + .25f, .04f, D.Post),
                };
            return new[]
            {
                new Zone(0f, .22f, .04f, D.StoneWall),
                new Zone(.22f, 1.75f, .02f, D.Namako),
                new Zone(1.75f, 1.84f, .06f, D.PlasterWhite),
                new Zone(1.84f, tk, .008f, D.PlasterWhite),
                new Zone(tk, tk + .28f, .05f, D.PlasterWhite),
            };
        }

        sealed class CladLine
        {
            public int group;
            public Edge e;
            public readonly List<Vector2> runs = new List<Vector2>();
            public readonly List<Gap> gaps = new List<Gap>();
        }

        static void ExteriorWalls(Transform parent)
        {
            var lines = new Dictionary<string, CladLine>();
            foreach (var room in DRooms)
            {
                if (room.group == GFence) continue;
                foreach (var e in EdgesOf(room))
                {
                    if (!HasWall(room, e)) continue;
                    float a = e.alongX ? room.r.xMin : room.r.yMin, b = e.alongX ? room.r.xMax : room.r.yMax;
                    var cuts = new List<float> { a, b };
                    foreach (var o in DRooms)
                    {
                        if (o.group != room.group || o == room) continue;
                        foreach (float v in e.alongX ? new[] { o.r.xMin, o.r.xMax } : new[] { o.r.yMin, o.r.yMax })
                            if (v > a + .01f && v < b - .01f) cuts.Add(v);
                    }
                    cuts.Sort();
                    var runs = new List<Vector2>();
                    for (int i = 0; i < cuts.Count - 1; i++)
                    {
                        if (cuts[i + 1] - cuts[i] < .02f) continue;
                        float mid = (cuts[i] + cuts[i + 1]) / 2;
                        if (RoomAt(OnWall(e, mid, 1f, -.05f), room.group) == null) runs.Add(new Vector2(cuts[i], cuts[i + 1]));
                    }
                    if (runs.Count == 0) continue;
                    string key = $"{room.group}_{e.alongX}_{e.inward}_{Mathf.RoundToInt(e.line * 100)}";
                    if (!lines.TryGetValue(key, out var cl)) lines[key] = cl = new CladLine { group = room.group, e = e };
                    cl.runs.AddRange(runs);
                    foreach (var g in GapsOn(room, e))
                        foreach (var r in runs)
                            if (g.b > r.x && g.a < r.y) { cl.gaps.Add(g); break; }
                }
            }

            var mb = new MeshBatch();
            foreach (var cl in lines.Values)
            {
                var e = cl.e;
                cl.runs.Sort((p, q) => p.x.CompareTo(q.x));
                var merged = new List<Vector2>();
                foreach (var r in cl.runs)
                {
                    if (merged.Count > 0 && r.x <= merged[merged.Count - 1].y + .02f)
                        merged[merged.Count - 1] = new Vector2(merged[merged.Count - 1].x, Mathf.Max(merged[merged.Count - 1].y, r.y));
                    else merged.Add(r);
                }
                foreach (var run in merged)
                {
                    foreach (var z in ZonesFor(cl.group))
                        ZoneFill(mb, z.m, e, run.x - z.d, run.y + z.d, z.y0, z.y1, -z.d, 0f, cl.gaps);
                    if (cl.group == G0) // 押縁
                        for (float s = run.x + .3f; s < run.y - .1f; s += .455f)
                            ZoneFill(mb, D.Post, e, s - .015f, s + .015f, .32f, 1.95f, -.032f, -.012f, cl.gaps);
                }
                foreach (var g in cl.gaps) GapTrim(mb, cl.group, e, g);
            }
            mb.Build(parent, "ExteriorWalls");
        }

        /// <summary>沿墙铺一段饰面，在门窗洞处断开。</summary>
        static void ZoneFill(MeshBatch mb, Material m, Edge e, float s0, float s1, float y0, float y1, float d0, float d1, List<Gap> gaps)
        {
            var cuts = new List<float> { s0, s1 };
            foreach (var g in gaps)
            {
                if (g.a > s0 && g.a < s1) cuts.Add(g.a);
                if (g.b > s0 && g.b < s1) cuts.Add(g.b);
            }
            cuts.Sort();
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float a = cuts[i], b = cuts[i + 1];
                if (b - a < .002f) continue;
                float mid = (a + b) / 2;
                Gap hit = null;
                foreach (var g in gaps) if (mid > g.a && mid < g.b) hit = g;
                if (hit == null) { WallBox(mb, m, e, a, b, y0, y1, d0, d1, true); continue; }
                if (hit.bottom > y0) WallBox(mb, m, e, a, b, y0, Mathf.Min(y1, hit.bottom), d0, d1, true);
                if (hit.top < y1) WallBox(mb, m, e, a, b, Mathf.Max(y0, hit.top), y1, d0, d1, true);
            }
        }

        static void GapTrim(MeshBatch mb, int group, Edge e, Gap g)
        {
            bool kura = group == GKura;
            if (g.window)
            {
                float y0 = g.bottom, y1 = g.top;
                var fm = kura ? D.Black : D.Post;
                WallBox(mb, fm, e, g.a - .05f, g.a, y0 - .06f, y1 + .06f, -.05f, 0f);
                WallBox(mb, fm, e, g.b, g.b + .05f, y0 - .06f, y1 + .06f, -.05f, 0f);
                WallBox(mb, fm, e, g.a - .05f, g.b + .05f, y1, y1 + .06f, -.05f, 0f);
                WallBox(mb, fm, e, g.a - .08f, g.b + .08f, y0 - .06f, y0, -.08f, 0f);
                if (kura)
                {
                    for (float s = g.a + .1f; s < g.b - .05f; s += .1f)
                        WallBox(mb, D.Iron, e, s - .009f, s + .009f, y0, y1, -.03f, -.012f);
                }
                else if (group == G0 && !g.room.id.StartsWith("Engawa"))
                {
                    // 出格子
                    WallBox(mb, D.Post, e, g.a, g.b, y0, y0 + .04f, -.09f, -.045f);
                    WallBox(mb, D.Post, e, g.a, g.b, y1 - .04f, y1, -.09f, -.045f);
                    for (float s = g.a + .04f; s < g.b - .02f; s += .06f)
                        WallBox(mb, D.Post, e, s - .011f, s + .011f, y0 + .04f, y1 - .04f, -.08f, -.05f);
                }
                if (group == G1) // 窗上小庇
                {
                    WallBox(mb, D.RidgeTile, e, g.a - .2f, g.b + .2f, y1 + .14f, y1 + .19f, -.38f, 0f);
                    WallBox(mb, D.BeamDark, e, g.a - .2f, g.b + .2f, y1 + .09f, y1 + .14f, -.34f, 0f);
                }
                return;
            }
            if (!g.door) return;
            float pw = kura ? .25f : .1f, pd = kura ? .14f : .06f, lh = kura ? .3f : .12f;
            var dm = kura ? D.PlasterWhite : D.Post;
            WallBox(mb, dm, e, g.a - pw, g.a, g.bottom, g.top + lh, -pd, 0f);
            WallBox(mb, dm, e, g.b, g.b + pw, g.bottom, g.top + lh, -pd, 0f);
            WallBox(mb, dm, e, g.a - pw, g.b + pw, g.top, g.top + lh, -pd, 0f);
            if (!kura) return;
            // 土蔵的厚土扉，向外开着贴在墙上
            const float leaf = .75f, lt = .18f;
            foreach (float s0 in new[] { g.a - pw - .03f - leaf, g.b + pw + .03f })
            {
                WallBox(mb, D.PlasterWhite, e, s0, s0 + leaf, .02f, g.top - .02f, -pd - lt, -pd);
                WallBox(mb, D.Black, e, s0, s0 + leaf, g.top - .1f, g.top - .02f, -pd - lt - .006f, -pd);
                WallBox(mb, D.Black, e, s0, s0 + leaf, .02f, .14f, -pd - lt - .006f, -pd);
                WallBox(mb, D.Iron, e, s0 + leaf / 2 - .04f, s0 + leaf / 2 + .04f, 1.0f, 1.12f, -pd - lt - .02f, -pd - lt);
            }
        }

        // =====================================================================
        // 屋顶：高度场 h = c + k·d（d = 到墙线的距离），同一层的几个屋顶取最大值，
        // 每个坡面是凸多边形（半平面裁剪），被别的屋顶盖住的部分减掉。
        // =====================================================================
        enum RoofKind { Hip, Gable, Irimoya }

        sealed class RoofDef
        {
            public string name;
            public RoofKind kind;
            public int layer, order;
            public float x0, z0, x1, z1, c, k, g;
            public float[] over; // 南 北 西 东

            public Vector2 N(int s) => s == 0 ? new Vector2(0, 1) : s == 1 ? new Vector2(0, -1) : s == 2 ? new Vector2(1, 0) : new Vector2(-1, 0);
            public float O(int s) => s == 0 ? -z0 : s == 1 ? z1 : s == 2 ? -x0 : x1;
            public float Dist(int s, Vector2 p) => Vector2.Dot(N(s), p) + O(s);

            public bool In(Vector2 p) =>
                p.x >= x0 - over[2] - 1e-3f && p.x <= x1 + over[3] + 1e-3f && p.y >= z0 - over[0] - 1e-3f && p.y <= z1 + over[1] + 1e-3f;

            public float H(Vector2 p)
            {
                float m = Mathf.Min(Dist(0, p), Dist(1, p));
                float dw = Dist(2, p), de = Dist(3, p);
                if (kind == RoofKind.Hip) m = Mathf.Min(m, Mathf.Min(dw, de));
                else if (kind == RoofKind.Irimoya)
                {
                    if (dw < g) m = Mathf.Min(m, dw);
                    if (de < g) m = Mathf.Min(m, de);
                }
                return c + k * m;
            }

            public List<Vector2> Domain() => new List<Vector2>
            {
                new Vector2(x0 - over[2], z0 - over[0]), new Vector2(x1 + over[3], z0 - over[0]),
                new Vector2(x1 + over[3], z1 + over[1]), new Vector2(x0 - over[2], z1 + over[1]),
            };
        }

        struct HP
        {
            public Vector2 n;
            public float o;
            public HP(Vector2 n, float o) { this.n = n; this.o = o; }
            public float Eval(Vector2 p) => Vector2.Dot(n, p) + o;
            public HP Neg => new HP(-n, -o);
        }

        sealed class RoofFace
        {
            public RoofDef roof;
            public int side;
            public List<Vector2> poly;
        }

        static List<RoofDef> RoofDefs()
        {
            float ps = PlanScale;
            float c0 = Levels[G0].wallTop + .27f, c1 = Levels[G1].wallTop + .25f, ck = Levels[GKura].wallTop + .28f;
            const float o = 1.2f; // 出檐
            var list = new List<RoofDef>();
            void Add(string n, RoofKind kind, int layer, float x0, float z0, float x1, float z1, float c, float k,
                float oS, float oN, float oW, float oE, float g = 0f) =>
                list.Add(new RoofDef
                {
                    name = n, kind = kind, layer = layer, order = list.Count, x0 = x0, z0 = z0, x1 = x1, z1 = z1,
                    c = c, k = k, g = g, over = new[] { oS, oN, oW, oE },
                });
            // 一楼下屋：南侧一条 + 西、东两翼，靠二楼墙的一边不出檐
            Add("South", RoofKind.Hip, 0, 0f, 0f, 18.655f * ps, 5.005f * ps, c0, .45f, o, 0f, o, o);
            Add("West", RoofKind.Hip, 0, 0f, 0f, 3.64f * ps, 10.01f * ps, c0, .45f, o, o, o, 0f);
            Add("East", RoofKind.Hip, 0, 14.56f * ps, 0f, 18.655f * ps, 10.01f * ps, c0, .45f, o, o, 0f, o);
            // 二楼主屋：入母屋，东西两端山墙
            Add("Main", RoofKind.Irimoya, 1, 3.64f * ps, 5.005f * ps, 14.56f * ps, 14.56f * ps, c1, .45f, o, o, o, o, 3.1f);
            // 土蔵：切妻，屋脊东西向
            Add("Kura", RoofKind.Gable, 2, 5.46f * ps, 17.29f * ps, 10.92f * ps, 21.84f * ps, ck, .55f, .8f, .8f, .6f, .6f);
            return list;
        }

        static void BuildRoofs(Transform parent)
        {
            var defs = RoofDefs();
            var taken = new Dictionary<Vector3Int, int>();
            for (int layerId = 0; layerId < 3; layerId++)
            {
                var layer = defs.FindAll(r => r.layer == layerId);
                var faces = new List<RoofFace>();
                foreach (var r in layer) faces.AddRange(FacesOf(r));
                var mb = new MeshBatch();
                foreach (var f in faces)
                    foreach (var piece in VisiblePieces(f, faces))
                    {
                        EmitRoofPiece(mb, f, piece);
                        Fascia(mb, f, piece);
                    }
                foreach (var r in layer)
                {
                    RoofCaps(mb, r, layer, taken);
                    if (r.kind == RoofKind.Irimoya) IrimoyaGables(mb, r);
                    if (r.kind == RoofKind.Gable) KuraGables(mb, r);
                }
                mb.Build(parent, layerId == 0 ? "Roof_1F" : layerId == 1 ? "Roof_2F_Irimoya" : "Roof_Kura_Tiles");
            }
        }

        static List<RoofFace> FacesOf(RoofDef R)
        {
            var list = new List<RoofFace>();
            HP Le(int i, int j) => new HP(R.N(j) - R.N(i), R.O(j) - R.O(i)); // d_i <= d_j
            HP LeG(int i) => new HP(-R.N(i), R.g - R.O(i));                   // d_i <= g
            HP GeG(int i) => new HP(R.N(i), R.O(i) - R.g);                    // d_i >= g
            void Add(int side, params HP[] hps)
            {
                var poly = R.Domain();
                foreach (var h in hps)
                {
                    poly = Clip(poly, h);
                    if (poly.Count < 3) return;
                }
                if (Mathf.Abs(Area(poly)) > 1e-4f) list.Add(new RoofFace { roof = R, side = side, poly = poly });
            }
            switch (R.kind)
            {
                case RoofKind.Hip:
                    for (int i = 0; i < 4; i++)
                    {
                        var hs = new List<HP>();
                        for (int j = 0; j < 4; j++) if (j != i) hs.Add(Le(i, j));
                        Add(i, hs.ToArray());
                    }
                    break;
                case RoofKind.Gable:
                    Add(0, Le(0, 1));
                    Add(1, Le(1, 0));
                    break;
                case RoofKind.Irimoya:
                    for (int i = 0; i < 2; i++)
                    {
                        Add(i, Le(i, 1 - i), LeG(i), Le(i, 2), Le(i, 3));
                        Add(i, Le(i, 1 - i), GeG(i), GeG(2), GeG(3));
                    }
                    for (int i = 2; i < 4; i++) Add(i, Le(i, 0), Le(i, 1), LeG(i), Le(i, 5 - i));
                    break;
            }
            return list;
        }

        static List<Vector2> Clip(List<Vector2> poly, HP h)
        {
            var res = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                float fa = h.Eval(a), fb = h.Eval(b);
                if (fa >= 0) res.Add(a);
                if ((fa >= 0) != (fb >= 0)) res.Add(Vector2.Lerp(a, b, fa / (fa - fb)));
            }
            return res;
        }

        static float Area(List<Vector2> p)
        {
            float s = 0;
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % p.Count];
                s += a.x * b.y - b.x * a.y;
            }
            return s / 2;
        }

        /// <summary>凸多边形 P 减去凸多边形 Q，结果是若干凸多边形。</summary>
        static List<List<Vector2>> Subtract(List<Vector2> P, List<Vector2> Q)
        {
            var res = new List<List<Vector2>>();
            float sgn = Mathf.Sign(Area(Q));
            var rem = P;
            for (int i = 0; i < Q.Count && rem.Count >= 3; i++)
            {
                var a = Q[i];
                var d = Q[(i + 1) % Q.Count] - a;
                if (d.sqrMagnitude < 1e-10f) continue;
                var n = new Vector2(-d.y, d.x) * sgn;
                var h = new HP(n, -Vector2.Dot(n, a));
                var outside = Clip(rem, h.Neg);
                if (outside.Count >= 3 && Mathf.Abs(Area(outside)) > 1e-5f) res.Add(outside);
                rem = Clip(rem, h);
            }
            return res;
        }

        static void FacePlane(RoofFace f, out Vector2 A, out float B)
        {
            var R = f.roof;
            A = R.k * R.N(f.side);
            B = R.c + R.k * R.O(f.side);
        }

        static List<List<Vector2>> VisiblePieces(RoofFace F, List<RoofFace> layerFaces)
        {
            var pieces = new List<List<Vector2>> { F.poly };
            FacePlane(F, out var Af, out float Bf);
            foreach (var G in layerFaces)
            {
                if (G.roof == F.roof) continue;
                FacePlane(G, out var Ag, out float Bg);
                var dA = Ag - Af;
                float dB = Bg - Bf;
                List<Vector2> hidden;
                if (dA.sqrMagnitude < 1e-8f)
                {
                    // 平行：更高的盖住；共面时序号小的屋顶保留
                    if (dB > 1e-4f || (Mathf.Abs(dB) <= 1e-4f && G.roof.order < F.roof.order)) hidden = G.poly;
                    else continue;
                }
                else
                {
                    hidden = Clip(G.poly, new HP(dA, dB));
                    if (hidden.Count < 3 || Mathf.Abs(Area(hidden)) < 1e-5f) continue;
                }
                var next = new List<List<Vector2>>();
                foreach (var p in pieces) next.AddRange(Subtract(p, hidden));
                pieces = next;
                if (pieces.Count == 0) break;
            }
            return pieces;
        }

        static void EmitRoofPiece(MeshBatch mb, RoofFace f, List<Vector2> poly)
        {
            var R = f.roof;
            FacePlane(f, out var A, out float B);
            var normal = new Vector3(-A.x, 1f, -A.y).normalized;
            float slope = Mathf.Sqrt(1 + R.k * R.k);
            var n2 = R.N(f.side);
            var tan = new Vector2(n2.y, -n2.x);
            int c = poly.Count;
            var top = new Vector3[c];
            var under = new Vector3[c];
            var uvTop = new Vector2[c];
            var uvUnder = new Vector2[c];
            for (int i = 0; i < c; i++)
            {
                var p = poly[i];
                float h = Vector2.Dot(A, p) + B;
                top[i] = new Vector3(p.x, h, p.y);
                under[i] = new Vector3(p.x, h - .1f, p.y);
                uvTop[i] = new Vector2(Vector2.Dot(tan, p), R.Dist(f.side, p) * slope);
                uvUnder[i] = Vector2.Dot(tan, p) * Vector2.right + Vector2.up * R.Dist(f.side, p);
            }
            mb.Poly(D.Kawara, top, uvTop, normal);
            mb.Poly(D.Soffit, under, uvUnder, -normal);
        }

        /// <summary>檐口：瓦头 + 鼻隠し板。只在出檐的边界上做。</summary>
        static void Fascia(MeshBatch mb, RoofFace f, List<Vector2> poly)
        {
            var R = f.roof;
            FacePlane(f, out var A, out float B);
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                if ((b - a).sqrMagnitude < 1e-6f) continue;
                for (int s = 0; s < 4; s++)
                {
                    if (R.over[s] <= 0f) continue;
                    if (Mathf.Abs(R.Dist(s, a) + R.over[s]) > 2e-3f || Mathf.Abs(R.Dist(s, b) + R.over[s]) > 2e-3f) continue;
                    var outN = new Vector3(-R.N(s).x, 0, -R.N(s).y);
                    var a3 = new Vector3(a.x, Vector2.Dot(A, a) + B, a.y);
                    var b3 = new Vector3(b.x, Vector2.Dot(A, b) + B, b.y);
                    float ua = Vector2.Dot(new Vector2(outN.z, -outN.x), a), ub = Vector2.Dot(new Vector2(outN.z, -outN.x), b);
                    Strip(mb, D.RidgeTile, a3, b3, .03f, -.05f, outN, ua, ub);
                    Strip(mb, D.BeamDark, a3, b3, -.05f, -.2f, outN, ua, ub);
                }
            }
        }

        static void Strip(MeshBatch mb, Material m, Vector3 a, Vector3 b, float top, float bottom, Vector3 n, float ua, float ub)
        {
            var pts = new[] { a + Vector3.up * top, b + Vector3.up * top, b + Vector3.up * bottom, a + Vector3.up * bottom };
            var uv = new[] { new Vector2(ua, top), new Vector2(ub, top), new Vector2(ub, bottom), new Vector2(ua, bottom) };
            mb.Poly(m, pts, uv, n);
        }

        static float LayerH(List<RoofDef> layer, Vector2 p)
        {
            float best = -1e9f;
            foreach (var r in layer) if (r.In(p)) best = Mathf.Max(best, r.H(p));
            return best;
        }

        /// <summary>屋脊和隅棟：沿两坡交线采样，只在没被盖住、且是凸棱的地方铺脊瓦。</summary>
        static void RoofCaps(MeshBatch mb, RoofDef R, List<RoofDef> layer, Dictionary<Vector3Int, int> taken)
        {
            var pairs = new List<Vector2Int>();
            if (R.kind != RoofKind.Gable)
            {
                pairs.Add(new Vector2Int(0, 2)); pairs.Add(new Vector2Int(0, 3));
                pairs.Add(new Vector2Int(1, 2)); pairs.Add(new Vector2Int(1, 3));
            }
            pairs.Add(new Vector2Int(0, 1));
            if (R.kind == RoofKind.Hip) pairs.Add(new Vector2Int(2, 3));

            foreach (var pr in pairs)
            {
                int i = pr.x, j = pr.y;
                var a = R.N(i) - R.N(j);
                float b = R.O(i) - R.O(j);
                var p0 = -b * a / a.sqrMagnitude;
                var dir = new Vector2(-a.y, a.x).normalized;
                var hps = new List<HP>
                {
                    new HP(new Vector2(1, 0), -(R.x0 - R.over[2])), new HP(new Vector2(-1, 0), R.x1 + R.over[3]),
                    new HP(new Vector2(0, 1), -(R.z0 - R.over[0])), new HP(new Vector2(0, -1), R.z1 + R.over[1]),
                };
                bool ridgePair = (i < 2 && j < 2) || (i >= 2 && j >= 2);
                for (int k = 0; k < 4; k++)
                {
                    if (k == i || k == j) continue;
                    if (R.kind == RoofKind.Gable && k >= 2) continue;
                    if (R.kind == RoofKind.Irimoya && ridgePair && k >= 2) continue;
                    hps.Add(new HP(R.N(k) - R.N(i), R.O(k) - R.O(i)));
                }
                if (R.kind == RoofKind.Irimoya)
                {
                    if (j >= 2) hps.Add(new HP(-R.N(j), R.g - R.O(j)));
                    else
                    {
                        hps.Add(new HP(R.N(2), R.O(2) - R.g));
                        hps.Add(new HP(R.N(3), R.O(3) - R.g));
                    }
                }
                float t0 = -1e5f, t1 = 1e5f;
                foreach (var h in hps)
                {
                    float nd = Vector2.Dot(h.n, dir), v = h.Eval(p0);
                    if (Mathf.Abs(nd) < 1e-6f) { if (v < -1e-4f) { t0 = 1; t1 = 0; } continue; }
                    float t = -v / nd;
                    if (nd > 0) t0 = Mathf.Max(t0, t); else t1 = Mathf.Min(t1, t);
                }
                if (t1 - t0 < .1f) continue;

                int n = Mathf.Max(2, Mathf.CeilToInt((t1 - t0) / .15f) + 1);
                var nperp = a.normalized;
                var ok = new bool[n];
                var pts = new Vector3[n];
                var keys = new Vector3Int[n];
                for (int s = 0; s < n; s++)
                {
                    var p = p0 + dir * Mathf.Lerp(t0, t1, s / (float)(n - 1));
                    float h = R.H(p);
                    pts[s] = new Vector3(p.x, h, p.y);
                    keys[s] = Vector3Int.RoundToInt(pts[s] / .1f);
                    if (LayerH(layer, p) > h + .01f) continue;
                    if (LayerH(layer, p + nperp * .12f) + LayerH(layer, p - nperp * .12f) > 2 * h - .015f) continue;
                    bool dup = false;
                    for (int dx = -1; dx <= 1 && !dup; dx++)
                        for (int dy = -1; dy <= 1 && !dup; dy++)
                            for (int dz = -1; dz <= 1 && !dup; dz++)
                                if (taken.TryGetValue(keys[s] + new Vector3Int(dx, dy, dz), out int owner) && owner != R.order) dup = true;
                    ok[s] = !dup;
                }
                for (int s = 0; s < n; s++) if (ok[s]) taken[keys[s]] = R.order;

                bool flat = Mathf.Abs(pts[n - 1].y - pts[0].y) < .02f;
                float w = flat ? .26f : .18f, hh = flat ? .24f : .13f;
                float half = (t1 - t0) / (n - 1) / 2;
                for (int s = 0; s < n; s++)
                {
                    if (!ok[s]) continue;
                    int e = s;
                    while (e + 1 < n && ok[e + 1]) e++;
                    var A3 = pts[s];
                    var B3 = pts[e];
                    var d3 = (B3 - A3).normalized;
                    if (s > 0) A3 -= d3 * half;
                    if (e < n - 1) B3 += d3 * half;
                    CapSeg(mb, A3, B3, w, hh);
                    if (flat) CapSeg(mb, A3 + Vector3.up * (hh - .02f), B3 + Vector3.up * (hh - .02f), .15f, .1f);
                    if (flat)
                    {
                        if (s == 0) Onigawara(mb, A3, -d3, .32f, .42f);
                        if (e == n - 1) Onigawara(mb, B3, d3, .32f, .42f);
                    }
                    else
                    {
                        // 隅棟下端的小鬼瓦
                        if (s == 0 && pts[0].y < pts[n - 1].y) Onigawara(mb, A3, -d3, .2f, .26f);
                        if (e == n - 1 && pts[n - 1].y < pts[0].y) Onigawara(mb, B3, d3, .2f, .26f);
                    }
                    s = e;
                }
            }
        }

        static void CapSeg(MeshBatch mb, Vector3 a, Vector3 b, float w, float h)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < .05f) return;
            var rot = Quaternion.LookRotation(d / len, Vector3.up);
            var up = rot * Vector3.up;
            mb.Box(D.RidgeTile, (a + b) / 2 + up * (h / 2 - .04f), new Vector3(w, h, len), rot);
        }

        static void Onigawara(MeshBatch mb, Vector3 end, Vector3 outward, float w, float h)
        {
            var flatDir = new Vector3(outward.x, 0, outward.z);
            if (flatDir.sqrMagnitude < 1e-6f) return;
            var rot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            mb.Box(D.RidgeTile, end + Vector3.up * (h / 2 - .04f), new Vector3(w, h, .12f), rot);
            mb.Box(D.RidgeTile, end + Vector3.up * (h - .02f) + flatDir.normalized * .04f, new Vector3(w * .7f, .06f, .16f), rot);
        }

        static void IrimoyaGables(MeshBatch mb, RoofDef R)
        {
            float zm = (R.z0 + R.z1) / 2, ridge = R.c + R.k * (R.z1 - R.z0) / 2, hg = R.c + R.k * R.g;
            float za = R.z0 + R.g, zb = R.z1 - R.g;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? R.x0 + R.g : R.x1 - R.g;
                float sx = side == 0 ? -1f : 1f;
                var nrm = new Vector3(sx, 0, 0);
                var tri = new[] { new Vector3(x, hg, za), new Vector3(x, hg, zb), new Vector3(x, ridge - .03f, zm) };
                mb.Poly(D.PlasterWhite, tri, new[] { new Vector2(za, hg), new Vector2(zb, hg), new Vector2(zm, ridge) }, nrm);
                // 木连格子
                for (float z = za + .3f; z < zb - .1f; z += .3f)
                {
                    float top = R.c + R.k * Mathf.Min(z - R.z0, R.z1 - z) - .08f;
                    if (top - hg < .25f) continue;
                    mb.Box(D.BeamDark, new Vector3(x + sx * .015f, (hg + .24f + top) / 2, z), new Vector3(.03f, top - hg - .24f, .04f));
                }
                for (float y = hg + .6f; y < ridge - .25f; y += .35f)
                {
                    float reach = (y + .08f - R.c) / R.k;
                    float z0 = R.z0 + reach, z1 = R.z1 - reach;
                    if (z1 - z0 < .2f) continue;
                    mb.Box(D.BeamDark, new Vector3(x + sx * .02f, y, (z0 + z1) / 2), new Vector3(.03f, .035f, z1 - z0));
                }
                mb.Box(D.BeamDark, new Vector3(x + sx * .025f, hg + .15f, zm), new Vector3(.05f, .18f, zb - za - .2f));
                mb.Box(D.RidgeTile, new Vector3(x + sx * .09f, hg + .03f, zm), new Vector3(.2f, .1f, zb - za + .1f));
                // 破風板 + 懸魚
                float xh = x + sx * .07f;
                foreach (float zs in new[] { za - .35f, zb + .35f })
                {
                    float ys = R.c + R.k * Mathf.Min(zs - R.z0, R.z1 - zs);
                    var a3 = new Vector3(xh, ys + .06f - .15f, zs);
                    var b3 = new Vector3(xh, ridge + .06f - .15f, zm);
                    var d = b3 - a3;
                    var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
                    mb.Box(D.BeamDark, (a3 + b3) / 2, new Vector3(.05f, .3f, d.magnitude), rot);
                }
                mb.Box(D.BeamDark, new Vector3(xh + sx * .01f, ridge - .42f, zm), new Vector3(.05f, .36f, .42f));
                mb.Box(D.Gold, new Vector3(xh + sx * .04f, ridge - .42f, zm), new Vector3(.01f, .12f, .12f));
            }
        }

        static void KuraGables(MeshBatch mb, RoofDef R)
        {
            float zm = (R.z0 + R.z1) / 2, ridge = R.c + R.k * (R.z1 - R.z0) / 2, y0 = Levels[GKura].wallTop;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? R.x0 - .012f : R.x1 + .012f, sx = side == 0 ? -1f : 1f;
                var pts = new[]
                {
                    new Vector3(x, y0, R.z0), new Vector3(x, R.c, R.z0), new Vector3(x, ridge, zm),
                    new Vector3(x, R.c, R.z1), new Vector3(x, y0, R.z1),
                };
                var uv = new Vector2[pts.Length];
                for (int i = 0; i < pts.Length; i++) uv[i] = new Vector2(pts[i].z, pts[i].y);
                mb.Poly(D.PlasterWhite, pts, uv, new Vector3(sx, 0, 0));
                // 家纹（只是个黑圆环）
                float crest = (R.c + ridge) / 2;
                var rot = Quaternion.Euler(0, 0, sx > 0 ? -90f : 90f);
                mb.Cyl(D.Black, new Vector3(x, crest, zm), .3f, .3f, .012f, 28, true, rot);
                mb.Cyl(D.PlasterWhite, new Vector3(x + sx * .012f, crest, zm), .22f, .22f, .004f, 28, true, rot);
                mb.Cyl(D.Black, new Vector3(x + sx * .016f, crest, zm), .07f, .07f, .004f, 16, true, rot);
            }
        }

        // =====================================================================
        // 围墙：石基 + 瓦顶；冠木门
        // =====================================================================
        static void FenceAndGate(Transform parent)
        {
            var fence = RoomById("Fence");
            var mb = new MeshBatch();
            if (fence != null)
            {
                foreach (var e in EdgesOf(fence))
                {
                    float s0 = e.alongX ? e.from - .1f : e.from, s1 = e.alongX ? e.to + .1f : e.to;
                    var cuts = new List<Vector2>();
                    foreach (var g in GapsOn(fence, e)) cuts.Add(new Vector2(g.a, g.b));
                    foreach (var run in Minus(s0, s1, cuts))
                    {
                        WallBox(mb, D.StoneWall, e, run.x, run.y, 0f, .3f, -.03f, 0f, true);
                        WallBox(mb, D.StoneWall, e, run.x, run.y, 0f, .3f, T, T + .03f, true);
                        FenceCoping(mb, e, run.x, run.y);
                    }
                }
            }

            // 冠木门
            // 门洞 3.6 m 宽，门柱比围墙高 0.5 m
            float cx = 2.73f * PlanScale, gz = -5.46f * PlanScale + T / 2, fh = Levels[GFence].wallTop;
            float px = 2.4f * OpeningWidthScale / 2 + .15f, span = 2 * px + .64f;
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Box(D.Post, new Vector3(cx + sx * px, (fh + .5f) / 2, gz), new Vector3(.3f, fh + .5f, .3f));
                mb.Box(D.Stone, new Vector3(cx + sx * px, .06f, gz), new Vector3(.45f, .12f, .45f));
            }
            mb.Box(D.Post, new Vector3(cx, fh + .15f, gz), new Vector3(span, .24f, .22f));
            mb.Box(D.BeamDark, new Vector3(cx, fh + .38f, gz), new Vector3(span - .2f, .12f, .55f));
            foreach (float sz in new[] { -1f, 1f })
                mb.Box(D.Kawara, new Vector3(cx, fh + .62f, gz + sz * .42f), new Vector3(span + .4f, .05f, .95f), Quaternion.Euler(sz * 24f, 0, 0), 0);
            mb.Box(D.RidgeTile, new Vector3(cx, fh + .82f, gz), new Vector3(span + .5f, .12f, .16f));
            foreach (float sx in new[] { -1f, 1f })
                mb.Box(D.RidgeTile, new Vector3(cx + sx * (span / 2 + .25f), fh + .9f, gz), new Vector3(.1f, .26f, .2f));
            // 表札
            mb.Box(D.TrimLight, new Vector3(cx + px, 1.6f, gz - .163f), new Vector3(.12f, .36f, .025f));
            mb.Box(D.Ink, new Vector3(cx + px, 1.6f, gz - .176f), new Vector3(.03f, .24f, .002f));
            mb.Build(parent, "Fence_Coping_Gate");
        }

        static void FenceCoping(MeshBatch mb, Edge e, float s0, float s1)
        {
            float dc = T / 2, len = s1 - s0, fh = Levels[GFence].wallTop;
            WallBox(mb, D.PlasterWhite, e, s0, s1, fh, fh + .06f, dc - .1f, dc + .1f, true);
            float sm = (s0 + s1) / 2;
            foreach (float side in new[] { -1f, 1f })
            {
                var c = OnWall(e, sm, fh + .125f, dc + side * .125f);
                // side 是 d 方向；换算成世界坐标的正负
                float worldSign = side * e.inward;
                if (e.alongX)
                    mb.Box(D.Kawara, c, new Vector3(len, .03f, .27f), Quaternion.Euler(worldSign * 22f, 0, 0), 0);
                else
                    mb.Box(D.Kawara, c, new Vector3(.27f, .03f, len), Quaternion.Euler(0, 0, -worldSign * 22f), 2);
            }
            WallBox(mb, D.RidgeTile, e, s0, s1, fh + .14f, fh + .2f, dc - .05f, dc + .05f, true);
        }
    }
}
