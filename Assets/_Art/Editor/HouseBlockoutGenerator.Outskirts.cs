using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第六轮（2026-11-24，用户：按推荐开工）：院墙外的风景——朝南山坡上的废村，到处没有一盏灯。
    /// <list type="bullet">
    /// <item>南（大门外）：贴着院墙一条 U 形水泥沟（门口盖着沟盖）、3.6 m 宽的旧柏油村道、路肩，
    ///   然后石砌的一道坎下去，是一层层荒掉的梯田，一直到一排树后面陡下去的山谷（谷底不建，对面的山画在天空盒里）。</item>
    /// <item>北：院墙后 1.5 m 一道石砌挡土墙，上面是种满杉树的山坡。</item>
    /// <item>西：竹林从墙外探进来；再往西一座塌了屋角的邻家空屋，屋脊从墙头露出来。</item>
    /// <item>东：一棵没人摘的柿子树斜过墙头（柿子压暗，不和仏壇的红抢）、一间铁皮顶的旧仓房、一条田间小路。</item>
    /// <item>大门东边一根水泥电线杆：路灯只有灯具、从来不亮；拉到房子的那根进户线断了，挂在墙头上（所以没电）。</item>
    /// </list>
    /// 只是视觉：没有碰撞体；全部不投影（月光的影子不进院子，院子里的明暗不变）；不加灯。
    /// Ground_Garden 碰撞体不动（它本来就伸出墙外：南北 5.5 m、东西 11.8 m）；大门外 5.58 m 内的沟盖、路面、路肩都在 y = 0，和它齐平。
    /// 雾里（探索者）7.5 m 以外都是雾色，院外主要是天空前面的黑剪影；GhostSight 关雾以后看得清，所以近处、中景都要经得起看。
    /// 随机数单独一条。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        // 院墙外皮（Fence 房间的四条边）
        static float OX0, OX1, OZ0, OZ1;
        static System.Random orng;
        static float OR(float a, float b) => a + (b - a) * (float)orng.NextDouble();

        const float RoadW = 3.6f, DitchW = .48f, VergeW = 1.5f;
        // 梯田：每层比上一层低多少、进深多少（第 0 层前面是石砌的坎，后面的是土坡田埂，坡宽 BankW）
        static readonly float[] TerDrop = { 1.7f, 1.6f, 1.9f, 1.5f, 1.8f, 1.7f, 1.6f, 1.9f };
        static readonly float[] TerDepth = { 9f, 8.5f, 9.5f, 8f, 9f, 10f, 8.5f, 9f };
        const float BankW = 1.7f;
        // 院外地面的范围（再往外是天空盒里画的山）
        const float OutXMin = -140f, OutXMax = 190f;
        static float OutZMax => OZ1 + 120f; // 北坡再往上从院子里看不到（被近处的杉林挡住）
        // 梯田东西两头渐渐变成林子里的自然坡
        const float TerWestA = -85f, TerWestB = -110f, TerEastA = 135f, TerEastB = 160f;

        static float S01(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float ZVerge => OZ0 - DitchW - RoadW - VergeW; // 路肩外沿 = 石坎顶（离墙 5.58 m）
        static float ZRoadS => OZ0 - DitchW - RoadW;
        static float ZRoadN => OZ0 - DitchW;
        static float GateX => 2.73f * PlanScale;
        static float PoleZ => ZRoadN - .3f;

        static void BuildOutskirts(Transform parent)
        {
            var fence = RoomById("Fence");
            if (fence == null) return;
            OX0 = fence.r.xMin; OX1 = fence.r.xMax; OZ0 = fence.r.yMin; OZ1 = fence.r.yMax;
            orng = new System.Random(6024);
            FloraLoad();
            OutskirtsLoad();

            var xs = OutColumns();
            var ground = new MeshBatch();
            var grass = new MeshBatch();
            terraceLips.Clear();
            terraceFields.Clear();
            var lastEdge = OutskirtsSouth(ground, xs);
            OutskirtsSides(ground, xs);
            OutskirtsNorth(ground, xs);
            RoadAndDitch(ground, grass);
            TerraceGrass(grass);
            var groundGo = ground.Build(parent, "Outskirts_Ground", false);
            grass.Build(parent, "Outskirts_Grass", false);

            var poles = new MeshBatch();
            UtilityPoles(poles);
            poles.Build(parent, "Outskirts_PolesAndWires", false);

            // 树、房子按地面高度摆：临时给地面加一个碰撞体往下打射线，摆完删掉（院外不留碰撞体）
            if (groundGo != null)
            {
                outProbe = groundGo.AddComponent<MeshCollider>();
                outProbe.sharedMesh = groundGo.GetComponent<MeshFilter>().sharedMesh;
            }
            try
            {
                OutskirtsTrees(parent, lastEdge);
                OutskirtsBuildings(parent);
            }
            finally
            {
                if (outProbe != null) Object.DestroyImmediate(outProbe);
                outProbe = null;
            }
            long tris = 0;
            int subs = 0;
            var per = new System.Text.StringBuilder();
            foreach (var mf in parent.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                subs += mesh.subMeshCount;
                long t = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) t += mesh.GetIndexCount(i) / 3;
                tris += t;
                per.Append($"{mf.name} {t}/{mesh.subMeshCount}；");
            }
            Debug.Log($"[Hako Art] 院外风景：{parent.GetComponentsInChildren<MeshRenderer>(true).Length} 个渲染器、{subs} 个子网格（同时可见时的 draw call 上限）、{tris} 个三角形");
            Debug.Log($"[Hako Art] 院外风景分项（三角形/子网格）：{per}");
        }

        // =====================================================================
        // 网格工具
        // =====================================================================
        /// <summary>地面的列：院子附近密（2.5 m）、远处稀（5 m、10 m）；院墙东西两条墙线上正好各有一列。</summary>
        static List<float> OutColumns()
        {
            var xs = new List<float>();
            for (float x = OutXMin; x < OutXMax - .01f;)
            {
                xs.Add(x);
                x += x > -45f && x < 95f ? 2.5f : x > -95f && x < 145f ? 5f : 10f;
            }
            xs.Add(OutXMax);
            Snap(xs, OX0);
            Snap(xs, OX1);
            return xs;
        }

        static void Snap(List<float> xs, float v)
        {
            int best = 0;
            for (int i = 1; i < xs.Count; i++)
                if (Mathf.Abs(xs[i] - v) < Mathf.Abs(xs[best] - v)) best = i;
            xs[best] = v;
        }

        /// <summary>田埂的弯：每道田埂一条，上下沿用同一条（坡宽不变）。</summary>
        static float Wig(float x, int k) =>
            k == 0 ? 0f : 1.3f * (.6f * Mathf.Sin(x / 23f + k * 1.7f) + .4f * Mathf.Sin(x / 8.7f + k * 2.9f));

        /// <summary>梯田东西两头：0 = 梯田，1 = 林子里的自然坡。</summary>
        static float TerFade(float x) => Mathf.Max(S01(TerWestA, TerWestB, x), S01(TerEastA, TerEastB, x));

        static Vector3 UpNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.y < 0f) n = -n;
            return n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
        }

        /// <summary>
        /// 两条线（同一组列上的点）之间铺一条地面，法线只在这一条里平滑（田埂、石坎的折线保持锐利）。
        /// UV = (x, z + y)，以米为单位（坡面不至于拉得太长）。mat 返回 null 的格子不铺。
        /// </summary>
        static void GroundStrip(MeshBatch mb, Vector3[] a, Vector3[] b, System.Func<Vector3, Material> mat)
        {
            int n = a.Length;
            var na = new Vector3[n];
            var nb = new Vector3[n];
            for (int i = 0; i < n - 1; i++)
            {
                var q = UpNormal(a[i], a[i + 1], b[i]) + UpNormal(b[i + 1], b[i], a[i + 1]);
                na[i] += q; na[i + 1] += q; nb[i] += q; nb[i + 1] += q;
            }
            Vector2 U(Vector3 p) => new Vector2(p.x, p.z + p.y);
            for (int i = 0; i < n - 1; i++)
            {
                var m = mat((a[i] + a[i + 1] + b[i] + b[i + 1]) * .25f);
                if (m == null) continue;
                Vector3 n0 = na[i].normalized, n1 = na[i + 1].normalized, n2 = nb[i + 1].normalized, n3 = nb[i].normalized;
                mb.TriS(m, a[i], a[i + 1], b[i + 1], n0, n1, n2, U(a[i]), U(a[i + 1]), U(b[i + 1]));
                mb.TriS(m, a[i], b[i + 1], b[i], n0, n2, n3, U(a[i]), U(b[i + 1]), U(b[i]));
            }
        }

        static void GroundStrip(MeshBatch mb, Vector3[] a, Vector3[] b, Material m) => GroundStrip(mb, a, b, _ => m);

        // =====================================================================
        // 南：路肩外的石坎 → 梯田 → 一排树后面陡下去
        // =====================================================================
        /// <summary>返回最后一层梯田南沿的线（排树用）。</summary>
        static Vector3[] OutskirtsSouth(MeshBatch mb, List<float> xs)
        {
            int n = xs.Count;
            float z0 = ZVerge;
            // 每层地块北沿、南沿的基准 z 和高度
            int N = TerDrop.Length;
            var zN = new float[N];
            var zS = new float[N];
            var yk = new float[N];
            float y = 0f, z = z0 - .45f;
            for (int k = 0; k < N; k++)
            {
                y -= TerDrop[k];
                yk[k] = y;
                zN[k] = z;
                zS[k] = z - TerDepth[k];
                z = zS[k] - BankW;
            }
            float zLast = zS[N - 1], yLast = yk[N - 1];
            // 东西两头的自然坡：从路肩（y = 0）到最后一层的南沿，再往下按陡坡
            float slope = yLast / (zLast - z0);
            float Smooth(float zz) => zz >= z0 ? 0f : zz >= zLast ? (zz - z0) * slope : yLast + (zz - zLast) * .55f;

            Vector3[] Line(System.Func<float, float> zf, System.Func<float, float> yf)
            {
                var pts = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float x = xs[i], zz = zf(x), f = TerFade(x);
                    // 起伏从路肩往外渐渐加上去（路肩那条线 y = 0，和路面接得上）
                    float yy = Mathf.Lerp(yf(x), Smooth(zz), f) + f * S01(0f, 8f, z0 - zz) * (Mathf.PerlinNoise(x * .05f + 3f, zz * .05f) - .5f) * 2.4f;
                    pts[i] = new Vector3(x, yy, zz);
                }
                return pts;
            }
            // 第六轮 b：隔一层换一种田（深一点、纹理大一点）；从路上往下看，田埂的坡面朝外看不见，全靠这个和田边的芒草分层
            Material Field(Vector3 c, int k) => TerFade(c.x) > .5f ? D.Ground : k % 2 == 0 ? D.FieldGrass : D.FieldFallow;
            Material Bank(Vector3 c) => TerFade(c.x) > .5f ? D.Ground : D.FieldBank;
            Material Stone(Vector3 c) => TerFade(c.x) > .5f ? D.Ground : D.StoneWall;

            var verge = Line(_ => z0, _ => 0f);
            var prev = verge;
            for (int k = 0; k < N; k++)
            {
                int kk = k;
                float yy = yk[k];
                var north = Line(x => zN[kk] + Wig(x, kk), _ => yy);
                GroundStrip(mb, prev, north, k == 0 ? (System.Func<Vector3, Material>)Stone : Bank);
                var south = Line(x => zS[kk] + Wig(x, kk + 1), _ => yy);
                GroundStrip(mb, north, south, c => Field(c, kk));
                terraceLips.Add(south);
                terraceFields.Add((north, south));
                prev = south;
            }
            var lastEdge = prev;
            // 陡坡：林子盖着，从院子里看不到坡面，只看到坡顶那排树
            float[] dz = { 3f, 10f, 22f, 42f, 70f };
            float[] dy = { 2.5f, 8f, 17f, 28f, 40f };
            for (int s = 0; s < dz.Length; s++)
            {
                int ss = s;
                var next = Line(x => zS[N - 1] + Wig(x, N) - dz[ss], _ => yLast - dy[ss]);
                for (int i = 0; i < n; i++)
                    next[i].y += (Mathf.PerlinNoise(xs[i] * .04f, s * 1.7f) - .5f) * 3f;
                GroundStrip(mb, prev, next, D.Ground);
                prev = next;
            }
            // 第一道石坎：站在第一层梯田上回头看（审图机位）
            ShotOut("terrace_wall", new Vector3(GateX + 7.5f, yk[0] + 1.6f, zN[0] - 4.5f), new Vector3(GateX + 6f, -.9f, z0 - .2f));
            ShotOut("terraces_from_road", new Vector3(GateX + 1.5f, 1.65f, ZRoadS + .3f), new Vector3(GateX + 4f, -5f, z0 - 22f));
            return lastEdge;
        }

        // =====================================================================
        // 东西两边：墙外的平地（邻家空屋、竹林、柿子树、仓房、田间小路都在上面）
        // =====================================================================
        static void OutskirtsSides(MeshBatch mb, List<float> xs)
        {
            var zs = new List<float>();
            for (float z = OZ0; z < OZ1 - .01f; z += 3f) zs.Add(z);
            zs.Add(OZ1);
            foreach (bool west in new[] { true, false })
            {
                var cols = new List<float>();
                foreach (float x in xs)
                    if (west ? x <= OX0 + .001f : x >= OX1 - .001f) cols.Add(x);
                if (cols.Count < 2) continue;
                float wallX = west ? OX0 : OX1;
                Vector3 P(float x, float z) => new Vector3(x, SideY(x, z, wallX), z);
                Material Mat(Vector3 c)
                {
                    if (west)
                    {
                        if (c.x < -80f || (c.x > -26f && c.z > 9f)) return D.Ground; // 林子、竹林下面是落叶土
                        return D.FieldGrass;
                    }
                    if (c.x > 135f) return D.Ground;
                    return D.FieldGrass;
                }
                for (int j = 0; j < zs.Count - 1; j++)
                {
                    var a = new Vector3[cols.Count];
                    var b = new Vector3[cols.Count];
                    for (int i = 0; i < cols.Count; i++) { a[i] = P(cols[i], zs[j]); b[i] = P(cols[i], zs[j + 1]); }
                    GroundStrip(mb, a, b, Mat);
                }
            }
            // 东边的田间小路：从村道（沟上盖着沟盖）往东北，经过仓房南边
            var path = new List<Vector3>();
            for (float t = 0f; t <= 1.0001f; t += .05f)
            {
                float x = Mathf.Lerp(PathX, OX1 + 62f, t);
                float z = OZ0 + .05f + 34f * t * t + 2.2f * Mathf.Sin(t * 7f);
                path.Add(new Vector3(x, SideY(x, z, OX1) + .05f, z));
            }
            Ribbon(mb, D.Dirt, path, 1.1f);
        }

        static float PathX => OX1 + 9.5f;

        /// <summary>院墙东西两边那片平地的高度：墙根、路边、北边挡土墙脚都是 y = 0，离开一点才有起伏，远处起伏大一些。</summary>
        static float SideY(float x, float z, float wallX)
        {
            float ramp = S01(0f, 6f, z - OZ0) * S01(0f, 6f, OZ1 - z) * S01(0f, 5f, Mathf.Abs(x - wallX));
            float far = S01(40f, 90f, Mathf.Abs(x - wallX));
            return ramp * ((Mathf.PerlinNoise(x * .09f + 11f, z * .09f) - .5f) * .5f + far * (Mathf.PerlinNoise(x * .03f, z * .03f + 5f) - .3f) * 4f);
        }

        /// <summary>贴地的一条带子（小路）：pts 是中线，宽 w，y 用点本身的。</summary>
        static void Ribbon(MeshBatch mb, Material m, IList<Vector3> pts, float w)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var d = (pts[i + 1] - pts[i]);
                d.y = 0f;
                var side = Vector3.Cross(Vector3.up, d.normalized) * (w * .5f);
                Vector3 a = pts[i] - side, b = pts[i] + side, c = pts[i + 1] + side, e = pts[i + 1] - side;
                mb.Poly(m, new[] { a, b, c, e }, new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(e.x, e.z) }, Vector3.up);
            }
        }

        // =====================================================================
        // 北：墙后 1.5 m 平地、石砌挡土墙、杉树山坡
        // =====================================================================
        const float NorthStrip = 1.5f, NorthWallH = 2.6f, NorthBatter = .3f;

        static void OutskirtsNorth(MeshBatch mb, List<float> xs)
        {
            int n = xs.Count;
            float zFoot = OZ1 + NorthStrip, zTop = zFoot + NorthBatter;
            Vector3[] Row(float z, System.Func<float, float, float> yf)
            {
                var p = new Vector3[n];
                for (int i = 0; i < n; i++) p[i] = new Vector3(xs[i], yf(xs[i], z), z);
                return p;
            }
            var r0 = Row(OZ1, (x, z) => 0f);
            var r1 = Row(zFoot, (x, z) => 0f);
            GroundStrip(mb, r0, r1, D.Ground);
            // 挡土墙：竖着的条带，UV 按 (x, y) 铺（石头不拉长）
            for (int i = 0; i < n - 1; i++)
            {
                var a = new Vector3(xs[i], 0f, zFoot);
                var b = new Vector3(xs[i + 1], 0f, zFoot);
                var c = new Vector3(xs[i + 1], NorthWallH, zTop);
                var d = new Vector3(xs[i], NorthWallH, zTop);
                var nrm = new Vector3(0f, NorthBatter, -NorthWallH).normalized;
                mb.Poly(D.StoneWall, new[] { a, b, c, d },
                    new[] { new Vector2(a.x, a.y), new Vector2(b.x, b.y), new Vector2(c.x, c.y), new Vector2(d.x, d.y) }, nrm);
                // 墙顶压一条水泥（看得出是人砌的）
                mb.Box(D.Concrete, new Vector3((xs[i] + xs[i + 1]) * .5f, NorthWallH + .06f, zTop + .12f), new Vector3(xs[i + 1] - xs[i] + .01f, .12f, .36f), null, 0);
            }
            float HillY(float x, float z)
            {
                float dzz = z - zTop;
                float ramp = S01(0f, 9f, dzz);
                return NorthWallH + .1f + dzz * .55f + ramp * (Mathf.PerlinNoise(x * .025f + 7f, z * .025f) - .5f) * 9f
                       + ramp * (Mathf.PerlinNoise(x * .11f, z * .11f + 3f) - .5f) * 1.2f;
            }
            var prev = Row(zTop + .3f, HillY);
            var lip = Row(zTop + .3f, (x, z) => NorthWallH + .12f);
            GroundStrip(mb, lip, prev, D.Ground);
            for (float z = zTop + 4f; z < OutZMax + .01f; z += z < zTop + 40f ? 4f : 9f)
            {
                var next = Row(z, HillY);
                GroundStrip(mb, prev, next, D.Ground);
                prev = next;
            }
        }

        /// <summary>北坡上某点的地面高度（种树用，和 OutskirtsNorth 一致）。</summary>
        static float NorthHillY(float x, float z)
        {
            float zTop = OZ1 + NorthStrip + NorthBatter, dzz = z - zTop;
            if (dzz < .3f) return 0f;
            float ramp = S01(0f, 9f, dzz);
            return NorthWallH + .1f + dzz * .55f + ramp * (Mathf.PerlinNoise(x * .025f + 7f, z * .025f) - .5f) * 9f
                   + ramp * (Mathf.PerlinNoise(x * .11f, z * .11f + 3f) - .5f) * 1.2f;
        }

        // =====================================================================
        // 村道、U 形沟、路肩
        // =====================================================================
        static void RoadAndDitch(MeshBatch mb, MeshBatch weeds)
        {
            float xa = OutXMin, xb = OutXMax;
            // 路面、路肩（y = 0，和 Ground_Garden 的碰撞体顶面齐平）
            void Flat(Material m, float z1, float z2, float y = 0f)
            {
                for (float x = xa; x < xb - .01f; x += 10f)
                {
                    float x2 = Mathf.Min(xb, x + 10f);
                    var a = new Vector3(x, y, z1); var b = new Vector3(x2, y, z1); var c = new Vector3(x2, y, z2); var d = new Vector3(x, y, z2);
                    mb.Poly(m, new[] { a, b, c, d }, new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z) }, Vector3.up);
                }
            }
            Flat(D.Asphalt, ZRoadN, ZRoadS);
            Flat(D.FieldGrass, ZRoadS, ZVerge);

            // U 形沟：贴着院墙（坡上那一边接雨水）。院子附近一节 2 m，远处一节 6 m（反正在雾里）；
            // 大门前、田间小路口盖着水泥沟盖（顶面 y = 0，和沟壁顶面同一个面、同一种材质、同样的 UV，不闪）
            const float wall = .07f, depth = .3f, slab = .07f;
            float zc = (OZ0 + ZRoadN) * .5f;
            for (float x = xa; x < xb - .01f;)
            {
                float seg = x > -40f && x < 90f ? 2f : 6f;
                float len = Mathf.Min(seg, xb - x) - .012f, cx = x + len * .5f;
                mb.Box(D.Concrete, new Vector3(cx, -depth * .5f, OZ0 - wall * .5f), new Vector3(len, depth, wall), null, 0);
                mb.Box(D.Concrete, new Vector3(cx, -depth * .5f, ZRoadN + wall * .5f), new Vector3(len, depth, wall), null, 0);
                mb.Box(D.Concrete, new Vector3(cx, -depth - slab * .5f, zc), new Vector3(len, slab, DitchW), null, 0);
                mb.Box(D.Ground, new Vector3(cx, -depth + .015f, zc), new Vector3(len, .03f, DitchW - 2f * wall), null, 0); // 沟底的烂泥和落叶（第六轮 b：原来的枯叶色太黄）
                x += seg;
            }
            foreach (var (l0, l1) in new[] { (GateX - 2.1f, GateX + 2.1f), (PathX - .6f, PathX + .6f) })
                for (float x = l0; x < l1 - .01f; x += .6f)
                    mb.Box(D.Concrete, new Vector3(x + .3f, -.04f, zc), new Vector3(.588f, .08f, DitchW - .02f), null, 0);
            // 第六轮 c：机位挪到沟边、往下看（原来站在路对面，沟只剩墙脚下一条看不清的暗线）
            ShotOut("gate_ditch", new Vector3(GateX + .6f, 1.55f, ZRoadN - 1f), new Vector3(GateX + 3.2f, -.25f, zc));

            // 路面边上冒出来的枯草、门口飘来的落叶
            if (floraShrub != null)
            {
                for (float x = xa + 2f; x < xb - 2f; x += OR(1.1f, 2.6f))
                {
                    // 路肩上
                    var p = new Vector3(x, 0f, OR(ZVerge + .2f, ZRoadS - .05f));
                    var g = (Vector3.up + RandDir(orng) * .35f).normalized;
                    FloraCross(weeds, floraShrub, p, g, OR(.5f, .9f), orng.Next(5) < 3 ? 3 : orng.Next(3), (g + Vector3.back * .3f).normalized, OR(0f, 180f));
                    // 第六轮 b：路肩外沿隔几米一丛芒草（从大门看是天空前面的剪影）
                    if (susukiMat != null && orng.Next(3) == 0)
                        FloraCross(weeds, susukiMat, new Vector3(x + OR(-.4f, .4f), 0f, OR(ZVerge + .1f, ZVerge + .6f)), (Vector3.up + RandDir(orng) * .1f).normalized,
                            OR(.9f, 1.5f), orng.Next(4) == 0 ? 3 : orng.Next(3), Vector3.up, OR(0f, 180f));
                    // 沟和墙之间、沟的另一边：稀一点
                    if (orng.Next(3) == 0)
                    {
                        var q = new Vector3(x + OR(-.5f, .5f), 0f, ZRoadN - .03f);
                        FloraCross(weeds, floraShrub, q, (Vector3.up + RandDir(orng) * .3f).normalized, OR(.35f, .6f), 3, Vector3.up, OR(0f, 180f));
                    }
                }
            }
            if (floraMaple != null)
            {
                const float c0 = 1f / 8f, ins = 3f / 1024f;
                for (int i = 0; i < 28; i++) // 第六轮 b：70 片在柏油路上像一滴滴血，减到 28
                {
                    float a = OR(0f, 6.28f), rr = Mathf.Sqrt(OR(0f, 1f)) * 4.5f;
                    var at = new Vector3(GateX + Mathf.Cos(a) * rr * 1.4f, .006f + OR(0f, .004f), ZRoadN - .6f + Mathf.Sin(a) * rr * .45f);
                    if (at.z > ZRoadN - .05f || at.z < ZRoadS + .05f) continue;
                    var g = Quaternion.Euler(0, OR(0f, 360f), 0) * Vector3.forward;
                    float s = OR(.07f, .1f);
                    var uv = new Rect(.5f + orng.Next(4) * c0 + ins, orng.Next(4) * c0 + ins, c0 - 2 * ins, c0 - 2 * ins);
                    FloraCard(weeds, floraMaple, at, g, Vector3.Cross(Vector3.up, g), s, s, uv, Vector3.up);
                }
            }
        }

        // 梯田每层南沿（田边，下面是田埂）和每层地块（北沿、南沿两条线），TerraceGrass 用
        static readonly List<Vector3[]> terraceLips = new List<Vector3[]>();
        static readonly List<(Vector3[] n, Vector3[] s)> terraceFields = new List<(Vector3[] n, Vector3[] s)>();

        /// <summary>
        /// 第六轮 b：梯田的田边一排芒草（薄）、田里零星的芒草丛和灌木（荒了十几年的田）。
        /// 第一版从路上往下看是一整片平地：田埂的坡面朝南、看不见；有了田边这排草，一层层才分得出来。
        /// </summary>
        static void TerraceGrass(MeshBatch weeds)
        {
            if (susukiMat == null) return;
            int nEdge = 0, nField = 0, nShrub = 0;
            foreach (var lip in terraceLips)
                for (int i = 0; i < lip.Length - 1; i++)
                {
                    float x0 = lip[i].x, x1 = lip[i + 1].x;
                    for (float x = x0 + OR(0f, 1f); x < x1; x += OR(.9f, 1.9f))
                    {
                        if (TerFade(x) > .45f) continue;
                        var p = Vector3.Lerp(lip[i], lip[i + 1], Mathf.InverseLerp(x0, x1, x)) + new Vector3(OR(-.3f, .3f), -.05f, OR(-.5f, .6f));
                        FloraCross(weeds, susukiMat, p, (Vector3.up + RandDir(orng) * .12f).normalized, OR(1.1f, 1.9f),
                            orng.Next(5) == 0 ? 3 : orng.Next(3), Vector3.up, OR(0f, 180f));
                        nEdge++;
                    }
                }
            foreach (var (n, s) in terraceFields)
                for (int i = 0; i < n.Length - 1; i++)
                {
                    float x0 = n[i].x, x1 = n[i + 1].x;
                    if (TerFade((x0 + x1) * .5f) > .45f) continue;
                    // 每 2.5 m 一格里平均一丛多一点：芒草、低的枯草、偶尔一丛灌木
                    int count = Mathf.RoundToInt((x1 - x0) / 2.5f * OR(.6f, 1.6f));
                    for (int c = 0; c < count; c++)
                    {
                        float u = OR(0f, 1f), v = OR(.08f, .92f);
                        var p = Vector3.Lerp(Vector3.Lerp(n[i], n[i + 1], u), Vector3.Lerp(s[i], s[i + 1], u), v) - Vector3.up * .05f;
                        int r = orng.Next(10);
                        if (r < 6)
                        {
                            FloraCross(weeds, susukiMat, p, (Vector3.up + RandDir(orng) * .1f).normalized, OR(.8f, 1.7f), r < 3 ? 3 : orng.Next(3), Vector3.up, OR(0f, 180f));
                            nField++;
                        }
                        else if (r < 8 && floraShrub != null)
                        {
                            for (int j = 0; j < 3; j++)
                                FloraCross(weeds, floraShrub, p + new Vector3(OR(-.4f, .4f), 0f, OR(-.4f, .4f)), (Vector3.up + RandDir(orng) * .3f).normalized,
                                    OR(.6f, 1.1f), orng.Next(4) == 0 ? 3 : orng.Next(3), Vector3.up, OR(0f, 180f));
                            nShrub++;
                        }
                    }
                }
            Debug.Log($"[Hako Art] 梯田的草：田边芒草 {nEdge} 丛、田里 {nField} 丛、灌木 {nShrub} 丛");
        }

        // =====================================================================
        // 电线杆、路灯（不亮）、电线、剪断的进户线
        // =====================================================================
        static void UtilityPoles(MeshBatch mb)
        {
            float gp = GateX + 3.55f; // 大门东边那根
            var xs = new List<float>();
            for (float x = gp; x > OutXMin + 5f; x -= 36f) xs.Insert(0, x);
            for (float x = gp + 36f; x < OutXMax - 5f; x += 36f) xs.Add(x);
            const float hvY = 9.85f, lvY = 8.55f, telY = 7.15f;
            var hvOff = new[] { -.75f, 0f, .75f };
            var lvOff = new[] { -.28f, .28f };
            foreach (float x in xs)
                Pole(mb, new Vector3(x, 0f, PoleZ), Mathf.Abs(x - gp) < .1f);
            for (int i = 0; i < xs.Count - 1; i++)
            {
                var a = new Vector3(xs[i], 0f, PoleZ);
                var b = new Vector3(xs[i + 1], 0f, PoleZ);
                foreach (float o in hvOff) Wire(mb, a + new Vector3(0, hvY + .12f, o), b + new Vector3(0, hvY + .12f, o), .55f, .0075f);
                foreach (float o in lvOff) Wire(mb, a + new Vector3(0, lvY + .08f, o), b + new Vector3(0, lvY + .08f, o), .7f, .009f);
                Wire(mb, a + new Vector3(0, telY, -.2f), b + new Vector3(0, telY, -.2f), .95f, .013f);
            }
            // 拉到房子的进户线：从低压横担往院子里拉，在墙头附近断了——杆子这头搭在墙头的瓦上，断头垂在墙里侧
            float fh = Levels[GFence].wallTop;
            var arm = new Vector3(gp, lvY + .08f, PoleZ + .28f);
            var over = new Vector3(gp - .6f, fh + .23f, OZ0 + .02f);
            var pts = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                var p = Vector3.Lerp(arm, over, t);
                p.y -= .35f * Mathf.Sin(Mathf.PI * t) * (1f - t * .5f);
                pts.Add(p);
            }
            pts.Add(over + new Vector3(-.03f, -.06f, .14f));
            pts.Add(over + new Vector3(-.05f, -.45f, .2f));
            pts.Add(over + new Vector3(-.02f, -.95f, .19f));
            pts.Add(over + new Vector3(.03f, -1.22f, .17f));
            var rad = new float[pts.Count];
            for (int i = 0; i < rad.Length; i++) rad[i] = .007f;
            Limb(mb, D.Cable, pts, rad, 5);
            ShotOut("cut_drop", new Vector3(over.x - 1.5f, 1.75f, OZ0 + 2.7f), over + new Vector3(0f, -.5f, .15f));
            ShotOut("pole_top", new Vector3(gp - 3.4f, 8.2f, PoleZ - 4.6f), new Vector3(gp, 8.7f, PoleZ));
        }

        /// <summary>水泥电线杆（10.5 m）。gate = 大门边那根：多一个变压器、一盏不亮的路灯。</summary>
        static void Pole(MeshBatch mb, Vector3 b, bool gate)
        {
            const float h = 10.6f;
            mb.Cyl(D.ConcretePole, b - Vector3.up * .3f, .17f, .105f, h + .3f, 10, true);
            // 脚钉：2 m 以上左右交替
            for (float y = 1.9f; y < h - .9f; y += .45f)
            {
                float s = Mathf.Repeat(y, .9f) < .45f ? 1f : -1f;
                float r = Mathf.Lerp(.17f, .105f, y / h);
                mb.Cyl(D.Iron, b + new Vector3(s * r, y, 0f), .011f, .011f, .2f, 4, false, Quaternion.Euler(0, 0, -s * 90f));
            }
            // 高压横担（垂直于线路，沿 z）、瓷瓶
            var top = b + Vector3.up * 9.85f;
            mb.Box(D.Iron, top, new Vector3(.08f, .08f, 1.8f));
            foreach (float o in new[] { -.75f, 0f, .75f })
            {
                var at = top + new Vector3(0f, .04f, o);
                if (Mathf.Abs(o) < .01f) at += new Vector3(.11f, 0f, 0f);
                mb.Cyl(D.CeramicWhite, at, .045f, .035f, .11f, 8, true);
            }
            // 低压横担
            var lv = b + Vector3.up * 8.55f;
            mb.Box(D.Iron, lv, new Vector3(.06f, .06f, .9f));
            foreach (float o in new[] { -.28f, .28f }) mb.Cyl(D.CeramicWhite, lv + new Vector3(0f, .03f, o), .03f, .025f, .07f, 6, true);
            // 电话线的吊架
            mb.Box(D.Iron, b + new Vector3(0f, 7.15f, -.12f), new Vector3(.05f, .05f, .25f));
            // 杆号牌（铁皮）
            mb.Box(D.Iron, b + new Vector3(0f, 2.3f, -.17f), new Vector3(.12f, .3f, .012f));
            if (!gate) return;
            // 柱上变压器：灰色铁罐，两根铁箍
            var tc = b + new Vector3(.38f, 7.5f, 0f);
            mb.Cyl(D.Iron, tc, .21f, .21f, .62f, 12, true);
            mb.Cyl(D.Iron, tc + Vector3.up * .62f, .22f, .12f, .08f, 12, true);
            foreach (float y in new[] { .14f, .5f }) mb.Box(D.Iron, b + new Vector3(.19f, 7.5f + y, 0f), new Vector3(.38f, .04f, .06f));
            // 路灯：一根弯管从杆子往路中间伸，灯头是长方形的荧光灯罩（脏的乳白灯罩，不亮）
            var a0 = b + new Vector3(0f, 6.1f, -.12f);
            var arm = new List<Vector3> { a0, a0 + new Vector3(0f, .25f, -.45f), a0 + new Vector3(0f, .42f, -1.05f), a0 + new Vector3(0f, .46f, -1.45f) };
            Limb(mb, D.Iron, arm, new[] { .028f, .026f, .024f, .022f }, 6);
            var head = a0 + new Vector3(0f, .4f, -1.75f);
            mb.Box(D.Iron, head, new Vector3(.2f, .09f, .62f), Quaternion.Euler(-8f, 0f, 0f));
            mb.Box(D.LampGlass, head + new Vector3(0f, -.065f, 0f), new Vector3(.16f, .05f, .56f), Quaternion.Euler(-8f, 0f, 0f));
            mb.Box(D.Iron, head + new Vector3(0f, .07f, .2f), new Vector3(.05f, .05f, .05f)); // 光控开关
            ShotOut("lamp", new Vector3(b.x - 2.4f, 1.7f, b.z - 3.9f), head);
        }

        /// <summary>两点之间下垂的一根线（悬链线近似成抛物线）。</summary>
        static void Wire(MeshBatch mb, Vector3 a, Vector3 b, float sag, float r)
        {
            const int segs = 14;
            var pts = new Vector3[segs + 1];
            var rad = new float[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                pts[i] = Vector3.Lerp(a, b, t) - Vector3.up * (4f * sag * t * (1f - t));
                rad[i] = r;
            }
            Limb(mb, D.Cable, pts, rad, 4);
        }
    }
}
