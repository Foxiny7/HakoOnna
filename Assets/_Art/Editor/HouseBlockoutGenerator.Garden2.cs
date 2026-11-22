using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 庭院（二）：竹篱笆分区、池塘、雪见灯笼、红叶、蹲踞、苔藓、竹林、柴棚、大门门扇的细节。
    /// 坐标都是新尺寸下的世界米数。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        const float FenceH = 1.95f;
        static readonly float[] FenceRails = { .28f, .66f, 1.04f, 1.42f, 1.78f };

        /// <summary>
        /// 建仁寺垣：两面各一层竖排的劈竹，五道押縁，顶上玉縁，约 1.8 m 一根丸太柱。
        /// 碰撞体单独放在 Structure/GardenFences 下（名字不以 Wall 开头，测试不把它当房间墙）。
        /// </summary>
        static void BambooFence(MeshBatch mb, Transform colliders, string name, Vector3 a, Vector3 b, System.Random rng)
        {
            var up = Vector3.up;
            var dir = b - a; dir.y = 0;
            float len = dir.magnitude; dir /= len;
            var q = Quaternion.LookRotation(dir);   // 局部 z = 沿篱笆，x = 横穿
            var side = q * Vector3.right;
            var along = Quaternion.FromToRotation(up, dir);
            Vector3 P(float s, float y, float d) => a + dir * s + up * y + side * d;

            // 立子之间的暗缝
            mb.Box(D.BeamDark, P(len / 2, FenceH / 2, 0), new Vector3(.03f, FenceH, len), q);
            // 立子：劈开的竹片，宽 4 cm，两面各一层，顶端高低略有参差
            for (float s = .022f; s < len - .02f; s += .044f)
                foreach (float d in new[] { -1f, 1f })
                {
                    float h = FenceH - .02f - (float)rng.NextDouble() * .03f;
                    mb.Box(D.BambooDry, P(s, h / 2, d * .021f), new Vector3(.012f, h, .04f), q, 2, new Vector2(s * 1.07f + d * .5f, 0));
                }
            // 押縁：横向的细竹
            foreach (float y in FenceRails)
                foreach (float d in new[] { -1f, 1f })
                    mb.Cyl(D.BambooDry, P(0, y, d * .04f), .018f, .018f, len, 6, true, along);
            // 玉縁：盖板 + 一根粗竹
            mb.Box(D.BeamDark, P(len / 2, FenceH + .01f, 0), new Vector3(.1f, .03f, len), q);
            mb.Cyl(D.BambooDry, P(0, FenceH + .065f, 0), .045f, .045f, len, 8, true, along);
            // 丸太柱 + 押縁上的棕榈绳结
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 1.8f));
            for (int i = 0; i <= n; i++)
            {
                float s = Mathf.Clamp(i * len / n, .06f, len - .06f);
                mb.Cyl(D.Bark, P(s, 0, 0), .055f, .05f, FenceH + .12f, 8);
                foreach (float y in FenceRails)
                    foreach (float d in new[] { -1f, 1f })
                        mb.Box(D.Ink, P(s, y, d * .062f), new Vector3(.012f, .045f, .045f), q * Quaternion.Euler(45f, 0, 0));
            }

            var go = new GameObject(name);
            go.transform.SetParent(colliders, false);
            go.transform.SetPositionAndRotation(P(len / 2, 0, 0), q);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, (FenceH + .12f) / 2, 0);
            bc.size = new Vector3(.14f, FenceH + .12f, len);
        }

        /// <summary>不规则的池塘：深色水面（略高于地面，免得被地面盖住）+ 岸边一圈大小交错的石头 + 几片睡莲叶。</summary>
        static void Pond(MeshBatch mb, Vector3 c, float rx, float rz, System.Random rng, MeshBatch rocks)
        {
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            const int N = 30;
            var rim = new Vector3[N];
            var pts = new List<Vector3> { c + Vector3.up * .014f };
            for (int i = 0; i < N; i++)
            {
                float t = i * Mathf.PI * 2 / N;
                float k = 1f + .15f * Mathf.Sin(t * 2f + .7f) + .09f * Mathf.Sin(t * 3f + 2.1f);
                rim[i] = c + new Vector3(Mathf.Cos(t) * rx * k, .014f, Mathf.Sin(t) * rz * k);
                pts.Add(rim[i]);
            }
            pts.Add(rim[0]);
            var uv = new Vector2[pts.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(pts[i].x, pts[i].z);
            mb.Poly(D.Water, pts, uv, Vector3.up);
            for (int i = 0; i < N; i++)
            {
                var o = rim[i] - c; o.y = 0; o.Normalize();
                float r = i % 3 == 0 ? R(.3f, .42f) : R(.17f, .26f);
                float tx = R(-6, 6), yaw = R(0, 360), tz = R(-6, 6);
                // 第五轮：池边石换成 CC0 的苔石（较扁的那一套）；缺模型才画原来的扁球
                var at = rim[i] + o * r * .4f;
                if (!RockModel(rocks, new Vector3(at.x, 0f, at.z), new Vector3(r, r * .62f, r * .85f), yaw, i * 3 + 1, true))
                    mb.Sphere(D.Stone, at + Vector3.up * r * .2f, new Vector3(r, r * .55f, r * .8f), 7, Quaternion.Euler(tx, yaw, tz));
            }
            for (int i = 0; i < 6; i++)
            {
                float t = R(0, Mathf.PI * 2), d = R(.25f, .7f);
                var p = c + new Vector3(Mathf.Cos(t) * rx * d, .018f, Mathf.Sin(t) * rz * d);
                float r = R(.1f, .17f);
                mb.Cyl(D.Leaf, p, r, r, .006f, 10, true, Quaternion.Euler(0, R(0, 360), 0));
            }
        }

        /// <summary>红叶（伊吕波红叶）：主干微弯分三枝，伞形树冠由许多小叶团组成。</summary>
        static void Maple(MeshBatch mb, Vector3 b, float h, float yaw, System.Random rng)
        {
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            var q = Quaternion.Euler(0, yaw, 0);
            var lean = (q * new Vector3(.2f, 1f, .06f)).normalized;
            float th = h * .36f;
            mb.Cyl(D.Bark, b, .11f, .085f, th + .05f, 9, false, Quaternion.FromToRotation(up, lean));
            var fork = b + lean * th;
            var crown = fork + up * h * .3f;
            float cw = h * .5f, ch = h * .2f;
            for (int i = 0; i < 4; i++)
            {
                var d = (q * Quaternion.Euler(0, i * 95f + R(-20, 20), 0) * new Vector3(.75f, 1f, 0)).normalized;
                float bl = h * R(.3f, .4f);
                mb.Cyl(D.Bark, fork, .06f, .025f, bl, 7, false, Quaternion.FromToRotation(up, d));
                var tip = fork + d * bl;
                var d2 = (d + q * Quaternion.Euler(0, 60f, 0) * Vector3.right * .6f).normalized;
                mb.Cyl(D.Bark, tip - d * bl * .3f, .025f, .01f, bl * .5f, 5, false, Quaternion.FromToRotation(up, d2));
            }
            // 叶团：分布在扁圆的伞面上，上层多、下缘一圈，越往外越低
            for (int i = 0; i < 30; i++)
            {
                float t = R(0, Mathf.PI * 2), rr = Mathf.Sqrt(R(0f, 1f));
                var p = crown + new Vector3(Mathf.Cos(t) * cw * rr, ch * (1f - rr * rr) - ch * .5f + R(-.15f, .15f), Mathf.Sin(t) * cw * rr * .9f);
                float r = R(.32f, .5f);
                mb.Sphere(D.MapleLeaf, p, new Vector3(r, r * .36f, r * .85f), 7, Quaternion.Euler(R(-12, 12), R(0, 360), R(-12, 12)));
            }
            // 地上的落叶
            for (int i = 0; i < 14; i++)
            {
                float t = R(0, Mathf.PI * 2), rr = R(.2f, 1.1f) * cw;
                mb.Box(D.MapleLeaf, b + new Vector3(Mathf.Cos(t) * rr, .004f, Mathf.Sin(t) * rr), new Vector3(.05f, .003f, .05f),
                    Quaternion.Euler(0, R(0, 90), 0));
            }
        }

        /// <summary>蹲踞：低的手水钵 + 前石、手烛石、汤桶石，后面一根竹筧往钵里滴水，钵上横一把柄杓。front 朝向人站的一边。</summary>
        static void Tsukubai(MeshBatch mb, Vector3 b, float yaw, MeshBatch rocks)
        {
            var up = Vector3.up;
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 L(float x, float y, float z) => b + q * new Vector3(x, y, z);
            mb.Cyl(D.Gravel, L(0, 0, 0), .9f, .9f, .012f, 30);           // 海：铺卵石的低处
            // 第五轮：手水钵换成车削的石钵，前石换成不规则的扁石（GardenStone.cs）
            TsukubaiBasin(mb, L(0, 0, 0));
            FlatStone(mb, L(0, 0, .66f), .3f, yaw + 6f, 3.7f, .11f, 1.35f);
            // 第五轮：手烛石、汤桶石换成 CC0 的苔石
            if (!RockModel(rocks, L(-.62f, 0f, .12f), new Vector3(.24f, .26f, .2f), yaw + 40f, 4, true))
                mb.Sphere(D.Stone, L(-.62f, .1f, .12f), new Vector3(.24f, .2f, .2f), 7);   // 手烛石
            if (!RockModel(rocks, L(.6f, 0f, .1f), new Vector3(.22f, .2f, .2f), yaw + 200f, 1, true))
                mb.Sphere(D.Stone, L(.6f, .08f, .1f), new Vector3(.22f, .16f, .2f), 7);    // 汤桶石
            // 筧
            var spout = L(.04f, .62f, -.62f);
            mb.Cyl(D.Bamboo, L(.04f, 0, -.62f), .035f, .035f, .66f, 8);
            mb.Cyl(D.Bamboo, spout, .022f, .022f, .5f, 8, true, Quaternion.FromToRotation(up, q * Vector3.forward));
            // 柄杓：木柄横在钵口上，杓子在一端
            mb.Cyl(D.TrimLight, L(-.26f, .392f, .05f), .007f, .007f, .44f, 5, true, Quaternion.FromToRotation(up, q * Vector3.right));
            mb.Cyl(D.TrimLight, L(.2f, .38f, .05f), .042f, .04f, .06f, 10);
            mb.Sphere(D.Moss, L(-.3f, .005f, -.4f), new Vector3(.5f, .012f, .35f), 8);
        }

        static void MossPatch(MeshBatch mb, float x, float z, float rx, float rz, float yaw) =>
            mb.Sphere(D.Moss, new Vector3(x, .002f, z), new Vector3(rx, .012f, rz), 10, Quaternion.Euler(0, yaw, 0));

        /// <summary>竹林：细竹竿带竹节，上半截的节上长出侧枝，枝梢一簇簇下垂的细长竹叶。</summary>
        static void BambooGrove(Transform parent, Rect area, int count, System.Random rng)
        {
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            var mb = new MeshBatch();
            void Leaves(Vector3 at, int n)
            {
                for (int l = 0; l < n; l++)
                {
                    var ld = Quaternion.Euler(R(20, 70), R(0, 360), 0);
                    float ll = R(.16f, .25f);
                    mb.Box(D.BambooLeaf, at + ld * Vector3.forward * ll * .5f, new Vector3(.028f, .003f, ll), ld);
                }
            }
            for (int i = 0; i < count; i++)
            {
                var b = new Vector3(R(area.xMin, area.xMax), 0, R(area.yMin, area.yMax));
                float h = R(6.5f, 10f), r = R(.035f, .055f);
                var tilt = Quaternion.Euler(R(-4, 4), 0, R(-4, 4));
                var axis = tilt * up;
                mb.Cyl(D.Bamboo, b, r, r * .6f, h, 7, false, tilt);
                for (float y = .35f; y < h - .3f; y += R(.3f, .42f))
                    mb.Cyl(D.Bamboo, b + axis * y, r * 1.15f, r * 1.15f, .016f, 7, false, tilt);
                int side = 0;
                for (float y = h * .5f; y < h - .25f; y += R(.36f, .52f))
                {
                    side++;
                    var bd = (Quaternion.Euler(0, side * 137f + R(-25, 25), 0) * Vector3.right + up * R(.45f, .9f)).normalized;
                    float bl = R(.45f, .95f) * (1.15f - y / h);
                    var bs = b + axis * y;
                    mb.Cyl(D.Bamboo, bs, .009f, .004f, bl, 4, false, Quaternion.FromToRotation(up, bd));
                    Leaves(bs + bd * bl * .55f, 3 + (int)R(0, 2.99f));
                    Leaves(bs + bd * bl, 4 + (int)R(0, 2.99f));
                }
                Leaves(b + axis * h, 6);
            }
            mb.Build(parent, "BambooGrove");
        }

        /// <summary>
        /// 靠墙的薪棚：两排劈柴的端面朝外（开放式），单坡小屋顶，旁边一个劈柴墩。
        /// wallX = 墙外皮的 x，柴垛沿 z 排开、朝 -x。
        /// </summary>
        static void FirewoodShed(MeshBatch mb, float wallX, float zc, float len, System.Random rng)
        {
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            var toWall = Quaternion.FromToRotation(up, Vector3.right);
            const float depth = .48f, H = 1.42f;
            float x0 = wallX - .04f;
            foreach (float dx in new[] { .12f, .38f })
                mb.Box(D.BeamDark, new Vector3(x0 - dx, .04f, zc), new Vector3(.08f, .08f, len));
            for (float y = .145f; y < H; y += .122f)
                for (float z = zc - len / 2 + .07f; z < zc + len / 2 - .06f; z += R(.118f, .142f))
                {
                    float r = R(.048f, .066f);
                    mb.Cyl(D.TrimLight, new Vector3(x0 - depth - R(0f, .05f), y + R(-.01f, .01f), z), r, r * R(.9f, 1f), depth, 6, true,
                        toWall * Quaternion.Euler(0, R(0, 60), 0));
                }
            // 柱子和单坡屋顶：靠墙那头高（墙上一根横木托着），外侧压在柱顶横梁上，雨水往外淌
            float px = x0 - depth - .28f;
            foreach (float s in new[] { -1f, 1f })
                mb.Box(D.Post, new Vector3(px, .88f, zc + s * (len / 2 + .02f)), new Vector3(.08f, 1.76f, .08f));
            mb.Box(D.BeamDark, new Vector3(px, 1.78f, zc), new Vector3(.1f, .08f, len + .2f));
            const float pitch = 16f, beamTop = 1.82f;
            float tan = Mathf.Tan(pitch * Mathf.Deg2Rad), xa = px - .24f, xb = wallX - .005f, xm = (xa + xb) / 2;
            mb.Box(D.BeamDark, new Vector3(wallX - .04f, beamTop + (wallX - .08f - px) * tan - .05f, zc), new Vector3(.08f, .1f, len + .2f));
            mb.Box(D.BeamDark, new Vector3(xm, beamTop + .018f + (xm - px) * tan, zc),
                new Vector3((xb - xa) / Mathf.Cos(pitch * Mathf.Deg2Rad), .035f, len + .4f), Quaternion.Euler(0, 0, pitch));
            // 劈柴墩 + 几块劈好的柴
            var stump = new Vector3(px - .75f, 0, zc + len / 2 + .3f);
            mb.Cyl(D.Bark, stump, .23f, .21f, .42f, 10);
            mb.Cyl(D.TrimLight, stump + up * .42f, .2f, .2f, .006f, 10);
            for (int i = 0; i < 4; i++)
                mb.Box(D.TrimLight, stump + new Vector3(R(-.6f, .5f), .035f, R(-.5f, .5f)), new Vector3(.07f, .07f, .42f),
                    Quaternion.Euler(0, R(0, 180), 0));
            // 碰撞体停在墙皮前 2 cm，不和墙的碰撞体重叠
            float bx0 = px - .06f, bx1 = wallX - .02f;
            Blocker("FirewoodShed", new Vector3((bx0 + bx1) / 2, 0, zc), 0, new Vector3(bx1 - bx0, 1.95f, len + .15f));
            Blocker("ChoppingBlock", stump, 0, new Vector3(.46f, .44f, .46f));
        }

        /// <summary>
        /// 大门门扇的细节（竖板缝、框、三道横桟、铁钉、合页铁片、门环），做成门扇的子物体，
        /// 门扇以后被玩法代码转动或隐藏时细节跟着走。门扇本身的碰撞体不动。
        /// </summary>
        static void GateLeafDetails(Transform root)
        {
            var gate = root.Find("Gameplay/Gate_Main");
            if (gate == null) return;
            foreach (var leafName in new[] { "GateDoor_L", "GateDoor_R" })
            {
                var leaf = gate.Find(leafName);
                if (leaf == null) continue;
                var lb = leaf.GetComponent<MeshRenderer>().bounds;
                var mb = new MeshBatch();
                var o = gate.position;
                float x0 = lb.min.x - o.x, x1 = lb.max.x - o.x, h = lb.max.y, hz = lb.extents.z, zc = lb.center.z - o.z;
                bool left = leafName.EndsWith("_L");
                float hinge = left ? x0 : x1, meet = left ? x1 : x0, hs = left ? 1f : -1f;
                foreach (float f in new[] { -1f, 1f })
                {
                    float z0 = zc + f * hz;
                    Vector3 Z(float x, float y, float dz) => new Vector3(x, y, z0 + f * dz);
                    for (float x = x0 + .17f; x < x1 - .1f; x += .17f)
                        mb.Box(D.Ink, Z(x, h / 2, .001f), new Vector3(.006f, h - .04f, .002f));
                    mb.Box(D.BeamDark, Z(x0 + .05f, h / 2, .012f), new Vector3(.1f, h, .024f));
                    mb.Box(D.BeamDark, Z(x1 - .05f, h / 2, .012f), new Vector3(.1f, h, .024f));
                    mb.Box(D.BeamDark, Z((x0 + x1) / 2, h - .07f, .012f), new Vector3(x1 - x0, .14f, .024f));
                    mb.Box(D.BeamDark, Z((x0 + x1) / 2, .07f, .012f), new Vector3(x1 - x0, .14f, .024f));
                    foreach (float y in new[] { .55f, h / 2, h - .55f })
                    {
                        mb.Box(D.BeamDark, Z((x0 + x1) / 2, y, .018f), new Vector3(x1 - x0 - .2f, .14f, .036f));
                        if (f < 0) // 外侧（街面）：铁钉
                            for (float x = x0 + .2f; x < x1 - .15f; x += .22f)
                                mb.Sphere(D.Iron, Z(x, y, .036f), new Vector3(.02f, .02f, .012f), 6);
                    }
                    // 合页的铁片
                    foreach (float y in new[] { .55f, h - .55f })
                        mb.Box(D.Iron, Z(hinge + hs * .3f, y, .04f), new Vector3(.6f, .06f, .006f));
                }
                // 门环的圆座金（两面各一个，在两扇门的交接处附近）
                foreach (float f in new[] { -1f, 1f })
                {
                    var c = new Vector3(meet - hs * .22f, 1.25f, zc + f * (hz + .001f));
                    var outward = Quaternion.Euler(f > 0 ? 90f : -90f, 0, 0);
                    mb.Cyl(D.Iron, c, .075f, .075f, .01f, 14, true, outward);
                    mb.Cyl(D.Iron, c, .028f, .022f, .035f, 10, true, outward);
                }
                var go = mb.Build(gate, leafName + "_Detail");
                if (go != null) go.transform.SetParent(leaf, true);
            }
        }
    }
}
