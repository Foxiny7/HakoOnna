using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第七轮（2026-11-26）主庭园的陈设（二）：第二扇玻璃门前（三轮车、石臼、水钵、藤架、风铃）、
    /// 东縁側门前（鸟居、石祠、地藏、塌了一半的袖垣）、远处（柿树、手推车、花盆和瓦、菜地）、草木。说明见 GardenDressing.cs。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        static void BuildGardenDressing2(MeshBatch mb, MeshBatch soft)
        {
            // ---------------- 第二扇玻璃门 ----------------
            HangingBracket(mb, new Vector3(28.1f, 2.9f, -.1f), new Vector3(28.62f, 2.86f, -.52f));
            At(mb, new Vector3(28.62f, 2.86f, -.52f), Quaternion.Euler(0f, 35f, 0f), () => WindChime(mb));
            ShotGarden("windchime", new Vector3(29.6f, 2.2f, -1.6f), new Vector3(28.62f, 2.5f, -.52f));

            // 倒着的三轮车。球不放（用户 2026-11-26 定：不要球；三轮车先保持侧躺）
            // 2026-11-26：这组门以后能开（V1.4 3.4），三轮车从门边挪到门口 2 m 以外
            At(mb, new Vector3(32.4f, .25f, -2.5f), Quaternion.Euler(0f, 40f, 0f) * Quaternion.Euler(0f, 0f, 86f), () => Tricycle(mb), 1.15f);
            GBlock(mb, "Tricycle", new Vector3(32.4f, 0f, -2.5f), 40f, new Vector3(.75f, .5f, .65f));
            ShotGarden("tricycle", new Vector3(31.3f, 1.1f, -3.6f), new Vector3(32.4f, .15f, -2.5f));

            // 石臼的上下两扇当飞石，通到藤架；藤架西北角一口石水钵
            Millstone(mb, new Vector3(30.75f, 0f, -1.78f), 15f, false);
            Millstone(mb, new Vector3(31.02f, 0f, -2.58f), 70f, true);
            At(mb, new Vector3(30.35f, 0f, -3.8f), Quaternion.Euler(0f, 0f, 3f), () => StoneBasin(mb));
            GBlock(mb, "StoneBasin", new Vector3(30.35f, 0f, -3.8f), 0f, new Vector3(.7f, .45f, .7f));
            ShotGarden("millstone_basin", new Vector3(29.4f, 1.3f, -1.4f), new Vector3(30.7f, .15f, -3.1f));

            At(mb, new Vector3(33.0f, 0f, -4.6f), Quaternion.Euler(0f, -3f, 0f), () =>
            {
                WisteriaTrellis(mb, soft);
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        GBlock(mb, "TrellisPost", new Vector3(sx * 1.7f, 0f, sz * 1.3f), 0f, sx < 0f && sz > 0f ? new Vector3(.36f, 2.3f, .36f) : new Vector3(.18f, 2.3f, .18f));
                GBlock(mb, "StoneTable", new Vector3(.1f, 0f, -.05f), 0f, new Vector3(.95f, .7f, .95f));
                GBlock(mb, "StoneStool", new Vector3(-.78f, 0f, .2f), 0f, new Vector3(.42f, .45f, .42f));
                GBlock(mb, "StoneStool", new Vector3(.95f, 0f, -.75f), 35f, new Vector3(.5f, .4f, .5f));
            });
            ShotGarden("trellis", new Vector3(29.6f, 1.6f, -1.6f), new Vector3(33.0f, 1.3f, -4.6f));
            ShotGarden("trellis_under", new Vector3(31.2f, 1.3f, -6.6f), new Vector3(33.2f, 1.2f, -4.4f));

            // ---------------- 东縁側的玻璃门 ----------------
            // 正面朝西（朝门）：局部 +z = 世界 −x
            At(mb, new Vector3(42.7f, 0f, 9.6f), Quaternion.Euler(0f, -90f, 0f), () =>
            {
                StoneShrine(mb);
                At(mb, new Vector3(0f, 0f, 1.7f), Quaternion.Euler(0f, 0f, 2.5f), () => StoneTorii(mb));
                At(mb, new Vector3(1.5f, 0f, .85f), Quaternion.Euler(0f, 14f, 0f), () => Jizo(mb));
                GBlock(mb, "Shrine", Vector3.zero, 0f, new Vector3(1f, 1.05f, 1f));
                GBlock(mb, "Torii", new Vector3(0f, 0f, 1.7f), 0f, new Vector3(1.5f, 1.6f, .22f)); // 鸟居只有 1.55 m 高，人钻不过去
                GBlock(mb, "Jizo", new Vector3(1.5f, 0f, .85f), 0f, new Vector3(.4f, .6f, .4f));
            });
            ShotGarden("shrine", new Vector3(39.6f, 1.5f, 8.6f), new Vector3(42.4f, .7f, 9.7f));
            ShotGarden("jizo", new Vector3(40.6f, .9f, 10.4f), new Vector3(41.85f, .3f, 11.1f));
            SleeveFence(mb, new Vector3(37.45f, 0f, 6.45f), new Vector3(39.2f, 0f, 6.45f), 1.55f, .55f);
            GBlock(mb, "SleeveFence_E", new Vector3(38.33f, 0f, 6.45f), 0f, new Vector3(1.85f, 1.55f, .6f));
            ShotGarden("sodegaki_east", new Vector3(40.4f, 1.4f, 8.2f), new Vector3(38.4f, .6f, 6.5f));

            // ---------------- 远处：东南角 ----------------
            GardenPersimmon(mb, soft, new Vector3(44.4f, 0f, 1.6f));
            GBlock(mb, "PersimmonTrunk", new Vector3(44.4f, 0f, 1.6f), 0f, new Vector3(.45f, 2f, .45f));
            At(mb, new Vector3(42.9f, 0f, 2.6f), Quaternion.Euler(0f, 215f, 0f), () =>
            {
                Wheelbarrow(mb, soft);
                GBlock(mb, "Wheelbarrow", new Vector3(0f, 0f, -.1f), 0f, new Vector3(.7f, .7f, 1.6f));
            });
            ShotGarden("wheelbarrow", new Vector3(41.4f, 1.3f, 1.2f), new Vector3(42.9f, .4f, 2.6f));
            At(mb, new Vector3(46.7f, 0f, 4.6f), Quaternion.Euler(0f, -90f, 0f), () =>
            {
                PotsAndTiles(mb);
                GBlock(mb, "PotsAndTiles", new Vector3(.25f, 0f, -.05f), 0f, new Vector3(2.3f, .5f, .8f));
            });
            ShotGarden("pots_tiles", new Vector3(44.6f, 1.3f, 5.6f), new Vector3(46.6f, .25f, 4.6f));
            VegetablePatch(mb, soft);
            GBlock(mb, "BeanFrame", new Vector3(39.75f, 0f, -5.9f), 0f, new Vector3(.9f, 1.8f, 3.8f));
            ShotGarden("vegpatch", new Vector3(36.6f, 2.0f, -2.6f), new Vector3(40.3f, .5f, -6.2f));

            GardenPlants(mb, soft);
        }

        // =====================================================================
        // 三轮车（局部：+z 朝前，原点在三个轮子中间的地面上；外面再整个放倒）
        // =====================================================================
        static void ToyWheel(MeshBatch mb, Vector3 c, float radius, float tyre)
        {
            Hoop(mb, D.Black, c, Vector3.right, radius - tyre, tyre, 14, 5);
            var q = Quaternion.Euler(0f, 0f, -90f);
            mb.Cyl(D.RustSheet, c - Vector3.right * .007f, radius - tyre * 1.5f, radius - tyre * 1.5f, .014f, 12, true, q);
            mb.Cyl(D.Iron, c - Vector3.right * .022f, .013f, .013f, .044f, 6, true, q);
        }

        static void Tricycle(MeshBatch mb)
        {
            var q = Quaternion.Euler(0f, 0f, -90f);
            ToyWheel(mb, new Vector3(0f, .125f, .26f), .125f, .014f);
            foreach (float sx in new[] { -1f, 1f })
            {
                ToyWheel(mb, new Vector3(sx * .2f, .09f, -.2f), .09f, .012f);
                // 前叉、脚蹬
                Tube(mb, D.PaintGreen, new[] { new Vector3(0f, .37f, .2f), new Vector3(sx * .03f, .3f, .215f), new Vector3(sx * .036f, .125f, .26f) }, .009f, 6);
                mb.Box(D.Iron, new Vector3(sx * .06f, .125f + sx * .035f, .26f), new Vector3(.05f, .01f, .012f));
                mb.Box(D.Black, new Vector3(sx * .11f, .125f + sx * .07f, .26f), new Vector3(.07f, .016f, .04f));
                mb.Cyl(D.Black, new Vector3(sx * .2f - .03f, .5f, .1f), .014f, .014f, .06f, 7, true, q);
            }
            // 车架：一根弯管从车头到后轴；后轴；后面的踏板
            Tube(mb, D.PaintGreen, new[] { new Vector3(0f, .35f, .19f), new Vector3(0f, .27f, .03f), new Vector3(0f, .14f, -.13f), new Vector3(0f, .09f, -.2f) }, .016f, 7);
            mb.Cyl(D.Iron, new Vector3(-.21f, .09f, -.2f), .006f, .006f, .42f, 6, true, q);
            BevelBox(mb, D.RustSheet, new Vector3(0f, .1f, -.25f), new Vector3(.3f, .008f, .07f), .002f);
            // 车头立管、车把
            Tube(mb, D.RustSheet, new[] { new Vector3(0f, .33f, .2f), new Vector3(0f, .42f, .185f), new Vector3(0f, .5f, .17f) }, .011f, 6);
            Tube(mb, D.RustSheet, new[]
            {
                new Vector3(-.19f, .5f, .1f), new Vector3(-.15f, .515f, .16f), new Vector3(0f, .5f, .17f), new Vector3(.15f, .515f, .16f), new Vector3(.19f, .5f, .1f),
            }, .009f, 6);
            // 鞍座：一块裂了皮的小坐垫，底下一根立管
            mb.Sphere(D.Black, new Vector3(0f, .31f, -.07f), new Vector3(.085f, .022f, .1f), 9);
            mb.Cyl(D.RustSheet, new Vector3(0f, .2f, -.07f), .009f, .009f, .1f, 6);
        }

        // =====================================================================
        // 石臼、石水钵
        // =====================================================================
        /// <summary>埋了大半截的一扇石臼（当飞石）：上扇中间有个添料的孔，磨面上一道道放射的沟。</summary>
        static void Millstone(MeshBatch mb, Vector3 at, float yaw, bool upper)
        {
            At(mb, at, Quaternion.Euler(G7(-2f, 2f), yaw, G7(-2f, 2f)), () =>
            {
                float hole = upper ? .045f : .022f;
                Lathe(mb, D.Stone, Vector3.zero, Quaternion.identity, new[]
                {
                    P2(.215f, -.03f), P2(.215f, .045f), P2(.205f, .056f), P2(.205f, .056f), P2(hole, .056f), P2(hole, .056f), P2(hole * .9f, .02f), P2(0f, .02f),
                }, 22);
                mb.Cyl(D.Black, new Vector3(0f, .02f, 0f), hole * .9f, hole * .9f, .002f, 8);
                for (int i = 0; i < 8; i++)
                {
                    var q = Quaternion.Euler(0f, i * 45f, 0f);
                    for (int k = 0; k < 3; k++)
                    {
                        float len = .14f - k * .035f;
                        mb.Box(D.BeamDark, q * new Vector3(.2f - len / 2f - .01f, .0566f, .018f + k * .034f), new Vector3(len, .0012f, .005f), q);
                    }
                }
                mb.Sphere(D.Moss, new Vector3(.1f, .05f, -.12f), new Vector3(.1f, .012f, .07f), 7);
            });
        }

        /// <summary>石水钵：一块粗凿的圆石，上面凿出一个浅钵，积着黑水，沿上长苔。</summary>
        static void StoneBasin(MeshBatch mb)
        {
            Lathe(mb, D.Stone, Vector3.zero, Quaternion.identity, new[]
            {
                P2(.21f, -.03f), P2(.3f, .07f), P2(.33f, .2f), P2(.31f, .3f), P2(.27f, .345f), P2(.24f, .345f), P2(.21f, .31f), P2(.17f, .25f), P2(0f, .21f),
            }, 22);
            mb.Cyl(D.Water, new Vector3(0f, .298f, 0f), .2f, .2f, .003f, 16);
            mb.Sphere(D.Moss, new Vector3(-.2f, .32f, .12f), new Vector3(.12f, .02f, .09f), 7, Quaternion.Euler(0f, 0f, 14f));
            mb.Sphere(D.Moss, new Vector3(.1f, .0f, .3f), new Vector3(.36f, .012f, .22f), 8);
            for (int i = 0; i < 4; i++) DeadLeaf(mb, new Vector3(G7(-.15f, .15f), .303f, G7(-.15f, .15f)));
        }

        // =====================================================================
        // 藤架（局部：x 沿长边，柱在 ±1.7、±1.3；+z 朝房子）。藤早枯了，只剩缠着柱子的老干和垂下来的枯蔓
        // =====================================================================
        static void WisteriaTrellis(MeshBatch mb, MeshBatch soft)
        {
            const float hx = 1.7f, hz = 1.3f, H = 2.25f;
            var toWorld = mb.M;
            var up = Vector3.up;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    BevelBox(mb, D.Post, new Vector3(sx * hx, H / 2f, sz * hz), new Vector3(.1f, H, .1f), .012f);
                    HexRings(mb, D.Stone, new Vector3(sx * hx, 0f, sz * hz), 45f, new[] { new Vector2(.14f, -.02f), new Vector2(.115f, .1f) }, 4);
                }
            foreach (float sz in new[] { -1f, 1f })
                BevelBox(mb, D.Post, new Vector3(0f, H + .055f, sz * hz), new Vector3(2f * hx + .7f, .11f, .09f), .01f);
            for (int i = 0; i < 8; i++)
            {
                float x = -hx - .1f + i * (2f * hx + .2f) / 7f;
                if (i == 5)
                {
                    // 一根垂木掉下来了：一头戳在地上，一头搭在石桌边
                    BevelBox(mb, D.TrimMid, new Vector3(.52f, .36f, -.55f), new Vector3(.05f, .06f, 2f * hz + .6f), .006f, Quaternion.Euler(-14f, 38f, 0f));
                    continue;
                }
                BevelBox(mb, D.TrimMid, new Vector3(x, H + .14f, 0f), new Vector3(.05f, .06f, 2f * hz + .6f), .006f, Quaternion.Euler(0f, G7(-1.5f, 1.5f), 0f));
            }
            for (int j = 0; j < 6; j++)
            {
                if (j == 4) continue; // 烂掉一根
                float z = -hz + .1f + j * (2f * hz - .2f) / 5f;
                mb.Cyl(D.BambooDry, new Vector3(-hx - .3f, H + .185f, z), .015f, .015f, 2f * hx + .6f, 6, true, Quaternion.Euler(0f, 0f, -90f));
            }

            // 老藤：缠着西北角的柱子上去
            var trunk = new List<Vector3>();
            var rad = new List<float>();
            for (int i = 0; i <= 14; i++)
            {
                float a = i * .92f, y = -.05f + i * .168f;
                trunk.Add(new Vector3(-hx + Mathf.Cos(a) * .105f, y, hz + Mathf.Sin(a) * .105f));
                rad.Add(Mathf.Lerp(.058f, .032f, i / 14f));
            }
            Limb(mb, D.Bark, trunk, rad, 7);
            // 架顶上爬开的几根枝，枝上垂下来的枯蔓、干豆荚、剩下的几片枯叶
            var start = new Vector3(-hx, H + .22f, hz);
            var ends = new[] { new Vector3(1.5f, H + .23f, 1.0f), new Vector3(1.9f, H + .22f, .1f), new Vector3(.9f, H + .23f, -1.1f), new Vector3(-1.2f, H + .22f, -1.3f) };
            foreach (var e in ends)
            {
                var pts = new List<Vector3>();
                var rr = new List<float>();
                float ph = G7(0f, 6.28f);
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    var side = Vector3.Cross(up, (e - start).normalized);
                    pts.Add(Vector3.Lerp(start, e, t) + side * (.16f * Mathf.Sin(t * 7f + ph) * Mathf.Sin(t * Mathf.PI)) + up * (.015f * Mathf.Sin(t * 13f + ph)));
                    rr.Add(Mathf.Lerp(.03f, .009f, t));
                }
                Limb(mb, D.Bark, pts, rr, 6);
                for (int i = 1; i < pts.Count; i++)
                {
                    int n = 1 + g7.Next(2);
                    for (int k = 0; k < n; k++)
                    {
                        var p = Vector3.Lerp(pts[i - 1], pts[i], G7(0f, 1f)) - up * .02f;
                        float len = G7(.25f, .95f);
                        var drift = new Vector3(G7(-.12f, .12f), 0f, G7(-.12f, .12f));
                        var tip = p - up * len + drift;
                        Limb(mb, D.Bark, new[] { p, p - up * (len * .5f) + drift * .8f, tip }, new[] { .006f, .0045f, .0025f }, 4);
                        if (g7.Next(3) == 0)
                            mb.Box(D.LeafDead, tip - up * .05f, new Vector3(.014f, .12f, .006f), Quaternion.Euler(G7(-14f, 14f), G7(0f, 360f), G7(-14f, 14f)));
                        if (floraShrub != null && g7.Next(4) == 0)
                            FloraCross(soft, floraShrub, toWorld.MultiplyPoint3x4(p - up * (len * .35f)), (Vector3.down + RandDir(g7) * .4f).normalized, .24f, 3, Vector3.up, G7(0f, 180f));
                    }
                }
            }

            // 石桌：一根鼓腰的石柱托着圆桌面
            var tb = new Vector3(.1f, 0f, -.05f);
            Lathe(mb, D.Stone, tb, Quaternion.identity, new[] { P2(.27f, -.02f), P2(.29f, .04f), P2(.2f, .09f), P2(.13f, .2f), P2(.125f, .42f), P2(.2f, .5f), P2(.23f, .535f) }, 18);
            Lathe(mb, D.Stone, tb, Quaternion.Euler(1.5f, 0f, 1f), new[] { P2(0f, .535f), P2(.43f, .535f), P2(.455f, .555f), P2(.455f, .6f), P2(.44f, .62f), P2(0f, .625f) }, 26);
            mb.Sphere(D.Moss, tb + new Vector3(-.2f, .622f, .15f), new Vector3(.16f, .01f, .11f), 7);
            At(mb, tb + new Vector3(.18f, .626f, -.12f), Quaternion.Euler(0f, 40f, 94f), () => Yunomi(mb, new Vector3(0f, -.03f, 0f), D.CeramicBlue));
            for (int i = 0; i < 12; i++) DeadLeaf(mb, tb + new Vector3(G7(-.38f, .38f), .63f, G7(-.38f, .38f)));
            // 石凳（鼓形）：一只立着，一只倒了
            var stool = new[] { P2(0f, 0f), P2(.15f, 0f), P2(.19f, .13f), P2(.2f, .22f), P2(.19f, .31f), P2(.15f, .43f), P2(0f, .43f) };
            Lathe(mb, D.Stone, new Vector3(-.78f, -.02f, .2f), Quaternion.identity, stool, 16);
            Lathe(mb, D.Stone, new Vector3(.95f, .185f, -.75f), Quaternion.Euler(0f, 35f, 0f) * Quaternion.Euler(0f, 0f, 90f), stool, 16);
            for (int i = 0; i < 46; i++) DeadLeaf(mb, new Vector3(G7(-hx - .4f, hx + .4f), .012f, G7(-hz - .4f, hz + .4f)));
        }

        // =====================================================================
        // 石祠、石鸟居、地藏（局部：+z 朝正面）
        // =====================================================================
        static void StoneShrine(MeshBatch mb)
        {
            // 基坛：两层切石，脚下塞着几块野石
            BevelBox(mb, D.Stone, new Vector3(0f, .08f, 0f), new Vector3(.98f, .2f, .98f), .02f, Quaternion.Euler(0f, 2f, 0f));
            BevelBox(mb, D.Stone, new Vector3(0f, .24f, -.02f), new Vector3(.72f, .13f, .72f), .015f, Quaternion.Euler(0f, -1.5f, .8f));
            RockModel(mb, new Vector3(-.6f, 0f, .35f), new Vector3(.2f, .15f, .18f), 30f, 2, true);
            RockModel(mb, new Vector3(.62f, 0f, -.2f), new Vector3(.22f, .16f, .2f), 140f, 5, true);
            // 祠身：正面一个黑洞洞的龛（没有门），两边刻的框
            const float y0 = .305f;
            BevelBox(mb, D.Stone, new Vector3(0f, y0 + .2f, -.03f), new Vector3(.42f, .4f, .36f), .01f);
            mb.Box(D.Black, new Vector3(0f, y0 + .2f, .1515f), new Vector3(.22f, .26f, .003f));
            foreach (float sx in new[] { -1f, 1f })
                BevelBox(mb, D.Stone, new Vector3(sx * .135f, y0 + .2f, .158f), new Vector3(.035f, .3f, .02f), .004f);
            BevelBox(mb, D.Stone, new Vector3(0f, y0 + .355f, .158f), new Vector3(.3f, .035f, .02f), .004f);
            BevelBox(mb, D.Stone, new Vector3(0f, y0 + .045f, .165f), new Vector3(.3f, .03f, .035f), .004f);
            // 屋顶：流造——前坡长、往前探出去；屋脊上一根圆石
            float e = y0 + .4f, rz = -.06f, ry = e + .2f;
            const float hw = .37f;
            Slab(mb, D.Stone, new Vector3(-hw, e - .03f, .4f), new Vector3(hw, e - .03f, .4f), new Vector3(hw, ry, rz), new Vector3(-hw, ry, rz), .05f);
            Slab(mb, D.Stone, new Vector3(hw, e + .03f, -.34f), new Vector3(-hw, e + .03f, -.34f), new Vector3(-hw, ry, rz), new Vector3(hw, ry, rz), .05f);
            foreach (float sx in new[] { -1f, 1f })
                mb.Poly(D.Stone, new[] { new Vector3(sx * (hw - .04f), e - .06f, .3f), new Vector3(sx * (hw - .04f), e - .01f, -.3f), new Vector3(sx * (hw - .04f), ry - .05f, rz) },
                    new[] { new Vector2(.3f, 0f), new Vector2(-.3f, .05f), new Vector2(rz, .2f) }, new Vector3(sx, 0f, 0f));
            mb.Cyl(D.Stone, new Vector3(-hw - .02f, ry + .01f, rz), .04f, .04f, 2f * hw + .04f, 8, true, Quaternion.Euler(0f, 0f, -90f));
            mb.Sphere(D.Moss, new Vector3(.1f, e + .1f, .16f), new Vector3(.22f, .015f, .16f), 8, Quaternion.Euler(27f, 0f, 0f));
            // 供着的东西：一只空碗、一只倒了的杯子、一只插着枯枝的小瓶
            Lathe(mb, D.CeramicWhite, new Vector3(-.12f, .306f, .27f), Quaternion.identity, new[]
            {
                P2(.022f, 0f), P2(.026f, .006f), P2(.05f, .03f), P2(.055f, .04f), P2(.05f, .04f), P2(.044f, .03f), P2(.02f, .01f), P2(0f, .01f),
            }, 14);
            At(mb, new Vector3(.08f, .336f, .29f), Quaternion.Euler(0f, 70f, 92f), () => Yunomi(mb, new Vector3(0f, -.03f, 0f), D.CeramicWhite));
            Lathe(mb, D.CeramicWhite, new Vector3(.24f, .306f, .22f), Quaternion.identity, new[]
            {
                P2(.02f, 0f), P2(.032f, .03f), P2(.026f, .07f), P2(.012f, .09f), P2(.015f, .11f), P2(.01f, .11f), P2(.008f, .09f), P2(0f, .09f),
            }, 10);
            Limb(mb, D.Bark, new[] { new Vector3(.24f, .39f, .22f), new Vector3(.25f, .5f, .23f), new Vector3(.29f, .58f, .2f) }, new[] { .004f, .003f, .002f }, 4);
            Limb(mb, D.Bark, new[] { new Vector3(.25f, .47f, .23f), new Vector3(.21f, .55f, .26f) }, new[] { .003f, .0015f }, 4);
            for (int i = 0; i < 14; i++) DeadLeaf(mb, new Vector3(G7(-.5f, .5f), i < 5 ? .182f : .012f, i < 5 ? G7(.38f, .47f) : G7(.5f, 1.4f)));
        }

        /// <summary>小石鸟居（明神型，高 1.55 m）：两根略往里倾的石柱、贯、島木和两头翘的笠木；一道快烂断的注连绳，剩两片纸垂。</summary>
        static void StoneTorii(MeshBatch mb)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Cyl(D.Stone, new Vector3(sx * .56f, -.03f, 0f), .062f, .052f, 1.46f, 12, true, Quaternion.Euler(0f, 0f, sx * 2.2f));
                Lathe(mb, D.Stone, new Vector3(sx * .56f, 0f, 0f), Quaternion.identity, new[] { P2(.11f, -.02f), P2(.1f, .05f), P2(.07f, .08f) }, 12);
            }
            BevelBox(mb, D.Stone, new Vector3(0f, 1.1f, 0f), new Vector3(1.32f, .075f, .055f), .008f);
            BevelBox(mb, D.Stone, new Vector3(0f, 1.25f, 0f), new Vector3(.09f, .23f, .05f), .006f);
            BevelBox(mb, D.Stone, new Vector3(0f, 1.42f, 0f), new Vector3(1.46f, .075f, .1f), .01f);
            // 笠木：中间平、两头往上翘
            foreach (float sx in new[] { -1f, 1f })
            {
                BevelBox(mb, D.Stone, new Vector3(sx * .38f, 1.5f, 0f), new Vector3(.78f, .065f, .14f), .01f, Quaternion.Euler(0f, 0f, sx * 3f));
                BevelBox(mb, D.Stone, new Vector3(sx * .82f, 1.535f, 0f), new Vector3(.16f, .065f, .14f), .01f, Quaternion.Euler(0f, 0f, sx * 13f));
            }
            // 注连绳
            var rope = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                rope.Add(new Vector3(Mathf.Lerp(-.52f, .52f, t), .98f - .13f * Mathf.Sin(t * Mathf.PI) - (t > .7f ? (t - .7f) * .5f : 0f), .062f));
            }
            Tube(mb, D.Straw, rope, .014f, 6);
            foreach (float x in new[] { -.2f, .12f })
            {
                float y = .98f - .13f * Mathf.Sin((x + .52f) / 1.04f * Mathf.PI) - .014f;
                var pts = new[] { new Vector3(x, y, .064f), new Vector3(x + .04f, y - .01f, .064f), new Vector3(x + .05f, y - .07f, .07f), new Vector3(x + .015f, y - .13f, .066f), new Vector3(x - .01f, y - .07f, .07f) };
                var uv = new[] { Vector2.zero, new Vector2(.04f, 0f), new Vector2(.05f, .07f), new Vector2(.015f, .13f), new Vector2(-.01f, .07f) };
                mb.Poly(D.PaperOld, pts, uv, Vector3.forward);
                mb.Poly(D.PaperOld, pts, uv, Vector3.back);
            }
            mb.Sphere(D.Moss, new Vector3(-.3f, 1.537f, 0f), new Vector3(.2f, .012f, .06f), 7);
        }

        /// <summary>地藏（高约 0.52 m）：莲座、直筒的袈裟、合着的手、圆头，脖子上一条褪色的格子围兜，头顶和肩上长了苔。</summary>
        static void Jizo(MeshBatch mb)
        {
            var id = Quaternion.identity;
            Lathe(mb, D.Stone, Vector3.zero, id, new[] { P2(.16f, -.02f), P2(.17f, .03f), P2(.135f, .06f), P2(.15f, .075f), P2(.125f, .095f), P2(0f, .095f) }, 16);
            Lathe(mb, D.Stone, Vector3.zero, id, new[]
            {
                P2(.118f, .09f), P2(.108f, .2f), P2(.098f, .3f), P2(.102f, .355f), P2(.08f, .392f), P2(.042f, .41f), P2(.036f, .425f),
            }, 16);
            mb.Sphere(D.Stone, new Vector3(0f, .478f, 0f), new Vector3(.064f, .07f, .066f), 12);
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sphere(D.Stone, new Vector3(sx * .064f, .475f, 0f), new Vector3(.009f, .024f, .014f), 6);                   // 耳
                Limb(mb, D.Stone, new[] { new Vector3(sx * .098f, .36f, .01f), new Vector3(sx * .1f, .3f, .05f), new Vector3(sx * .03f, .31f, .1f) },
                    new[] { .03f, .028f, .022f }, 6);                                                                         // 袖子
            }
            mb.Sphere(D.Stone, new Vector3(0f, .32f, .104f), new Vector3(.026f, .034f, .018f), 7);                              // 合掌
            // 围兜
            LatheArc(mb, D.GinghamRed, Vector3.zero, id, new[] { P2(.106f, .27f), P2(.11f, .3f), P2(.112f, .36f), P2(.092f, .392f), P2(.056f, .41f) }, 8, 22f, 158f);
            Hoop(mb, D.GinghamRed, new Vector3(0f, .414f, 0f), Vector3.up, .046f, .008f, 12, 5);
            mb.Sphere(D.Moss, new Vector3(.01f, .54f, -.01f), new Vector3(.045f, .012f, .045f), 7);
            mb.Sphere(D.Moss, new Vector3(-.07f, .398f, -.03f), new Vector3(.04f, .012f, .05f), 7, Quaternion.Euler(0f, 0f, 30f));
            mb.Sphere(D.Moss, new Vector3(0f, .0f, .05f), new Vector3(.34f, .012f, .3f), 9);
            // 脚边一只杯子，倒了
            At(mb, new Vector3(.13f, .03f, .2f), Quaternion.Euler(0f, 20f, 90f), () => Yunomi(mb, new Vector3(0f, -.03f, 0f), D.CeramicWhite));
            for (int i = 0; i < 8; i++) DeadLeaf(mb, new Vector3(G7(-.3f, .3f), .014f, G7(-.1f, .4f)));
        }

        // =====================================================================
        // 院子里的老柿树：比墙外那棵小，柿子只剩梢上零星几个，地上烂了几个
        // =====================================================================
        static void GardenPersimmon(MeshBatch mb, MeshBatch soft, Vector3 b)
        {
            var rng = new System.Random(7261);
            float R(float a, float c) => a + (c - a) * (float)rng.NextDouble();
            var up = Vector3.up;
            var lean = new Vector3(.12f, 1f, -.07f).normalized;
            var fork = b + lean * 1.7f;
            Limb(mb, D.Bark, new[] { b - up * .15f, b + lean * .8f + new Vector3(.05f, 0f, .04f), fork }, new[] { .2f, .16f, .14f }, 9);
            var tips = new List<(Vector3 p, Vector3 d)>();
            var mains = new[] { new Vector3(-.7f, .72f, .1f), new Vector3(-.3f, .8f, -.55f), new Vector3(.1f, 1f, .05f), new Vector3(.55f, .75f, .4f), new Vector3(.4f, .74f, -.45f) };
            float[] lens = { 1.7f, 1.5f, 1.4f, 1.3f, 1.3f };
            for (int i = 0; i < mains.Length; i++)
                Grow(mb, rng, fork + new Vector3(R(-.06f, .06f), R(-.08f, .1f), R(-.06f, .06f)), mains[i].normalized, lens[i], .085f, 1, 4, .72f, tips);
            int fruit = 0;
            foreach (var (p, d) in tips)
            {
                if (rng.Next(10) >= 2 || fruit >= 34 || p.y < 2.6f) continue;
                var at = p - up * R(.04f, .08f) + new Vector3(R(-.04f, .04f), 0f, R(-.04f, .04f));
                float k = R(.9f, 1.08f);
                mb.Sphere(D.Persimmon, at, new Vector3(.042f, .036f, .042f) * k, 6);
                mb.Box(D.BeamDark, at + up * (.035f * k), new Vector3(.03f, .008f, .03f) * k);
                fruit++;
            }
            for (int i = 0; i < 9; i++)
            {
                var at = new Vector3(b.x + R(-1.7f, 1.7f), .014f, b.z + R(-1.7f, 1.7f));
                mb.Sphere(D.Persimmon, at, new Vector3(.045f, .02f, .042f), 6, Quaternion.Euler(R(-12f, 12f), R(0f, 360f), 0f));
            }
            if (floraShrub != null)
                foreach (var (p, d) in tips)
                {
                    if (rng.Next(8) != 0) continue;
                    FloraCross(soft, floraShrub, p, (d + up * .3f).normalized, .3f, 3, (d + up).normalized, R(0f, 180f));
                }
            for (int i = 0; i < 40; i++) DeadLeaf(mb, new Vector3(b.x + G7(-2f, 2f), .012f, b.z + G7(-2f, 2f)));
            Debug.Log($"[Hako Art] 院子里的柿树：枝梢 {tips.Count} 个，柿子 {fruit} 个");
        }

        // =====================================================================
        // 手推车（猫車）（局部：+z 朝前）。轮胎瘪了，斗里积了半斗烂叶，长出一棵草
        // =====================================================================
        static void SheetQuad(MeshBatch mb, Material outer, Material inner, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 centre)
        {
            var n = Vector3.Cross(b - a, d - a).normalized;
            if (Vector3.Dot(n, (a + b + c + d) * .25f - centre) < 0f) n = -n;
            float w = (b - a).magnitude, h = (d - a).magnitude;
            var uv = new[] { Vector2.zero, new Vector2(w, 0f), new Vector2(w, h), new Vector2(0f, h) };
            var pts = new[] { a, b, c, d };
            mb.Poly(outer, pts, uv, n);
            mb.Poly(inner, pts, uv, -n);
        }

        static void Wheelbarrow(MeshBatch mb, MeshBatch soft)
        {
            var toWorld = mb.M;
            // 斗：底小口大的铁皮斗
            Vector3 B(float sx, float sz) => new Vector3(sx * .17f, .3f, sz > 0 ? .22f : -.3f);
            Vector3 Tp(float sx, float sz) => new Vector3(sx * .29f, .53f, sz > 0 ? .44f : -.42f);
            var c0 = new Vector3(0f, .42f, 0f);
            SheetQuad(mb, D.PaintGreen, D.RustSheet, B(-1, -1), B(1, -1), B(1, 1), B(-1, 1), c0);
            SheetQuad(mb, D.PaintGreen, D.RustSheet, B(-1, 1), B(1, 1), Tp(1, 1), Tp(-1, 1), c0);
            SheetQuad(mb, D.PaintGreen, D.RustSheet, B(1, -1), B(-1, -1), Tp(-1, -1), Tp(1, -1), c0);
            SheetQuad(mb, D.PaintGreen, D.RustSheet, B(-1, -1), B(-1, 1), Tp(-1, 1), Tp(-1, -1), c0);
            SheetQuad(mb, D.PaintGreen, D.RustSheet, B(1, 1), B(1, -1), Tp(1, -1), Tp(1, 1), c0);
            Tube(mb, D.RustSheet, new[] { Tp(-1, -1), Tp(1, -1), Tp(1, 1), Tp(-1, 1) }, .011f, 6, true);
            // 车架：两根管从把手顺着斗底收到前轮轴；两条 U 形的腿
            foreach (float sx in new[] { -1f, 1f })
            {
                Tube(mb, D.RustSheet, new[]
                {
                    new Vector3(sx * .29f, .53f, -.9f), new Vector3(sx * .28f, .43f, -.55f), new Vector3(sx * .2f, .29f, -.28f), new Vector3(sx * .17f, .28f, .2f),
                    new Vector3(sx * .08f, .2f, .58f),
                }, .014f, 6);
                mb.Cyl(D.Black, new Vector3(sx * .29f, .53f, -.9f), .019f, .019f, .13f, 7, true,
                    Quaternion.FromToRotation(Vector3.up, (new Vector3(sx * .28f, .43f, -.55f) - new Vector3(sx * .29f, .53f, -.9f)).normalized));
                Tube(mb, D.RustSheet, new[] { new Vector3(sx * .2f, .29f, -.2f), new Vector3(sx * .23f, .02f, -.26f), new Vector3(sx * .23f, .012f, -.4f), new Vector3(sx * .21f, .3f, -.34f) }, .011f, 5);
            }
            // 瘪了的轮胎
            At(mb, new Vector3(0f, .165f, .58f), Quaternion.identity, () =>
            {
                Hoop(mb, D.Black, Vector3.zero, Vector3.right, .15f, .042f, 16, 6);
                var q = Quaternion.Euler(0f, 0f, -90f);
                mb.Cyl(D.RustSheet, -Vector3.right * .02f, .115f, .115f, .04f, 12, true, q);
                mb.Cyl(D.Iron, -Vector3.right * .1f, .009f, .009f, .2f, 6, true, q);
            });
            // 斗里的烂叶和一棵草
            mb.Sphere(D.LeafDead, new Vector3(0f, .36f, .0f), new Vector3(.2f, .07f, .3f), 8);
            for (int i = 0; i < 14; i++) DeadLeaf(mb, new Vector3(G7(-.17f, .17f), .435f, G7(-.28f, .3f)));
            SusukiClump(soft, toWorld.MultiplyPoint3x4(new Vector3(.03f, .42f, .08f)), .32f);
        }

        // =====================================================================
        // 墙根的花盆和旧瓦（局部：+z 朝院子里，墙在 −z）
        // =====================================================================
        static void KawaraTile(MeshBatch mb, Vector3 at, Quaternion rot)
        {
            At(mb, at, rot, () => LatheArc(mb, D.RidgeTile, new Vector3(0f, -.282f, -.15f), Quaternion.Euler(90f, 0f, 0f),
                new[] { P2(.3f, 0f), P2(.3f, .3f), P2(.3f, .3f), P2(.285f, .3f), P2(.285f, .3f), P2(.285f, 0f), P2(.285f, 0f), P2(.3f, 0f) }, 4, 250f, 290f));
        }

        static void PotsAndTiles(MeshBatch mb)
        {
            // 平着摞的一摞瓦，旁边几片靠墙立着，地上几块碎的
            for (int i = 0; i < 7; i++)
                KawaraTile(mb, new Vector3(-.5f + G7(-.012f, .012f), .002f + i * .021f, -.2f + G7(-.012f, .012f)), Quaternion.Euler(0f, G7(-4f, 4f), 0f));
            for (int i = 0; i < 4; i++)
                KawaraTile(mb, new Vector3(-.05f + i * .05f, .15f, -.36f + i * .012f), Quaternion.Euler(0f, 90f + G7(-3f, 3f), 0f) * Quaternion.Euler(-70f - i * 2f, 0f, 0f));
            for (int i = 0; i < 6; i++)
            {
                var c = new Vector3(G7(-.9f, .5f), .006f, G7(.05f, .6f));
                float s = G7(.05f, .11f);
                mb.Poly(D.RidgeTile, new[] { c, c + new Vector3(s, .012f, .2f * s), c + new Vector3(.8f * s, .004f, s), c + new Vector3(-.1f * s, .0f, .7f * s) },
                    new[] { Vector2.zero, new Vector2(s, 0f), new Vector2(s, s), new Vector2(0f, s) }, Vector3.up);
            }
            // 瓦盆：两摞、一只倒着、一只裂开的
            float ps = ExternalModels.ScaleToHeight("planter_pot_clay", .2f);
            for (int i = 0; i < 2; i++)
                GModel(mb, "planter_pot_clay", new Vector3(.5f + G7(-.008f, .008f), i * .065f, -.2f), Quaternion.Euler(0f, G7(0f, 360f), 0f), ps);
            GModel(mb, "planter_pot_clay", new Vector3(.84f, 0f, -.05f), Quaternion.Euler(0f, G7(0f, 360f), 0f), ps * 1.15f);
            GModel(mb, "planter_pot_clay", new Vector3(.3f, 0f, .3f), Quaternion.Euler(0f, 40f, 93f), ps * 1.2f);
            GModel(mb, "wooden_bucket_02", new Vector3(1.25f, 0f, .1f), Quaternion.Euler(0f, 200f, 0f), ExternalModels.ScaleToHeight("wooden_bucket_02", .3f));
            for (int i = 0; i < 16; i++) DeadLeaf(mb, new Vector3(G7(-1f, 1.4f), .012f, G7(-.3f, .8f)));
        }

        // =====================================================================
        // 荒掉的菜地（世界坐标：东南角 x 37.7–42.9，z −8.3…−3.8）：四道塌平了的畦，一排歪倒的竹架，架上缠着枯蔓
        // =====================================================================
        static void VegetablePatch(MeshBatch mb, MeshBatch soft)
        {
            float[] xs = { 38.3f, 39.6f, 40.9f, 42.2f };
            const float zc = -6.05f, hl = 2.15f;
            for (int i = 0; i < xs.Length; i++)
                mb.Sphere(D.Ground, new Vector3(xs[i], -.01f, zc + G7(-.1f, .1f)), new Vector3(.3f, G7(.12f, .17f), hl), 10, Quaternion.Euler(0f, G7(-1.5f, 1.5f), 0f));
            // 合掌式的竹架：沿第二道畦，整排往东歪，南头两对倒了
            At(mb, new Vector3(xs[1], 0f, zc), Quaternion.Euler(0f, 0f, -13f), () =>
            {
                for (int i = 0; i < 6; i++)
                {
                    float z = -1.75f + i * .7f;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        var foot = new Vector3(sx * .36f, -.04f, z + sx * .03f);
                        var topP = new Vector3(-sx * .13f, 1.9f, z);
                        if (i < 2)
                        {
                            // 倒了：躺在畦上
                            mb.Cyl(D.BambooDry, new Vector3(sx * G7(.2f, .6f), .2f, z + G7(-.2f, .2f)), .012f, .01f, G7(1.6f, 1.95f), 6, true,
                                Quaternion.Euler(G7(70f, 86f), G7(-40f, 40f), 13f));
                            continue;
                        }
                        mb.Cyl(D.BambooDry, foot, .012f, .01f, (topP - foot).magnitude, 6, true, Quaternion.FromToRotation(Vector3.up, (topP - foot).normalized));
                    }
                    if (i >= 2) mb.Box(D.Straw, new Vector3(0f, 1.66f, z), new Vector3(.03f, .03f, .03f), Quaternion.Euler(45f, 0f, 45f));
                }
                mb.Cyl(D.BambooDry, new Vector3(0f, 1.67f, -.45f), .013f, .012f, 2.3f, 6, true, Quaternion.Euler(90f, 0f, 0f));
                // 枯蔓：在竿之间来回缠
                var world = mb.M;
                for (int k = 0; k < 9; k++)
                {
                    float z = G7(-.3f, 1.7f), sx = g7.Next(2) == 0 ? -1f : 1f;
                    var pts = new List<Vector3>();
                    for (int j = 0; j <= 5; j++)
                    {
                        float t = j / 5f, y = Mathf.Lerp(.15f, 1.75f, t);
                        float x = sx * Mathf.Lerp(.3f, -.1f, t) + .05f * Mathf.Sin(t * 9f + k);
                        pts.Add(new Vector3(x, y, z + .14f * Mathf.Sin(t * 5f + k * 1.7f)));
                    }
                    Tube(mb, D.Bark, pts, .004f, 4);
                    if (floraShrub != null)
                        for (int j = 1; j < 5; j += 2)
                            FloraCross(soft, floraShrub, world.MultiplyPoint3x4(pts[j]), (Vector3.down + RandDir(g7) * .5f).normalized, .22f, 3, Vector3.up, G7(0f, 180f));
                }
            });
            // 畦上干掉的秆子（荨麻的枯株）、杂草
            for (int i = 0; i < 12; i++)
            {
                var p = new Vector3(xs[2 + g7.Next(2)] + G7(-.15f, .15f), .1f, zc + G7(-hl * .9f, hl * .9f));
                DryStalk(mb, soft, p, G7(.7f, 1.3f));
            }
            for (int i = 0; i < 5; i++)
                SusukiClump(soft, new Vector3(G7(37.8f, 42.8f), 0f, G7(-8.3f, -3.9f)), 1f);
        }

        // =====================================================================
        // 草木：芒草、枯绣球、八角金盘、落叶堆、枯枝、树桩
        // =====================================================================
        static void SusukiClump(MeshBatch soft, Vector3 p, float size)
        {
            if (susukiMat == null) return;
            int n = 3 + g7.Next(3);
            for (int i = 0; i < n; i++)
            {
                var at = p + new Vector3(G7(-.28f, .28f), -.05f, G7(-.28f, .28f)) * size;
                FloraCross(soft, susukiMat, at, (Vector3.up + RandDir(g7) * .14f).normalized, G7(1.0f, 1.7f) * size, g7.Next(5) == 0 ? 3 : g7.Next(3), Vector3.up, G7(0f, 180f));
            }
        }

        /// <summary>畦上干掉的一棵秆子（没收的茄子、秋葵之类）：一根歪秆、两三根短杈，挂着几片枯叶。</summary>
        static void DryStalk(MeshBatch mb, MeshBatch soft, Vector3 p, float h)
        {
            var lean = new Vector3(G7(-.25f, .25f), 1f, G7(-.25f, .25f)).normalized;
            var top = p + lean * h;
            Limb(mb, D.Bark, new[] { p - Vector3.up * .05f, p + lean * (h * .5f) + new Vector3(G7(-.04f, .04f), 0f, G7(-.04f, .04f)), top }, new[] { .012f, .009f, .005f }, 5);
            for (int i = 0; i < 3; i++)
            {
                var from = p + lean * (h * G7(.35f, .85f));
                var d = (RandDir(g7) + Vector3.up * .4f).normalized;
                var tip = from + d * G7(.15f, .3f);
                Limb(mb, D.Bark, new[] { from, tip }, new[] { .006f, .003f }, 4);
                if (floraShrub != null) FloraCross(soft, floraShrub, tip, (d - Vector3.up * .8f).normalized, .2f, 3, Vector3.up, G7(0f, 180f));
            }
        }

        /// <summary>枯绣球：一丛细秆，梢上一个个干成褐色的花球，底下几片耷拉的枯叶。</summary>
        static void DeadHydrangea(MeshBatch mb, MeshBatch soft, Vector3 p, float s)
        {
            int stems = 9 + g7.Next(5);
            for (int i = 0; i < stems; i++)
            {
                float a = G7(0f, 6.283f), spread = G7(.1f, .55f) * s, h = G7(.6f, 1.1f) * s;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tip = p + dir * spread + Vector3.up * h;
                Limb(mb, D.Bark, new[] { p + dir * .04f - Vector3.up * .03f, p + dir * (spread * .35f) + Vector3.up * (h * .55f), tip }, new[] { .011f, .008f, .005f }, 5);
                if (floraShrub == null) continue;
                // 花球：几张枯叶面片朝四面攒成一团
                for (int k = 0; k < 4; k++)
                {
                    var g = (RandDir(g7) + Vector3.up * .3f).normalized;
                    FloraCross(soft, floraShrub, tip - g * .06f, g, G7(.15f, .2f), 3, (g + Vector3.up).normalized, G7(0f, 180f));
                }
                if (g7.Next(2) == 0)
                    FloraCross(soft, floraShrub, Vector3.Lerp(p, tip, G7(.4f, .7f)), (dir - Vector3.up * .5f).normalized, .24f, 3, Vector3.up, G7(0f, 180f));
            }
        }

        /// <summary>八角金盘：几根光秆，顶上一轮大叶子（喜阴，长在墙根）。</summary>
        static void Fatsia(MeshBatch mb, MeshBatch soft, Vector3 p, float s)
        {
            int stems = 4 + g7.Next(3);
            for (int i = 0; i < stems; i++)
            {
                float a = G7(0f, 6.283f), spread = G7(.1f, .5f) * s, h = G7(1.0f, 1.7f) * s;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tip = p + dir * spread + Vector3.up * h;
                Limb(mb, D.Bark, new[] { p + dir * .05f - Vector3.up * .03f, p + dir * (spread * .5f) + Vector3.up * (h * .5f), tip }, new[] { .022f, .017f, .012f }, 6);
                if (floraShrub == null) continue;
                int leaves = 6 + g7.Next(3);
                for (int k = 0; k < leaves; k++)
                {
                    float la = k * 6.283f / leaves + G7(-.3f, .3f);
                    var g = (new Vector3(Mathf.Cos(la), G7(-.15f, .45f), Mathf.Sin(la))).normalized;
                    var at = tip - Vector3.up * G7(0f, .25f);
                    FloraCard(soft, floraShrub, at, g, Vector3.Cross(Vector3.up, g).normalized, G7(.44f, .6f), G7(.44f, .6f), FloraCell(g7.Next(8) == 0 ? 3 : g7.Next(3)),
                        (Vector3.up * 1.5f + g).normalized);
                }
            }
        }

        /// <summary>落叶堆：一个扁的土包似的叶堆，面上、周围散着一片片叶子。</summary>
        static void LeafPile(MeshBatch mb, Vector3 p, float r, int n)
        {
            float h = r * .2f;
            mb.Sphere(D.LeafDead, p - Vector3.up * .01f, new Vector3(r, h, r * .8f), 9, Quaternion.Euler(0f, G7(0f, 360f), 0f));
            for (int i = 0; i < n; i++)
            {
                float a = G7(0f, 6.283f), d = Mathf.Sqrt(G7(0f, 1f)) * 1.35f;
                float y = d < 1f ? h * Mathf.Sqrt(1f - d * d) * .92f : 0f;
                DeadLeaf(mb, p + new Vector3(Mathf.Cos(a) * r * d, y + .012f, Mathf.Sin(a) * r * .8f * d));
            }
        }

        /// <summary>掉在地上的一根大枯枝：主枝贴着地，几根小枝翘着。</summary>
        static void FallenBranch(MeshBatch mb, Vector3 p, float yaw, float len)
        {
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var side = Vector3.Cross(Vector3.up, dir);
            var pts = new Vector3[7];
            var rad = new float[7];
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                rad[i] = Mathf.Lerp(.06f, .018f, t);
                pts[i] = p + dir * (len * t) + side * (.18f * Mathf.Sin(t * 4f)) + Vector3.up * (rad[i] * .8f + .05f * t * t);
            }
            Limb(mb, D.Bark, pts, rad, 7);
            for (int i = 2; i < 6; i++)
            {
                var d = (Quaternion.AngleAxis((i % 2 == 0 ? 1f : -1f) * G7(35f, 60f), Vector3.up) * dir + Vector3.up * G7(.15f, .5f)).normalized;
                Grow(mb, g7, pts[i], d, len * G7(.2f, .32f), rad[i] * .55f, 2, 3, .4f, null);
            }
        }

        /// <summary>树桩：有 Poly Haven 的 tree_stump_01 就用它，没有就画一个（鼓出来的根、锯平的顶、一层苔）。</summary>
        static void Stump(MeshBatch mb, Vector3 p, float yaw)
        {
            if (ExternalModels.Has("tree_stump_01"))
            {
                float s = ExternalModels.ScaleToFit("tree_stump_01", new Vector3(.95f, .55f, .95f));
                GModel(mb, "tree_stump_01", p - Vector3.up * .03f, Quaternion.Euler(0f, yaw, 0f), s);
                return;
            }
            At(mb, p, Quaternion.Euler(0f, yaw, 0f), () =>
            {
                Limb(mb, D.Bark, new[] { new Vector3(0f, -.06f, 0f), new Vector3(.01f, .08f, 0f), new Vector3(.015f, .24f, .01f), new Vector3(.02f, .4f, .01f) },
                    new[] { .34f, .25f, .215f, .205f }, 12);
                Lathe(mb, D.TrimLight, new Vector3(.02f, .4f, .01f), Quaternion.Euler(3f, 0f, -2f), new[] { P2(.205f, -.004f), P2(.2f, .004f), P2(0f, .008f) }, 12);
                for (int i = 0; i < 5; i++)
                {
                    var d = Quaternion.Euler(0f, i * 72f + G7(-18f, 18f), 0f) * Vector3.right;
                    Limb(mb, D.Bark, new[] { d * .2f + Vector3.up * .16f, d * .36f + Vector3.up * .05f, d * .58f - Vector3.up * .04f }, new[] { .09f, .07f, .04f }, 7);
                }
                mb.Sphere(D.Moss, new Vector3(.05f, .408f, -.03f), new Vector3(.13f, .012f, .1f), 7);
            });
        }

        static void GardenPlants(MeshBatch mb, MeshBatch soft)
        {
            // 芒草：玻璃门前 2–6 m 的空地、枯山水的白砂里、池边
            foreach (var p in new[]
                     {
                         new Vector2(21.9f, -4.7f), new Vector2(22.4f, -6.2f), new Vector2(26.5f, -8.8f), new Vector2(19.9f, -6.6f), new Vector2(11.9f, -6.9f),
                         new Vector2(18.3f, -9.3f), new Vector2(28.9f, -7.3f), new Vector2(30.9f, -7.0f), new Vector2(36.3f, -4.2f), new Vector2(29.3f, -9.7f),
                         new Vector2(24.4f, -10.1f), new Vector2(15.8f, -10.0f), new Vector2(25.1f, -3.0f), new Vector2(13.3f, -5.4f), new Vector2(35.9f, -6.2f),
                         new Vector2(39.9f, 11.9f), new Vector2(43.9f, 7.6f), new Vector2(40.6f, 4.0f), new Vector2(44.9f, 10.9f), new Vector2(38.6f, 15.6f), new Vector2(45.6f, 13.9f),
                     })
                SusukiClump(soft, new Vector3(p.x, 0f, p.y), G7(.85f, 1.15f));
            // 枯绣球、八角金盘：墙根
            DeadHydrangea(mb, soft, new Vector3(24.95f, 0f, -.62f), 1f);
            DeadHydrangea(mb, soft, new Vector3(32.7f, 0f, -.72f), 1.1f);
            DeadHydrangea(mb, soft, new Vector3(14.5f, 0f, -.7f), .9f);
            DeadHydrangea(mb, soft, new Vector3(38.0f, 0f, 12.4f), 1f);
            ShotGarden("hydrangea", new Vector3(26.2f, 1.2f, -2.6f), new Vector3(24.95f, .6f, -.62f));
            Fatsia(mb, soft, new Vector3(35.4f, 0f, -.8f), 1f);
            Fatsia(mb, soft, new Vector3(11.9f, 0f, -.85f), .9f);
            Fatsia(mb, soft, new Vector3(38.1f, 0f, 3.4f), 1f);
            // 落叶堆
            LeafPile(mb, new Vector3(13.7f, 0f, -2.1f), .55f, 60);
            LeafPile(mb, new Vector3(12.1f, 0f, -4.6f), .6f, 60);
            LeafPile(mb, new Vector3(27.6f, 0f, -.6f), .45f, 50);
            LeafPile(mb, new Vector3(21.5f, 0f, -.55f), .4f, 40);
            LeafPile(mb, new Vector3(35.6f, 0f, -5.6f), .65f, 60);
            LeafPile(mb, new Vector3(38.3f, 0f, 9.6f), .4f, 40);
            LeafPile(mb, new Vector3(43.0f, 0f, 12.0f), .6f, 60);
            // 枯枝：一根大的横在两扇门之间的飞石旁边；CC0 的小枯枝散几处
            FallenBranch(mb, new Vector3(21.6f, 0f, -4.3f), 68f, 2.4f);
            FallenBranch(mb, new Vector3(39.4f, 0f, 14.3f), 200f, 2.0f);
            if (ExternalModels.Has("dry_branches_medium_01"))
                foreach (var p in new[] { new Vector2(28.9f, -3.4f), new Vector2(40.9f, 7.2f) })
                {
                    string n = BranchParts[g7.Next(BranchParts.Length)];
                    mb.Model("dry_branches_medium_01", ExternalModels.Place("dry_branches_medium_01", n, new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, G7(0f, 360f), 0f), G7(.9f, 1.2f)), n);
                }
            // 树桩
            Stump(mb, new Vector3(29.5f, 0f, -5.7f), 40f);
            GBlock(mb, "Stump", new Vector3(29.5f, 0f, -5.7f), 0f, new Vector3(.9f, .45f, .9f));
            ShotGarden("stump", new Vector3(30.6f, 1.2f, -3.9f), new Vector3(29.5f, .25f, -5.7f));
        }
    }
}
