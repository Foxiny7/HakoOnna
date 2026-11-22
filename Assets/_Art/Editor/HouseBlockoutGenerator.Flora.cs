using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第五轮（2026-11-21）：院子里原来用球体拼的松、红叶、灌木、石头。
    /// <list type="bullet">
    /// <item>枝干：一根根弯曲、逐渐变细的圆管（Limb），贴已有的 pine_bark。</item>
    /// <item>枝叶：透明裁切的“小枝”面片。贴图是 Tools~/prep_tex_r5.py 用 CC0 的叶片（ambientCG LeafSet028 / LeafSet022）
    ///   和松针颜色（Poly Haven pine_sapling_small）拼的，一张四格，小枝从格子底边中点往上长。</item>
    /// <item>法线：面片不用自己的面法线，用“从树冠（灌木）中心往外”的方向——URP Lit 双面渲染时背面也用正面法线打光，
    ///   按面法线的话从背后看是黑的。</item>
    /// <item>荒废程度（用户定）：还看得出原来修剪的云片、圆丛，但枝叶乱冒，有枯黄的空洞。</item>
    /// <item>石头：Poly Haven rock_moss_set_01 / 02（Blender 减过面，见 Tools~/decimate_fbx.py）。</item>
    /// </list>
    /// 只是视觉：挡路的 Blocker 还是 Garden.cs 里原来那些，位置、大小不变。随机数单独一条。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        const string FloraMatDir = "Assets/_Art/Materials/External/Foliage";
        const string FloraRev = "r5";
        static Material floraPine, floraMaple, floraShrub;

        static Material FloraMat(string name, string tex, Color tint)
        {
            ArtKit.EnsureFolder(FloraMatDir);
            string path = $"{FloraMatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && m.GetTag("HakoArtRev", false, "") == FloraRev) return m;
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ExternalMats.Root}/Foliage/{tex}/{tex}_diff_a.png");
            if (t == null)
            {
                Debug.LogWarning($"[Hako Art] 缺少枝叶贴图 {tex}（院子的树和灌木不画叶子）");
                return null;
            }
            bool created = m == null;
            if (created) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetTexture("_BaseMap", t);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", .12f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.doubleSidedGI = true;
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", .38f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("HakoArtRev", FloraRev);
            if (created) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        static void FloraLoad()
        {
            ExternalMats.EnsureImportSettings();
            floraPine = FloraMat("MX_PineTuft", "pine_tuft", new Color(.80f, .84f, .74f, 1f));
            floraMaple = FloraMat("MX_MapleSprig", "maple_sprig", new Color(.74f, .54f, .52f, 1f));
            floraShrub = FloraMat("MX_ShrubSprig", "shrub_sprig", new Color(.66f, .70f, .62f, 1f));
        }

        // =====================================================================
        // 网格工具
        // =====================================================================
        /// <summary>沿一串点、半径逐点给定的圆管（树干、树枝）。UV：u 绕一圈、v 沿长度，都以米为单位。</summary>
        static void Limb(MeshBatch mb, Material m, IList<Vector3> pts, IList<float> rad, int seg = 8)
        {
            int n = pts.Count;
            if (n < 2 || m == null || mb.Mute) return;
            var tan = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var t = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                tan[i] = t.sqrMagnitude > 1e-14f ? t.normalized : (i > 0 ? tan[i - 1] : Vector3.up);
            }
            var nn = new Vector3[n];
            var hint = Mathf.Abs(Vector3.Dot(tan[0], Vector3.up)) > .9f ? Vector3.right : Vector3.up;
            nn[0] = Vector3.Cross(tan[0], hint).normalized;
            for (int i = 1; i < n; i++)
            {
                var q = nn[i - 1] - Vector3.Dot(nn[i - 1], tan[i]) * tan[i];
                nn[i] = q.sqrMagnitude > 1e-12f ? q.normalized : nn[i - 1];
            }
            var G = new Vector3[n, seg + 1];
            var GN = new Vector3[n, seg + 1];
            var GU = new Vector2[n, seg + 1];
            float along = 0f;
            for (int k = 0; k < n; k++)
            {
                if (k > 0) along += (pts[k] - pts[k - 1]).magnitude;
                var bn = Vector3.Cross(tan[k], nn[k]);
                for (int s = 0; s <= seg; s++)
                {
                    float a = 2f * Mathf.PI * s / seg;
                    var d = Mathf.Cos(a) * nn[k] + Mathf.Sin(a) * bn;
                    G[k, s] = pts[k] + d * rad[k];
                    GN[k, s] = d;
                    GU[k, s] = new Vector2(a * Mathf.Max(rad[k], .012f), along);
                }
            }
            EmitGrid(mb, m, G, GN, GU);
        }

        /// <summary>图集里的一格：0 左上、1 右上、2 左下、3 右下（和 prep_tex_r5.py 一致）。</summary>
        static Rect FloraCell(int cell)
        {
            const float inset = 7f / 1024f;
            return new Rect((cell % 2) * .5f + inset, (cell < 2 ? .5f : 0f) + inset, .5f - 2f * inset, .5f - 2f * inset);
        }

        /// <summary>一张面片：at 是底边中点，grow 是小枝生长的方向，right 是面片的横向；n 是打光用的法线。</summary>
        static void FloraCard(MeshBatch mb, Material m, Vector3 at, Vector3 grow, Vector3 right, float w, float h, Rect uv, Vector3 n)
        {
            if (m == null) return;
            Vector3 a = at - right * (w * .5f), b = at + right * (w * .5f), c = b + grow * h, d = a + grow * h;
            Vector2 ua = new Vector2(uv.xMin, uv.yMin), ub = new Vector2(uv.xMax, uv.yMin), uc = new Vector2(uv.xMax, uv.yMax), ud = new Vector2(uv.xMin, uv.yMax);
            mb.TriS(m, a, b, c, n, n, n, ua, ub, uc);
            mb.TriS(m, a, c, d, n, n, n, ua, uc, ud);
        }

        /// <summary>两张十字交叉的面片（从哪边看都有厚度）。roll：绕生长方向转多少度。</summary>
        static void FloraCross(MeshBatch mb, Material m, Vector3 at, Vector3 grow, float size, int cell, Vector3 n, float roll)
        {
            var hint = Mathf.Abs(Vector3.Dot(grow, Vector3.up)) > .92f ? Vector3.right : Vector3.up;
            var r1 = Quaternion.AngleAxis(roll, grow) * Vector3.Cross(hint, grow).normalized;
            var r2 = Vector3.Cross(grow, r1);
            var uv = FloraCell(cell);
            FloraCard(mb, m, at, grow, r1, size, size, uv, n);
            FloraCard(mb, m, at, grow, r2, size, size, uv, n);
        }

        static Vector3 RandDir(System.Random rng)
        {
            float z = (float)rng.NextDouble() * 2f - 1f, a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        // =====================================================================
        // 松（庭木的黑松：弯曲的主干、一层层横出去的枝、枝头一片片“云”）
        // =====================================================================
        static void FloraPine(MeshBatch mb, System.Random rng, Vector3 b, float h, float yaw)
        {
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 lean = q * Vector3.right, side = q * Vector3.forward;
            float amp = h * .1f, ph = R(.3f, 1.1f);
            Vector3 P(float t) => b + up * (h * t - .06f)
                                  + lean * (amp * Mathf.Sin(t * Mathf.PI * 1.25f) + h * .1f * t)
                                  + side * (amp * .6f * Mathf.Sin(t * Mathf.PI * 2.1f + ph));
            float Rt(float t) => Mathf.Lerp(.2f, .05f, Mathf.Pow(t, .8f)) * (1f + .5f * Mathf.Exp(-t * 20f));

            const int n = 16;
            var pts = new Vector3[n + 1];
            var rad = new float[n + 1];
            for (int i = 0; i <= n; i++) { pts[i] = P(i / (float)n); rad[i] = Rt(i / (float)n); }
            Limb(mb, D.Bark, pts, rad, 10);

            // 一层层的枝：越往上越短，方位按黄金角错开
            const int layers = 6;
            for (int k = 0; k < layers; k++)
            {
                float t = .33f + k * .115f + R(-.02f, .02f);
                float az = yaw + 20f + k * 137.5f + R(-18f, 18f);
                var d = Quaternion.Euler(0, az, 0) * Vector3.right;
                float len = Mathf.Lerp(.42f, .15f, k / (layers - 1f)) * h * R(.85f, 1.1f);
                float r0 = Rt(t) * .5f;
                var end = PineBranch(mb, rng, P(t), d, len, r0, out var bpts);
                float rx = Mathf.Lerp(.95f, .55f, k / (layers - 1f)) * R(.9f, 1.1f);
                PinePad(mb, rng, end + up * .05f, rx, rx * R(.72f, .88f), d, 1f);
                if (k == 0) ShotDir("r5_pinepad", end, (d + Vector3.Cross(up, d) * .7f).normalized, 2.8f, -.9f);
                // 枝的中段往两边分的小枝，各带一小片
                foreach (float sgn in new[] { -1f, 1f })
                {
                    if (R(0f, 1f) < .25f) continue;
                    int at = Mathf.Clamp(Mathf.RoundToInt(bpts.Count * R(.45f, .7f)), 1, bpts.Count - 2);
                    var d2 = Quaternion.Euler(0, sgn * R(38f, 62f), 0) * d;
                    var e2 = PineBranch(mb, rng, bpts[at], d2, len * R(.38f, .5f), r0 * .55f, out _);
                    PinePad(mb, rng, e2 + up * .04f, rx * R(.55f, .7f), rx * R(.45f, .6f), d2, 1f);
                }
                // 没人剪：枝上乱冒的徒长枝（直直往上的一两簇）
                for (int s = 0; s < 2; s++)
                {
                    var p = bpts[rng.Next(1, bpts.Count - 1)];
                    var g = (up + RandDir(rng) * .3f).normalized;
                    FloraCross(mb, floraPine, p - g * .04f, g, R(.34f, .46f), rng.Next(3), (g + d * .3f).normalized, R(0f, 180f));
                }
            }
            // 树顶
            PinePad(mb, rng, P(1f) + up * .08f, .72f, .62f, lean, 1.3f);
        }

        /// <summary>松枝：先平着伸出去、中段略垂、梢头翘起。返回梢头的位置。</summary>
        static Vector3 PineBranch(MeshBatch mb, System.Random rng, Vector3 start, Vector3 d, float len, float r0, out List<Vector3> pts)
        {
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var side = Vector3.Cross(Vector3.up, d);
            float wob = R(-.12f, .12f), droop = R(.07f, .13f), lift = R(.16f, .26f);
            const int n = 7;
            pts = new List<Vector3>(n + 1);
            var rad = new List<float>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float s = i / (float)n;
                pts.Add(start + d * (len * s) + Vector3.up * (len * (-droop * Mathf.Sin(Mathf.PI * s) + lift * s * s))
                        + side * (len * wob * Mathf.Sin(Mathf.PI * s * 1.5f)));
                rad.Add(Mathf.Lerp(r0, Mathf.Max(.018f, r0 * .28f), s));
            }
            Limb(mb, D.Bark, pts, rad, 7);
            return pts[n];
        }

        /// <summary>一片“云”：扁圆的一层松针簇，中间朝上、边上朝外；底下垂几簇枯的。mess 越大越乱。</summary>
        static void PinePad(MeshBatch mb, System.Random rng, Vector3 c, float rx, float rz, Vector3 axis, float mess)
        {
            if (floraPine == null) return;
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            axis.y = 0f;
            var ax = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.right;
            var bx = Vector3.Cross(up, ax);
            var core = c - up * (rx * .6f);
            int count = Mathf.Max(8, Mathf.RoundToInt(42f * rx * rz));
            float th0 = R(0f, 6.28f);
            for (int i = 0; i < count; i++)
            {
                float rho = Mathf.Sqrt((i + .5f) / count), th = th0 + i * 2.39996f;
                var off = ax * (Mathf.Cos(th) * rho * rx) + bx * (Mathf.Sin(th) * rho * rz);
                var p = c + off + up * (rx * .24f * (1f - rho * rho) + R(-.05f, .05f));
                var outward = off.sqrMagnitude > 1e-6f ? off.normalized : ax;
                var g = (up * (1.15f - .7f * rho) + outward * (.2f + .95f * rho * rho) + RandDir(rng) * .22f).normalized;
                float size = R(.32f, .42f);
                int cell = R(0f, 1f) < .07f * mess ? 3 : rng.Next(3);
                var nrm = (p + g * (size * .5f) - core).normalized;
                FloraCross(mb, floraPine, p - g * (size * .22f), g, size, cell, nrm, R(0f, 180f));
            }
            // 冒出云片的徒长枝
            int stray = Mathf.RoundToInt(R(1.2f, 3.4f) * mess);
            for (int i = 0; i < stray; i++)
            {
                float th = R(0f, 6.28f), rho = R(.1f, .8f);
                var p = c + ax * (Mathf.Cos(th) * rho * rx) + bx * (Mathf.Sin(th) * rho * rz) + up * (rx * .2f);
                var g = (up + RandDir(rng) * .25f).normalized;
                FloraCross(mb, floraPine, p, g, R(.42f, .56f), rng.Next(3), (g + (p - core).normalized).normalized, R(0f, 180f));
            }
            // 云片底下垂着的枯枝叶
            int dead = Mathf.RoundToInt(R(1.5f, 3.5f) * mess);
            for (int i = 0; i < dead; i++)
            {
                float th = R(0f, 6.28f), rho = R(.35f, .95f);
                var off = ax * (Mathf.Cos(th) * rho * rx) + bx * (Mathf.Sin(th) * rho * rz);
                var g = (off.normalized * .55f - up + RandDir(rng) * .2f).normalized;
                FloraCross(mb, floraPine, c + off - up * .02f, g, R(.26f, .36f), 3, (off.normalized * .7f - up * .3f).normalized, R(0f, 180f));
            }
            // 托着云片的细枝
            for (int i = 0; i < 5; i++)
            {
                float th = th0 + i * 1.2566f + R(-.3f, .3f), rho = R(.45f, .85f);
                var e = c + ax * (Mathf.Cos(th) * rho * rx) + bx * (Mathf.Sin(th) * rho * rz) + up * (rx * .08f);
                var mid = Vector3.Lerp(c, e, .5f) - up * .05f;
                Limb(mb, D.Bark, new[] { c - up * .06f, mid, e }, new[] { .02f, .014f, .008f }, 5);
            }
        }

        // =====================================================================
        // 红叶（伊吕波红叶）：主干微斜、分三四枝，一层层平着铺开的伞形树冠；地上落叶
        // =====================================================================
        static void FloraMaple(MeshBatch mb, System.Random rng, Vector3 b, float h, float yaw)
        {
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            var q = Quaternion.Euler(0, yaw, 0);
            var lean = (q * new Vector3(.2f, 1f, .06f)).normalized;
            float th = h * .36f;
            var bend = q * Vector3.forward;
            const int n = 8;
            var pts = new Vector3[n + 1];
            var rad = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                pts[i] = b - up * .06f + lean * ((th + .06f) * t) + bend * (.07f * Mathf.Sin(t * Mathf.PI));
                rad[i] = Mathf.Lerp(.115f, .08f, t) * (1f + .5f * Mathf.Exp(-t * 16f));
            }
            Limb(mb, D.Bark, pts, rad, 9);
            var fork = pts[n];
            float cw = h * .5f, ch = h * .2f;
            var crown = fork + up * (h * .3f);
            var core = crown - up * (ch * 2.2f);
            var tips = new List<(Vector3 p, Vector3 d)>();
            int limbs = 4;
            float az0 = R(0f, 360f);
            for (int i = 0; i < limbs; i++)
            {
                var d = (q * Quaternion.Euler(0, az0 + i * (360f / limbs) + R(-22f, 22f), 0) * new Vector3(.78f, 1f, 0)).normalized;
                Grow(mb, rng, fork, d, h * R(.28f, .36f), .062f, 1, 3, .62f, tips);
            }
            if (floraMaple == null) return;
            foreach (var (p, d) in tips)
            {
                int k = 9 + rng.Next(4);
                for (int i = 0; i < k; i++)
                {
                    // 一层层平铺：小枝朝外、略垂，面片大致水平（带一点侧倾，从侧面看也有厚度）
                    var flat = new Vector3(d.x, 0f, d.z);
                    flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.right;
                    var g = (Quaternion.Euler(0, R(-85f, 85f), 0) * flat + up * R(-.38f, .1f)).normalized;
                    var right = Quaternion.AngleAxis(R(-42f, 42f), g) * Vector3.Cross(up, g).normalized;
                    float size = R(.34f, .52f);
                    var at = p - d * R(0f, .45f) + RandDir(rng) * .2f;
                    var nrm = (at + g * (size * .5f) - core).normalized;
                    nrm = (nrm + up * .35f).normalized;
                    FloraCard(mb, floraMaple, at - g * (size * .12f), g, right, size, size, FloraCell(rng.Next(3)), nrm);
                }
            }
            // 落叶：一片片平躺在地上（图集右下那格是 4 × 4 的单片叶子）
            int fallen = 70 + rng.Next(30);
            for (int i = 0; i < fallen; i++)
            {
                float a = R(0f, 6.28f), rr = Mathf.Sqrt(R(.02f, 1.3f)) * cw;
                var at = new Vector3(b.x + Mathf.Cos(a) * rr, .012f + R(0f, .004f), b.z + Mathf.Sin(a) * rr);
                var g = Quaternion.Euler(0, R(0f, 360f), 0) * Vector3.forward;
                float s = R(.075f, .11f);
                const float c0 = 1f / 8f, ins = 3f / 1024f;
                var uv = new Rect(.5f + rng.Next(4) * c0 + ins, rng.Next(4) * c0 + ins, c0 - 2 * ins, c0 - 2 * ins);
                FloraCard(mb, floraMaple, at, g, Vector3.Cross(up, g), s, s, uv, up);
            }
        }

        /// <summary>
        /// 递归长枝：一根略弯的枝，到头分两三叉；flatten 越小分出去的枝越往平里长（伞形）。
        /// depth 到 maxDepth 的枝梢记进 tips（给它挂叶子）；tips 为空就只长枝（枯树）。
        /// </summary>
        static void Grow(MeshBatch mb, System.Random rng, Vector3 p, Vector3 dir, float len, float r, int depth, int maxDepth, float flatten,
            List<(Vector3 p, Vector3 d)> tips)
        {
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var perp = Vector3.Cross(dir, RandDir(rng));
            perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.right;
            const int n = 5;
            var pts = new Vector3[n + 1];
            var rad = new float[n + 1];
            float bow = len * R(.08f, .2f);
            for (int i = 0; i <= n; i++)
            {
                float s = i / (float)n;
                pts[i] = p + dir * (len * s) + perp * (bow * Mathf.Sin(Mathf.PI * s)) + Vector3.up * (len * .05f * s * s);
                rad[i] = Mathf.Lerp(r, r * .6f, s);
            }
            Limb(mb, D.Bark, pts, rad, depth <= 1 ? 7 : 5);
            var end = pts[n];
            var endDir = (pts[n] - pts[n - 1]).normalized;
            if (depth >= maxDepth)
            {
                tips?.Add((end, endDir));
                tips?.Add((pts[n - 2], endDir));
                tips?.Add((pts[n - 3], endDir));
                return;
            }
            int kids = 2 + (rng.Next(3) == 0 ? 1 : 0);
            float az0 = R(0f, 360f);
            for (int c = 0; c < kids; c++)
            {
                var axis = Quaternion.AngleAxis(az0 + c * (360f / kids) + R(-25f, 25f), endDir) * perp;
                var cd = Quaternion.AngleAxis(R(26f, 48f), axis) * endDir;
                cd.y *= flatten;
                cd = cd.normalized;
                Grow(mb, rng, end, cd, len * R(.62f, .8f), r * .6f, depth + 1, maxDepth, flatten, tips);
            }
            // 枝的中段再冒一根小侧枝
            if (depth >= 1 && tips != null && rng.Next(2) == 0)
            {
                var sd = (Quaternion.AngleAxis(R(40f, 70f), perp) * endDir);
                sd.y *= flatten;
                Grow(mb, rng, pts[3], sd.normalized, len * R(.4f, .55f), r * .4f, Mathf.Max(depth + 1, maxDepth), maxDepth, flatten, tips);
            }
        }

        // =====================================================================
        // 灌木（杜鹃 / 山茶一类的常绿丛）：两三个圆丘挤在一起，枝条乱冒，有的一面枯了
        // =====================================================================
        static void FloraShrub(MeshBatch mb, System.Random rng, float x, float z, float s)
        {
            if (floraShrub == null) return;
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            int lobes = 2 + rng.Next(2);
            for (int l = 0; l < lobes; l++)
            {
                float rl = R(.4f, .56f) * s, hl = rl * R(1.15f, 1.5f);
                var c = new Vector3(x + R(-.34f, .34f) * s, 0f, z + R(-.3f, .3f) * s);
                // 枯掉的一面：这个方位上的小枝大半没了，剩下的是枯叶
                bool gap = rng.Next(5) < 2;
                float ga = R(0f, 6.28f), gw = R(.5f, .8f);
                // 从根上叉开的几根枝（叶子稀的地方看得见）
                int stems = 4 + rng.Next(3);
                for (int i = 0; i < stems; i++)
                {
                    float a = R(0f, 6.28f), el = R(.25f, .7f);
                    var tip = c + new Vector3(Mathf.Cos(a) * rl * el, hl * R(.55f, .85f), Mathf.Sin(a) * rl * el);
                    var mid = Vector3.Lerp(c, tip, .5f) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (rl * .12f);
                    Limb(mb, D.Bark, new[] { c - up * .03f, mid, tip }, new[] { .022f, .014f, .007f }, 5);
                }
                int count = Mathf.RoundToInt(44f * (rl / .5f) * (rl / .5f));
                float th0 = R(0f, 6.28f);
                for (int i = 0; i < count; i++)
                {
                    // 半球上均匀撒点（斐波那契），再压成椭球
                    float y = 1f - (i + .5f) / count * .97f, rr = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), a = th0 + i * 2.39996f;
                    var dn = new Vector3(rr * Mathf.Cos(a), y, rr * Mathf.Sin(a));
                    int cell = rng.Next(3);
                    if (gap && y < .78f && Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, ga * Mathf.Rad2Deg)) < gw * 57.3f)
                    {
                        if (R(0f, 1f) < .55f) continue;
                        cell = 3;
                    }
                    else if (R(0f, 1f) < .06f) cell = 3;
                    float k = R(.76f, 1f);
                    var p = c + new Vector3(dn.x * rl, dn.y * hl, dn.z * rl) * k;
                    var g = (dn + up * .35f + RandDir(rng) * .38f).normalized;
                    float size = R(.34f, .48f);
                    var nrm = (dn + up * .25f).normalized;
                    FloraCross(mb, floraShrub, p - g * (size * .5f), g, size, cell, nrm, R(0f, 180f));
                }
                // 里层再垫一层，免得一眼看穿
                for (int i = 0; i < count / 3; i++)
                {
                    var dn = RandDir(rng);
                    dn.y = Mathf.Abs(dn.y);
                    var p = c + new Vector3(dn.x * rl, dn.y * hl, dn.z * rl) * R(.3f, .6f);
                    var g = (dn + up * .5f).normalized;
                    FloraCross(mb, floraShrub, p - g * .15f, g, R(.34f, .44f), rng.Next(3), (dn + up * .3f).normalized, R(0f, 180f));
                }
                // 没人剪：几根窜出圆丘的长枝
                int shoots = 2 + rng.Next(4);
                for (int i = 0; i < shoots; i++)
                {
                    float a = R(0f, 6.28f), el = R(.15f, .75f);
                    var dn = new Vector3(Mathf.Cos(a) * el, Mathf.Sqrt(1f - el * el), Mathf.Sin(a) * el);
                    var from = c + new Vector3(dn.x * rl, dn.y * hl, dn.z * rl) * .6f;
                    var g = (dn + up * .6f + RandDir(rng) * .2f).normalized;
                    float len = R(.3f, .62f) * s;
                    var tip = from + new Vector3(dn.x * rl, dn.y * hl, dn.z * rl) * .4f + g * len;
                    Limb(mb, D.Bark, new[] { from, Vector3.Lerp(from, tip, .55f) + RandDir(rng) * .03f, tip }, new[] { .01f, .007f, .004f }, 4);
                    FloraCross(mb, floraShrub, tip - g * .22f, g, R(.3f, .4f), rng.Next(3), (dn + up * .3f).normalized, R(0f, 180f));
                    FloraCross(mb, floraShrub, Vector3.Lerp(from, tip, .6f) - g * .2f, (g + RandDir(rng) * .5f).normalized, R(.26f, .34f), rng.Next(3),
                        (dn + up * .3f).normalized, R(0f, 180f));
                }
            }
        }

        // =====================================================================
        // 枯树：只有枝，歪歪扭扭
        // =====================================================================
        static void FloraDeadTree(MeshBatch mb, System.Random rng, Vector3 b, float h)
        {
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            var lean = (Quaternion.Euler(4, 0, -5) * up).normalized;
            var side = Vector3.Cross(lean, Vector3.forward).normalized;
            const int n = 9;
            var pts = new Vector3[n + 1];
            var rad = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                pts[i] = b - up * .06f + lean * ((h + .06f) * t) + side * (.09f * Mathf.Sin(t * Mathf.PI * 1.6f));
                rad[i] = Mathf.Lerp(.18f, .075f, Mathf.Pow(t, .8f)) * (1f + .5f * Mathf.Exp(-t * 16f));
            }
            Limb(mb, D.Bark, pts, rad, 9);
            // 断掉的树顶
            mb.Cyl(D.Bark, pts[n] - lean * .01f, .075f, .03f, .12f, 7, true, Quaternion.FromToRotation(up, (lean + side * .5f).normalized));
            for (int i = 0; i < 6; i++)
            {
                float t = R(.45f, .97f);
                var start = pts[Mathf.RoundToInt(t * n)];
                var d = (Quaternion.Euler(0, i * 61f + R(-15f, 15f), 0) * Vector3.right + up * R(.35f, 1.0f)).normalized;
                Grow(mb, rng, start, d, R(.8f, 1.35f), Mathf.Lerp(.06f, .04f, t), 1, 3, .85f, null);
            }
        }

        // =====================================================================
        // 石头（CC0 模型）
        // =====================================================================
        static readonly string[] RockBig = { "rock04", "rock01", "rock06", "rock03", "rock05", "rock02" }; // 先用立得起来的
        static readonly string[] RockSmall = { "rock07", "rock08", "rock09", "rock10", "rock11", "rock12", "rock13" };

        /// <summary>
        /// 一块石头：占的地方和原来那个椭球差不多（radii 是原来的半轴），底下埋进土里一截。
        /// small = 用较扁的那一套（池边、蹲踞）。缺模型时返回 false（调用的地方照旧画椭球）。
        /// </summary>
        static bool RockModel(MeshBatch mb, Vector3 ground, Vector3 radii, float yaw, int pick, bool small)
        {
            string id = small ? "rock_moss_set_02" : "rock_moss_set_01";
            var parts = small ? RockSmall : RockBig;
            string part = parts[((pick % parts.Length) + parts.Length) % parts.Length];
            if (!ExternalModels.Has(id, part)) return false;
            // 大石头按高度配（立石要有个头，占地比原来的椭球宽一点没关系），池边的小石头按占地配
            float foot = (radii.x + radii.z) * (small ? 1.15f : 1.5f);
            float sc = ExternalModels.ScaleToFit(id, new Vector3(foot, radii.y * (small ? 1.9f : 1.7f), foot), part);
            float hgt = ExternalModels.LocalBounds(id, part).size.y * sc;
            var at = new Vector3(ground.x, ground.y - hgt * .12f, ground.z);
            mb.Model(id, ExternalModels.Place(id, part, at, Quaternion.Euler(0f, yaw, 0f), sc), part);
            return true;
        }
    }
}
