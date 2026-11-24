using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第六轮（院外风景）第二部分：树（北坡杉林、梯田尽头那排树、东西两边的林子）、墙外的竹林、柿子树、
    /// 西边邻家塌了一角的空屋、东边铁皮顶的旧仓房。
    /// 摆放高度：生成时给院外地面临时加一个 MeshCollider 往下打射线（<see cref="OutGround"/>），摆完就删，场景里不留碰撞体。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        static Material cedarMat, bambooMat, susukiMat;
        static MeshCollider outProbe;

        static void OutskirtsLoad()
        {
            // 第六轮 b：杉叶、竹叶、芒草（薄）是 prep_tex_r6b.py 画的面片（颜色取自已经下载的 CC0 贴图）。
            // 第一版借的黑松针面片只有 18% 是实的，杉林远看是一根根透光的细杆
            cedarMat = FloraMat("MX_SugiSpray", "cedar_spray", new Color(.66f, .70f, .62f, 1f));
            bambooMat = FloraMat("MX_BambooSpray", "bamboo_spray", new Color(.62f, .66f, .56f, 1f));
            susukiMat = FloraMat("MX_SusukiTuft", "susuki_tuft", new Color(.58f, .55f, .50f, 1f));
        }

        /// <summary>院外地面在 (x, z) 的高度。</summary>
        static float OutGround(float x, float z)
        {
            if (outProbe != null && outProbe.Raycast(new Ray(new Vector3(x, 600f, z), Vector3.down), out var hit, 1200f)) return hit.point.y;
            return 0f;
        }

        /// <summary>院外的审图机位（机位直接给，不夹进院墙里）。ArtSnapshot 按“探索者视角 + 箱女视角 + 白天”各拍一张。</summary>
        static void ShotOut(string name, Vector3 cam, Vector3 look)
        {
            name = "r6_" + name;
            foreach (var s in RuinShots)
                if (s.name == name) return;
            RuinShots.Add((name, cam, look));
        }

        // =====================================================================
        // 树
        // =====================================================================
        static void OutskirtsTrees(Transform parent, Vector3[] lastEdge)
        {
            var north = new MeshBatch();
            var south = new MeshBatch();
            var sides = new MeshBatch();

            // 北坡杉林：墙后近处几排细做（从后院、二楼北窗看得到），往上越来越简单
            float zA = OZ1 + NorthStrip + NorthBatter + 1.4f;
            int nNorth = 0;
            for (float z = zA; z < OZ1 + 100f; z += z < OZ1 + 50f ? 5.2f : 8f)
                for (float x = -82f; x < 132f; x += z < OZ1 + 50f ? 5.4f : 8.5f)
                {
                    if (orng.Next(10) == 0) continue; // 林子里的空当
                    float px = x + OR(-2f, 2f), pz = z + OR(-1.8f, 1.8f);
                    bool near = pz < OZ1 + 26f && px > OX0 - 22f && px < OX1 + 22f;
                    int detail = near ? 2 : pz < OZ1 + 50f ? 1 : 0;
                    var b = new Vector3(px, OutGround(px, pz) - .3f, pz);
                    if (orng.Next(16) == 0) BareTree(north, b, OR(9f, 14f), detail > 0);
                    else Cedar(north, b, OR(18f, 27f), detail);
                    nNorth++;
                }
            north.Build(parent, "Outskirts_CedarHill", false);

            // 梯田尽头：陡坡顶上一排杉和落了叶的杂木（从大门、二楼南窗看是对面山前的一道黑剪影），坡下稀一点。
            // 第六轮 c：按低频噪声分成一丛丛、高矮不齐，丛和丛之间留空当（第二版从大门、路上看像一把整齐的梳子）
            int nSouth = 0;
            for (float x = OutXMin + 3f; x < OutXMax - 3f; x += OR(3.6f, 5.6f))
            {
                var e = EdgeAt(lastEdge, x);
                float clump = Mathf.PerlinNoise(x * .04f + 3.1f, 7.7f), tall = Mathf.PerlinNoise(x * .11f + 11.3f, 2.9f);
                if (clump < .3f) continue; // 空当：从路上看得见后面的山
                float hk = Mathf.Lerp(.65f, 1.2f, tall) * Mathf.Lerp(.85f, 1.05f, clump);
                foreach (float back in new[] { OR(1.2f, 4f), OR(5.5f, 10f) })
                {
                    if (back < 5f && clump < .42f) continue; // 丛的边上只留后排
                    float pz = e.z - back, px = x + OR(-1.5f, 1.5f);
                    var b = new Vector3(px, OutGround(px, pz) - .3f, pz);
                    if (orng.Next(4) == 0) BareTree(south, b, OR(8f, 13f) * hk, false);
                    else Cedar(south, b, OR(14f, 22f) * hk, 1);
                    nSouth++;
                }
                if (orng.Next(3) == 0)
                {
                    float pz = e.z - OR(18f, 60f), px = x + OR(-2f, 2f);
                    Cedar(south, new Vector3(px, OutGround(px, pz) - .3f, pz), OR(16f, 24f), 0);
                    nSouth++;
                }
            }
            south.Build(parent, "Outskirts_ValleyTrees", false);

            // 东西两边的林子（离院子 30 m 以外，主要是从墙头上露出来的树梢）
            int nSides = 0;
            foreach (bool west in new[] { true, false })
            {
                float xa = west ? OutXMin + 3f : TerEastA, xb = west ? TerWestA : OutXMax - 3f;
                for (float x = xa; x < xb; x += 8f)
                    for (float z = OZ1 - 1f; z > -100f; z -= 8.5f)
                    {
                        float px = x + OR(-2.5f, 2.5f), pz = z + OR(-2.5f, 2.5f);
                        if (pz < OZ0 + 2.5f && pz > ZVerge - 2f) continue; // 村道
                        if (pz < ZVerge && TerFade(px) < .45f) continue;   // 梯田
                        var b = new Vector3(px, OutGround(px, pz) - .3f, pz);
                        if (orng.Next(5) == 0) BareTree(sides, b, OR(9f, 13f), false);
                        else Cedar(sides, b, OR(16f, 25f), 0);
                        nSides++;
                    }
            }
            // 柿子树（东墙外，探过墙头）
            Persimmon(sides, new Vector3(OX1 + 2.3f, 0f, 26f));
            sides.Build(parent, "Outskirts_SideTrees", false);

            WestBamboo(parent);
            Debug.Log($"[Hako Art] 院外的树：北坡 {nNorth}、谷边 {nSouth}、东西 {nSides} 棵");
            ShotOut("cedars_backyard", new Vector3(OX0 + 3.2f, 1.7f, OZ1 - 7f), new Vector3(OX0 + 9f, 13f, OZ1 + 10f));
        }

        /// <summary>一条线（按 x 递增的点）在 x 处插值。</summary>
        static Vector3 EdgeAt(Vector3[] line, float x)
        {
            if (x <= line[0].x) return line[0];
            for (int i = 0; i < line.Length - 1; i++)
                if (x <= line[i + 1].x)
                    return Vector3.Lerp(line[i], line[i + 1], Mathf.InverseLerp(line[i].x, line[i + 1].x, x));
            return line[line.Length - 1];
        }

        /// <summary>
        /// 杉：笔直的干，下半截是光干（人工林修过枝），树冠从 36%–48% 高处起是窄圆锥。
        /// 树冠 = 两张交叉的竖面片当“芯”（远看是一整块实心的锥）+ 一圈圈横出去、往下垂的枝叶面片。
        /// detail 2 = 近处（13 层 × 7 片），1 = 中景（8 × 5），0 = 远处（5 × 4）。
        /// </summary>
        static void Cedar(MeshBatch mb, Vector3 b, float h, int detail)
        {
            var axis = new Vector3(OR(-.03f, .03f), 1f, OR(-.03f, .03f)).normalized;
            float r0 = h * .008f + .06f;
            mb.Cyl(D.Bark, b, r0, r0 * .22f, h * .93f, detail == 2 ? 7 : 5, false, Quaternion.FromToRotation(Vector3.up, axis));
            if (cedarMat == null) return;
            int Cell() => orng.Next(14) == 0 ? 3 : orng.Next(3); // 偶尔一片发红褐（枯了的、冬天变色的）
            float crown0 = h * OR(.36f, .48f), top = h * .97f, crownR = h * OR(.095f, .12f);
            float roll = OR(0f, 90f);
            var side0 = Vector3.Cross(axis, Vector3.forward).normalized;
            for (int i = 0; i < 2; i++)
            {
                var right = Quaternion.AngleAxis(roll + i * 90f, axis) * side0;
                FloraCard(mb, cedarMat, b + axis * crown0, axis, right, crownR * 2.1f, top - crown0, FloraCell(Cell()), Vector3.up);
            }
            int whorls = detail == 2 ? 13 : detail == 1 ? 8 : 5, cards = detail == 2 ? 7 : detail == 1 ? 5 : 4;
            for (int k = 0; k < whorls; k++)
            {
                float t = (k + OR(0f, .6f)) / whorls; // 0 = 树冠底，1 = 树梢
                float y = Mathf.Lerp(crown0, top - 1f, t);
                float rr = crownR * (1f - t * .85f) * OR(.85f, 1.15f);
                float a0 = OR(0f, 360f);
                var at0 = b + axis * y;
                for (int c = 0; c < cards; c++)
                {
                    float a = (a0 + c * 360f / cards + OR(-18f, 18f)) * Mathf.Deg2Rad;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var grow = (radial - Vector3.up * OR(.15f, .45f)).normalized;
                    var right = Quaternion.AngleAxis(OR(20f, 70f), grow) * Vector3.Cross(Vector3.up, radial);
                    float size = rr * OR(1.15f, 1.45f) + .6f;
                    FloraCard(mb, cedarMat, at0 - grow * (size * .18f), grow, right, size * .95f, size, FloraCell(Cell()), (radial + Vector3.up * .6f).normalized);
                }
            }
            FloraCross(mb, cedarMat, b + axis * (top - h * .08f), axis, h * .08f + .5f, Cell(), Vector3.up, OR(0f, 180f));
        }

        /// <summary>落了叶的杂木（栗、栎一类）：干 + 四五根主枝，每根两三根小枝，没有叶子。</summary>
        static void BareTree(MeshBatch mb, Vector3 b, float h, bool fine)
        {
            float r0 = h * .016f + .05f;
            var top = b + new Vector3(OR(-.4f, .4f), h * .55f, OR(-.4f, .4f));
            Limb(mb, D.Bark, new[] { b - Vector3.up * .2f, Vector3.Lerp(b, top, .5f) + new Vector3(OR(-.2f, .2f), 0f, OR(-.2f, .2f)), top },
                new[] { r0, r0 * .75f, r0 * .5f }, fine ? 6 : 5);
            int mains = 4 + orng.Next(2);
            float a0 = OR(0f, 360f);
            for (int i = 0; i < mains; i++)
            {
                float a = (a0 + i * 360f / mains + OR(-20f, 20f)) * Mathf.Deg2Rad;
                var start = Vector3.Lerp(b, top, OR(.7f, 1f));
                var d = new Vector3(Mathf.Cos(a), OR(.9f, 1.6f), Mathf.Sin(a)).normalized;
                float len = h * OR(.32f, .45f);
                var mid = start + d * (len * .5f) + Vector3.up * (len * .08f);
                var end = start + d * len + new Vector3(OR(-.3f, .3f), OR(-.2f, .3f), OR(-.3f, .3f)) * len * .3f;
                Limb(mb, D.Bark, new[] { start, mid, end }, new[] { r0 * .45f, r0 * .3f, r0 * .12f }, 4);
                int twigs = 2 + orng.Next(2);
                for (int k = 0; k < twigs; k++)
                {
                    var s = Vector3.Lerp(mid, end, OR(0f, .8f));
                    var td = (d + RandDir(orng) * .9f + Vector3.up * .5f).normalized;
                    Limb(mb, D.Bark, new[] { s, s + td * len * OR(.25f, .45f) }, new[] { r0 * .14f, r0 * .04f }, 3);
                }
            }
        }

        /// <summary>
        /// 没人摘的柿子树（老树）：干粗、离地 1.9 m 就分叉，六根主枝往四周斜着长，西边（墙那边）的长、翘过墙头；
        /// 叶子掉光了，枝梢挂满柿子（压暗：远看是暗橙色的点，不和仏壇的红抢）。墙头以下没有柿子（够得着的早被摘了）。
        /// </summary>
        static void Persimmon(MeshBatch mb, Vector3 b)
        {
            b.y = OutGround(b.x, b.z) - .15f;
            var rng = new System.Random(6025);
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var lean = new Vector3(-Mathf.Sin(12f * Mathf.Deg2Rad), Mathf.Cos(12f * Mathf.Deg2Rad), .04f).normalized;
            var fork = b + lean * 1.95f;
            Limb(mb, D.Bark, new[] { b - Vector3.up * .2f, b + lean * .9f + new Vector3(.07f, 0f, -.05f), fork },
                new[] { .27f, .22f, .19f }, 9);
            var tips = new List<(Vector3 p, Vector3 d)>();
            var mains = new[]
            {
                new Vector3(-.72f, .7f, .08f), new Vector3(-.5f, .78f, -.42f), new Vector3(-.42f, .8f, .48f),
                new Vector3(.05f, 1f, .02f), new Vector3(.5f, .75f, .35f), new Vector3(.42f, .72f, -.45f),
            };
            float[] lens = { 2.3f, 2.0f, 2.0f, 1.8f, 1.6f, 1.6f };
            for (int i = 0; i < mains.Length; i++)
                Grow(mb, rng, fork + new Vector3(R(-.08f, .08f), R(-.1f, .15f), R(-.08f, .08f)), mains[i].normalized, lens[i], .11f, 1, 4, .72f, tips);
            float minY = Levels[GFence].wallTop + .3f;
            int fruit = 0;
            foreach (var (p, d) in tips)
            {
                if (rng.Next(10) >= 7 || fruit >= 120) continue;
                int n = rng.Next(3) == 0 ? 2 : 1;
                for (int j = 0; j < n; j++)
                {
                    var at = p - Vector3.up * R(.04f, .08f) + d * R(-.05f, .03f) + new Vector3(R(-.05f, .05f), 0f, R(-.05f, .05f));
                    if (at.y < minY) continue;
                    float k = R(.92f, 1.1f);
                    mb.Sphere(D.Persimmon, at, new Vector3(.044f, .037f, .044f) * k, 6);
                    mb.Box(D.BeamDark, at + Vector3.up * (.036f * k), new Vector3(.03f, .008f, .03f) * k);
                    fruit++;
                }
            }
            // 剩下的几片枯叶
            if (floraShrub != null)
                foreach (var (p, d) in tips)
                {
                    if (rng.Next(7) != 0) continue;
                    FloraCross(mb, floraShrub, p, (d + Vector3.up * .3f).normalized, .3f, 3, (d + Vector3.up).normalized, (float)rng.NextDouble() * 180f);
                }
            Debug.Log($"[Hako Art] 柿子树：枝梢 {tips.Count} 个，柿子 {fruit} 个");
            ShotOut("persimmon", new Vector3(OX1 - 4.2f, 1.6f, b.z - 3.6f), new Vector3(OX1 - .5f, 4.4f, b.z));
        }

        // =====================================================================
        // 西墙外的竹林：杆往东弯、竹梢探过墙头
        // =====================================================================
        static void WestBamboo(Transform parent)
        {
            var mb = new MeshBatch();
            var rng = new System.Random(6026);
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            float xa = OX0 - 12.5f, xb = OX0 - .5f, za = 8f, zb = OZ1 - 1f;
            int n = 0, leafCards = 0;
            for (int i = 0; i < 160 && n < 92; i++)
            {
                // 墙边密、往西稀
                float u = R(0f, 1f);
                float x = Mathf.Lerp(xb, xa, u * u), z = R(za, zb);
                if (rng.Next(5) == 0 && x < OX0 - 6f) continue;
                n++;
                var b = new Vector3(x, OutGround(x, z) - .1f, z);
                float h = R(8f, 12f), r = R(.038f, .058f);
                // 离墙越近越往院子里弯
                float arch = Mathf.Lerp(.42f, .12f, Mathf.InverseLerp(OX0 - .5f, OX0 - 12f, x)) * R(.75f, 1.2f);
                var bend = new Vector3(1f, 0f, R(-.35f, .35f)).normalized;
                const int segs = 10;
                var pts = new Vector3[segs + 1];
                var rad = new float[segs + 1];
                for (int k = 0; k <= segs; k++)
                {
                    float t = k / (float)segs;
                    pts[k] = b + Vector3.up * (h * t * (1f - arch * .25f * t * t)) + bend * (h * arch * t * t * t);
                    rad[k] = Mathf.Lerp(r, r * .3f, t);
                }
                Limb(mb, D.Bamboo, pts, rad, 5);
                if (bambooMat == null) continue;
                // 第六轮 b：竹叶换成面片（prep_tex_r6b.py）。杆子 40% 高处往上每节两边各一根小枝，枝梢挂两三张往外垂的叶片
                float az = R(0f, 360f);
                for (float s = R(.4f, .48f); s < .96f; s += R(.065f, .085f))
                {
                    float fk = s * segs;
                    int k0 = Mathf.Min((int)fk, segs - 1);
                    var p = Vector3.Lerp(pts[k0], pts[k0 + 1], fk - k0);
                    az += 180f + R(-35f, 35f);
                    for (int side = 0; side < 2; side++)
                    {
                        float a = (az + side * 180f + R(-30f, 30f)) * Mathf.Deg2Rad;
                        // 小枝往外、往上，往弯的方向（院子那边）多一点
                        var dir = (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) + bend * .45f + Vector3.up * R(.3f, .8f)).normalized;
                        var tw = p + dir * (R(.4f, .8f) * (1.15f - s * .45f));
                        Limb(mb, D.Bamboo, new[] { p, tw }, new[] { .009f, .004f }, 3);
                        int nc = rng.Next(3) == 0 ? 3 : 2;
                        for (int c = 0; c < nc; c++)
                        {
                            var g = (dir * R(.4f, 1f) + Vector3.down * R(.35f, 1f) + RandDir(rng) * .35f).normalized;
                            var right = Vector3.Cross(g, Vector3.up);
                            right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
                            right = Quaternion.AngleAxis(R(-40f, 40f), g) * right;
                            float size = R(.85f, 1.3f) * (1.2f - s * .35f);
                            var at = Vector3.Lerp(p, tw, R(.55f, 1f)) - g * (size * .12f);
                            FloraCard(mb, bambooMat, at, g, right, size * .85f, size, FloraCell(rng.Next(9) == 0 ? 3 : rng.Next(3)), (Vector3.up + g * .3f).normalized);
                            leafCards++;
                        }
                    }
                }
                // 竹梢：往弯的方向垂下来的一束
                var tg = (bend * .8f + Vector3.down * .5f).normalized;
                FloraCard(mb, bambooMat, pts[segs] - tg * .1f, tg, Vector3.Cross(tg, Vector3.up).normalized, .9f, 1.1f, FloraCell(rng.Next(3)), Vector3.up);
                leafCards++;
            }
            mb.Build(parent, "Outskirts_Bamboo", false);
            Debug.Log($"[Hako Art] 墙外竹林：{n} 根，竹叶面片 {leafCards} 张");
            ShotOut("bamboo_over_wall", new Vector3(OX0 + 5.5f, 1.6f, 30f), new Vector3(OX0 - .5f, 6f, 31f));
        }

        // =====================================================================
        // 房子：西边邻家的空屋、东边的铁皮仓房
        // =====================================================================
        static void OutskirtsBuildings(Transform parent)
        {
            var ruin = new MeshBatch();
            NeighbourRuin(ruin);
            ruin.Build(parent, "Outskirts_NeighbourRuin", false);
            var barn = new MeshBatch();
            TinBarn(barn);
            barn.Build(parent, "Outskirts_Barn", false);
        }

        /// <summary>四个角点之间一块有厚度的板（屋面、塌下来的瓦）：顶面 UV = (x, 沿坡)，米为单位。</summary>
        static void Slab(MeshBatch mb, Material m, Vector3 p00, Vector3 p10, Vector3 p11, Vector3 p01, float thick)
        {
            var n = Vector3.Cross(p01 - p00, p10 - p00).normalized;
            if (n.y < 0f) n = -n;
            var off = -n * thick;
            float slope = (p01 - p00).magnitude;
            var top = new[] { p00, p10, p11, p01 };
            var uv = new[] { new Vector2(p00.x, 0f), new Vector2(p10.x, 0f), new Vector2(p11.x, slope), new Vector2(p01.x, slope) };
            mb.Poly(m, top, uv, n);
            mb.Poly(m, new[] { p00 + off, p10 + off, p11 + off, p01 + off }, uv, -n);
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = top[i], b = top[(i + 1) % 4];
                var en = Vector3.Cross(b - a, n).normalized;
                var c = (p00 + p10 + p11 + p01) * .25f;
                if (Vector3.Dot(en, (a + b) * .5f - c) < 0f) en = -en;
                float l = (b - a).magnitude;
                mb.Poly(m, new[] { a, b, b + off, a + off }, new[] { new Vector2(0f, 0f), new Vector2(l, 0f), new Vector2(l, thick), new Vector2(0f, thick) }, en);
            }
        }

        /// <summary>
        /// 西边邻家的空屋：两层木造、瓦顶（东西向屋脊、屋脊 7.7 m，从院子里看在墙头上露出一大截），
        /// 东头塌了：屋脊往下塌了 1.4 m，南坡东段的瓦没了、露出椽子，一块瓦顶掉下来靠在墙上。窗洞全黑，有的钉着防雨板。
        /// </summary>
        static void NeighbourRuin(MeshBatch mb)
        {
            float x0 = -26.5f, x1 = -16f, z0 = -4f, z1 = 4.5f;
            float cx = (x0 + x1) * .5f, cz = (z0 + z1) * .5f, len = x1 - x0, dep = z1 - z0;
            float gy = Mathf.Min(OutGround(x0, z0), OutGround(x1, z0), OutGround(x0, z1), OutGround(x1, z1));
            float f2 = gy + 2.95f, eave = gy + 5.55f;
            const float ov = .65f, pitch = 26f;
            float ridge0 = eave + (dep * .5f) * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float xs = x1 - 4.2f, sag = 1.4f;
            float Ridge(float x) => ridge0 - sag * Mathf.Pow(Mathf.Clamp01((x - xs) / (x1 + ov - xs)), 1.3f);

            // 石基、墙（深色雨淋板）、里面一个不反光的黑盒子（窗洞、塌的地方看进去是黑的）
            mb.Box(D.StoneWall, new Vector3(cx, gy - .1f + .22f, cz), new Vector3(len + .12f, .64f, dep + .12f));
            mb.Box(D.RanmaDark, new Vector3(cx, (gy + eave) * .5f, cz), new Vector3(len - .1f, eave - gy - .05f, dep - .1f));
            foreach (float s in new[] { -1f, 1f })
            {
                float zw = cz + s * (dep * .5f - .04f);
                mb.Box(D.Siding, new Vector3(cx, (gy + .3f + eave) * .5f, zw), new Vector3(len, eave - gy - .3f, .08f), null, 0);
                mb.Box(D.BeamDark, new Vector3(cx, f2, zw + s * .03f), new Vector3(len + .04f, .18f, .1f)); // 二楼地板那一圈腰板
            }
            foreach (float s in new[] { -1f, 1f })
            {
                float xw = cx + s * (len * .5f - .04f);
                mb.Box(D.Siding, new Vector3(xw, (gy + .3f + eave) * .5f, cz), new Vector3(.08f, eave - gy - .3f, dep), null, 2);
            }
            // 西山墙（完整）、东山墙（跟着屋脊塌下去，顶上几块板翘着）
            foreach (bool west in new[] { true, false })
            {
                float xw = west ? x0 - .002f : x1 + .002f, top = west ? Ridge(x0) - .25f : Ridge(x1) - .25f;
                var tri = new[] { new Vector3(xw, eave, z0 + .04f), new Vector3(xw, eave, z1 - .04f), new Vector3(xw, top, cz) };
                var tuv = new[] { new Vector2(z0, eave), new Vector2(z1, eave), new Vector2(cz, top) };
                mb.Poly(D.Siding, tri, tuv, west ? Vector3.left : Vector3.right);
                mb.Poly(D.RanmaDark, tri, tuv, west ? Vector3.right : Vector3.left);
                if (!west)
                    for (int k = 0; k < 4; k++)
                        mb.Box(D.Siding, new Vector3(xw + .02f, top + OR(-.4f, .3f), cz + OR(-1.4f, 1.4f)), new Vector3(.03f, OR(.6f, 1.1f), .2f),
                            Quaternion.Euler(OR(-25f, 25f), 0f, OR(-8f, 8f)));
            }
            // 窗洞（黑）：一楼南面两个大的、玄関；二楼两个（一个钉着防雨板）；西面一个
            void Hole(Vector3 c, Vector3 size, Vector3 n) => mb.Box(D.RanmaDark, c + n * .045f, size);
            Hole(new Vector3(x0 + 2.0f, gy + 1.35f, z0), new Vector3(1.7f, 1.7f, .02f), Vector3.back);
            Hole(new Vector3(x0 + 5.0f, gy + 1.45f, z0), new Vector3(2.4f, 1.5f, .02f), Vector3.back);
            Hole(new Vector3(x1 - 1.6f, gy + 1.1f, z0), new Vector3(1.6f, 2.0f, .02f), Vector3.back); // 玄関
            Hole(new Vector3(x0 + 3.0f, f2 + 1.25f, z0), new Vector3(1.6f, 1.0f, .02f), Vector3.back);
            Hole(new Vector3(x1 - 3.6f, f2 + 1.25f, z0), new Vector3(1.6f, 1.0f, .02f), Vector3.back);
            Hole(new Vector3(x0, gy + 1.5f, cz + 1.2f), new Vector3(.02f, 1.1f, 1.6f), Vector3.left);
            Hole(new Vector3(x1, f2 + 1.2f, cz - .6f), new Vector3(.02f, 1.0f, 1.4f), Vector3.right);
            // 窗框残骸、防雨板
            foreach (float dx in new[] { -.85f, 0f, .85f })
                mb.Box(D.BeamDark, new Vector3(x0 + 2.0f + dx, gy + 1.35f, z0 - .07f), new Vector3(.04f, 1.7f, .04f));
            mb.Box(D.BeamDark, new Vector3(x0 + 5.0f, gy + 1.45f, z0 - .07f), new Vector3(2.4f, .04f, .04f));
            for (int k = 0; k < 4; k++)
                mb.Box(D.Siding, new Vector3(x1 - 3.6f + (k - 1.5f) * .4f, f2 + 1.25f - (k == 3 ? .25f : 0f), z0 - .08f), new Vector3(.38f, k == 3 ? .5f : 1.0f, .025f),
                    Quaternion.Euler(0f, 0f, k == 2 ? 6f : 0f), 1);
            // 一楼南面的下屋（小披檐），东头断了一截
            Slab(mb, D.Kawara, new Vector3(x0 - .2f, f2 + .45f, z0 - .02f), new Vector3(x1 - .9f, f2 + .45f, z0 - .02f),
                new Vector3(x1 - .9f, f2 + .05f, z0 - 1.0f), new Vector3(x0 - .2f, f2 + .05f, z0 - 1.0f), .06f);

            // 主屋顶：沿屋脊 1.5 m 一段；北坡完整（跟着塌：绕墙顶转下去，出檐那头反而翘起来）；
            // 南坡东段没瓦，露出椽子、两根檩条，下面是黑的天花
            float EOut(float x) => eave - (Ridge(x) - eave) / (dep * .5f) * ov;
            float step = 1.5f;
            for (float a = x0 - ov; a < x1 + ov - .01f; a += step)
            {
                float b = Mathf.Min(a + step, x1 + ov);
                var nA = new Vector3(a, Ridge(a), cz); var nB = new Vector3(b, Ridge(b), cz);
                var sA = new Vector3(a, EOut(a), z0 - ov); var sB = new Vector3(b, EOut(b), z0 - ov);
                Slab(mb, D.Kawara, nA, nB, new Vector3(b, EOut(b), z1 + ov), new Vector3(a, EOut(a), z1 + ov), .08f);
                bool missing = a > xs - .2f;
                if (!missing)
                {
                    Slab(mb, D.Kawara, nA, nB, sB, sA, .08f);
                    continue;
                }
                // 露出的椽子（有的断了）、黑的天花
                for (float x = a + .2f; x < b; x += .45f)
                {
                    float broken = orng.Next(4) == 0 ? OR(.35f, .75f) : 1f;
                    var p0 = new Vector3(x, Ridge(x) - .1f, cz);
                    var p1 = Vector3.Lerp(p0, new Vector3(x, EOut(x) - .1f, z0 - ov), broken);
                    Limb(mb, D.BeamDark, new[] { p0, p1 }, new[] { .04f, .04f }, 4);
                }
                foreach (float f in new[] { .35f, .7f })
                {
                    var q0 = Vector3.Lerp(nA, sA, f) - Vector3.up * .16f;
                    var q1 = Vector3.Lerp(nB, sB, f) - Vector3.up * .16f;
                    if (f > .5f && b > x1) q1 = Vector3.Lerp(q0, q1, .55f) - Vector3.up * .5f; // 断了垂下来
                    Limb(mb, D.BeamDark, new[] { q0, q1 }, new[] { .055f, .05f }, 4);
                }
                var s0 = new Vector3(a, Ridge(a) - .45f, cz); var s1 = new Vector3(b, Ridge(b) - .45f, cz);
                mb.Poly(D.RanmaDark, new[] { s0, s1, new Vector3(b, eave - .05f, z0 + .1f), new Vector3(a, eave - .05f, z0 + .1f) },
                    new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, Vector3.up);
            }
            // 屋脊瓦（跟着塌）、两头的鬼瓦
            for (float a = x0 - ov; a < x1 + ov - .01f; a += step)
            {
                float b = Mathf.Min(a + step, x1 + ov);
                Limb(mb, D.RidgeTile, new[] { new Vector3(a, Ridge(a) + .06f, cz), new Vector3(b, Ridge(b) + .06f, cz) }, new[] { .11f, .11f }, 5);
            }
            mb.Box(D.RidgeTile, new Vector3(x0 - ov, ridge0 + .12f, cz), new Vector3(.12f, .42f, .5f));
            // 掉下来的一块瓦顶，靠在一楼东南角的墙上
            Slab(mb, D.Kawara, new Vector3(x1 - 3.4f, gy + .05f, z0 - 1.9f), new Vector3(x1 - 1.4f, gy + .05f, z0 - 1.75f),
                new Vector3(x1 - 1.3f, gy + 2.2f, z0 - .25f), new Vector3(x1 - 3.3f, gy + 2.3f, z0 - .3f), .07f);
            // 墙根的杂草
            if (floraShrub != null)
                for (int i = 0; i < 26; i++)
                {
                    bool alongX = i % 2 == 0;
                    float x = alongX ? OR(x0, x1) : (orng.Next(2) == 0 ? x0 - .15f : x1 + .15f);
                    float z = alongX ? (orng.Next(2) == 0 ? z0 - .2f : z1 + .2f) : OR(z0, z1);
                    FloraCross(mb, floraShrub, new Vector3(x, OutGround(x, z) - .02f, z), (Vector3.up + RandDir(orng) * .3f).normalized, OR(.6f, 1.1f), orng.Next(5) < 3 ? 3 : orng.Next(3), Vector3.up, OR(0f, 180f));
                }
            ShotOut("ruin_from_road", new Vector3(x1 + 3.5f, gy + 1.7f, ZRoadS + .8f), new Vector3(cx + 1.5f, gy + 4.2f, cz));
            ShotOut("ruin_over_wall", new Vector3(OX0 + 7.1f, 1.6f, 8f), new Vector3(cx, 6.4f, cz));
        }

        /// <summary>
        /// 东边的铁皮仓房：木柱、锈铁皮墙和顶（波纹顺着坡往下）、南面一个大开口（黑的，推拉门拉开一半），东边一个单坡披屋，下面堆着旧木料和一个油桶。
        /// </summary>
        static void TinBarn(MeshBatch mb)
        {
            float x0 = 57.5f, x1 = 65.5f, z0 = 1.5f, z1 = 8f;
            float cx = (x0 + x1) * .5f, cz = (z0 + z1) * .5f, len = x1 - x0, dep = z1 - z0;
            float gy = Mathf.Min(OutGround(x0, z0), OutGround(x1, z0), OutGround(x0, z1), OutGround(x1, z1)) - .05f;
            float eave = gy + 3.0f, ridge = gy + 4.6f;
            const float ov = .4f;
            // 地基（低的水泥台）、里面的黑盒子
            mb.Box(D.Concrete, new Vector3(cx, gy + .06f, cz), new Vector3(len + .1f, .24f, dep + .1f));
            mb.Box(D.RanmaDark, new Vector3(cx, (gy + eave) * .5f, cz), new Vector3(len - .1f, eave - gy, dep - .1f));
            // 柱
            for (float x = x0; x <= x1 + .01f; x += len / 3f)
                foreach (float z in new[] { z0, z1 })
                    mb.Box(D.BeamDark, new Vector3(x, (gy + eave) * .5f, z), new Vector3(.12f, eave - gy, .12f));
            // 墙：南面开口 3.6 m（x0+1.2 起）
            float o0 = x0 + 1.2f, o1 = o0 + 3.6f, oTop = gy + 2.5f;
            void WallX(float a, float b, float y0, float y1, float z, float s)
            {
                if (b - a < .05f) return;
                mb.Box(D.TinRoof, new Vector3((a + b) * .5f, (y0 + y1) * .5f, z + s * .07f), new Vector3(b - a, y1 - y0, .02f), null, 0);
            }
            WallX(x0, o0, gy + .18f, eave, z0, -1f);
            WallX(o1, x1, gy + .18f, eave, z0, -1f);
            WallX(o0, o1, oTop, eave, z0, -1f);
            WallX(x0, x1, gy + .18f, eave, z1, 1f);
            foreach (float s in new[] { -1f, 1f })
            {
                float xw = s < 0 ? x0 : x1;
                mb.Box(D.TinRoof, new Vector3(xw + s * .07f, (gy + .18f + eave) * .5f, cz), new Vector3(.02f, eave - gy - .18f, dep), null, 2);
                var tri = new[] { new Vector3(xw + s * .08f, eave, z0), new Vector3(xw + s * .08f, eave, z1), new Vector3(xw + s * .08f, ridge - .05f, cz) };
                var tuv = new[] { new Vector2(z0, eave), new Vector2(z1, eave), new Vector2(cz, ridge) };
                mb.Poly(D.TinRoof, tri, tuv, new Vector3(s, 0f, 0f));
                mb.Poly(D.RanmaDark, tri, tuv, new Vector3(-s, 0f, 0f));
            }
            // 推拉门：拉开一半，挡在开口东半边前面；上面的门轨
            mb.Box(D.TinRoof, new Vector3(o1 - .9f, (gy + .05f + oTop) * .5f, z0 - .14f), new Vector3(1.85f, oTop - gy - .05f, .03f), Quaternion.Euler(0f, 0f, 1.2f), 0);
            mb.Box(D.Iron, new Vector3((o0 + o1) * .5f + .9f, oTop + .05f, z0 - .14f), new Vector3(5.6f, .06f, .05f));
            // 屋顶：两坡锈铁皮（波纹顺坡往下 = u 沿屋脊）、屋脊盖板
            float ang = Mathf.Atan2(ridge - eave, dep * .5f) * Mathf.Rad2Deg;
            float slopeLen = (dep * .5f + ov) / Mathf.Cos(ang * Mathf.Deg2Rad);
            foreach (float s in new[] { -1f, 1f })
            {
                var dir = new Vector3(0f, -Mathf.Sin(ang * Mathf.Deg2Rad), s * Mathf.Cos(ang * Mathf.Deg2Rad));
                var c = new Vector3(cx, ridge + .03f, cz) + dir * (slopeLen * .5f);
                mb.Box(D.TinRoof, c, new Vector3(len + 2f * ov, .02f, slopeLen), Quaternion.Euler(s * ang, 0f, 0f), 0);
            }
            mb.Box(D.Iron, new Vector3(cx, ridge + .06f, cz), new Vector3(len + 2f * ov + .02f, .05f, .34f));
            // 东边的披屋：单坡（往东低），两根柱，下面堆木料、一个油桶
            float lx = x1 + 2.1f;
            foreach (float z in new[] { z0 + .4f, z1 - .4f })
                mb.Box(D.BeamDark, new Vector3(lx, gy + 1.15f, z), new Vector3(.1f, 2.3f, .1f));
            mb.Box(D.BeamDark, new Vector3(lx, gy + 2.32f, cz), new Vector3(.12f, .1f, dep - .6f));
            Slab(mb, D.TinRoof, new Vector3(x1 + .05f, gy + 2.85f, z0 + .1f), new Vector3(lx + .35f, gy + 2.3f, z0 + .1f),
                new Vector3(lx + .35f, gy + 2.3f, z1 - .1f), new Vector3(x1 + .05f, gy + 2.85f, z1 - .1f), .02f);
            for (int i = 0; i < 9; i++)
                mb.Box(D.TrimLight, new Vector3(x1 + 1.1f + OR(-.15f, .15f), gy + .08f + (i / 3) * .13f, z0 + 1.8f + (i % 3) * .5f), new Vector3(1.6f + OR(-.2f, .2f), .12f, .14f),
                    Quaternion.Euler(0f, 90f + OR(-6f, 6f), 0f));
            mb.Cyl(D.RustSheet, new Vector3(x1 + 1.3f, gy, z1 - 1.2f), .29f, .29f, .88f, 12, true);
            ShotOut("barn", new Vector3(x0 - 3.2f, gy + 1.7f, z0 - 7.5f), new Vector3(cx, gy + 2.2f, cz));
        }
    }
}
