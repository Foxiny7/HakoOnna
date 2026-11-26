using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第七轮（2026-11-26）：主庭园的陈设。用户说縁側两扇锁着的玻璃门外面太空，清单按推荐定的：
    /// <list type="bullet">
    /// <item>第一扇门（x 16.3–19.7）：袖垣和靠着的工具、盆栽架、红叶枝上的秋千、倒掉摔成几截的石灯笼、添水、池上的石板、檐下的空鸟笼。</item>
    /// <item>两扇门之间：縁台（上面翻倒的蚊遣り豚、空花盆）。</item>
    /// <item>第二扇门（x 28.3–31.7）：倒着的三轮车、石臼做的飞石、石水钵、藤架（石桌石凳）、檐下的风铃。</item>
    /// <item>东縁側那扇门（z 7.3–10.7）：石鸟居、石祠、地藏、倒了一半的袖垣；远处老柿树、手推车、花盆和碎瓦。</item>
    /// <item>东南角：荒掉的菜地。到处：芒草、枯绣球、八角金盘、落叶堆、枯枝、树桩。</item>
    /// </list>
    /// 不加灯。雾里 7.5 m 以外全黑，所以东西都摆在离玻璃门 1–6 m 的地方。
    /// 碰撞：开工时主庭园只能看、进不去，没做碰撞。同一天用户在 V1.4（3.4）定了主庭园以后能用室外钥匙开玻璃门走进去，
    /// 所以给新加的实心东西补了简单的挡块（GBlock → Props/Dressing_Blockers 下的 Blocker_Garden7_*）；草、落叶、枯枝、贴地的石臼不挡路。
    /// 能开的两组门（V1.4 3.4，用户已定：座敷前那组 x 28.3–31.7、東縁側东面那组 z 7.3–10.7）正前方 2 m 不放挡路的东西。
    /// 不放的东西：和 9 种物品像的（锁链、油罐、白木桩、娃娃）、看起来能搜的箱子柜子、真人大小的人形。
    /// 另见 GardenDressing2.cs。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        static System.Random g7;
        static float G7(float a, float b) => a + (b - a) * (float)g7.NextDouble();

        /// <summary>在 pos、rot 的局部坐标里画（套在当前 mb.M 上，画完恢复）。</summary>
        static void At(MeshBatch mb, Vector3 pos, Quaternion rot, System.Action draw, float scale = 1f)
        {
            var old = mb.M;
            mb.M = old * Matrix4x4.TRS(pos, rot, Vector3.one * scale);
            try { draw(); }
            finally { mb.M = old; }
        }

        static int g7Blocks;

        /// <summary>
        /// 新陈设的挡块：localFloor / localYaw 是当前 mb.M 局部坐标里的底面中心和朝向（只在平移 + 绕竖轴转的坐标系里用）。
        /// </summary>
        static void GBlock(MeshBatch mb, string name, Vector3 localFloor, float localYaw, Vector3 size)
        {
            var m = mb.M;
            Blocker("Garden7_" + name, m.MultiplyPoint3x4(localFloor), localYaw + Mathf.Atan2(m.m02, m.m00) * Mathf.Rad2Deg, size);
            g7Blocks++;
        }

        /// <summary>第七轮的审图机位（机位直接给）。ArtSnapshot 按“白天 + 探索者视角 + 箱女视角”各拍一张。</summary>
        static void ShotGarden(string name, Vector3 cam, Vector3 look)
        {
            name = "r7_" + name;
            foreach (var s in RuinShots)
                if (s.name == name) return;
            RuinShots.Add((name, cam, look));
        }

        /// <summary>CC0 模型：转好以后最低点落在 basePos；缺模型时什么也不画。</summary>
        static bool GModel(MeshBatch mb, string id, Vector3 basePos, Quaternion rot, float scale = 1f, string only = null)
        {
            if (!ExternalModels.Has(id, only)) return false;
            mb.Model(id, ExternalModels.Place(id, only, basePos, rot, scale), only);
            return true;
        }

        static void BuildGardenDressing(Transform parent)
        {
            g7 = new System.Random(7126);
            g7Blocks = 0;
            var mb = new MeshBatch();   // 木、石、铁
            var soft = new MeshBatch(); // 面片：芒草、枯叶、藤上剩的叶子
            var up = Vector3.up;

            // ---------------- 第一扇玻璃门 ----------------
            // 门西边的袖垣（和墙垂直，挡住从门口往西看的视线），东面靠着扫帚、熊手、铁锹
            SleeveFence(mb, new Vector3(15.75f, 0f, -.14f), new Vector3(15.75f, 0f, -1.62f), 1.55f, 0f);
            GBlock(mb, "SleeveFence_W", new Vector3(15.95f, 0f, -.88f), 0f, new Vector3(.75f, 1.6f, 1.56f)); // 连靠着的工具
            At(mb, new Vector3(15.79f, 0f, -.14f), Quaternion.Euler(0f, 90f, 0f), () =>
            {
                // 局部：x 沿袖垣往外（世界 −z），z 是袖垣的东面往外
                GModel(mb, "wooden_broom", new Vector3(.42f, 0f, .16f), Quaternion.Euler(-14f, 8f, 0f));
                GModel(mb, "rusted_spade_01", new Vector3(.95f, 0f, .15f), Quaternion.Euler(-11f, -6f, 0f));
                BambooRake(mb, new Vector3(1.28f, 0f, .2f), Quaternion.Euler(-13f, 4f, 3f));
            });
            GModel(mb, "watering_can_metal_01", new Vector3(16.5f, 0f, -1.75f), Quaternion.Euler(0f, 130f, 96f)); // 倒着
            GModel(mb, "rubber_boots", new Vector3(16.3f, 0f, -.5f), Quaternion.Euler(0f, 70f, 0f), 1f, "dirt_r");
            GModel(mb, "rubber_boots", new Vector3(16.55f, 0f, -.62f), Quaternion.Euler(0f, 110f, 90f), 1f, "dirty_l");
            ShotGarden("sodegaki", new Vector3(18.2f, 1.5f, -2.4f), new Vector3(16.1f, .7f, -.8f));

            // 门东边的盆栽架：正面朝西（朝门这边）
            At(mb, new Vector3(20.38f, 0f, -1.32f), Quaternion.Euler(0f, -90f, 0f), () =>
            {
                BonsaiShelf(mb, soft);
                GBlock(mb, "BonsaiShelf", Vector3.zero, 0f, new Vector3(1.6f, 1.15f, .5f));
            });
            ShotGarden("bonsai", new Vector3(18.5f, 1.35f, -1.9f), new Vector3(20.4f, .7f, -1.3f));

            // 红叶（12.9, −3.3）上的秋千
            MapleSwing(mb, soft);
            GBlock(mb, "Swing", new Vector3(14.62f, 0f, -2.8f), -21f, new Vector3(.75f, 1f, .2f));
            ShotGarden("swing", new Vector3(16.9f, 1.4f, -4.9f), new Vector3(14.6f, 1.1f, -2.8f));

            // 第三盏石灯笼：倒了，朝西南摔成几截，长了苔
            At(mb, new Vector3(16.0f, 0f, -3.55f), Quaternion.Euler(0f, 169.6f, 0f), () =>
            {
                FallenLantern(mb);
                // 一个挡块包住几截（测试不许两个挡块的包围盒相交）
                GBlock(mb, "FallenLantern", new Vector3(1.1f, 0f, .05f), 0f, new Vector3(2.95f, .45f, .9f));
            });
            ShotGarden("fallen_lantern", new Vector3(16.6f, 1.4f, -1.5f), new Vector3(14.9f, .2f, -3.8f));

            // 池东北岸的添水（鹿威し），早就不响了
            At(mb, new Vector3(17.85f, 0f, -5.25f), Quaternion.Euler(0f, 205f, 0f), () =>
            {
                ShishiOdoshi(mb);
                GBlock(mb, "ShishiOdoshi", new Vector3(.1f, 0f, 0f), 0f, new Vector3(1.35f, 1f, .4f));
            });
            ShotGarden("shishi_odoshi", new Vector3(17.0f, 1.1f, -3.5f), new Vector3(17.85f, .35f, -5.25f));

            // 池上的两块石板（错开搭的沢渡り），中间一块墩石
            FlatStone(mb, new Vector3(16.75f, 0f, -5.75f), .28f, 94f, 5.1f, .2f, 3.4f);
            FlatStone(mb, new Vector3(16.3f, 0f, -7.45f), .28f, 87f, 8.3f, .2f, 3.3f);
            RockModel(mb, new Vector3(16.52f, 0f, -6.62f), new Vector3(.3f, .2f, .3f), 40f, 5, true);
            ShotGarden("pond_bridge", new Vector3(18.6f, 1.6f, -4.2f), new Vector3(16.4f, .1f, -6.8f));

            // 檐下的鸟笼（门开着，空的）
            HangingBracket(mb, new Vector3(19.95f, 2.98f, -.1f), new Vector3(19.38f, 2.94f, -.58f));
            At(mb, new Vector3(19.38f, 2.94f, -.58f), Quaternion.Euler(0f, 24f, 0f), () => BirdCage(mb));
            ShotGarden("birdcage", new Vector3(18.5f, 2.1f, -2.0f), new Vector3(19.38f, 2.3f, -.58f));

            // ---------------- 两扇门之间 ----------------
            At(mb, new Vector3(26.3f, 0f, -1.35f), Quaternion.Euler(0f, 4f, 0f), () =>
            {
                Endai(mb, soft);
                GBlock(mb, "Endai", Vector3.zero, 0f, new Vector3(1.65f, .9f, .55f));
            });
            ShotGarden("endai", new Vector3(27.9f, 1.4f, -3.1f), new Vector3(26.3f, .4f, -1.35f));

            BuildGardenDressing2(mb, soft);

            var hard = mb.Build(parent, "GardenDressing");
            var flora = soft.Build(parent, "GardenDressingFlora");
            int tris = 0, subs = 0, rends = 0;
            var per = new SortedDictionary<string, int>();
            foreach (var go in new[] { hard, flora })
            {
                if (go == null) continue;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    int t = mf.sharedMesh.triangles.Length / 3;
                    tris += t;
                    subs += mf.sharedMesh.subMeshCount;
                    rends++;
                    // 自己画的记在根节点名下，CC0 模型按模型名（根节点下第一层子物体的名字）合计
                    var top = mf.transform;
                    while (top != go.transform && top.parent != go.transform) top = top.parent;
                    string key = top == go.transform ? go.name : top.name;
                    per[key] = (per.TryGetValue(key, out int old) ? old : 0) + t;
                }
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in per) sb.Append($"{kv.Key} {kv.Value}；");
            Debug.Log($"[Hako Art] 主庭园陈设（第七轮）：{tris} 个三角形，{rends} 个渲染器，{subs} 个子网格；挡块 {g7Blocks} 个（Blocker_Garden7_*），没有灯");
            Debug.Log($"[Hako Art] 主庭园陈设分项（三角形）：{sb}");

            // 隔着玻璃门从縁側里往外看（探索者能看到的就是这几张）
            const float eye = .55f + 1.6f;
            ShotGarden("door1_centre", new Vector3(18.0f, eye, 1.3f), new Vector3(16.8f, .5f, -4.5f));
            ShotGarden("door1_to_west", new Vector3(19.2f, eye, 1.0f), new Vector3(14.4f, .8f, -3.0f));
            ShotGarden("door1_to_east", new Vector3(16.9f, eye, 1.0f), new Vector3(20.6f, 1.0f, -1.6f));
            ShotGarden("door2_centre", new Vector3(30.0f, eye, 1.3f), new Vector3(31.8f, .6f, -4.2f));
            ShotGarden("door2_to_west", new Vector3(31.2f, eye, 1.0f), new Vector3(26.6f, .5f, -1.6f));
            ShotGarden("door2_to_east", new Vector3(28.9f, eye, 1.0f), new Vector3(32.6f, .5f, -2.2f));
            ShotGarden("door_east", new Vector3(36.0f, eye, 9.0f), new Vector3(42.0f, .8f, 9.7f));
            // 从院子里看整体
            ShotGarden("overview_south_w", new Vector3(12.2f, 3.4f, -9.6f), new Vector3(19.5f, .3f, -2.0f));
            ShotGarden("overview_south_e", new Vector3(29.6f, 3.6f, -10.2f), new Vector3(31.2f, .3f, -2.8f));
            ShotGarden("overview_east", new Vector3(46.2f, 3.4f, 15.2f), new Vector3(41.0f, .3f, 8.6f));
        }

        // =====================================================================
        // 袖垣：縁側门边的短竹篱，顶上一道往外低下去的弧。collapse > 0：外半截塌了
        // =====================================================================
        static void SleeveFence(MeshBatch mb, Vector3 a, Vector3 b, float h, float collapse)
        {
            var d = b - a;
            d.y = 0f;
            float len = d.magnitude;
            At(mb, a, Quaternion.LookRotation(d / len), () =>
            {
                var up = Vector3.up;
                float keep = len * (1f - collapse);       // 这之前是完好的
                float Top(float s) => h - .42f * Mathf.Pow(s / len, 2.2f);
                // 靠墙的丸太柱；外头那根：塌的时候往外歪
                mb.Cyl(D.Bark, new Vector3(0f, 0f, .05f), .05f, .045f, h + .1f, 8);
                var outRot = collapse > 0f ? Quaternion.Euler(24f, 0f, -9f) : Quaternion.identity;
                mb.Cyl(D.Bark, new Vector3(0f, 0f, len - .05f), .045f, .04f, Top(len) + .08f, 8, true, outRot);
                // 立子：细竹一根挨一根
                int n = Mathf.FloorToInt((len - .16f) / .034f);
                for (int i = 0; i < n; i++)
                {
                    float s = .1f + i * .034f, th = Top(s) - G7(0f, .025f);
                    var uvo = new Vector2(i * .37f, 0f);
                    if (s <= keep) { mb.Box(D.BambooDry, new Vector3(0f, th / 2f, s), new Vector3(.024f, th, .028f), null, 1, uvo); continue; }
                    float r = G7(0f, 1f);
                    if (r < .35f) continue;                                                     // 没了
                    if (r < .7f)                                                                // 歪着
                        mb.Box(D.BambooDry, new Vector3(G7(-.05f, .2f), th * .46f, s + G7(-.05f, .12f)), new Vector3(.024f, th * G7(.7f, 1f), .028f),
                            Quaternion.Euler(G7(8f, 34f), 0f, G7(-38f, -8f)), 1, uvo);
                    else                                                                        // 躺在地上
                        mb.Box(D.BambooDry, new Vector3(G7(.25f, .9f), .016f, s + G7(-.3f, .4f)), new Vector3(.024f, th * G7(.6f, 1f), .028f),
                            Quaternion.Euler(90f, G7(-50f, 50f), 0f), 1, uvo);
                }
                // 押縁：两面各三道劈竹
                float railLen = collapse > 0f ? keep + .06f : len - .04f;
                foreach (float y in new[] { .26f, h * .52f, h - .5f })
                    foreach (float sx in new[] { -1f, 1f })
                        mb.Cyl(D.BambooDry, new Vector3(sx * .028f, y, .02f), .014f, .014f, railLen, 6, true, Quaternion.Euler(90f, 0f, 0f));
                // 玉縁：顶上一捆细竹顺着弧走；塌的那截垂到地上
                var cap = new List<Vector3>();
                for (int i = 0; i <= 10; i++)
                {
                    float s = len * i / 10f;
                    if (collapse > 0f && s > keep)
                    {
                        float k = Mathf.InverseLerp(keep, len, s);
                        cap.Add(new Vector3(.3f * k, Mathf.Lerp(Top(keep) + .03f, .05f, Mathf.Sqrt(k)), s + .1f * k));
                    }
                    else cap.Add(new Vector3(0f, Top(s) + .03f, s));
                }
                Tube(mb, D.BambooDry, cap, .034f, 7);
                // 棕榈绳结
                foreach (float y in new[] { .26f, h * .52f, h - .5f })
                    foreach (float s in new[] { .12f, railLen * .5f, railLen - .08f })
                        foreach (float sx in new[] { -1f, 1f })
                            mb.Box(D.Ink, new Vector3(sx * .045f, y, s), new Vector3(.012f, .04f, .04f), Quaternion.Euler(45f, 0f, 0f));
                if (collapse > 0f)
                    mb.Cyl(D.BambooDry, new Vector3(.34f, .02f, keep + .1f), .014f, .014f, len * collapse * .8f, 6, true, Quaternion.Euler(84f, 28f, 0f));
            });
        }

        /// <summary>熊手：竹柄 + 一把扇形摊开、梢上弯钩的竹齿，中间一道横竹和铁丝捆着。at = 齿梢着地的位置，rot 之前柄朝上。</summary>
        static void BambooRake(MeshBatch mb, Vector3 at, Quaternion rot)
        {
            At(mb, at, rot, () =>
            {
                mb.Cyl(D.BambooDry, new Vector3(0f, .42f, 0f), .013f, .011f, 1.15f, 7);
                for (int i = 0; i < 13; i++)
                {
                    float f = (i - 6) / 6f, x = f * .2f;
                    Tube(mb, D.BambooDry, new[]
                    {
                        new Vector3(f * .02f, .46f, 0f), new Vector3(x * .55f, .26f, .004f), new Vector3(x * .95f, .05f, .01f),
                        new Vector3(x, .012f, .028f), new Vector3(x, .0f, .05f),
                    }, .0042f, 4);
                }
                mb.Cyl(D.BambooDry, new Vector3(-.13f, .2f, -.006f), .006f, .006f, .26f, 5, true, Quaternion.Euler(0f, 0f, -90f));
                mb.Cyl(D.Iron, new Vector3(0f, .43f, 0f), .017f, .017f, .05f, 7);
            });
        }

        // =====================================================================
        // 盆栽架：敞开的三层木架（局部：x 沿长边 1.5 m，z 朝正面）。枯死的盆栽、空盆、摔在地上的一盆
        // =====================================================================
        static void BonsaiShelf(MeshBatch mb, MeshBatch soft)
        {
            const float W = 1.5f, Dp = .42f;
            float[] tiers = { .34f, .68f, 1.02f };
            foreach (float sx in new[] { -1f, 1f })
            {
                BevelBox(mb, D.Post, new Vector3(sx * (W / 2 - .04f), .56f, -Dp / 2 + .03f), new Vector3(.05f, 1.12f, .05f), .006f);
                BevelBox(mb, D.Post, new Vector3(sx * (W / 2 - .04f), .53f, Dp / 2 - .03f), new Vector3(.05f, 1.06f, .05f), .006f);
                foreach (float y in tiers)
                    BevelBox(mb, D.Post, new Vector3(sx * (W / 2 - .04f), y - .028f, 0f), new Vector3(.04f, .034f, Dp - .02f), .004f);
            }
            // 背后一根斜撑
            BevelBox(mb, D.Post, new Vector3(0f, .55f, -Dp / 2 + .012f), new Vector3(1.62f, .034f, .016f), .003f, Quaternion.Euler(0f, 0f, 33f));
            for (int t = 0; t < 3; t++)
                for (int p = 0; p < 3; p++)
                {
                    if (t == 2 && p == 2)
                    {
                        // 顶层最外面那块板掉了：一头搭在底层的板边上，一头戳在地上
                        BevelBox(mb, D.TrimMid, new Vector3(0f, .185f, Dp / 2 + .17f), new Vector3(W, .022f, .125f), .004f, Quaternion.Euler(0f, 14f, 13f));
                        continue;
                    }
                    BevelBox(mb, D.TrimMid, new Vector3(G7(-.01f, .01f), tiers[t], -Dp / 2 + .07f + p * .14f), new Vector3(W + .06f, .022f, .125f), .004f);
                }

            // 盆：(x, 层, z, 宽, 形, 釉色, 有没有树)
            var pots = new (float x, int tier, float z, float w, int kind, Material m, bool tree)[]
            {
                (-.52f, 2, -.07f, .26f, 0, D.CeramicBrown, true), (-.08f, 2, -.06f, .2f, 1, D.CeramicBlue, true), (.36f, 2, -.08f, .24f, 0, D.CeramicBrown, false),
                (-.5f, 1, 0f, .22f, 1, D.CeramicBrown, true), (-.02f, 1, .02f, .3f, 0, D.CeramicBlue, true), (.5f, 1, -.02f, .2f, 1, D.CeramicWhite, false),
                (-.3f, 0, .02f, .28f, 0, D.CeramicBrown, false), (.48f, 0, 0f, .26f, 1, D.CeramicBrown, true),
            };
            foreach (var p in pots)
                BonsaiPot(mb, soft, new Vector3(p.x, tiers[p.tier] + .011f, p.z), G7(-9f, 9f), p.w, p.kind, p.m, p.tree);
            // 底层：两只空瓦盆，一只歪倒（CC0 的瓦盆一只 3000 面，不多放）
            float ps = ExternalModels.ScaleToHeight("planter_pot_clay", .15f);
            GModel(mb, "planter_pot_clay", new Vector3(.08f, tiers[0] + .011f, .0f), Quaternion.Euler(0f, G7(0f, 360f), 0f), ps);
            GModel(mb, "planter_pot_clay", new Vector3(-.62f, tiers[0] + .011f, .05f), Quaternion.Euler(0f, 30f, 88f), ps);
            // 摔在架子前面地上的一盆：盆裂成两半，土撒了一摊，树倒在一边
            At(mb, new Vector3(.3f, 0f, Dp / 2 + .52f), Quaternion.Euler(0f, 35f, 0f), () =>
            {
                var prof = new[] { P2(.07f, 0f), P2(.085f, .012f), P2(.118f, .09f), P2(.128f, .1f), P2(.112f, .1f), P2(.1f, .086f), P2(.06f, .012f), P2(0f, .012f) };
                LatheArc(mb, D.CeramicBlue, new Vector3(0f, .1f, 0f), Quaternion.Euler(0f, 0f, 100f), prof, 9, 10f, 200f);
                LatheArc(mb, D.CeramicBlue, new Vector3(.2f, .012f, .14f), Quaternion.Euler(176f, 60f, 0f), prof, 7, 30f, 170f);
                mb.Sphere(D.Dirt, new Vector3(-.1f, .0f, .02f), new Vector3(.2f, .035f, .15f), 8, Quaternion.Euler(0f, 20f, 0f));
                for (int i = 0; i < 7; i++)
                {
                    var c = new Vector3(G7(-.3f, .34f), .004f, G7(-.22f, .3f));
                    float s = G7(.02f, .045f);
                    mb.Poly(D.CeramicBlue, new[] { c, c + new Vector3(s, .006f, .3f * s), c + new Vector3(.3f * s, .002f, s) },
                        new[] { Vector2.zero, new Vector2(s, 0f), new Vector2(0f, s) }, Vector3.up);
                }
                DeadBonsai(mb, soft, new Vector3(-.12f, .03f, .03f), Quaternion.Euler(0f, -40f, 82f), .3f, false);
            });
            // 架子顶上、地上的落叶
            for (int i = 0; i < 26; i++)
                DeadLeaf(mb, new Vector3(G7(-W / 2, W / 2), i < 9 ? tiers[2] + .012f : .004f, i < 9 ? G7(-.2f, .06f) : G7(-.3f, .75f)));
        }

        /// <summary>盆栽的盆：0 = 长方盆（四只小脚），1 = 圆盆。盆里是干裂的土；tree = 一棵枯死的小树。</summary>
        static void BonsaiPot(MeshBatch mb, MeshBatch soft, Vector3 at, float yaw, float w, int kind, Material m, bool tree)
        {
            At(mb, at, Quaternion.Euler(0f, yaw, 0f), () =>
            {
                float h = w * .3f, top;
                if (kind == 0)
                {
                    float d = w * .66f;
                    BevelBox(mb, m, new Vector3(0f, .012f + h / 2f, 0f), new Vector3(w, h, d), .008f);
                    BevelBox(mb, m, new Vector3(0f, .012f + h - .004f, 0f), new Vector3(w + .016f, .012f, d + .016f), .004f);
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sz in new[] { -1f, 1f })
                            mb.Box(m, new Vector3(sx * (w / 2 - .03f), .006f, sz * (d / 2 - .025f)), new Vector3(.035f, .012f, .028f));
                    top = .012f + h + .003f;
                    mb.Box(D.Dirt, new Vector3(0f, top, 0f), new Vector3(w - .03f, .004f, d - .03f));
                }
                else
                {
                    float r = w / 2f;
                    Lathe(mb, m, Vector3.zero, Quaternion.identity, new[]
                    {
                        P2(r * .6f, 0f), P2(r * .64f, .012f), P2(r * .96f, h * .7f), P2(r * 1.06f, h), P2(r * .95f, h), P2(r * .9f, h - .012f), P2(0f, h - .012f),
                    }, 14);
                    top = h - .012f;
                    mb.Cyl(D.Dirt, new Vector3(0f, top, 0f), r * .9f, r * .9f, .004f, 12);
                    top += .004f;
                }
                if (tree) DeadBonsai(mb, soft, new Vector3(G7(-.03f, .03f), top, 0f), Quaternion.Euler(0f, G7(0f, 360f), 0f), w * G7(1.0f, 1.35f), true);
            });
        }

        /// <summary>枯死的盆栽：一根扭着的干、几根光秃秃的枝，梢上偶尔还挂着一两片枯叶。h = 树高。</summary>
        static void DeadBonsai(MeshBatch mb, MeshBatch soft, Vector3 at, Quaternion rot, float h, bool leaves)
        {
            var world = mb.M * Matrix4x4.TRS(at, rot, Vector3.one);
            At(mb, at, rot, () =>
            {
                var pts = new Vector3[6];
                var rad = new float[6];
                float ph = G7(0f, 6.28f);
                for (int i = 0; i < 6; i++)
                {
                    float t = i / 5f;
                    pts[i] = new Vector3(Mathf.Sin(t * 4.2f + ph) * h * .16f, t * h * .8f - .006f, Mathf.Cos(t * 3.1f + ph) * h * .1f);
                    rad[i] = Mathf.Lerp(h * .06f, h * .018f, t) * (1f + .6f * Mathf.Exp(-t * 9f));
                }
                Limb(mb, D.Bark, pts, rad, 6);
                var tips = new List<(Vector3 p, Vector3 d)>();
                for (int i = 0; i < 3; i++)
                {
                    var start = pts[2 + i];
                    var dir = (Quaternion.Euler(0f, i * 127f + G7(-20f, 20f), 0f) * Vector3.right + Vector3.up * G7(.2f, .7f)).normalized;
                    Grow(mb, g7, start, dir, h * G7(.32f, .46f), h * .022f, 2, 3, .7f, tips);
                }
                if (!leaves || floraShrub == null) return;
                foreach (var (p, d) in tips)
                {
                    if (g7.Next(6) != 0) continue;
                    var wp = world.MultiplyPoint3x4(p);
                    FloraCross(soft, floraShrub, wp, (world.MultiplyVector(d) - Vector3.up * .6f).normalized, h * .34f, 3, Vector3.up, G7(0f, 180f));
                }
            });
        }

        /// <summary>一片躺着的枯叶（偶尔一片红叶）：一个略翘的小四边形。</summary>
        static void DeadLeaf(MeshBatch mb, Vector3 at)
        {
            float s = G7(.03f, .05f), yaw = G7(0f, 6.283f);
            var u = new Vector3(Mathf.Cos(yaw), G7(-.12f, .12f), Mathf.Sin(yaw)) * s;
            var v = new Vector3(-Mathf.Sin(yaw), G7(-.12f, .12f), Mathf.Cos(yaw)) * (s * .62f);
            var m = g7.Next(5) == 0 ? D.MapleLeaf : D.LeafDead;
            mb.Poly(m, new[] { at - u, at - v, at + u, at + v }, new[] { new Vector2(0f, .5f), new Vector2(.5f, 0f), new Vector2(1f, .5f), new Vector2(.5f, 1f) }, Vector3.up);
        }

        // =====================================================================
        // 秋千：从红叶的树杈上另长出一根朝门口伸的横枝，两根麻绳吊着一块木板，一头绳子松了、板子歪着
        // =====================================================================
        static void MapleSwing(MeshBatch mb, MeshBatch soft)
        {
            var bough = new[]
            {
                new Vector3(13.213f, 1.479f, -3.374f), new Vector3(13.55f, 1.98f, -3.25f), new Vector3(14.05f, 2.36f, -3.05f),
                new Vector3(14.65f, 2.52f, -2.82f), new Vector3(15.25f, 2.56f, -2.6f), new Vector3(15.72f, 2.5f, -2.42f),
            };
            Limb(mb, D.Bark, bough, new[] { .085f, .075f, .066f, .056f, .045f, .018f }, 8);
            // 枝梢上的小枝和几片没掉的红叶
            var tips = new List<(Vector3 p, Vector3 d)>();
            Grow(mb, g7, bough[4], new Vector3(.6f, .5f, .5f).normalized, .7f, .03f, 2, 3, .6f, tips);
            Grow(mb, g7, bough[3], new Vector3(.2f, .6f, -.7f).normalized, .8f, .032f, 2, 3, .6f, tips);
            if (floraMaple != null)
                foreach (var (p, d) in tips)
                    for (int i = 0; i < 3; i++)
                    {
                        var g = (new Vector3(d.x, 0f, d.z).normalized + Vector3.up * G7(-.3f, .1f) + RandDir(g7) * .3f).normalized;
                        FloraCard(soft, floraMaple, p + RandDir(g7) * .12f, g, Vector3.Cross(Vector3.up, g).normalized, .42f, .42f, FloraCell(g7.Next(3)), Vector3.up);
                    }

            Vector3 r1 = Vector3.Lerp(bough[2], bough[3], .58f), r2 = Vector3.Lerp(bough[3], bough[4], .42f);
            Vector3 s1 = new Vector3(r1.x + .03f, .54f, r1.z + .05f), s2 = new Vector3(r2.x - .02f, .42f, r2.z + .09f);
            var ax = (s2 - s1).normalized;
            var fw = Vector3.Cross(ax, Vector3.up).normalized;
            var rot = Quaternion.LookRotation(fw, Vector3.Cross(fw, ax));
            var mid = (s1 + s2) * .5f;
            BevelBox(mb, D.TrimMid, mid, new Vector3((s2 - s1).magnitude + .12f, .028f, .17f), .005f, rot);
            foreach (var (top, seat, slack) in new[] { (r1, s1, .0f), (r2, s2, .05f) })
            {
                // 绳子在枝上绕两圈，垂下来穿过板子，底下打个结
                var boughDir = (bough[4] - bough[2]).normalized;
                foreach (float o in new[] { -.012f, .012f })
                    Hoop(mb, D.Straw, top + boughDir * o, boughDir, .064f, .008f, 12, 5);
                var a = top - Vector3.up * .06f;
                Tube(mb, D.Straw, new[] { a, Vector3.Lerp(a, seat, .5f) + fw * slack, seat + Vector3.up * .02f, seat - Vector3.up * .05f }, .008f, 5);
                mb.Sphere(D.Straw, seat - Vector3.up * .035f, new Vector3(.02f, .018f, .02f), 6);
            }
            for (int i = 0; i < 3; i++) DeadLeaf(mb, mid + ax * G7(-.18f, .18f) + Vector3.up * .02f + fw * G7(-.05f, .05f));
            // 板子底下被脚蹭秃的一小块地，早长满了草：只留一圈落叶
            for (int i = 0; i < 30; i++)
                DeadLeaf(mb, new Vector3(mid.x + G7(-.9f, .9f), .012f, mid.z + G7(-.8f, .8f)));
        }

        // =====================================================================
        // 倒掉的春日灯笼（局部：x = 倒下去的方向）。尺寸和 KasugaLantern 的各段一样
        // =====================================================================
        static void FallenLantern(MeshBatch mb)
        {
            var id = Quaternion.identity;
            // 基础还在原地，被拱歪了
            At(mb, new Vector3(0f, -.03f, 0f), Quaternion.Euler(0f, 20f, -9f), () =>
            {
                HexRings(mb, D.Stone, Vector3.zero, 0f, new[] { new Vector2(.33f, -.02f), new Vector2(.33f, .1f), new Vector2(.29f, .14f) });
                Lathe(mb, D.Stone, Vector3.zero, id, new[] { new Vector2(.24f, .14f), new Vector2(.2f, .19f), new Vector2(.13f, .22f), new Vector2(.11f, .24f), new Vector2(0f, .235f) }, 20);
            });
            // 竿：断成两截，躺着
            At(mb, new Vector3(.36f, .085f, .05f), Quaternion.Euler(0f, 7f, 0f) * Quaternion.Euler(0f, 0f, -90f), () =>
                Lathe(mb, D.Stone, Vector3.zero, id, new[]
                {
                    new Vector2(0f, 0f), new Vector2(.1f, 0f), new Vector2(.094f, .08f), new Vector2(.104f, .09f), new Vector2(.104f, .11f), new Vector2(.094f, .12f),
                    new Vector2(.092f, .3f), new Vector2(.112f, .32f), new Vector2(.112f, .36f), new Vector2(.088f, .375f), new Vector2(0f, .37f),
                }, 16));
            At(mb, new Vector3(.82f, .08f, -.07f), Quaternion.Euler(0f, -16f, 0f) * Quaternion.Euler(0f, 0f, -90f), () =>
                Lathe(mb, D.Stone, Vector3.zero, id, new[]
                {
                    new Vector2(0f, .01f), new Vector2(.088f, 0f), new Vector2(.09f, .18f), new Vector2(.102f, .19f), new Vector2(.102f, .21f), new Vector2(.09f, .22f),
                    new Vector2(.09f, .28f), new Vector2(0f, .28f),
                }, 16));
            // 中台：侧着
            At(mb, new Vector3(1.3f, .21f, .2f), Quaternion.Euler(0f, 25f, 0f) * Quaternion.Euler(0f, 0f, -74f), () =>
                HexRings(mb, D.Stone, Vector3.zero, 0f, new[] { new Vector2(.14f, 0f), new Vector2(.25f, .08f), new Vector2(.27f, .09f), new Vector2(.27f, .13f), new Vector2(.25f, .14f) }, 6, true));
            // 火袋：滚到一边，窗洞朝天
            At(mb, new Vector3(1.55f, .14f, -.3f), Quaternion.Euler(0f, -24f, 0f) * Quaternion.Euler(0f, 0f, -90f), () =>
            {
                LanternFirebox(mb, Vector3.zero, 30f, .165f, 0f, .26f);
                HexRings(mb, D.Stone, Vector3.zero, 30f, new[] { new Vector2(.12f, 0f), new Vector2(.165f, 0f) }, 6, false, false);
                HexRings(mb, D.Stone, Vector3.zero, 30f, new[] { new Vector2(.165f, .26f), new Vector2(.12f, .26f) }, 6, false, false);
                mb.Cyl(D.Black, new Vector3(0f, .02f, 0f), .122f, .122f, .22f, 6);
            });
            // 笠：翻过来斜靠在地上，蕨手断了两只
            At(mb, new Vector3(2.08f, .31f, .28f), Quaternion.Euler(0f, 40f, 0f) * Quaternion.Euler(0f, 0f, 141f), () =>
            {
                HexRings(mb, D.Stone, Vector3.zero, 0f, new[]
                {
                    new Vector2(.2f, 0f), new Vector2(.45f, .02f), new Vector2(.46f, .05f), new Vector2(.33f, .1f), new Vector2(.2f, .17f),
                    new Vector2(.11f, .25f), new Vector2(.08f, .27f),
                }, 6, true);
                for (int s = 0; s < 6; s++)
                {
                    if (s == 1 || s == 4) continue;
                    float a = 2f * Mathf.PI * s / 6f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var p0 = d * .41f + Vector3.up * .055f;
                    Limb(mb, D.Stone, new[] { p0, p0 + d * .06f + Vector3.up * .025f, p0 + d * .075f + Vector3.up * .075f, p0 + d * .045f + Vector3.up * .105f },
                        new[] { .026f, .024f, .02f, .016f }, 6);
                }
            });
            // 请花 + 宝珠：滚得最远
            At(mb, new Vector3(2.5f, .075f, -.12f), Quaternion.Euler(0f, 62f, 0f) * Quaternion.Euler(0f, 0f, -102f), () =>
                Lathe(mb, D.Stone, Vector3.zero, id, new[]
                {
                    new Vector2(0f, 0f), new Vector2(.08f, 0f), new Vector2(.1f, .02f), new Vector2(.06f, .04f), new Vector2(.085f, .08f), new Vector2(.078f, .12f),
                    new Vector2(.035f, .16f), new Vector2(0f, .175f),
                }, 14));
            // 苔：长在朝上的那一面
            mb.Sphere(D.Moss, new Vector3(.55f, .175f, .05f), new Vector3(.2f, .016f, .07f), 8, Quaternion.Euler(0f, 7f, 0f));
            mb.Sphere(D.Moss, new Vector3(1.57f, .285f, -.3f), new Vector3(.12f, .014f, .1f), 8);
            mb.Sphere(D.Moss, new Vector3(1.9f, .0f, .1f), new Vector3(.6f, .012f, .45f), 10);
            mb.Sphere(D.Moss, new Vector3(-.1f, .13f, .05f), new Vector3(.2f, .018f, .16f), 8, Quaternion.Euler(0f, 0f, -9f));
            for (int i = 0; i < 16; i++) DeadLeaf(mb, new Vector3(G7(-.2f, 2.6f), .014f, G7(-.6f, .6f)));
        }

        // =====================================================================
        // 添水（鹿威し）（局部：+x = 出水的筧那一头）。水早断了：竹筒尾巴搁在石头上不动，筒口里塞着枯叶
        // =====================================================================
        static void ShishiOdoshi(MeshBatch mb)
        {
            var up = Vector3.up;
            // 支架：两根短竹桩夹着一根轴
            foreach (float sz in new[] { -1f, 1f })
                mb.Cyl(D.BambooDry, new Vector3(0f, -.03f, sz * .07f), .026f, .026f, .47f, 8);
            mb.Cyl(D.Ink, new Vector3(0f, .37f, -.1f), .007f, .007f, .2f, 5, true, Quaternion.Euler(90f, 0f, 0f));
            // 竹筒：尾巴（节封着的那头）朝 −x 搁在石头上，筒口朝 +x 翘着
            var dir = Quaternion.Euler(0f, 0f, 24f) * Vector3.right;
            var tail = new Vector3(0f, .37f, 0f) - dir * .46f;
            var q = Quaternion.FromToRotation(up, dir);
            mb.Cyl(D.BambooDry, tail, .038f, .036f, .82f, 10, true, q);
            foreach (float s in new[] { .02f, .3f, .58f })
                mb.Cyl(D.BambooDry, tail + dir * s, .041f, .041f, .012f, 10, true, q);
            mb.Cyl(D.Black, tail + dir * .815f, .029f, .029f, .008f, 8, true, q);
            RockModel(mb, new Vector3(-.45f, 0f, 0f), new Vector3(.2f, .14f, .18f), 70f, 3, true);
            // 筧：一根高竹桩，顶上横出一根细竹，口对着筒口
            mb.Cyl(D.BambooDry, new Vector3(.62f, -.03f, 0f), .032f, .03f, .98f, 9);
            mb.Cyl(D.BambooDry, new Vector3(.66f, .86f, 0f), .019f, .019f, .44f, 7, true, Quaternion.Euler(0f, 0f, 96f));
            mb.Cyl(D.Ink, new Vector3(.62f, .82f, 0f), .034f, .034f, .05f, 9);
            DeadLeaf(mb, tail + dir * .83f + up * .01f);
            DeadLeaf(mb, new Vector3(.3f, .012f, .1f));
            mb.Sphere(D.Moss, new Vector3(.2f, .0f, 0f), new Vector3(.5f, .012f, .32f), 9);
        }

        // =====================================================================
        // 檐下：墙上伸出来的铁钩；鸟笼；风铃
        // =====================================================================
        /// <summary>墙上的铁钩：从墙面 wall 伸到 hook，底下一道斜撑，梢上往上弯一个钩。</summary>
        static void HangingBracket(MeshBatch mb, Vector3 wall, Vector3 hook)
        {
            var d = hook - wall;
            Tube(mb, D.Iron, new[] { wall, wall + d * .5f + Vector3.up * .02f, hook + Vector3.up * .012f, hook + d.normalized * .03f + Vector3.up * .04f }, .007f, 5);
            Tube(mb, D.Iron, new[] { wall - Vector3.up * .22f, wall + d * .3f - Vector3.up * .1f, wall + d * .55f + Vector3.up * .012f }, .005f, 5);
            mb.Box(D.Iron, wall - Vector3.up * .1f, new Vector3(.03f, .3f, .03f));
        }

        /// <summary>
        /// 竹鸟笼（局部原点 = 挂钩，笼子在下面；+z 是正面）：方底、细竹条、四坡的顶。正面的小门推上去开着，
        /// 里面一根栖木、一只白瓷小水盅，没有鸟。
        /// </summary>
        static void BirdCage(MeshBatch mb)
        {
            const float w = .3f, bot = -.95f, top = -.6f, apex = -.47f;
            // 吊环和铁丝
            Hoop(mb, D.Iron, new Vector3(0f, -.03f, 0f), Vector3.forward, .026f, .004f, 10, 4);
            mb.Cyl(D.Iron, new Vector3(0f, apex, 0f), .003f, .003f, -apex - .055f, 4);
            // 底：木框 + 抽屉式的底板
            BevelBox(mb, D.TrimMid, new Vector3(0f, bot - .02f, 0f), new Vector3(w + .03f, .04f, w + .03f), .006f);
            float h2 = w / 2f;
            // 四角的柱、上下横档、中间一道横档
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    mb.Box(D.BambooDry, new Vector3(sx * h2, (bot + top) / 2f, sz * h2), new Vector3(.012f, top - bot, .012f));
            foreach (float y in new[] { top, (bot + top) / 2f + .02f })
            {
                foreach (float sz in new[] { -1f, 1f }) mb.Box(D.BambooDry, new Vector3(0f, y, sz * h2), new Vector3(w, .008f, .008f));
                foreach (float sx in new[] { -1f, 1f }) mb.Box(D.BambooDry, new Vector3(sx * h2, y, 0f), new Vector3(.008f, .008f, w));
            }
            // 竹条：每面 9 根；正面中间 3 根的下半截是门洞
            float doorTop = (bot + top) / 2f + .02f;
            for (int side = 0; side < 4; side++)
                for (int i = 1; i <= 9; i++)
                {
                    float t = -h2 + i * (w / 10f);
                    var p = side == 0 ? new Vector3(t, 0f, h2) : side == 1 ? new Vector3(t, 0f, -h2) : side == 2 ? new Vector3(h2, 0f, t) : new Vector3(-h2, 0f, t);
                    float y0 = bot, y1 = top;
                    if (side == 0 && i >= 4 && i <= 6) y0 = doorTop;
                    mb.Box(D.BambooDry, new Vector3(p.x, (y0 + y1) / 2f, p.z), new Vector3(.005f, y1 - y0, .005f));
                    // 顶：往中间收到尖上
                    Tube(mb, D.BambooDry, new[] { new Vector3(p.x, top, p.z), new Vector3(p.x * .55f, top + (apex - top) * .62f, p.z * .55f), new Vector3(p.x * .08f, apex, p.z * .08f) }, .0026f, 4);
                }
            mb.Sphere(D.TrimMid, new Vector3(0f, apex + .006f, 0f), new Vector3(.022f, .016f, .022f), 8);
            // 推上去的小门：三根竹条夹在两道细横档里，停在门洞上方
            At(mb, new Vector3(0f, doorTop + .075f, h2 + .008f), Quaternion.identity, () =>
            {
                foreach (float y in new[] { -.065f, .065f }) mb.Box(D.BambooDry, new Vector3(0f, y, 0f), new Vector3(.1f, .007f, .006f));
                foreach (float x in new[] { -.03f, 0f, .03f }) mb.Box(D.BambooDry, new Vector3(x, 0f, 0f), new Vector3(.005f, .13f, .005f));
            });
            // 栖木、水盅
            mb.Cyl(D.TrimLight, new Vector3(-h2, bot + .13f, -.02f), .005f, .005f, w, 5, true, Quaternion.Euler(0f, 0f, -90f));
            Lathe(mb, D.CeramicWhite, new Vector3(.09f, bot, .1f), Quaternion.identity, new[] { P2(.014f, 0f), P2(.02f, .02f), P2(.017f, .02f), P2(.012f, .004f), P2(0f, .004f) }, 8);
            mb.Poly(D.Paper, new[] { new Vector3(-.12f, bot + .001f, -.12f), new Vector3(.12f, bot + .001f, -.12f), new Vector3(.12f, bot + .001f, .12f), new Vector3(-.12f, bot + .001f, .12f) },
                new[] { Vector2.zero, new Vector2(.24f, 0f), new Vector2(.24f, .24f), new Vector2(0f, .24f) }, Vector3.up);
        }

        /// <summary>铁风铃（局部原点 = 挂钩）：一口小铁钟、舌、一条撕掉半截的短册。不响。</summary>
        static void WindChime(MeshBatch mb)
        {
            mb.Cyl(D.Iron, new Vector3(0f, -.2f, 0f), .0022f, .0022f, .2f, 4);
            Lathe(mb, D.Iron, new Vector3(0f, -.26f, 0f), Quaternion.identity, new[]
            {
                P2(.03f, 0f), P2(.034f, .004f), P2(.031f, .03f), P2(.02f, .052f), P2(.006f, .06f), P2(0f, .06f),
            }, 12);
            mb.Cyl(D.Iron, new Vector3(0f, -.36f, 0f), .0016f, .0016f, .13f, 4);
            mb.Cyl(D.Brass, new Vector3(0f, -.285f, 0f), .012f, .012f, .004f, 8);
            var a = new Vector3(-.02f, -.36f, 0f);
            var pts = new[] { a, a + new Vector3(.04f, 0f, 0f), a + new Vector3(.043f, -.1f, .006f), a + new Vector3(.012f, -.075f, .004f), a + new Vector3(0f, -.12f, .008f) };
            var uv = new[] { Vector2.zero, new Vector2(.04f, 0f), new Vector2(.04f, .1f), new Vector2(.012f, .075f), new Vector2(0f, .12f) };
            mb.Poly(D.PaperOld, pts, uv, Vector3.forward);
            mb.Poly(D.PaperOld, pts, uv, Vector3.back);
        }

        // =====================================================================
        // 縁台：院子里乘凉的长凳（局部：x 沿长边 1.6 m）。上面一只翻倒的蚊遣り豚、两只空花盆、落叶
        // =====================================================================
        static void Endai(MeshBatch mb, MeshBatch soft)
        {
            const float L = 1.6f, Wd = .5f, H = .4f;
            for (int i = 0; i < 5; i++)
            {
                float z = -Wd / 2 + .05f + i * .1f;
                if (i == 3)
                {
                    // 一块板朽断了：半截还在，半截一头掉在地上
                    BevelBox(mb, D.TrimMid, new Vector3(-.42f, H - .012f, z), new Vector3(.76f, .024f, .09f), .004f);
                    BevelBox(mb, D.TrimMid, new Vector3(.4f, H * .5f - .01f, z + .03f), new Vector3(.84f, .024f, .09f), .004f, Quaternion.Euler(0f, 3f, -27f));
                    continue;
                }
                BevelBox(mb, i % 2 == 0 ? D.TrimMid : D.TrimLight, new Vector3(G7(-.012f, .012f), H - .012f, z), new Vector3(L, .024f, .09f), .004f);
            }
            foreach (float sz in new[] { -1f, 1f })
                BevelBox(mb, D.Post, new Vector3(0f, H - .06f, sz * (Wd / 2 - .06f)), new Vector3(L - .16f, .07f, .035f), .005f);
            foreach (float sx in new[] { -1f, 1f })
            {
                BevelBox(mb, D.Post, new Vector3(sx * (L / 2 - .16f), H - .06f, 0f), new Vector3(.035f, .07f, Wd - .12f), .005f);
                foreach (float sz in new[] { -1f, 1f })
                    BevelBox(mb, D.Post, new Vector3(sx * (L / 2 - .14f), (H - .024f) / 2f, sz * (Wd / 2 - .07f)), new Vector3(.055f, H - .024f, .055f), .006f,
                        Quaternion.Euler(sz * -3.5f, 0f, sx * 3.5f));
                BevelBox(mb, D.Post, new Vector3(sx * (L / 2 - .14f), .12f, 0f), new Vector3(.03f, .04f, Wd - .14f), .004f);
            }
            At(mb, new Vector3(-.38f, H, .02f), Quaternion.Euler(0f, 28f, 0f), () => MosquitoPig(mb));
            float ps = ExternalModels.ScaleToHeight("planter_pot_clay", .16f);
            GModel(mb, "planter_pot_clay", new Vector3(.45f, H, -.1f), Quaternion.Euler(0f, 50f, 0f), ps);
            GModel(mb, "planter_pot_clay", new Vector3(.62f, H, .1f), Quaternion.Euler(0f, 200f, 92f), ps);
            for (int i = 0; i < 22; i++)
                DeadLeaf(mb, i < 10 ? new Vector3(G7(-L / 2, L / 2), H + .002f, G7(-Wd / 2, Wd / 2)) : new Vector3(G7(-L / 2 - .3f, L / 2 + .3f), .012f, G7(-.7f, .8f)));
        }

        /// <summary>
        /// 蚊遣り豚（局部原点 = 侧躺时贴着凳面的那一点）：素烧的小猪，屁股是敞开的大口（里面看得见是空的），
        /// 鼻子一头收小，背上两个出烟孔、一道铁丝提手。翻倒了，四只短脚朝着一边。
        /// </summary>
        static void MosquitoPig(MeshBatch mb)
        {
            At(mb, new Vector3(0f, .086f, 0f), Quaternion.Euler(74f, 0f, 0f), () =>
            {
                // 身子：绕 x 轴的回转体（先里面，过口沿，再外面，到鼻子）
                Lathe(mb, D.CeramicBrown, new Vector3(-.12f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f), new[]
                {
                    P2(0f, .2f), P2(.05f, .18f), P2(.072f, .1f), P2(.064f, 0f), P2(.074f, 0f), P2(.086f, .03f), P2(.088f, .12f), P2(.08f, .18f),
                    P2(.055f, .225f), P2(.036f, .24f), P2(.033f, .256f), P2(0f, .256f),
                }, 16);
                // 鼻孔、眼睛、耳朵、脚、出烟孔、提手
                foreach (float sz in new[] { -1f, 1f })
                {
                    mb.Cyl(D.Black, new Vector3(.1365f, .0f, sz * .012f), .006f, .006f, .001f, 6, true, Quaternion.Euler(0f, 0f, -90f));
                    mb.Sphere(D.Black, new Vector3(.085f, .034f, sz * .04f), new Vector3(.007f, .007f, .004f), 6);
                    mb.Sphere(D.CeramicBrown, new Vector3(.05f, .075f, sz * .045f), new Vector3(.022f, .026f, .008f), 6, Quaternion.Euler(sz * 25f, 0f, -25f));
                    foreach (float x in new[] { -.06f, .05f })
                        mb.Cyl(D.CeramicBrown, new Vector3(x, -.108f, sz * .04f), .015f, .017f, .03f, 7);
                    mb.Cyl(D.Black, new Vector3(sz * .03f - .01f, .0872f, 0f), .011f, .011f, .001f, 7);
                }
                Tube(mb, D.Iron, new[] { new Vector3(-.07f, .084f, 0f), new Vector3(-.04f, .15f, 0f), new Vector3(.02f, .15f, 0f), new Vector3(.05f, .08f, 0f) }, .0022f, 4);
            });
        }
    }
}
