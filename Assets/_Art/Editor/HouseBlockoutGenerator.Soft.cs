using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第四轮（2026-11-20）：软的布料、车床车出来的器物、倒角的木件。
    /// 用户的截图：被褥、坐垫、毛巾、床、洗衣机都是几块方块 / 圆柱拼的，没有纹理、没有褶皱。这里的网格都是算出来的：
    /// <list type="bullet">
    /// <item>Puff：塞了棉花的东西（座布団、敷布団、床垫、枕头、叠好的被子）——四边圆鼓、中间微微隆起、缝线一圈小褶，
    ///   可以压出凹坑（枕头上的头印）、中间塌下去（床垫）、一边被压扁。</item>
    /// <item>EmitCloth：有厚度的布（正面 + 背面 + 一圈圆的布边）。HangCloth 挂着的布（衣服、暖帘、晾的床单），
    ///   TowelOnBar 搭在杆上的毛巾，DustSheet 的厚被子模式（SheetSpec.thick）。</item>
    /// <item>Lathe：按剖面车出来的回转体（碗、锅、铁壶）。BevelBox：倒了角的方块（桌面、桌腿边上有一道高光）。Tube：管子、绳子、滚边。</item>
    /// </list>
    /// 坐标都是 Kit 当前的局部坐标（跟着家具搬家、放大）。UV 一律以米为单位（材质 Tiling = 1 / 贴图对应的米数），
    /// 圆边、垂下来的部分按展开的长度算，贴图不会被拉长。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        static float Pn(float x, float y) => Mathf.PerlinNoise(x, y) - .5f;

        // =====================================================================
        // 网格工具
        // =====================================================================
        /// <summary>网格上每个点的法线：cross(dv, du)（u 沿 +x、v 沿 +z 时朝上）；flip 反过来。</summary>
        static Vector3[,] GridNormals(Vector3[,] P, bool flip = false)
        {
            int nu = P.GetLength(0) - 1, nv = P.GetLength(1) - 1;
            var N = new Vector3[nu + 1, nv + 1];
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    var du = P[Mathf.Min(i + 1, nu), j] - P[Mathf.Max(i - 1, 0), j];
                    var dv = P[i, Mathf.Min(j + 1, nv)] - P[i, Mathf.Max(j - 1, 0)];
                    var n = Vector3.Cross(dv, du);
                    if (flip) n = -n;
                    N[i, j] = n.sqrMagnitude > 1e-14f ? n.normalized : (flip ? Vector3.down : Vector3.up);
                }
            return N;
        }

        /// <summary>按网格画一面（逐顶点法线，绕序跟着法线走）；flipNormals = 用反过来的法线（布的背面）。</summary>
        static void EmitGrid(MeshBatch mb, Material m, Vector3[,] P, Vector3[,] N, Vector2[,] U, bool flipNormals = false)
        {
            if (m == null || mb.Mute) return;
            int nu = P.GetLength(0) - 1, nv = P.GetLength(1) - 1;
            float s = flipNormals ? -1f : 1f;
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    Vector3 a = P[i, j], b = P[i + 1, j], c = P[i + 1, j + 1], d = P[i, j + 1];
                    if ((c - a).sqrMagnitude < 1e-12f && (d - b).sqrMagnitude < 1e-12f) continue;
                    mb.TriS(m, a, b, c, N[i, j] * s, N[i + 1, j] * s, N[i + 1, j + 1] * s, U[i, j], U[i + 1, j], U[i + 1, j + 1]);
                    mb.TriS(m, a, c, d, N[i, j] * s, N[i + 1, j + 1] * s, N[i, j + 1] * s, U[i, j], U[i + 1, j + 1], U[i, j + 1]);
                }
        }

        /// <summary>
        /// 有厚度的布：P 是布的中面，正面（法线 cross(dv, du) 那一侧）用 front、背面用 back，四条边上一圈圆的布边。
        /// puff（可空）：正面每个点往外鼓的倍数（被子的棉花鼓包）。
        /// </summary>
        static void EmitCloth(MeshBatch mb, Material front, Material back, Vector3[,] P, Vector2[,] U, float thick, float[,] puff = null)
        {
            if (mb.Mute) return;
            int nu = P.GetLength(0) - 1, nv = P.GetLength(1) - 1;
            var N = GridNormals(P);
            var A = new Vector3[nu + 1, nv + 1];
            var B = new Vector3[nu + 1, nv + 1];
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    float t = thick * .5f;
                    A[i, j] = P[i, j] + N[i, j] * t * (puff == null ? 1f : puff[i, j]);
                    B[i, j] = P[i, j] - N[i, j] * t;
                }
            EmitGrid(mb, front, A, puff == null ? N : GridNormals(A), U);
            EmitGrid(mb, back ?? front, B, N, U, true);
            if (thick < 1e-4f) return;
            // 布边：外面 → 往外鼓的中线 → 里面，三排点
            void Rim(List<(int i, int j, int ii, int ij)> run)
            {
                int n = run.Count;
                var G = new Vector3[3, n];
                var GN = new Vector3[3, n];
                var GU = new Vector2[3, n];
                float along = 0f;
                for (int k = 0; k < n; k++)
                {
                    var (i, j, ii, ij) = run[k];
                    var p = P[i, j];
                    var o = p - P[Mathf.Clamp(i + ii, 0, nu), Mathf.Clamp(j + ij, 0, nv)];
                    o -= Vector3.Dot(o, N[i, j]) * N[i, j];
                    o = o.sqrMagnitude > 1e-12f ? o.normalized : Vector3.zero;
                    if (k > 0) along += (p - P[run[k - 1].i, run[k - 1].j]).magnitude;
                    float r = thick * .5f * (puff == null ? 1f : puff[i, j]);
                    G[0, k] = A[i, j]; G[1, k] = p + o * r * .8f + N[i, j] * (r - thick * .5f) * .5f; G[2, k] = B[i, j];
                    GN[0, k] = (N[i, j] + o).normalized; GN[1, k] = o.sqrMagnitude > 0f ? o : N[i, j]; GN[2, k] = (o - N[i, j]).normalized;
                    GU[0, k] = new Vector2(along, 0f); GU[1, k] = new Vector2(along, thick * .8f); GU[2, k] = new Vector2(along, thick * 1.6f);
                }
                EmitGrid(mb, front, G, GN, GU);
            }
            var e = new List<(int, int, int, int)>();
            for (int i = 0; i <= nu; i++) e.Add((i, 0, 0, 1));
            Rim(e); e.Clear();
            for (int i = 0; i <= nu; i++) e.Add((i, nv, 0, -1));
            Rim(e); e.Clear();
            for (int j = 0; j <= nv; j++) e.Add((0, j, 1, 0));
            Rim(e); e.Clear();
            for (int j = 0; j <= nv; j++) e.Add((nu, j, -1, 0));
            Rim(e);
        }

        /// <summary>沿一串点的圆管（绳子、滚边、水管）。closed = 首尾相接。</summary>
        static void Tube(MeshBatch mb, Material m, IList<Vector3> pts, float r, int seg = 6, bool closed = false)
        {
            int n = pts.Count;
            if (n < 2 || m == null || mb.Mute) return;
            var T = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 a = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(i - 1, 0)];
                Vector3 b = closed ? pts[(i + 1) % n] : pts[Mathf.Min(i + 1, n - 1)];
                var t = b - a;
                T[i] = t.sqrMagnitude > 1e-14f ? t.normalized : (i > 0 ? T[i - 1] : Vector3.right);
            }
            var Nn = new Vector3[n];
            var hint = Mathf.Abs(Vector3.Dot(T[0], Vector3.up)) > .9f ? Vector3.right : Vector3.up;
            Nn[0] = Vector3.Cross(T[0], hint).normalized;
            for (int i = 1; i < n; i++)
            {
                var q = Nn[i - 1] - Vector3.Dot(Nn[i - 1], T[i]) * T[i];
                Nn[i] = q.sqrMagnitude > 1e-12f ? q.normalized : Nn[i - 1];
            }
            int rings = closed ? n + 1 : n;
            var G = new Vector3[rings, seg + 1];
            var GN = new Vector3[rings, seg + 1];
            var GU = new Vector2[rings, seg + 1];
            float along = 0f;
            for (int k = 0; k < rings; k++)
            {
                int i = k % n;
                if (k > 0) along += (pts[i] - pts[(k - 1) % n]).magnitude;
                var bn = Vector3.Cross(T[i], Nn[i]);
                for (int s = 0; s <= seg; s++)
                {
                    float a = 2f * Mathf.PI * s / seg;
                    var d = Mathf.Cos(a) * Nn[i] + Mathf.Sin(a) * bn;
                    G[k, s] = pts[i] + d * r;
                    GN[k, s] = d;
                    GU[k, s] = new Vector2(along, a * r);
                }
            }
            EmitGrid(mb, m, G, GN, GU);
        }

        /// <summary>
        /// 回转体：prof 是 (半径, 高度) 的剖面，从外面往上、过口沿、再往里往下走（法线 = (dy, -dr)）。
        /// 同一个点写两次 = 那里是一道硬边。at 是底面中心，rot 转整个东西。
        /// </summary>
        static void Lathe(MeshBatch mb, Material m, Vector3 at, Quaternion rot, IList<Vector2> prof, int seg = 20)
        {
            int n = prof.Count;
            if (n < 2 || m == null || mb.Mute) return;
            var pn = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var acc = Vector2.zero;
                if (i > 0) { var d = prof[i] - prof[i - 1]; if (d.sqrMagnitude > 1e-12f) acc += new Vector2(d.y, -d.x).normalized; }
                if (i < n - 1) { var d = prof[i + 1] - prof[i]; if (d.sqrMagnitude > 1e-12f) acc += new Vector2(d.y, -d.x).normalized; }
                pn[i] = acc.sqrMagnitude > 1e-12f ? acc.normalized : Vector2.up;
            }
            var G = new Vector3[seg + 1, n];
            var GN = new Vector3[seg + 1, n];
            var GU = new Vector2[seg + 1, n];
            for (int s = 0; s <= seg; s++)
            {
                float a = 2f * Mathf.PI * s / seg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float arc = 0f;
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) arc += (prof[i] - prof[i - 1]).magnitude;
                    var p = prof[i];
                    G[s, i] = at + rot * new Vector3(ca * p.x, p.y, sa * p.x);
                    GN[s, i] = rot * new Vector3(ca * pn[i].x, pn[i].y, sa * pn[i].x);
                    GU[s, i] = new Vector2(a * Mathf.Max(p.x, .02f), arc);
                }
            }
            EmitGrid(mb, m, G, GN, GU);
        }

        static readonly float[] BS1 = { -1, 1, 1, -1 }, BS2 = { -1, -1, 1, 1 };

        /// <summary>倒角方块：六个面 + 十二条 45° 倒角 + 八个角，平面着色（倒角上有一道高光，看着是一块真木头）。UV 同 MeshBatch.Box。</summary>
        static void BevelBox(MeshBatch mb, Material m, Vector3 c, Vector3 size, float bev, Quaternion? rot = null)
        {
            if (m == null || mb.Mute) return;
            var q = rot ?? Quaternion.identity;
            var h = size * .5f;
            bev = Mathf.Clamp(bev, 0f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .8f);
            var hi = h - Vector3.one * bev;
            Vector2 UV(Vector3 l, int ax)
            {
                int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                float cu = l[a1] + c[a1], cv = l[a2] + c[a2];
                return size[a1] >= size[a2] ? new Vector2(cu, cv) : new Vector2(cv, cu);
            }
            var pts = new Vector3[4];
            var uvs = new Vector2[4];
            for (int a = 0; a < 3; a++)
            {
                int b = (a + 1) % 3, e = (a + 2) % 3;
                for (int sa = -1; sa <= 1; sa += 2)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        var l = Vector3.zero;
                        l[a] = sa * h[a]; l[b] = BS1[k] * hi[b]; l[e] = BS2[k] * hi[e];
                        pts[k] = c + q * l; uvs[k] = UV(l, a);
                    }
                    var n = Vector3.zero; n[a] = sa;
                    mb.Poly(m, pts, uvs, q * n);
                }
            }
            if (bev <= 1e-5f) return;
            for (int a = 0; a < 3; a++)
                for (int b = a + 1; b < 3; b++)
                {
                    int e = 3 - a - b;
                    for (int sa = -1; sa <= 1; sa += 2)
                        for (int sb = -1; sb <= 1; sb += 2)
                        {
                            var l0 = Vector3.zero; l0[a] = sa * h[a]; l0[b] = sb * hi[b]; l0[e] = -hi[e];
                            var l1 = Vector3.zero; l1[a] = sa * hi[a]; l1[b] = sb * h[b]; l1[e] = -hi[e];
                            var l2 = l1; l2[e] = hi[e];
                            var l3 = l0; l3[e] = hi[e];
                            pts[0] = c + q * l0; pts[1] = c + q * l1; pts[2] = c + q * l2; pts[3] = c + q * l3;
                            int ax = size[a] <= size[b] ? a : b;
                            uvs[0] = UV(l0, ax); uvs[1] = UV(l1, ax); uvs[2] = UV(l2, ax); uvs[3] = UV(l3, ax);
                            var n = Vector3.zero; n[a] = sa; n[b] = sb;
                            mb.Poly(m, pts, uvs, q * n.normalized);
                        }
                }
            var tri = new Vector3[3];
            var tuv = new Vector2[3];
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var l0 = new Vector3(sx * h.x, sy * hi.y, sz * hi.z);
                        var l1 = new Vector3(sx * hi.x, sy * h.y, sz * hi.z);
                        var l2 = new Vector3(sx * hi.x, sy * hi.y, sz * h.z);
                        tri[0] = c + q * l0; tri[1] = c + q * l1; tri[2] = c + q * l2;
                        tuv[0] = UV(l0, 1); tuv[1] = UV(l1, 1); tuv[2] = UV(l2, 1);
                        mb.Poly(m, tri, tuv, q * new Vector3(sx, sy, sz).normalized);
                    }
        }

        // =====================================================================
        // Puff：塞了棉花的东西
        // =====================================================================
        sealed class PuffSpec
        {
            public float w = .52f, d = .55f;            // 占地（x、z）
            public float hTop = .045f, hBot = .015f;     // 缝线以上 / 以下的厚度；缝线在 y = hBot，底面贴着 y = 0
            public float corner = .06f;                 // 四角的圆角半径
            public float rampTop = .08f, rampBot = .025f; // 从缝线鼓到满厚度要往里走多远（越大越圆）
            public float dome = .25f;                   // 中间比四周再鼓多少（× hTop）
            public float sag;                           // 中间塌下去多少（米；床垫）
            public float sinkX;                         // +x 那一边被压扁多少（米）
            public float wrinkle = .003f;               // 顶面的皱
            public float pucker = .0025f;               // 缝线一圈的小褶
            public float dent, dentR = .08f;            // 压出来的凹坑（枕头上的头印）
            public Vector2 dentAt;
            public float tuft;                          // 中间缝住的一个凹点（座布団的綴じ）
            public float crumple;                       // 揉皱的布（一堆脏衣服）：细密的折痕，米
            public float pipe;                          // 滚边（缝线一圈的圆条）半径，0 = 没有
            public Material pipeMat;
            public float seed;
            public int nx = 20, nz = 20;
        }

        /// <summary>
        /// 一个鼓起来的软垫。at = 底面中心，rot 转整个垫子。顶面、底面在缝线处接上（缝线处两面的法线都是水平朝外，圆的）。
        /// 网格线在四边加密（-cos 分布），圆角用“方 → 四分之一圆”的映射，不会有挤成一点的格子。
        /// </summary>
        static void Puff(MeshBatch mb, Material top, Material bottom, Vector3 at, Quaternion rot, PuffSpec s)
        {
            if (mb.Mute) return;
            int nx = s.nx, nz = s.nz;
            var PT = new Vector3[nx + 1, nz + 1];
            var PB = new Vector3[nx + 1, nz + 1];
            var UT = new Vector2[nx + 1, nz + 1];
            var UB = new Vector2[nx + 1, nz + 1];
            float ax = s.w / 2f, az = s.d / 2f;
            float rc = Mathf.Min(s.corner, Mathf.Min(ax, az) * .95f), cx = ax - rc, cz = az - rc;
            float per = 2f * (ax + az);
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                {
                    float u = -Mathf.Cos(Mathf.PI * i / nx), v = -Mathf.Cos(Mathf.PI * j / nz);
                    float x = u * ax, z = v * az;
                    float dx = Mathf.Abs(x) - cx, dz = Mathf.Abs(z) - cz, edge;
                    Vector2 o;
                    if (dx > 0f && dz > 0f)
                    {
                        float l = Mathf.Sqrt(dx * dx + dz * dz), f = Mathf.Max(dx, dz) / l;
                        dx *= f; dz *= f;
                        x = Mathf.Sign(x) * (cx + dx); z = Mathf.Sign(z) * (cz + dz);
                        float r = Mathf.Sqrt(dx * dx + dz * dz);
                        edge = Mathf.Max(0f, rc - r);
                        o = new Vector2(Mathf.Sign(x) * dx, Mathf.Sign(z) * dz) / Mathf.Max(r, 1e-6f);
                    }
                    else
                    {
                        float ex = ax - Mathf.Abs(x), ez = az - Mathf.Abs(z);
                        if (ex < ez) { edge = ex; o = new Vector2(Mathf.Sign(x), 0f); }
                        else { edge = ez; o = new Vector2(0f, Mathf.Sign(z)); }
                    }
                    if (i == 0 || i == nx || j == 0 || j == nz) edge = 0f;
                    float mid = (1f - u * u) * (1f - v * v);
                    float tt = Mathf.Clamp01(edge / Mathf.Max(1e-4f, s.rampTop)), prof = Mathf.Sqrt(1f - (1f - tt) * (1f - tt));
                    float tb = Mathf.Clamp01(edge / Mathf.Max(1e-4f, s.rampBot)), profB = Mathf.Sqrt(1f - (1f - tb) * (1f - tb));
                    float sag = s.sag * mid, side = s.sinkX * (u * .5f + .5f);
                    float ang = Mathf.Atan2(z / az, x / ax) * per / (2f * Mathf.PI);
                    float pleat = s.pucker * Mathf.Sin(ang * 2f * Mathf.PI / .05f + s.seed * 3.1f) * 4f * tt * (1f - tt);
                    float wr = s.wrinkle * (1.4f * Pn(x * 6f + s.seed * 7.3f, z * 6f + 11f) + .6f * Pn(x * 15f - s.seed * 3.7f, z * 15f + 3f));
                    if (s.crumple > 0f) // 脊状噪声：一道道尖的折痕（两层，粗的 4 cm、细的 2 cm）
                        wr += s.crumple * ((1f - 4f * Mathf.Abs(Pn(x * 26f + s.seed * 5.3f, z * 26f - s.seed * 2.1f))) * .65f
                                           + (1f - 4f * Mathf.Abs(Pn(x * 55f - s.seed * 1.7f, z * 55f + s.seed * 4.4f))) * .35f);
                    float dent = 0f;
                    if (s.dent > 0f)
                    {
                        var q = new Vector2(x, z) - s.dentAt;
                        dent = s.dent * Mathf.Exp(-q.sqrMagnitude / (s.dentR * s.dentR));
                    }
                    if (s.tuft > 0f) dent += s.tuft * Mathf.Exp(-(x * x + z * z) / (.02f * .02f));
                    float yt = s.hBot + (s.hTop * (1f + s.dome * mid) - side + pleat + wr - dent) * prof - sag;
                    float yb = s.hBot * (1f - profB) - sag * .85f;
                    PT[i, j] = at + rot * new Vector3(x, yt, z);
                    PB[i, j] = at + rot * new Vector3(x, yb, z);
                    float offT = s.hTop * (1f - prof), offB = s.hBot * (1f - profB);
                    UT[i, j] = new Vector2(x + o.x * offT, z + o.y * offT) + new Vector2(s.seed * .37f, s.seed * .61f);
                    UB[i, j] = new Vector2(x + o.x * offB, -(z + o.y * offB));
                }
            EmitGrid(mb, top, PT, GridNormals(PT), UT);
            EmitGrid(mb, bottom ?? top, PB, GridNormals(PB, true), UB);
            if (s.pipe > 0f)
            {
                var loop = new List<Vector3>();
                for (int i = 0; i < nx; i++) loop.Add(PT[i, 0]);
                for (int j = 0; j < nz; j++) loop.Add(PT[nx, j]);
                for (int i = nx; i > 0; i--) loop.Add(PT[i, nz]);
                for (int j = nz; j > 0; j--) loop.Add(PT[0, j]);
                Tube(mb, s.pipeMat ?? top, loop, s.pipe, 5, true);
            }
        }

        /// <summary>垫子顶面在 (x, z) 处的高度（摆上面的东西用；不算皱和褶）。</summary>
        static float PuffTop(PuffSpec s, float x, float z)
        {
            float u = Mathf.Clamp(x / (s.w / 2f), -1f, 1f), v = Mathf.Clamp(z / (s.d / 2f), -1f, 1f);
            float edge = Mathf.Min(s.w / 2f - Mathf.Abs(x), s.d / 2f - Mathf.Abs(z));
            float tt = Mathf.Clamp01(edge / Mathf.Max(1e-4f, s.rampTop)), prof = Mathf.Sqrt(1f - (1f - tt) * (1f - tt));
            float mid = (1f - u * u) * (1f - v * v);
            float dent = 0f;
            if (s.dent > 0f) dent = s.dent * Mathf.Exp(-(new Vector2(x, z) - s.dentAt).sqrMagnitude / (s.dentR * s.dentR));
            if (s.tuft > 0f) dent += s.tuft * Mathf.Exp(-(x * x + z * z) / (.02f * .02f));
            return s.hBot + (s.hTop * (1f + s.dome * mid) - s.sinkX * (u * .5f + .5f) - dent) * prof - s.sag * mid;
        }

        // =====================================================================
        // 挂着的布
        // =====================================================================
        sealed class HangSpec
        {
            public float width = .3f, length = .4f;
            public float thick = .004f;
            public float foldAmp = .012f, foldLen = .08f; // 竖褶的深浅、宽窄（越往下越深）
            public float flare;                         // 下摆往两边张开（× 宽度）
            public float sway, twist;                   // 下摆往前飘多少（米）、拧多少度
            public float hem = .012f;                   // 下摆高低不齐
            public int pins;                            // 顶边用几个夹子夹着（夹子之间往下垂）
            public float pinSag = .02f;
            public float seed;
        }

        /// <summary>
        /// 顶边挂在一条直线上往下垂的布。局部：顶边从 (0,0,0) 沿 +x 到 (width,0,0)，往 -y 垂，正面朝 +z。
        /// at / rot 是顶边左端在 Kit 局部坐标里的位置和转向。
        /// </summary>
        /// <summary>
        /// 褶的深浅整体再放大的倍数（第四轮审图：1.0 时远看还是一块平板；褶要深到手电从正面照也看得出明暗）。
        /// </summary>
        const float FoldBoost = 1.7f;

        static void HangCloth(MeshBatch mb, Material front, Material back, Vector3 at, Quaternion rot, HangSpec s)
        {
            if (mb.Mute) return;
            int nu = Mathf.Max(4, Mathf.CeilToInt(s.width / .016f)), nv = Mathf.Max(4, Mathf.CeilToInt(s.length / .025f));
            var P = new Vector3[nu + 1, nv + 1];
            var U = new Vector2[nu + 1, nv + 1];
            float A = s.foldAmp * FoldBoost;
            float k1 = 2f * Mathf.PI / Mathf.Max(.02f, s.foldLen), k2 = k1 / 1.7f, k3 = k1 * 2.3f, p1 = s.seed * 5.1f, p2 = s.seed * 2.3f, p3 = s.seed * 7.7f;
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    float x = s.width * i / nu, f = (float)j / nv;
                    float g = .25f + .75f * Mathf.Pow(f, .8f);
                    // 竖褶：三个波叠在一起，往下褶的位置还慢慢偏一点（不是笔直的一道道）
                    float fold = A * g * (Mathf.Sin(k1 * x + p1 + f * .9f) * .55f + Mathf.Sin(k2 * x + p2) * .3f + Mathf.Sin(k3 * x + p3 - f * 1.4f) * .15f);
                    // 下摆高低不齐；褶深的地方下摆往上收一点
                    float hem = s.hem * (Mathf.Sin(x * 23f + s.seed * 4f) * .7f + Mathf.Sin(x * 61f - s.seed) * .3f) * f - Mathf.Abs(fold) * .4f * f * f;
                    float t = s.length * f + hem;
                    // 两条布边往后卷一点（离边 2.5 cm 以内），中间整片微微鼓出来
                    float e = Mathf.Min(x, s.width - x), curl = 1f - Mathf.Clamp01(e / .025f);
                    float belly = A * .45f * g * Mathf.Sin(Mathf.PI * x / s.width);
                    float xc = (x - s.width / 2f) * (1f + s.flare * f) + s.width / 2f;
                    float sag = 0f;
                    if (s.pins > 1) sag = s.pinSag * Mathf.Abs(Mathf.Sin(Mathf.PI * (s.pins - 1) * x / s.width)) * (1f - f * .5f);
                    var p = new Vector3(xc, -t - sag, fold + belly - curl * curl * A * 1.1f + s.sway * f * f);
                    if (s.twist != 0f) p = Quaternion.Euler(0f, s.twist * f * f, 0f) * (p - new Vector3(s.width / 2f, 0f, 0f)) + new Vector3(s.width / 2f, 0f, 0f);
                    P[i, j] = at + rot * p;
                    U[i, j] = new Vector2(x, -t) + new Vector2(s.seed * .53f, s.seed * .29f);
                }
            EmitCloth(mb, front, back, P, U, s.thick);
        }

        /// <summary>
        /// 搭在横杆上的毛巾：从背面下摆往上、绕过杆顶、再从正面垂下来，一整条（前后两片长短可以不一样，快掉下来的那条前长后短）。
        /// 局部：杆沿 +x，at 是毛巾左边缘在杆轴上的点；正面朝 +z。barR = 杆半径。
        /// </summary>
        static void TowelOnBar(MeshBatch mb, Material m, Material back, Vector3 at, Quaternion rot, float width, float frontLen, float backLen,
            float barR, float seed, float foldAmp = .009f, float thick = .006f, float skew = 0f)
        {
            if (mb.Mute) return;
            float R = barR + thick * .5f, wrap = Mathf.PI * R, total = backLen + wrap + frontLen;
            int nu = Mathf.Max(6, Mathf.CeilToInt(width / .014f)), nv = Mathf.Max(8, Mathf.CeilToInt(total / .018f));
            var P = new Vector3[nu + 1, nv + 1];
            var U = new Vector2[nu + 1, nv + 1];
            // 褶的宽窄跟着布宽走：毛巾两三道、床单五六道；深浅整体放大 FoldBoost
            float A = foldAmp * FoldBoost, lam = Mathf.Clamp(width * .4f, .09f, .25f), k1 = 2f * Mathf.PI / lam, k2 = k1 / 2.1f;
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    float x = width * i / nu, sg = total * j / nv;
                    Vector3 p;
                    float hang, side, len;
                    if (sg < backLen) { hang = backLen - sg; side = -1f; len = backLen; p = new Vector3(x, -hang, -R); }
                    else if (sg < backLen + wrap)
                    {
                        float th = (sg - backLen) / R;
                        hang = 0f; side = 0f; len = 1f;
                        p = new Vector3(x, Mathf.Sin(th) * R, -Mathf.Cos(th) * R);
                    }
                    else { hang = sg - backLen - wrap; side = 1f; len = frontLen; p = new Vector3(x, -hang, R); }
                    if (side != 0f)
                    {
                        float g = .2f + .8f * Mathf.Clamp01(hang / .25f);
                        float fold = A * g * (.6f * Mathf.Sin(x * k1 + seed * 3f + side + hang * 2.5f) + .4f * Mathf.Sin(x * k2 - seed * 2f + side * .7f));
                        // 布边往杆那边卷一点，中间鼓出来；越往下离杆越远一点
                        float e = Mathf.Min(x, width - x), curl = 1f - Mathf.Clamp01(e / .022f);
                        float belly = A * .45f * g * Mathf.Sin(Mathf.PI * x / width);
                        p.z += side * (Mathf.Abs(fold) * .6f + fold * .4f + belly - curl * curl * A * .9f) + side * hang * .03f;
                        // 快滑下来的那条（skew）：正面一头长一头短、背面反过来，下摆是斜的，整片往长的那头偏一点
                        float lean = skew * (x / width - .5f) * 2f;
                        p.y -= side * lean * hang * .35f;
                        p.x += skew * hang * .3f;
                        // 下摆不齐：两层波，褶深的地方往上收
                        float hw = Mathf.Clamp01(hang / Mathf.Max(.05f, len));
                        p.y += (Mathf.Abs(fold) * .5f - .009f * Mathf.Sin(x * 29f + seed) - .004f * Mathf.Sin(x * 73f - seed * 2f)) * hw * hw;
                    }
                    P[i, j] = at + rot * p;
                    U[i, j] = new Vector2(x + seed * .41f, sg);
                }
            EmitCloth(mb, m, back, P, U, thick);
        }

        // =====================================================================
        // 卷起来的东西
        // =====================================================================
        /// <summary>
        /// 卷起来的布 / 被子：截面是一圈螺旋（最外一层的布边在外面看得到一道台阶），压在地上的一面压扁；两头是平的螺旋面。
        /// 局部：轴沿 +x，at 是底下正中（贴地），len 长、r 半径。
        /// </summary>
        static void SoftRoll(MeshBatch mb, Material m, Material end, Vector3 at, Quaternion rot, float len, float r, float layer = .18f, float squash = .12f, float seed = 0f)
        {
            if (mb.Mute) return;
            const int seg = 24;
            int nx = Mathf.Max(4, Mathf.CeilToInt(len / .05f));
            var P = new Vector3[nx + 1, seg + 1];
            var U = new Vector2[nx + 1, seg + 1];
            float yc = r * (1f - squash);
            Vector3 Pt(float x, float th, float rr)
            {
                float y = yc + Mathf.Sin(th) * rr, z = Mathf.Cos(th) * rr;
                if (y < 0f) { z *= 1f + (-y) / Mathf.Max(rr, 1e-4f) * .6f; y = 0f; }
                return new Vector3(x, y, z);
            }
            float Rad(float x, float th)
            {
                float e = 2f * x / len;
                return r * (1f - layer * th / (2f * Mathf.PI)) * (1f - .07f * e * e * e * e) * (1f + .02f * Pn(x * 8f + seed, th * 2f));
            }
            for (int i = 0; i <= nx; i++)
                for (int k = 0; k <= seg; k++)
                {
                    float x = -len / 2f + len * i / nx, th = 2f * Mathf.PI * k / seg;
                    P[i, k] = at + rot * Pt(x, th, Rad(x, th));
                    U[i, k] = new Vector2(x, th * r);
                }
            EmitGrid(mb, m, P, GridNormals(P, true), U);
            // 最外一层的布边：一条台阶
            var S = new Vector3[nx + 1, 2];
            var SU = new Vector2[nx + 1, 2];
            for (int i = 0; i <= nx; i++)
            {
                S[i, 0] = P[i, 0];
                S[i, 1] = P[i, seg];
                SU[i, 0] = new Vector2(i * len / nx, 0f);
                SU[i, 1] = new Vector2(i * len / nx, r * layer);
            }
            var SN = new Vector3[nx + 1, 2];
            var stepN = rot * new Vector3(0f, -1f, 0f);
            for (int i = 0; i <= nx; i++) { SN[i, 0] = stepN; SN[i, 1] = stepN; }
            EmitGrid(mb, m, S, SN, SU);
            // 两头：螺旋面
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * len / 2f;
                var n = rot * new Vector3(sx, 0f, 0f);
                var c = at + rot * new Vector3(x, yc, 0f);
                for (int k = 0; k < seg; k++)
                {
                    float t0 = 2f * Mathf.PI * k / seg, t1 = 2f * Mathf.PI * (k + 1) / seg;
                    var a = P[sx < 0 ? 0 : nx, k];
                    var b = P[sx < 0 ? 0 : nx, k + 1];
                    mb.TriS(end ?? m, c, a, b, n, n, n, Vector2.zero, new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * r, new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * r);
                }
            }
        }

        // =====================================================================
        // 组合件：坐垫、叠好的布、被子、整套被褥
        // =====================================================================
        /// <summary>座布団：四边圆鼓，中间缝住一撮（綴じ糸，黑线打个叉）。at = 底面中心；squash = 压扁多少（米，摞起来的下面几块）。</summary>
        static void SoftZabuton(MeshBatch mb, Vector3 at, float yaw, Material m, float seed, float w = .52f, float d = .55f, float squash = 0f)
        {
            if (mb.Mute) return;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var s = new PuffSpec
            {
                w = w, d = d, hTop = .04f - squash, hBot = .027f, corner = .07f, rampTop = .09f, rampBot = .05f, dome = .22f,
                wrinkle = .0025f, pucker = .002f, tuft = .012f, seed = seed, nx = 16, nz = 16,
            };
            Puff(mb, m, m, at, rot, s);
            float y = PuffTop(s, 0f, 0f);
            foreach (float a in new[] { 45f, -45f })
                mb.Box(D.Black, at + rot * new Vector3(0f, y + .0005f, 0f), new Vector3(.026f, .003f, .005f), rot * Quaternion.Euler(0f, a, 0f));
        }

        /// <summary>叠好的布（衣服、毛巾、包袱布）：边角圆的一块软块，一边稍微塌一点。at = 底面中心，w × h × d。</summary>
        static void FoldedCloth(MeshBatch mb, Material m, Vector3 at, float yaw, float w, float h, float d, float seed)
        {
            if (mb.Mute) return;
            var s = new PuffSpec
            {
                w = w, d = d, hTop = h * .55f, hBot = h * .45f, corner = Mathf.Min(.03f, Mathf.Min(w, d) * .2f),
                rampTop = Mathf.Max(.008f, h * .45f), rampBot = Mathf.Max(.005f, h * .3f), dome = .04f,
                wrinkle = Mathf.Min(.004f, h * .06f), pucker = Mathf.Min(.0015f, h * .03f), sinkX = h * .08f, seed = seed,
                nx = Mathf.Clamp(Mathf.CeilToInt(w / .03f), 8, 20), nz = Mathf.Clamp(Mathf.CeilToInt(d / .03f), 8, 20),
            };
            Puff(mb, m, m, at, Quaternion.Euler(0f, yaw, 0f), s);
        }

        /// <summary>叠成三折的被子（押入れ里摞着的那种）：很厚、四边圆。at = 底面中心，t = 厚度。</summary>
        static void FoldedFuton(MeshBatch mb, Material m, Vector3 at, float yaw, float t, float seed, float w = 1.0f, float d = .66f)
        {
            if (mb.Mute) return;
            var s = new PuffSpec
            {
                w = w, d = d, hTop = t * .55f, hBot = t * .45f, corner = .05f, rampTop = Mathf.Min(.12f, t * .8f), rampBot = t * .5f,
                dome = .06f, wrinkle = .005f, pucker = .003f, sinkX = t * .06f, seed = seed, nx = 20, nz = 14,
            };
            Puff(mb, m, m, at, Quaternion.Euler(0f, yaw, 0f), s);
        }

        /// <summary>
        /// 被子 / 毛毯：盖在一块平顶（床垫、敷布団）上，四边各垂多长单独给（0 = 布边停在顶面上），有厚度、鼓包。
        /// s 是被子自己的坐标（at / rot 转到 Kit 局部）：顶面 x0..x1、z0..z1（z0 = 脚那头），floorY = 地面。
        /// </summary>
        static void Quilt(MeshBatch mb, Material top, Material under, Vector3 at, Quaternion rot, SheetSpec s,
            float dropL, float dropR, float dropFoot, float dropHead, float thick, int seed, float lumps = .35f)
        {
            if (mb.Mute) return;
            s.drop = s.backDrop = Mathf.Max(Mathf.Max(dropL, dropR), Mathf.Max(dropFoot, dropHead));
            var folds = new SheetFolds(new System.Random(seed), s);
            float arc = Mathf.Max(.005f, s.round) * Mathf.PI / 2f;
            var us = SheetAxis(s.x0 - dropL, s.x0, s.x1, s.x1 + dropR, arc, 0f, 0f, 0f);
            var vs = SheetAxis(s.z0 - dropFoot, s.z0, s.z1, s.z1 + dropHead, arc, 0f, 0f, 0f);
            int nu = us.Count - 1, nv = vs.Count - 1;
            var P = new Vector3[nu + 1, nv + 1];
            var U = new Vector2[nu + 1, nv + 1];
            var puff = new float[nu + 1, nv + 1];
            float o = Mathf.Abs(seed % 89) * .113f;
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    P[i, j] = at + rot * SheetPoint(s, folds, us[i], vs[j]);
                    U[i, j] = new Vector2(us[i] + o, vs[j] + o * .7f);
                    puff[i, j] = 1f + lumps * 2f * (Mathf.PerlinNoise(us[i] * 3.3f + o, vs[j] * 3.3f - o) - .5f);
                }
            EmitCloth(mb, top, under, P, U, thick, puff);
        }

        /// <summary>被子边折回来的一道圆棱：从 a 沿 rot 的 +x 走 len，两头圆的。</summary>
        static void FoldRoll(MeshBatch mb, Material m, Vector3 a, Quaternion rot, float len, float r, float seed)
        {
            if (mb.Mute) return;
            var pts = new List<Vector3>();
            int n = Mathf.Max(4, Mathf.CeilToInt(len / .05f));
            for (int i = 0; i <= n; i++)
            {
                float x = len * i / n;
                pts.Add(a + rot * new Vector3(x, .004f * Pn(x * 5f + seed, 1.3f), .008f * Pn(x * 4f - seed, 4.1f)));
            }
            Tube(mb, m, pts, r, 10);
            mb.Sphere(m, pts[0], Vector3.one * r, 10);
            mb.Sphere(m, pts[n], Vector3.one * r, 10);
        }

        /// <summary>
        /// 铺在榻榻米上的一套被褥（局部：头朝 +z，at = 敷布団底面中心）：敷布団（白床单）、掛布団（两边、脚那头垂到地上摊开，
        /// 头那头折回来一道白衿）、荞麦壳枕头（压出头印）。pulledBack = 被子掀到腰（人起来走了）。
        /// </summary>
        static void LaidFutonSoft(MeshBatch mb, Vector3 at, float yaw, Material quilt, Material sheet, Material pillow, int seed, bool pulledBack)
        {
            if (mb.Mute) return;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var ss = new PuffSpec
            {
                w = 1.0f, d = 2.0f, hTop = .05f, hBot = .035f, corner = .05f, rampTop = .1f, rampBot = .05f, dome = .12f,
                wrinkle = .004f, pucker = .003f, seed = Mathf.Abs(seed % 40) * .5f, nx = 20, nz = 36,
            };
            Puff(mb, sheet, sheet, at, rot, ss);
            float top = ss.hBot + ss.hTop * (1f + ss.dome);
            float z1 = pulledBack ? -.1f : .42f;
            const float thick = .055f;
            var qs = new SheetSpec { x0 = -.5f, x1 = .5f, z0 = -1f, z1 = z1, top = top + thick * .5f + .004f, round = .05f, cone = .05f, floorY = 0f };
            Quilt(mb, quilt, D.ClothWhite, at, rot, qs, .2f, .2f, .18f, 0f, thick, seed);
            FoldRoll(mb, pulledBack ? quilt : D.ClothWhite, at + rot * new Vector3(-.52f, qs.top, z1), rot, 1.04f, .032f, Mathf.Abs(seed % 50) * .1f);
            var ps = new PuffSpec
            {
                w = .5f, d = .3f, hTop = .05f, hBot = .045f, corner = .09f, rampTop = .12f, rampBot = .1f, dome = .35f,
                dent = .028f, dentAt = new Vector2(.02f, -.01f), dentR = .09f, wrinkle = .006f, pucker = .004f,
                seed = Mathf.Abs(seed % 30) * .7f, nx = 14, nz = 10,
            };
            const float pz = .72f;
            Puff(mb, pillow, pillow, at + rot * new Vector3(.02f, PuffTop(ss, 0f, pz) - .01f, pz), rot * Quaternion.Euler(0f, 4f, 0f), ps);
        }
    }
}
