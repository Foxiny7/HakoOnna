using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第五轮（2026-11-21）：院子里原来用圆柱、方块拼的石件——春日灯笼、雪见灯笼、蹲踞的手水钵、水井、飞石。
    /// 没有合适的 CC0 模型，用车削（Lathe）、六角棱台（HexRings）、不规则的扁石（FlatStone）重做，材质还是带贴图的 MD_Stone。
    /// 位置、挡路的 Blocker 都不变。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>
        /// 多边形棱台叠起来的一摞（石灯笼的笠、火袋、基础）：rings 是从下到上的 (外接圆半径, 高度)，每个侧面是平的（平面着色）。
        /// capBottom / capTop：封底、封顶。
        /// </summary>
        static void HexRings(MeshBatch mb, Material m, Vector3 c, float yaw, IList<Vector2> rings, int sides = 6, bool capBottom = false, bool capTop = true)
        {
            if (m == null || mb.Mute) return;
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 P(int ring, int s)
            {
                float a = 2f * Mathf.PI * s / sides;
                return c + q * new Vector3(Mathf.Cos(a) * rings[ring].x, rings[ring].y, Mathf.Sin(a) * rings[ring].x);
            }
            for (int r = 0; r < rings.Count - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    Vector3 a = P(r, s), b = P(r, s + 1), d = P(r + 1, s), e = P(r + 1, s + 1);
                    var n = Vector3.Cross(d - a, b - a);
                    if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(e - a, b - a);
                    n.Normalize();
                    var mid = (a + b + d + e) * .25f - c;
                    mid.y = 0f;
                    if (Vector3.Dot(n, mid) < 0f && Mathf.Abs(n.y) < .98f) n = -n;
                    else if (Mathf.Abs(n.y) >= .98f) n = rings[r + 1].x < rings[r].x ? Vector3.up : Vector3.down;
                    float w0 = (b - a).magnitude, h = ((a + b) * .5f - (d + e) * .5f).magnitude;
                    mb.Poly(m, new[] { a, b, e, d }, new[] { new Vector2(0, 0), new Vector2(w0, 0), new Vector2(w0, h), new Vector2(0, h) }, n);
                }
            void Cap(int ring, Vector3 n)
            {
                var pts = new Vector3[sides];
                var uvs = new Vector2[sides];
                for (int s = 0; s < sides; s++)
                {
                    pts[s] = P(ring, s);
                    uvs[s] = new Vector2(pts[s].x, pts[s].z);
                }
                mb.Poly(m, pts, uvs, n);
            }
            if (capBottom) Cap(0, Vector3.down);
            if (capTop && rings[rings.Count - 1].x > 1e-4f) Cap(rings.Count - 1, Vector3.up);
        }

        /// <summary>火袋：六角柱，隔一面开一个黑洞洞的窗（方的），其余是实心石面。</summary>
        static void LanternFirebox(MeshBatch mb, Vector3 c, float yaw, float r, float y0, float y1)
        {
            HexRings(mb, D.Stone, c, yaw, new[] { new Vector2(r, y0), new Vector2(r, y1) }, 6, false, false);
            var q = Quaternion.Euler(0f, yaw, 0f);
            float inR = r * Mathf.Cos(Mathf.PI / 6f), side = r, h = y1 - y0;
            for (int s = 0; s < 6; s += 2)
            {
                float a = 2f * Mathf.PI * (s + .5f) / 6f;
                var n = q * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var t = Vector3.Cross(Vector3.up, n);
                var o = c + n * (inR + .002f) + Vector3.up * (y0 + h * .5f);
                float w = side * .52f, hh = h * .56f;
                mb.Poly(D.Black, new[] { o - t * (w / 2) - Vector3.up * (hh / 2), o + t * (w / 2) - Vector3.up * (hh / 2), o + t * (w / 2) + Vector3.up * (hh / 2), o - t * (w / 2) + Vector3.up * (hh / 2) },
                    new[] { Vector2.zero, new Vector2(w, 0), new Vector2(w, hh), new Vector2(0, hh) }, n);
            }
        }

        /// <summary>春日灯笼：六角基础、带节的圆竿、中台、火袋、翘角的六角笠、宝珠。高约 1.68 m。</summary>
        static void KasugaLantern(MeshBatch mb, Vector3 b, float yaw)
        {
            var up = Vector3.up;
            var id = Quaternion.identity;
            // 基础：六角台 + 上面一圈反花
            HexRings(mb, D.Stone, b, yaw, new[] { new Vector2(.33f, -.02f), new Vector2(.33f, .1f), new Vector2(.29f, .14f) });
            Lathe(mb, D.Stone, b, id, new[] { new Vector2(.24f, .14f), new Vector2(.2f, .19f), new Vector2(.13f, .22f), new Vector2(.11f, .24f) }, 20);
            // 竿：中间一道节，上下两道细箍
            Lathe(mb, D.Stone, b, id, new[]
            {
                new Vector2(.1f, .22f), new Vector2(.094f, .3f), new Vector2(.104f, .31f), new Vector2(.104f, .33f), new Vector2(.094f, .34f),
                new Vector2(.092f, .52f), new Vector2(.112f, .54f), new Vector2(.112f, .58f), new Vector2(.092f, .6f),
                new Vector2(.09f, .78f), new Vector2(.102f, .79f), new Vector2(.102f, .81f), new Vector2(.09f, .82f), new Vector2(.09f, .88f),
            }, 18);
            // 中台：往上张开的六角台
            HexRings(mb, D.Stone, b, yaw, new[] { new Vector2(.14f, .88f), new Vector2(.25f, .96f), new Vector2(.27f, .97f), new Vector2(.27f, 1.01f), new Vector2(.25f, 1.02f) }, 6, true);
            LanternFirebox(mb, b, yaw, .165f, 1.02f, 1.28f);
            // 笠：六角、坡面内凹，檐角各翘一个蕨手
            HexRings(mb, D.Stone, b, yaw, new[]
            {
                new Vector2(.2f, 1.28f), new Vector2(.45f, 1.3f), new Vector2(.46f, 1.33f), new Vector2(.33f, 1.38f), new Vector2(.2f, 1.45f),
                new Vector2(.11f, 1.53f), new Vector2(.08f, 1.55f),
            }, 6, true);
            var q = Quaternion.Euler(0f, yaw, 0f);
            for (int s = 0; s < 6; s++)
            {
                float a = 2f * Mathf.PI * s / 6f;
                var d = q * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p0 = b + d * .41f + up * 1.335f;
                Limb(mb, D.Stone, new[] { p0, p0 + d * .06f + up * .025f, p0 + d * .075f + up * .075f, p0 + d * .045f + up * .105f },
                    new[] { .026f, .024f, .02f, .016f }, 6);
            }
            // 请花 + 宝珠
            Lathe(mb, D.Stone, b, id, new[]
            {
                new Vector2(.08f, 1.55f), new Vector2(.1f, 1.57f), new Vector2(.06f, 1.59f), new Vector2(.085f, 1.63f), new Vector2(.078f, 1.67f),
                new Vector2(.035f, 1.71f), new Vector2(0f, 1.725f),
            }, 16);
            mb.Sphere(D.Moss, b + q * new Vector3(.2f, 1.385f, .12f), new Vector3(.17f, .03f, .12f), 8, Quaternion.Euler(-16f, yaw + 30f, 0f));
            mb.Sphere(D.Moss, b + q * new Vector3(-.14f, .12f, .2f), new Vector3(.14f, .03f, .1f), 8);
        }

        /// <summary>雪见灯笼：三条外撇的弯腿、圆的中台、六角火袋、一顶大圆笠。高约 1 m。</summary>
        static void YukimiLanternStone(MeshBatch mb, Vector3 b, float yaw)
        {
            var up = Vector3.up;
            var id = Quaternion.identity;
            var q = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < 3; i++)
            {
                var d = q * (Quaternion.Euler(0, i * 120f + 30f, 0) * Vector3.right);
                Limb(mb, D.Stone, new[] { b + d * .15f + up * .42f, b + d * .2f + up * .3f, b + d * .29f + up * .13f, b + d * .36f - up * .02f },
                    new[] { .06f, .055f, .05f, .058f }, 7);
            }
            Lathe(mb, D.Stone, b, id, new[]
            {
                new Vector2(0f, .37f), new Vector2(.21f, .38f), new Vector2(.26f, .41f), new Vector2(.265f, .45f), new Vector2(.24f, .465f), new Vector2(0f, .465f),
            }, 24);
            LanternFirebox(mb, b, yaw, .16f, .465f, .7f);
            // 大圆笠：檐薄、顶上略鼓，底面封住
            Lathe(mb, D.Stone, b, id, new[]
            {
                new Vector2(0f, .7f), new Vector2(.6f, .7f), new Vector2(.63f, .715f), new Vector2(.62f, .735f), new Vector2(.5f, .78f), new Vector2(.34f, .85f),
                new Vector2(.18f, .93f), new Vector2(.09f, .96f),
            }, 28);
            Lathe(mb, D.Stone, b, id, new[]
            {
                new Vector2(.09f, .96f), new Vector2(.1f, .975f), new Vector2(.06f, .99f), new Vector2(.08f, 1.025f), new Vector2(.07f, 1.055f),
                new Vector2(.03f, 1.085f), new Vector2(0f, 1.095f),
            }, 14);
            mb.Sphere(D.Moss, b + q * new Vector3(.22f, .83f, .12f), new Vector3(.24f, .03f, .17f), 8, Quaternion.Euler(-20f, yaw + 28f, 0f));
        }

        /// <summary>一块不规则的扁石（飞石、蹲踞的前石）：顶面略鼓、边上一圈斜面，轮廓按 seed 起伏。顶面离地 top。</summary>
        static void FlatStone(MeshBatch mb, Vector3 c, float r, float yaw, float seed, float top = .05f, float stretch = 1f)
        {
            if (mb.Mute) return;
            const int n = 14;
            var q = Quaternion.Euler(0f, yaw, 0f);
            var outer = new Vector3[n];
            var edge = new Vector3[n];
            var foot = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                float k = 1f + .2f * Pn(seed * 3.1f + Mathf.Cos(a) * 1.3f, seed * 1.7f + Mathf.Sin(a) * 1.3f) * 2f
                          + .06f * Mathf.Sin(a * 3f + seed);
                var d = q * new Vector3(Mathf.Cos(a) * stretch, 0f, Mathf.Sin(a));
                outer[i] = c + d * (r * k * .86f) + Vector3.up * (top - .004f);
                edge[i] = c + d * (r * k) + Vector3.up * (top - .028f);
                foot[i] = c + d * (r * k * 1.03f) + Vector3.up * -.03f;
            }
            var centre = c + Vector3.up * (top + .004f);
            Vector2 U(Vector3 p) => new Vector2(p.x, p.z);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var nb = ((edge[i] - c).normalized + Vector3.up).normalized;
                var nbj = ((edge[j] - c).normalized + Vector3.up).normalized;
                var ns = (foot[i] - c);
                ns.y = 0f;
                ns.Normalize();
                var nsj = (foot[j] - c);
                nsj.y = 0f;
                nsj.Normalize();
                var nu = (Vector3.up * 4f + (outer[i] - c).normalized).normalized;
                var nuj = (Vector3.up * 4f + (outer[j] - c).normalized).normalized;
                mb.TriS(D.Stone, centre, outer[j], outer[i], Vector3.up, nuj, nu, U(centre), U(outer[j]), U(outer[i]));
                mb.TriS(D.Stone, outer[i], outer[j], edge[j], nu, nuj, nbj, U(outer[i]), U(outer[j]), U(edge[j]));
                mb.TriS(D.Stone, outer[i], edge[j], edge[i], nu, nbj, nb, U(outer[i]), U(edge[j]), U(edge[i]));
                float w = (edge[j] - edge[i]).magnitude;
                mb.TriS(D.Stone, edge[i], edge[j], foot[j], nb, nbj, nsj, new Vector2(0, .08f), new Vector2(w, .08f), new Vector2(w, 0));
                mb.TriS(D.Stone, edge[i], foot[j], foot[i], nb, nsj, ns, new Vector2(0, .08f), new Vector2(w, 0), new Vector2(0, 0));
            }
        }

        /// <summary>水井：厚口沿的石井圈（里面是黑的、水在下面）、盖了一半的木盖、井架、滑轮、绳子、搁在盖上的木桶（CC0）。</summary>
        static void WellStone(MeshBatch mb, Vector3 wb)
        {
            var up = Vector3.up;
            var id = Quaternion.identity;
            Lathe(mb, D.Stone, wb, id, new[]
            {
                new Vector2(.64f, -.02f), new Vector2(.635f, .3f), new Vector2(.62f, .62f), new Vector2(.6f, .7f), new Vector2(.56f, .73f),
                new Vector2(.5f, .73f), new Vector2(.47f, .7f), new Vector2(.47f, .7f), new Vector2(.47f, .18f),
            }, 28);
            mb.Cyl(D.Water, wb + up * .2f, .47f, .47f, .004f, 20);
            // 半边木盖：三块板 + 两根横档
            for (int i = 0; i < 3; i++)
                BevelBox(mb, D.TrimMid, wb + new Vector3(-.41f + i * .19f, .752f, 0f), new Vector3(.18f, .034f, 1.12f - i * .08f), .006f);
            foreach (float z in new[] { -.3f, .3f })
                BevelBox(mb, D.TrimMid, wb + new Vector3(-.22f, .785f, z), new Vector3(.56f, .032f, .06f), .006f);
            // 井架
            foreach (float sx in new[] { -1f, 1f })
                BevelBox(mb, D.Post, wb + new Vector3(sx * .7f, .95f, 0), new Vector3(.09f, 1.94f, .09f), .01f);
            BevelBox(mb, D.Post, wb + new Vector3(0, 1.88f, 0), new Vector3(1.62f, .09f, .09f), .01f);
            // 滑轮：带槽的木轮 + 铁轴，绳子从槽里垂下来、另一头搭在盖上
            var axle = wb + new Vector3(0, 1.68f, 0);
            Lathe(mb, D.TrimMid, axle - Vector3.forward * .03f, Quaternion.Euler(90f, 0f, 0f), new[]
            {
                new Vector2(0f, 0f), new Vector2(.11f, 0f), new Vector2(.115f, .012f), new Vector2(.085f, .03f), new Vector2(.115f, .048f), new Vector2(.11f, .06f), new Vector2(0f, .06f),
            }, 18);
            mb.Cyl(D.Iron, axle - Vector3.forward * .06f, .012f, .012f, .12f, 6, true, Quaternion.Euler(90f, 0f, 0f));
            foreach (float sz in new[] { -1f, 1f })
                mb.Box(D.Iron, axle + new Vector3(0, .1f, sz * .045f), new Vector3(.02f, .22f, .006f));
            var rope = new List<Vector3>();
            for (int i = 0; i <= 6; i++) rope.Add(axle + new Vector3(.09f, -.02f - i * .15f, 0f) + Vector3.forward * (.006f * Mathf.Sin(i * 1.3f)));
            Tube(mb, D.Straw, rope, .009f, 5);
            var rope2 = new List<Vector3> { axle + new Vector3(-.09f, -.02f, 0f), axle + new Vector3(-.1f, -.5f, .01f), wb + new Vector3(-.22f, .83f, .12f), wb + new Vector3(-.3f, .775f, .2f) };
            Tube(mb, D.Straw, rope2, .009f, 5);
            if (ExternalModels.Has("wooden_bucket_01"))
                mb.Model("wooden_bucket_01", ExternalModels.Place("wooden_bucket_01", null, wb + new Vector3(-.27f, .77f, .27f), Quaternion.Euler(0f, 35f, 0f),
                    ExternalModels.ScaleToHeight("wooden_bucket_01", .26f)));
            else
                mb.Cyl(D.TrimMid, wb + new Vector3(-.25f, .765f, .25f), .13f, .15f, .22f, 10);
        }

        /// <summary>蹲踞的手水钵：自然石凿出来的钵，口沿厚、里面凹下去盛着水。</summary>
        static void TsukubaiBasin(MeshBatch mb, Vector3 at)
        {
            Lathe(mb, D.Stone, at, Quaternion.Euler(0f, 0f, 2.5f), new[]
            {
                new Vector2(.26f, -.02f), new Vector2(.31f, .1f), new Vector2(.32f, .24f), new Vector2(.29f, .34f), new Vector2(.25f, .385f),
                new Vector2(.21f, .385f), new Vector2(.18f, .36f), new Vector2(.16f, .3f), new Vector2(0f, .27f),
            }, 22);
            mb.Cyl(D.Water, at + Vector3.up * .345f, .172f, .172f, .003f, 16);
        }
    }
}
