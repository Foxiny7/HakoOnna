using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 按材质合并的网格构建器。顶点经 M 变换到世界坐标；UV 以米为单位（贴图平铺写在材质上）。
    /// 生成的 Mesh 记录在 Created 里，最后统一存成一个资源文件，避免把网格数据塞进场景文件。
    /// </summary>
    internal sealed class MeshBatch
    {
        public static readonly List<Mesh> Created = new List<Mesh>();

        sealed class Part
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
        }

        readonly Dictionary<Material, Part> parts = new Dictionary<Material, Part>();
        readonly List<Material> order = new List<Material>();
        public Matrix4x4 M = Matrix4x4.identity;

        /// <summary>Track = true 时记录写入顶点的世界包围盒（家具搬家时量尺寸用）。</summary>
        public bool Track;
        /// <summary>Mute = true 时不写网格（搬家后放不下而撤掉的陈设）。</summary>
        public bool Mute;
        public bool HasBounds;
        public Vector3 Min, Max;

        void Grow(Vector3 p)
        {
            if (!HasBounds) { Min = Max = p; HasBounds = true; return; }
            Min = Vector3.Min(Min, p);
            Max = Vector3.Max(Max, p);
        }

        Part Get(Material m)
        {
            if (!parts.TryGetValue(m, out var p)) { p = new Part(); parts[m] = p; order.Add(m); }
            return p;
        }

        /// <summary>凸多边形，pts 为局部坐标，normal 为期望朝向（自动修正绕序）。</summary>
        public void Poly(Material m, IList<Vector3> pts, IList<Vector2> uvs, Vector3 normal)
        {
            if (m == null || pts.Count < 3 || Mute) return;
            var p = Get(m);
            int start = p.v.Count;
            var wn = M.MultiplyVector(normal).normalized;
            var w = new Vector3[pts.Count];
            for (int i = 0; i < w.Length; i++) w[i] = M.MultiplyPoint3x4(pts[i]);
            if (Track) foreach (var q in w) Grow(q);
            Vector3 nw = Vector3.zero;
            for (int i = 0; i < w.Length; i++)
            {
                var a = w[i]; var b = w[(i + 1) % w.Length];
                nw.x += (a.y - b.y) * (a.z + b.z);
                nw.y += (a.z - b.z) * (a.x + b.x);
                nw.z += (a.x - b.x) * (a.y + b.y);
            }
            bool flip = Vector3.Dot(nw, wn) < 0;
            for (int i = 0; i < w.Length; i++) { p.v.Add(w[i]); p.n.Add(wn); p.uv.Add(uvs[i]); }
            for (int i = 1; i < w.Length - 1; i++)
            {
                p.t.Add(start);
                p.t.Add(start + (flip ? i + 1 : i));
                p.t.Add(start + (flip ? i : i + 1));
            }
        }

        /// <summary>带逐顶点法线的三角形（圆柱、球体用）。</summary>
        public void TriS(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc,
            Vector2 ua, Vector2 ub, Vector2 uc)
        {
            if (Mute) return;
            var p = Get(m);
            int s = p.v.Count;
            Vector3 wa = M.MultiplyPoint3x4(a), wb = M.MultiplyPoint3x4(b), wc = M.MultiplyPoint3x4(c);
            if (Track) { Grow(wa); Grow(wb); Grow(wc); }
            Vector3 n1 = M.MultiplyVector(na).normalized, n2 = M.MultiplyVector(nb).normalized, n3 = M.MultiplyVector(nc).normalized;
            p.v.Add(wa); p.v.Add(wb); p.v.Add(wc);
            p.n.Add(n1); p.n.Add(n2); p.n.Add(n3);
            p.uv.Add(ua); p.uv.Add(ub); p.uv.Add(uc);
            bool ok = Vector3.Dot(Vector3.Cross(wb - wa, wc - wa), n1 + n2 + n3) >= 0;
            p.t.Add(s); p.t.Add(ok ? s + 1 : s + 2); p.t.Add(ok ? s + 2 : s + 1);
        }

        static readonly float[] S1 = { -1, 1, 1, -1 }, S2 = { -1, -1, 1, 1 };

        /// <summary>
        /// 立方体，c 为中心，size 为尺寸，rot 为局部旋转。每个面的 u 沿较长边（木纹方向）；
        /// uAxis（0=x 1=y 2=z）指定时，含该轴的面 u 固定沿该轴（外墙板、瓦这种有方向的贴图用）。
        /// </summary>
        public void Box(Material m, Vector3 c, Vector3 size, Quaternion? rot = null, int uAxis = -1)
        {
            if (m == null) return;
            var q = rot ?? Quaternion.identity;
            var h = size * .5f;
            var corners = new Vector3[4];
            var uvs = new Vector2[4];
            for (int axis = 0; axis < 3; axis++)
            {
                int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    var n = Vector3.zero; n[axis] = sgn;
                    bool uFirst = uAxis >= 0 && (a1 == uAxis || a2 == uAxis) ? a1 == uAxis : size[a1] >= size[a2];
                    for (int i = 0; i < 4; i++)
                    {
                        var lp = Vector3.zero;
                        lp[axis] = sgn * h[axis];
                        lp[a1] = S1[i] * h[a1];
                        lp[a2] = S2[i] * h[a2];
                        corners[i] = c + q * lp;
                        float cu = lp[a1] + c[a1], cv = lp[a2] + c[a2];
                        uvs[i] = uFirst ? new Vector2(cu, cv) : new Vector2(cv, cu);
                    }
                    Poly(m, corners, uvs, q * n);
                }
            }
        }

        /// <summary>圆台：底面中心 b，沿局部 +Y 方向（经 rot 旋转）延伸 h。</summary>
        public void Cyl(Material m, Vector3 b, float r0, float r1, float h, int seg = 12, bool caps = true, Quaternion? rot = null)
        {
            if (m == null) return;
            var q = rot ?? Quaternion.identity;
            float slope = (r0 - r1) / Mathf.Max(h, 1e-4f);
            var up = q * Vector3.up;
            var top = b + up * h;
            float circ = Mathf.PI * (r0 + r1);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 p00 = b + q * (d0 * r0), p01 = b + q * (d0 * r1 + Vector3.up * h);
                Vector3 p10 = b + q * (d1 * r0), p11 = b + q * (d1 * r1 + Vector3.up * h);
                Vector3 n0 = q * (d0 + Vector3.up * slope).normalized, n1 = q * (d1 + Vector3.up * slope).normalized;
                float u0 = circ * i / seg, u1 = circ * (i + 1) / seg;
                TriS(m, p00, p01, p11, n0, n0, n1, new Vector2(u0, 0), new Vector2(u0, h), new Vector2(u1, h));
                TriS(m, p00, p11, p10, n0, n1, n1, new Vector2(u0, 0), new Vector2(u1, h), new Vector2(u1, 0));
                if (caps)
                {
                    if (r1 > 1e-4f)
                        TriS(m, top, p11, p01, up, up, up, new Vector2(0, 0), new Vector2(d1.x * r1, d1.z * r1), new Vector2(d0.x * r1, d0.z * r1));
                    if (r0 > 1e-4f)
                        TriS(m, b, p00, p10, -up, -up, -up, new Vector2(0, 0), new Vector2(d0.x * r0, d0.z * r0), new Vector2(d1.x * r0, d1.z * r0));
                }
            }
        }

        /// <summary>椭球。</summary>
        public void Sphere(Material m, Vector3 c, Vector3 radii, int seg = 10, Quaternion? rot = null)
        {
            if (m == null) return;
            var q = rot ?? Quaternion.identity;
            int rings = Mathf.Max(3, seg / 2);
            var inv = new Vector3(1f / radii.x, 1f / radii.y, 1f / radii.z);
            Vector3 P(int r, int s, out Vector3 n)
            {
                float th = Mathf.PI * r / rings, ph = Mathf.PI * 2 * s / seg;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                n = q * Vector3.Scale(d, inv).normalized;
                return c + q * Vector3.Scale(d, radii);
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    var a = P(r, s, out var na); var b = P(r + 1, s, out var nb);
                    var d = P(r + 1, s + 1, out var nd); var e = P(r, s + 1, out var ne);
                    Vector2 ua = new Vector2(s, r) * .1f, ub = new Vector2(s, r + 1) * .1f, ud = new Vector2(s + 1, r + 1) * .1f, ue = new Vector2(s + 1, r) * .1f;
                    if (r > 0) TriS(m, a, b, e, na, nb, ne, ua, ub, ue);
                    if (r < rings - 1) TriS(m, e, b, d, ne, nb, nd, ue, ub, ud);
                }
        }

        public GameObject Build(Transform parent, string name, bool shadows = true)
        {
            if (order.Count == 0) return null;
            int total = 0;
            foreach (var m in order) total += parts[m].v.Count;
            var mesh = new Mesh { name = name };
            if (total > 65000) mesh.indexFormat = IndexFormat.UInt32;
            var V = new List<Vector3>(total); var N = new List<Vector3>(total); var U = new List<Vector2>(total);
            var subs = new List<int[]>();
            foreach (var m in order)
            {
                var p = parts[m];
                int off = V.Count;
                V.AddRange(p.v); N.AddRange(p.n); U.AddRange(p.uv);
                var t = new int[p.t.Count];
                for (int i = 0; i < t.Length; i++) t[i] = p.t[i] + off;
                subs.Add(t);
            }
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, U);
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Created.Add(mesh);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = order.ToArray();
            if (!shadows) mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }
    }

    /// <summary>可无缝平铺的值噪声。</summary>
    internal static class Noise
    {
        public static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)s * 0xcb1ab31fu;
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        public static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        public static float Value(float x, float y, int px, int py, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x0 = Mod(xi, px), x1 = Mod(xi + 1, px), y0 = Mod(yi, py), y1 = Mod(yi + 1, py);
            float a = Hash(x0, y0, s), b = Hash(x1, y0, s), c = Hash(x0, y1, s), d = Hash(x1, y1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>u,v ∈ [0,1)，fx/fy 为整数基础频率，保证平铺无缝。</summary>
        public static float Fbm(float u, float v, int fx, int fy, int oct, int s)
        {
            float sum = 0, amp = .5f, norm = 0;
            for (int i = 0; i < oct; i++)
            {
                sum += amp * Value(u * fx, v * fy, fx, fy, s + i * 101);
                norm += amp; amp *= .5f; fx *= 2; fy *= 2;
            }
            return sum / norm;
        }
    }

    /// <summary>程序生成的贴图：只在文件不存在时生成，可直接替换成正式贴图。</summary>
    internal static class DetailTextures
    {
        public const string Dir = "Assets/_Art/Textures/Generated";

        static string Full(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        public static Texture2D Albedo(string name, int w, int h, Func<float, float, Color> f, bool clamp = false)
        {
            string path = $"{Dir}/{name}.png";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null) return t;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = f((x + .5f) / w, (y + .5f) / h);
                    c.a = 1;
                    px[y * w + x] = c;
                }
            Write(path, w, h, px, false, clamp);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Texture2D Normal(string name, int w, int h, Func<float, float, float> hf, float strength)
        {
            string path = $"{Dir}/{name}.png";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null) return t;
            var hm = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    hm[y * w + x] = hf((x + .5f) / w, (y + .5f) / h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = hm[y * w + (x + 1) % w] - hm[y * w + (x - 1 + w) % w];
                    float dy = hm[((y + 1) % h) * w + x] - hm[((y - 1 + h) % h) * w + x];
                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    px[y * w + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1f);
                }
            Write(path, w, h, px, true, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Write(string path, int w, int h, Color[] px, bool normal, bool clamp)
        {
            ArtKit.EnsureFolder(Dir);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, normal);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(Full(path), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.filterMode = FilterMode.Trilinear;
            imp.SaveAndReimport();
        }
    }
}
