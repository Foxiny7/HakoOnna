using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>第五轮（室内）：瓷器小件、玩具、碁石碁笥、朝顔小便器、和式蹲便。说明见 Props5.cs。</summary>
    public static partial class HouseBlockoutGenerator
    {
        // =====================================================================
        // 瓷器：原来到处是“瓷质的圆台”（杯、碗、坛、盘、盖）。Kit.C 画瓷质圆台时（第二遍以后）改走这里，
        // 按圆台的上下半径和高度猜器形，车削出带圈足、口沿、内壁的东西。占的地方不超过原来的圆台。
        // =====================================================================
        static bool IsCeramic(Material m) => m != null && (m == D.CeramicWhite || m == D.CeramicBlue || m == D.CeramicBrown);

        static void Vessel(MeshBatch mb, Material m, Vector3 at, float r0, float r1, float h, Quaternion rot)
        {
            float R = Mathf.Max(r0, r1);
            float t = Mathf.Clamp(R * .09f, .0025f, .012f);
            var p = new List<Vector2>();
            if (h < R * .45f)
            {
                if (r1 > r0 * 1.15f)
                {
                    // 盘、碟：浅浅地张开，里面凹下去
                    p.Add(P2(0f, 0f)); p.Add(P2(r0 * .85f, 0f)); p.Add(P2(r0, h * .2f)); p.Add(P2(Mathf.Lerp(r0, r1, .7f), h * .7f)); p.Add(P2(r1, h));
                    p.Add(P2(r1 - t * .6f, h)); p.Add(P2(Mathf.Lerp(r0, r1, .6f) - t, h * .62f)); p.Add(P2(r0 * .8f, h * .38f)); p.Add(P2(0f, h * .34f));
                }
                else
                {
                    // 盖子、圆饼：边上倒圆，顶面略鼓，中间一个小钮
                    p.Add(P2(R * .97f, 0f)); p.Add(P2(R, h * .35f)); p.Add(P2(R * .93f, h * .8f)); p.Add(P2(R * .5f, h)); p.Add(P2(R * .16f, h * 1.02f));
                    p.Add(P2(R * .13f, h * 1.02f)); p.Add(P2(0f, h * 1.02f));
                }
            }
            else if (r1 >= r0 * .95f)
            {
                // 杯、碗、敞口的罐：圈足、鼓一点的腹、口沿，里面是空的
                float mid = Mathf.Lerp(r0, r1, .6f) + (r1 - r0 < R * .15f ? R * .04f : 0f);
                mid = Mathf.Min(mid, R);
                p.Add(P2(0f, 0f)); p.Add(P2(r0 * .88f, 0f)); p.Add(P2(r0, h * .07f)); p.Add(P2(mid, h * .5f)); p.Add(P2(r1, h * .93f)); p.Add(P2(r1 - t * .25f, h));
                p.Add(P2(r1 - t, h * .985f)); p.Add(P2(mid - t, h * .5f)); p.Add(P2(r0 * .8f, h * .16f)); p.Add(P2(0f, h * .13f));
            }
            else
            {
                // 往上收的：坛、瓶的肩和口（多半叠在另一个圆台上面）
                const int n = 5;
                for (int i = 0; i <= n; i++)
                {
                    float a = i / (float)n * Mathf.PI * .5f;
                    p.Add(P2(r1 + (r0 - r1) * Mathf.Pow(Mathf.Cos(a), .8f), h * .8f * Mathf.Sin(a)));
                }
                if (r1 < .018f)
                {
                    p.Add(P2(r1 * 1.25f, h * .9f)); p.Add(P2(r1, h)); p.Add(P2(0f, h));
                }
                else
                {
                    p.Add(P2(r1 * 1.1f, h * .9f)); p.Add(P2(r1 * 1.1f, h)); p.Add(P2(r1 * .96f, h)); p.Add(P2(r1 * .8f, h * .86f)); p.Add(P2(r1 * .8f, h * .25f));
                }
            }
            Lathe(mb, m, at, rot, p, R > .12f ? 22 : 16);
        }

        // =====================================================================
        // 茶具
        // =====================================================================
        static void Yunomi(MeshBatch mb, Vector3 at, Material m) => Lathe(mb, m, at, Quaternion.identity, YunomiProfile, 14);

        // =====================================================================
        // 玩具
        // =====================================================================
        /// <summary>不倒翁（高 0.16 × s）：红身子、脸上一块留白，只点了一只眼睛——愿没还上。</summary>
        static void Daruma(MeshBatch mb, Vector3 at, float s, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 L(float x, float y, float z) => at + q * (new Vector3(x, y, z) * s);
            var prof = new[] { P2(0f, 0f), P2(.045f, .003f), P2(.072f, .035f), P2(.08f, .08f), P2(.07f, .125f), P2(.045f, .152f), P2(0f, .162f) };
            var sp = new Vector2[prof.Length];
            for (int i = 0; i < prof.Length; i++) sp[i] = prof[i] * s;
            Lathe(mb, D.PlasticRed, at, q, sp, 16);
            mb.Sphere(D.PaperOld, L(0f, .1f, .056f), new Vector3(.044f, .04f, .024f) * s, 9, q);
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sphere(D.CeramicWhite, L(sx * .018f, .108f, .0745f), new Vector3(.012f, .012f, .005f) * s, 7, q);
                Tube(mb, D.Black, new[] { L(sx * .034f, .128f, .062f), L(sx * .02f, .13f, .074f), L(sx * .006f, .122f, .079f) }, .003f * s, 4);
                Tube(mb, D.Black, new[] { L(sx * .034f, .088f, .066f), L(sx * .02f, .076f, .076f), L(sx * .008f, .074f, .08f) }, .003f * s, 4);
            }
            mb.Sphere(D.Black, L(-.018f, .108f, .0785f), new Vector3(.005f, .005f, .003f) * s, 5, q);
            Tube(mb, D.Gold, new[] { L(-.03f, .045f, .066f), L(0f, .035f, .074f), L(.03f, .045f, .066f) }, .0025f * s, 4);
        }

        /// <summary>小芥子（高 0.2 × s）：细长的木身子上几道红线，圆头、黑发、两点眼睛。</summary>
        static void Kokeshi(MeshBatch mb, Vector3 at, float s, bool redBody)
        {
            var id = Quaternion.identity;
            Vector2[] Sc(Vector2[] a) { var b = new Vector2[a.Length]; for (int i = 0; i < a.Length; i++) b[i] = a[i] * s; return b; }
            Lathe(mb, redBody ? D.PlasticRed : D.TrimLight, at, id, Sc(new[] { P2(0f, 0f), P2(.022f, 0f), P2(.024f, .008f), P2(.02f, .06f), P2(.023f, .11f), P2(.016f, .123f) }), 12);
            foreach (float y in new[] { .02f, .03f, .096f })
                Hoop(mb, redBody ? D.Black : D.PlasticRed, at + Vector3.up * (y * s), Vector3.up, (y < .05f ? .0232f : .0225f) * s, .0014f * s, 12, 4);
            Lathe(mb, D.TrimLight, at, id, Sc(new[] { P2(.012f, .118f), P2(.028f, .135f), P2(.034f, .16f), P2(.03f, .185f), P2(.015f, .198f), P2(0f, .2f) }), 12);
            Lathe(mb, D.Black, at, id, Sc(new[] { P2(.0325f, .176f), P2(.031f, .187f), P2(.016f, .201f), P2(0f, .203f) }), 12);
            foreach (float sx in new[] { -1f, 1f })
                mb.Sphere(D.Black, at + new Vector3(sx * .011f, .16f, .0325f) * s, new Vector3(.004f, .0018f, .002f) * s, 5);
            mb.Sphere(D.PlasticRed, at + new Vector3(0f, .148f, .0325f) * s, new Vector3(.003f, .002f, .002f) * s, 5);
        }

        /// <summary>陀螺：木头车的，顶面一圈红漆，斜倒着。</summary>
        static void Koma(MeshBatch mb, Vector3 at, float yaw, float tilt)
        {
            var q = Quaternion.Euler(0f, yaw, tilt);
            Lathe(mb, D.TrimMid, at, q, new[] { P2(0f, 0f), P2(.004f, .006f), P2(.03f, .034f), P2(.05f, .044f) }, 14);
            Lathe(mb, D.PlasticRed, at, q, new[] { P2(.05f, .044f), P2(.05f, .054f), P2(.03f, .057f) }, 14);
            Lathe(mb, D.TrimMid, at, q, new[] { P2(.03f, .057f), P2(.012f, .058f), P2(.008f, .085f), P2(0f, .087f) }, 10);
        }

        /// <summary>手鞠：红线球上缠着几道金线、白线。</summary>
        static void Temari(MeshBatch mb, Vector3 c, float r)
        {
            mb.Sphere(D.ClothRed, c, Vector3.one * r, 14);
            Hoop(mb, D.Gold, c, Vector3.up, r * 1.004f, r * .03f, 18, 4);
            Hoop(mb, D.Gold, c, Vector3.right, r * 1.004f, r * .03f, 18, 4);
            Hoop(mb, D.Gold, c, Vector3.forward, r * 1.004f, r * .03f, 18, 4);
            Hoop(mb, D.ClothWhite, c, new Vector3(1f, 1f, 0f), r * 1.004f, r * .025f, 18, 4);
            Hoop(mb, D.ClothWhite, c, new Vector3(-1f, 1f, 0f), r * 1.004f, r * .025f, 18, 4);
            Hoop(mb, D.ClothIndigo, c, new Vector3(0f, 1f, 1f), r * 1.004f, r * .025f, 18, 4);
        }

        /// <summary>一片两面都看得见的三角形（折纸）。</summary>
        static void PaperTri(MeshBatch mb, Material m, Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            var uv = new[] { new Vector2(0, 0), new Vector2(.05f, 0), new Vector2(0, .05f) };
            mb.Poly(m, new[] { a, b, c }, uv, n);
            mb.Poly(m, new[] { a - n * .0004f, b - n * .0004f, c - n * .0004f }, uv, -n);
        }

        /// <summary>纸鹤（翼展 0.12 × s）。</summary>
        static void PaperCrane(MeshBatch mb, Vector3 at, float yaw, float s)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 L(float x, float y, float z) => at + q * (new Vector3(x, y, z) * s);
            var m = D.Paper;
            foreach (float sx in new[] { -1f, 1f })
            {
                PaperTri(mb, m, L(0f, .028f, .02f), L(0f, .028f, -.02f), L(sx * .06f, .05f, -.004f));   // 翅膀
                PaperTri(mb, m, L(0f, .03f, .022f), L(0f, .03f, -.022f), L(sx * .014f, .002f, 0f));     // 身子的两片
            }
            PaperTri(mb, m, L(0f, .012f, .012f), L(0f, .03f, .02f), L(0f, .06f, .048f));                // 脖子
            PaperTri(mb, m, L(0f, .06f, .048f), L(0f, .052f, .046f), L(0f, .05f, .062f));               // 头
            PaperTri(mb, m, L(0f, .012f, -.012f), L(0f, .03f, -.02f), L(0f, .065f, -.045f));            // 尾
        }

        /// <summary>坛子：鼓肚、收口；tied = 口上蒙着纸、用稻草绳扎紧（封着的），否则是敞着的空坛。</summary>
        static void Jar(MeshBatch mb, Vector3 at, float r, float h, Material m, bool tied)
        {
            var id = Quaternion.identity;
            Lathe(mb, m, at, id, new[]
            {
                P2(0f, 0f), P2(.6f * r, 0f), P2(.72f * r, .03f * h), P2(r, .42f * h), P2(.94f * r, .68f * h), P2(.64f * r, .85f * h), P2(.56f * r, .9f * h), P2(.6f * r, .94f * h),
            }, 18);
            if (tied)
            {
                Lathe(mb, D.PaperOld, at, id, new[] { P2(.66f * r, .84f * h), P2(.64f * r, .93f * h), P2(.6f * r, .965f * h), P2(.3f * r, .98f * h), P2(0f, .98f * h) }, 14);
                Hoop(mb, D.Straw, at + Vector3.up * (.895f * h), Vector3.up, .62f * r, Mathf.Max(.004f, .03f * r), 14, 5);
            }
            else
                Lathe(mb, m, at, id, new[] { P2(.6f * r, .94f * h), P2(.55f * r, .95f * h), P2(.5f * r, .9f * h), P2(.5f * r, .45f * h) }, 14);
        }

        // =====================================================================
        // 碁
        // =====================================================================
        static readonly Vector2[] GoStoneProfile = { P2(0f, 0f), P2(.007f, .0004f), P2(.011f, .0024f), P2(.007f, .0045f), P2(0f, .005f) };

        static void GoStone(MeshBatch mb, Vector3 at, bool black) => Lathe(mb, black ? D.Black : D.CeramicWhite, at, Quaternion.identity, GoStoneProfile, 8);

        /// <summary>碁盤的脚（梔子形）。</summary>
        static void GobanLeg(MeshBatch mb, Vector3 at, float h) =>
            Lathe(mb, D.TrimMid, at, Quaternion.identity, new[] { P2(.02f, 0f), P2(.03f, .012f), P2(.048f, h * .45f), P2(.05f, h * .62f), P2(.032f, h * .86f), P2(.036f, h) }, 12);

        /// <summary>碁笥：木头车的圆钵，里面是空的。</summary>
        static void GoBowl(MeshBatch mb, Vector3 at) =>
            Lathe(mb, D.TrimMid, at, Quaternion.identity, new[]
            {
                P2(0f, 0f), P2(.04f, 0f), P2(.06f, .018f), P2(.067f, .045f), P2(.06f, .07f), P2(.052f, .075f), P2(.047f, .07f), P2(.056f, .045f), P2(.04f, .014f), P2(0f, .012f),
            }, 16);

        /// <summary>碁笥的盖子，翻过来放着（当碟子装提掉的子）。</summary>
        static void GoLid(MeshBatch mb, Vector3 at) =>
            Lathe(mb, D.TrimMid, at, Quaternion.identity, new[]
            {
                P2(0f, 0f), P2(.03f, 0f), P2(.058f, .01f), P2(.064f, .024f), P2(.06f, .028f), P2(.052f, .016f), P2(0f, .01f),
            }, 16);

        // =====================================================================
        // 便所
        // =====================================================================
        /// <summary>朝顔（小便器）：白瓷的喇叭口（厚口沿、里面是空的）斜靠在瓷座上，下面排水管进地；铜冲水管从阀门下来接进口沿；两边各一块木隔板。</summary>
        static void DrawAsagao(Kit k, System.Random rng)
        {
            var mb = k.mb;
            var id = Quaternion.identity;
            BevelBox(mb, D.CeramicWhite, new Vector3(0f, .36f, .035f), new Vector3(.19f, .42f, .07f), .02f);
            k.Push(new Vector3(0, .2f, .15f), 0f, 16f);
            Lathe(mb, D.CeramicWhite, Vector3.zero, id, new[]
            {
                P2(.04f, 0f), P2(.05f, .02f), P2(.072f, .12f), P2(.118f, .22f), P2(.152f, .285f), P2(.166f, .3f), P2(.163f, .314f), P2(.15f, .309f),
                P2(.134f, .27f), P2(.1f, .19f), P2(.06f, .1f), P2(.036f, .03f), P2(0f, .022f),
            }, 24);
            mb.Cyl(D.Ash, new Vector3(0f, .03f, 0f), .034f, .034f, .002f, 12);
            mb.Cyl(D.Iron, new Vector3(0f, .032f, 0f), .013f, .013f, .004f, 8);
            // 口沿里一圈发黄的尿垢
            LatheArc(mb, D.Ash, Vector3.zero, id, new[] { P2(.07f, .123f), P2(.045f, .055f) }, 10, 200f, 340f);
            k.Pop();
            mb.Cyl(D.Iron, new Vector3(0f, 0f, .15f), .024f, .024f, .21f, 8);
            Lathe(mb, D.Iron, new Vector3(0f, 0f, .15f), id, new[] { P2(.06f, 0f), P2(.06f, .006f), P2(.03f, .014f) }, 12);
            Tube(mb, D.Brass, new[] { new Vector3(0f, 1.24f, .045f), new Vector3(0f, .64f, .045f), new Vector3(0f, .57f, .06f), new Vector3(0f, .52f, .1f) }, .011f, 7);
            Lathe(mb, D.Brass, new Vector3(0f, 1.24f, .045f), id, new[] { P2(.012f, 0f), P2(.026f, .01f), P2(.027f, .06f), P2(.014f, .07f), P2(0f, .072f) }, 10);
            mb.Cyl(D.Brass, new Vector3(-.04f, 1.325f, .045f), .006f, .006f, .08f, 6, true, AlongX);
            mb.Cyl(D.Brass, new Vector3(0f, 1.31f, .045f), .008f, .008f, .02f, 6);
            foreach (float sx in new[] { -.45f, .45f })
            {
                BevelBox(mb, D.TrimMid, new Vector3(sx, .74f, .27f), new Vector3(.025f, 1.24f, .52f), .004f);
                BevelBox(mb, D.TrimLight, new Vector3(sx, 1.3725f, .27f), new Vector3(.035f, .025f, .54f), .004f);
                for (float z = .08f; z < .5f; z += .12f) k.Bb(D.BeamDark, sx, .2f, z, .027f, 1.1f, .006f);
            }
            k.C(D.Straw, 0, .001f, .3f, .17f, .2f, .001f, 14); // 地上发黄的尿渍
        }

        /// <summary>
        /// 和式蹲便（昭和的水洗式）：瓷砖地台上一个长圆形的白瓷便器（口沿高出地台一圈），朝里墙那头一个半圆的金隠し；
        /// 墙上高处一个瓷水箱，铁管下来接到便器，一根拉链垂着；隔间西板上一个落とし紙的木盒；墙上一个插着枯花的一轮插。
        /// 局部原点 = 北墙脚中点、+z 朝南；sideX = 西板内侧在局部的 x（yaw 180 时西边是 +x）。
        /// </summary>
        static void DrawWashiki(Kit k, System.Random rng, float sideX)
        {
            var mb = k.mb;
            var id = Quaternion.identity;
            float pw = Mathf.Min(.9f, 2 * sideX - .02f);
            k.Bb(D.BathTile, 0, 0, .68f, pw, .08f, 1.3f);
            BevelBox(mb, D.TrimMid, new Vector3(0f, .04f, 1.34f), new Vector3(pw, .08f, .03f), .004f);
            const float y = .08f, cz = .66f;
            k.PushS(new Vector3(0, y, cz), Vector3.zero, new Vector3(1f, 1f, 1.8f));
            Lathe(mb, D.CeramicWhite, Vector3.zero, id, new[]
            {
                P2(.17f, -.002f), P2(.174f, .022f), P2(.162f, .032f), P2(.148f, .03f), P2(.132f, .016f), P2(.1f, .007f), P2(0f, .005f),
            }, 26);
            mb.Cyl(D.Ash, new Vector3(0f, .0085f, 0f), .118f, .118f, .002f, 20);
            mb.Cyl(D.Water, new Vector3(0f, .0095f, .06f), .05f, .05f, .002f, 12);
            mb.Cyl(D.Black, new Vector3(0f, .011f, .07f), .02f, .02f, .002f, 8);
            k.Pop();
            // 金隠し：朝墙那头的半个圆罩（里外两面）
            LatheArc(mb, D.CeramicWhite, new Vector3(0f, y + .02f, cz - .2f), id, new[]
            {
                P2(.165f, 0f), P2(.16f, .08f), P2(.125f, .145f), P2(.06f, .18f), P2(0f, .186f),
            }, 14, 180f, 360f);
            LatheArc(mb, D.CeramicWhite, new Vector3(0f, y + .02f, cz - .2f), id, new[]
            {
                P2(0f, .172f), P2(.055f, .166f), P2(.112f, .134f), P2(.146f, .075f), P2(.151f, 0f),
            }, 14, 180f, 360f);
            // 高水箱（瓷的，木盖）、铁管、拉链
            BevelBox(mb, D.CeramicWhite, new Vector3(-.14f, 1.67f, .12f), new Vector3(.44f, .24f, .2f), .02f);
            BevelBox(mb, D.TrimMid, new Vector3(-.14f, 1.8f, .12f), new Vector3(.46f, .02f, .22f), .004f);
            var p0 = new Vector3(-.14f, 1.55f, .045f);
            Tube(mb, D.Iron, new[] { p0, new Vector3(-.14f, .3f, .045f), new Vector3(-.12f, .18f, .08f), new Vector3(0f, y + .12f, cz - .3f) }, .017f, 8);
            foreach (float yy in new[] { .6f, 1.2f }) mb.Box(D.Iron, new Vector3(-.14f, yy, .03f), new Vector3(.06f, .02f, .03f));
            mb.Box(D.Iron, new Vector3(.02f, 1.6275f, .2f), new Vector3(.14f, .015f, .015f));
            Tube(mb, D.Iron, new[] { new Vector3(.09f, 1.62f, .2f), new Vector3(.092f, 1.3f, .2f), new Vector3(.088f, 1.04f, .2f) }, .003f, 4);
            Lathe(mb, D.TrimLight, new Vector3(.088f, .955f, .2f), id, new[] { P2(0f, 0f), P2(.012f, .005f), P2(.017f, .035f), P2(.012f, .075f), P2(.004f, .085f) }, 8);
            // 落とし紙的木盒（西板上，敞口，里面一沓纸）
            BevelBox(mb, D.TrimMid, new Vector3(sideX - .045f, .595f, .74f), new Vector3(.08f, .15f, .2f), .004f);
            k.Bb(D.Paper, sideX - .045f, .64f, .74f, .06f, .045f, .17f);
            k.Br(D.Paper, sideX - .2f, .085f, .9f, .12f, .002f, .1f, 0, Rf(rng, 0f, 90f), 0); // 掉在地台上的一张
            // 一轮插（干掉的花）
            Lathe(mb, D.CeramicBrown, new Vector3(.26f, 1.08f, .045f), id, new[]
            {
                P2(.02f, 0f), P2(.03f, .03f), P2(.026f, .09f), P2(.016f, .115f), P2(.02f, .13f), P2(.016f, .13f), P2(.013f, .11f),
            }, 10);
            k.B(D.Iron, .26f, 1.2f, .01f, .008f, .008f, .03f);
            Tube(mb, D.Straw, new[] { new Vector3(.26f, 1.2f, .045f), new Vector3(.235f, 1.32f, .05f), new Vector3(.212f, 1.43f, .045f) }, .0025f, 4);
            k.S(D.Bark, .21f, 1.43f, .045f, .02f, .016f, .02f, 6);
            Tube(mb, D.Straw, new[] { new Vector3(.27f, 1.2f, .05f), new Vector3(.31f, 1.3f, .06f), new Vector3(.335f, 1.34f, .06f) }, .002f, 4);
            KitShot(k, "r5i_washiki", new Vector3(0f, .15f, cz), .9f, 1.0f);
        }
    }
}
