using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 庭院：地面、白砂、飞石、石灯笼、松、灌木、竹林、水井、晾衣竿、枯树；
    /// 竹篱笆把室外分成三块：门前（玄関 ↔ 大门）、后院（台所后门 ↔ 土蔵，连着西侧）、
    /// 縁側前的主庭园（池塘、红叶、蹲踞、枯山水，只能隔着玻璃门看）。新加的东西见 Garden2。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>挡路物体的碰撞体（放在 Props/Dressing_Blockers 下，测试会检查它们）。floorPos 为底面中心。</summary>
        static void Blocker(string name, Vector3 floorPos, float yaw, Vector3 size)
        {
            if (FurnishPass)
            {
                // 室内陈设的挡块和它挡的东西一起搬家、放大（见 Rescale）
                var m = Kit.Claim(BlockerBounds(floorPos, yaw, size));
                if (Kit.Probing || Kit.IsDropped(m) || Kit.Ghost) return;
                floorPos = m.MultiplyPoint3x4(floorPos);
                // 矩阵只有平移、绕竖轴转 90° 的倍数和等比放大
                size *= new Vector3(m.m00, m.m10, m.m20).magnitude;
                yaw += Mathf.Atan2(m.m02, m.m00) * Mathf.Rad2Deg;
            }
            var go = new GameObject("Blocker_" + name);
            go.transform.SetParent(BlockerRoot, false);
            go.transform.SetPositionAndRotation(floorPos, Quaternion.Euler(0, yaw, 0));
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, size.y / 2, 0);
            bc.size = size;
        }

        static void BuildGarden(Transform parent, Transform structure)
        {
            var rng = new System.Random(1931);
            float Rr(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            // 庭院按比例跟着围墙和房子外扩（东西本身保持真实尺寸）
            float gk = OutdoorScale;
            Vector3 W(float x, float z) => new Vector3(x * gk, 0f, z * gk);
            Vector2 V(float x, float z) => new Vector2(x * gk, z * gk);
            Rect GR(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0 * gk, z0 * gk, x1 * gk, z1 * gk);

            // 地面：换成米制 UV 的泥土 + 苔藓（碰撞体仍是 Structure/Ground_Garden）
            var ground = structure.Find("Ground_Garden");
            if (ground != null)
            {
                var gr = ground.GetComponent<MeshRenderer>();
                var b = gr.bounds;
                gr.enabled = false;
                var gmb = new MeshBatch();
                gmb.Box(D.Ground, b.center, b.size);
                gmb.Build(parent, "GardenGround", false);
            }

            var mb = new MeshBatch();

            // 竹篱笆分区（碰撞体在 Structure/GardenFences）
            var fences = ArtKit.Empty(structure, "GardenFences", Vector3.zero);
            var fmb = new MeshBatch();
            float yardIn = -5.46f * PlanScale + T, yardOut = 23.66f * PlanScale - T, westIn = -4.55f * PlanScale + T;
            // 门前 | 主庭园：玄関和縁側交界的墙角一直拉到南墙
            BambooFence(fmb, fences, "GardenFence_Front", new Vector3(10.92f, 0, yardIn), new Vector3(10.92f, 0, 0f), rng);
            // 门前 | 西侧（后院）：西墙到玄関西南角
            BambooFence(fmb, fences, "GardenFence_West", new Vector3(westIn, 0, 0f), new Vector3(0f, 0, 0f), rng);
            // 主庭园东侧 | 后院：東縁側东北角到东墙
            BambooFence(fmb, fences, "GardenFence_East", new Vector3(37.31f, 0, 20.02f), new Vector3(yardOut, 0, 20.02f), rng);
            fmb.Build(parent, "BambooFences");
            ArtKit.MarkStaticRecursive(fences.gameObject);

            // 门前白砂小路（耙纹顺着路）+ 枯山水
            GravelRect(mb, GR(2.85f, -7.6f, 4.87f, -.04f), true);
            GravelRect(mb, GR(14.6f, -6.9f, 19.4f, -3.9f), false);

            // 飞石
            void Stones(params Vector2[] path)
            {
                for (int i = 0; i < path.Length - 1; i++)
                {
                    float len = Vector2.Distance(path[i], path[i + 1]);
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / .62f));
                    for (int k = 0; k < n; k++)
                    {
                        var p = Vector2.Lerp(path[i], path[i + 1], (k + .5f) / n) + new Vector2(Rr(-.07f, .07f), Rr(-.07f, .07f));
                        float r = Rr(.21f, .29f);
                        mb.Cyl(D.Stone, new Vector3(p.x, -.03f, p.y), r, r * .9f, .08f, 9, true, Quaternion.Euler(0, Rr(0, 360), 0));
                    }
                }
            }
            // 主庭园：第一扇玻璃门的沓脱石 → 池边；两扇玻璃门之间；東縁側玻璃门 → 松树
            Stones(new Vector2(18.1f, -1.3f), new Vector2(17.3f, -2.4f), new Vector2(16.9f, -3.6f));
            Stones(V(13.6f, -1.6f), V(15.4f, -2.5f), V(17.4f, -2.6f), V(19.2f, -2.2f), V(21.2f, -1.25f));
            Stones(new Vector2(38.75f, 8.7f), new Vector2(40.2f, 7.8f), new Vector2(41.4f, 6.3f));
            Stones(V(2.57f, 14.95f), V(2.8f, 18f), V(3.6f, 21.4f), V(7f, 22.5f),
                V(10.3f, 22.9f), V(11.58f, 23.45f));
            Stones(V(25.41f, 15.1f), V(24.6f, 18.5f), V(22f, 22.4f), V(19f, 25.9f), V(16.45f, 27.72f));

            // 石灯笼（春日型）
            var lb = W(19.9f, -3.2f);
            mb.Cyl(D.Stone, lb, .3f, .28f, .12f, 6);
            mb.Cyl(D.Stone, lb + up * .12f, .1f, .085f, .75f, 10);
            mb.Cyl(D.Stone, lb + up * .87f, .2f, .26f, .1f, 6);
            mb.Box(D.Stone, lb + up * 1.11f, new Vector3(.3f, .28f, .3f));
            mb.Box(D.Black, lb + up * 1.12f, new Vector3(.15f, .15f, .31f));
            mb.Box(D.Black, lb + up * 1.12f, new Vector3(.31f, .15f, .15f));
            mb.Cyl(D.Stone, lb + up * 1.25f, .42f, .06f, .24f, 6);
            mb.Sphere(D.Stone, lb + up * 1.53f, new Vector3(.08f, .1f, .08f), 8);
            mb.Sphere(D.Leaf, lb + new Vector3(.12f, 1.29f, .1f), new Vector3(.16f, .04f, .12f), 6);
            Blocker("StoneLantern", lb, 0, new Vector3(.62f, 1.6f, .62f));

            // 枯山水的石组
            void Rock(Vector3 c, Vector3 radii, bool block)
            {
                mb.Sphere(D.Stone, c + up * (radii.y * .35f), radii, 7, Quaternion.Euler(Rr(-8, 8), Rr(0, 360), Rr(-8, 8)));
                if (block) Blocker("Rock", new Vector3(c.x, 0, c.z), 0, new Vector3(radii.x * 1.6f, radii.y * 1.3f, radii.z * 1.6f));
            }
            Rock(W(15.6f, -5.6f), new Vector3(.62f, .55f, .48f), true);
            Rock(W(17.6f, -4.8f), new Vector3(.42f, .36f, .38f), true);
            Rock(W(18.4f, -6.1f), new Vector3(.25f, .18f, .22f), false);
            Rock(W(16.2f, -5.0f), new Vector3(.2f, .14f, .18f), false);

            // 松
            void Pine(Vector3 b, float h, float yaw)
            {
                var q = Quaternion.Euler(0, yaw, 0);
                Vector3[] dirs = { new Vector3(.12f, 1, 0), new Vector3(.38f, 1, .12f), new Vector3(-.05f, 1, -.18f) };
                var p = b;
                float r = .19f, seg = h / 3f;
                for (int i = 0; i < 3; i++)
                {
                    var d = (q * dirs[i]).normalized;
                    mb.Cyl(D.Bark, p, r, r * .78f, seg + .06f, 9, false, Quaternion.FromToRotation(up, d));
                    p += d * seg;
                    // 每段向外伸一根枝，末端是一团修剪成云朵状的松叶
                    var bd = q * Quaternion.Euler(0, i * 137f + 40f, 0) * Vector3.right;
                    float bl = Rr(.9f, 1.5f) * (1f - i * .2f);
                    var bstart = p - d * seg * .3f;
                    var bdir = (bd + up * .25f).normalized;
                    mb.Cyl(D.Bark, bstart, r * .45f, r * .25f, bl, 7, false, Quaternion.FromToRotation(up, bdir));
                    var pad = bstart + bdir * bl;
                    float pr = Rr(.7f, .95f) * (1f - i * .18f);
                    mb.Sphere(D.Pine, pad, new Vector3(pr, pr * .34f, pr * .85f), 9);
                    mb.Sphere(D.Pine, pad + new Vector3(pr * .4f, .1f, pr * .2f), new Vector3(pr * .55f, pr * .25f, pr * .5f), 8);
                    r *= .78f;
                }
                mb.Sphere(D.Pine, p + up * .1f, new Vector3(.75f, .32f, .7f), 9);
                mb.Sphere(D.Pine, p + new Vector3(.3f, .3f, -.2f), new Vector3(.45f, .24f, .4f), 8);
                Blocker("PineTrunk", b, 0, new Vector3(.42f, 2.2f, .42f));
            }
            Pine(new Vector3(8.84f, 0, -8.6f), 4.4f, 20f); // 门前：大门东侧的松，枝伸向大门
            Pine(W(30.2f, 3.0f), 5.0f, 200f);

            // 灌木（只是视觉，不挡路）
            void Shrub(float x, float z, float s)
            {
                int n = 3 + (int)Rr(0, 2.99f);
                for (int i = 0; i < n; i++)
                {
                    float rr = Rr(.35f, .55f) * s;
                    mb.Sphere(D.Shrub, new Vector3(x + Rr(-.35f, .35f) * s, rr * .55f, z + Rr(-.3f, .3f) * s), new Vector3(rr, rr * .8f, rr), 8,
                        Quaternion.Euler(0, Rr(0, 360), 0));
                }
            }
            foreach (var p in new[]
                     {
                         V(1.3f, -1.4f), V(6.3f, -1.3f), V(9.3f, -.95f), V(16.4f, -.9f),
                         V(23.7f, -1.4f), V(25.7f, -1.2f), V(-1.3f, 2.0f), V(-1.3f, 7.6f),
                         V(-1.4f, 13.1f), V(27.8f, 1.6f), V(27.8f, 3.4f), V(27.9f, 9.3f),
                         V(27.8f, 12.5f), V(-4.6f, -6.6f), V(-1.2f, -6.7f), V(8.5f, -1.9f),
                         V(8.35f, -7.1f), V(23.6f, -6.7f), V(27.6f, -6.6f), V(31.6f, -6.5f),
                         V(32.4f, 12f), V(32.3f, 20f), V(-5.4f, 20f), V(-5.5f, 28f),
                     })
                Shrub(p.x, p.y, Rr(.95f, 1.4f));

            // 竹林（后院东北角）
            BambooGrove(parent, GR(28.8f, 30.8f, 33.0f, 35.6f), 60, rng);
            Blocker("BambooGrove", W(30.85f, 33.15f), 0, new Vector3(4.9f * gk, 3f, 5.5f * gk));

            // 水井
            var wb = W(.5f, 19.5f);
            mb.Cyl(D.Stone, wb, .6f, .58f, .72f, 14);
            mb.Cyl(D.Water, wb + up * .72f, .47f, .47f, .004f, 14);
            mb.Box(D.TrimMid, wb + new Vector3(-.22f, .745f, 0), new Vector3(.58f, .04f, 1.12f));
            foreach (float sx in new[] { -1f, 1f })
                mb.Box(D.Post, wb + new Vector3(sx * .68f, .95f, 0), new Vector3(.08f, 1.9f, .08f));
            mb.Box(D.Post, wb + new Vector3(0, 1.86f, 0), new Vector3(1.5f, .08f, .08f));
            mb.Cyl(D.TrimMid, wb + new Vector3(0, 1.7f, -.035f), .1f, .1f, .07f, 12, true, Quaternion.Euler(90, 0, 0));
            mb.Box(D.Straw, wb + new Vector3(.1f, 1.25f, 0), new Vector3(.015f, .9f, .015f));
            mb.Cyl(D.TrimMid, wb + new Vector3(-.25f, .765f, .25f), .13f, .15f, .22f, 10);
            Blocker("Well", wb, 0, new Vector3(1.5f, .8f, 1.25f));

            // 晾衣竿 + 一块白布
            float lx = -3.2f * gk, lz0 = 7.0f * gk, lz1 = 11.0f * gk;
            foreach (float z in new[] { lz0, lz1 })
                mb.Box(D.Post, new Vector3(lx, 1.05f, z), new Vector3(.08f, 2.1f, .08f));
            mb.Cyl(D.Bamboo, new Vector3(lx, 1.95f, lz0 - .1f), .025f, .025f, lz1 - lz0 + .2f, 8, true, Quaternion.Euler(90, 0, 0));
            // 第四轮：竿上的床单、蓝布、白布从三块竖着的薄板换成搭在竿上、前后两片、起褶、被风吹得有点斜的布（Soft.cs TowelOnBar）
            var alongZ = Quaternion.Euler(0f, -90f, 0f);
            TowelOnBar(mb, D.ClothWhite, D.ClothWhite, new Vector3(lx, 1.95f, lz0 + .925f), alongZ, 1.35f, 1.15f, .9f, .025f, 1.2f, .02f, .004f, .04f);
            TowelOnBar(mb, D.ClothIndigo, D.ClothIndigo, new Vector3(lx, 1.95f, lz0 + 3.075f), alongZ, .45f, .62f, .5f, .025f, 3.4f, .012f, .004f, -.05f);
            TowelOnBar(mb, D.ClothWhite, D.ClothWhite, new Vector3(lx, 1.95f, lz1 - 1.75f), alongZ, .9f, .85f, .7f, .025f, 5.7f, .016f, .004f, .03f);
            ShotDir("garden_laundry", new Vector3(lx, 1.35f, (lz0 + lz1) / 2f), Quaternion.Euler(0f, -25f, 0f) * Vector3.right, 3.6f, .25f);

            // 枯树
            var tb = W(4.6f, 28.6f);
            var tq = Quaternion.Euler(4, 0, -5);
            mb.Cyl(D.Bark, tb, .18f, .1f, 2.7f, 9, false, tq);
            for (int i = 0; i < 6; i++)
            {
                var start = tb + tq * up * Rr(1.3f, 2.6f);
                var dir = (Quaternion.Euler(0, i * 61f + Rr(-15, 15), 0) * Vector3.right + up * Rr(.4f, 1.1f)).normalized;
                float len = Rr(.9f, 1.6f);
                mb.Cyl(D.Bark, start, .05f, .015f, len, 6, false, Quaternion.FromToRotation(up, dir));
                var mid = start + dir * len * .6f;
                var dir2 = (dir + Quaternion.Euler(0, 70, 0) * dir * .8f + up * .3f).normalized;
                mb.Cyl(D.Bark, mid, .025f, .008f, len * .5f, 5, false, Quaternion.FromToRotation(up, dir2));
            }
            Blocker("DeadTree", tb, 0, new Vector3(.4f, 2f, .4f));

            // ---- 主庭园：池塘 + 雪见灯笼 + 红叶 + 蹲踞 + 苔藓 ----
            Pond(mb, new Vector3(15.0f, 0, -6.7f), 2.8f, 2.0f, rng);
            YukimiLantern(mb, new Vector3(18.95f, 0, -5.0f), 15f);
            Blocker("YukimiLantern", new Vector3(18.95f, 0, -5.0f), 0, new Vector3(.62f, .95f, .62f));
            var maples = new[] { new Vector4(12.9f, -3.3f, 4.2f, 30f), new Vector4(34.6f, -7.4f, 4.8f, 200f),
                                 new Vector4(43.6f, 12.4f, 4.4f, 110f), new Vector4(-5.2f, -4.4f, 3.8f, 300f) }; // 最后一棵在门前西侧
            foreach (var m in maples)
            {
                Maple(mb, new Vector3(m.x, 0, m.y), m.z, m.w, rng);
                Blocker("MapleTrunk", new Vector3(m.x, 0, m.y), 0, new Vector3(.3f, 2f, .3f));
            }
            Tsukubai(mb, new Vector3(23.4f, 0, -1.75f), 0f);
            Blocker("Tsukubai", new Vector3(23.4f, 0, -1.75f), 0, new Vector3(1.6f, .7f, 1.6f));
            foreach (var m in new[]
                     {
                         new Vector4(12.4f, -3.6f, 1.6f, 1.1f), new Vector4(18.6f, -8.6f, 1.4f, 1.0f), new Vector4(11.9f, -9.4f, 1.0f, .8f),
                         new Vector4(28.9f, -4.0f, 1.3f, .9f), new Vector4(34.2f, -7.0f, 1.9f, 1.3f), new Vector4(31.0f, -2.6f, 1.2f, .7f),
                         new Vector4(20.0f, -3.1f, 1.0f, .6f), new Vector4(43.2f, 12.0f, 1.8f, 1.4f), new Vector4(41.0f, 5.2f, 1.5f, 1.0f),
                         new Vector4(45.0f, 17.0f, 1.2f, .9f), new Vector4(8.6f, -8.3f, 1.3f, 1.0f), new Vector4(-5.0f, -4.0f, 1.5f, 1.1f),
                     })
                MossPatch(mb, m.x, m.y, m.z, m.w, m.x * 37f);
            // 东侧主庭园的几块石头
            Rock(new Vector3(41.6f, 0, 14.6f), new Vector3(.5f, .42f, .4f), true);
            Rock(new Vector3(42.3f, 0, 15.1f), new Vector3(.24f, .18f, .2f), false);
            Rock(new Vector3(45.2f, 0, 6.1f), new Vector3(.4f, .34f, .36f), true);

            // ---- 后院：靠裏階段西墙的薪棚 ----
            FirewoodShed(mb, 3.64f * PlanScale, 24.6f, 3.0f, rng);

            mb.Build(parent, "GardenDetails");
            // 第四轮：墙根、院墙、竹篱笆脚下的杂草（Weeds.cs；随机数单独一条）
            GardenWeeds(parent, new[]
            {
                (new Vector3(10.92f, 0, yardIn), new Vector3(10.92f, 0, 0f)),
                (new Vector3(westIn, 0, 0f), new Vector3(0f, 0, 0f)),
                (new Vector3(37.31f, 0, 20.02f), new Vector3(yardOut, 0, 20.02f)),
            });
            GateLeafDetails(parent.root);
        }

        static void GravelRect(MeshBatch mb, Rect r, bool rakeAlongZ)
        {
            TopQuad(mb, D.Gravel, r, .006f, rakeAlongZ);
            const float w = .09f, h = .05f;
            mb.Box(D.Stone, new Vector3(r.center.x, h / 2 - .01f, r.yMin - w / 2), new Vector3(r.width + 2 * w, h, w));
            mb.Box(D.Stone, new Vector3(r.center.x, h / 2 - .01f, r.yMax + w / 2), new Vector3(r.width + 2 * w, h, w));
            mb.Box(D.Stone, new Vector3(r.xMin - w / 2, h / 2 - .01f, r.center.y), new Vector3(w, h, r.height));
            mb.Box(D.Stone, new Vector3(r.xMax + w / 2, h / 2 - .01f, r.center.y), new Vector3(w, h, r.height));
        }
    }
}
