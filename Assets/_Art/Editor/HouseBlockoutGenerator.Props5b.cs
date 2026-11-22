using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>第五轮（室内）：铠甲和兜、雏人形和人形、人台、脚踏缝纫机。说明见 Props5.cs。</summary>
    public static partial class HouseBlockoutGenerator
    {
        // =====================================================================
        // 兜、铠甲
        // =====================================================================
        /// <summary>
        /// 兜：锈铁的筋兜鉢（十二道筋）、眉庇、三段錣、吹返、金色鍬形和前立；下面黑漆面頬（露着金牙）、垂。
        /// o = 兜鉢底圈的圆心。面頬后面是一团黑——里面什么也没有。
        /// </summary>
        static void Kabuto(MeshBatch mb, Vector3 o)
        {
            var id = Quaternion.identity;
            var bowl = new[] { P2(.118f, 0f), P2(.123f, .03f), P2(.113f, .09f), P2(.082f, .14f), P2(.036f, .17f), P2(.014f, .178f), P2(.014f, .19f), P2(0f, .192f) };
            Lathe(mb, D.Iron, o, id, bowl, 20);
            for (int i = 0; i < 12; i++)
            {
                float a = (i * 30f + 15f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var pts = new List<Vector3>();
                for (int j = 0; j < 6; j++) pts.Add(o + d * (bowl[j].x + .002f) + Vector3.up * bowl[j].y);
                Tube(mb, D.Brass, pts, .0035f, 4);
            }
            Hoop(mb, D.Brass, o + Vector3.up * .004f, Vector3.up, .121f, .005f, 22);
            // 眉庇（上下两面）
            LatheArc(mb, D.Iron, o, id, new[] { P2(.17f, -.02f), P2(.118f, .004f) }, 8, 38f, 142f);
            LatheArc(mb, D.Black, o, id, new[] { P2(.118f, 0f), P2(.17f, -.024f) }, 8, 38f, 142f);
            // 錣：后面和两侧三段往外张的护颈（上下两面），每段下沿一道蓝威
            for (int j = 0; j < 3; j++)
            {
                float r0 = .122f + .036f * j, r1 = r0 + .04f, y0 = -.042f * j, y1 = y0 - .046f;
                LatheArc(mb, D.Lacquer, o, id, new[] { P2(r1, y1), P2(r0, y0) }, 14, 148f, 392f);
                LatheArc(mb, D.Black, o, id, new[] { P2(r0, y0 - .003f), P2(r1, y1 - .003f) }, 14, 148f, 392f);
                var cord = new List<Vector3>();
                for (int s = 0; s <= 14; s++)
                {
                    float a = Mathf.Lerp(148f, 392f, s / 14f) * Mathf.Deg2Rad;
                    cord.Add(o + new Vector3(Mathf.Cos(a) * (r1 - .004f), y1 + .006f, Mathf.Sin(a) * (r1 - .004f)));
                }
                Tube(mb, D.ClothIndigo, cord, .0045f, 4);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                // 吹返
                BevelBox(mb, D.Lacquer, o + new Vector3(s * .135f, -.01f, .075f), new Vector3(.012f, .075f, .06f), .004f, Quaternion.Euler(0f, s * -35f, s * -12f));
                // 鍬形：两段往外翘的金片
                mb.Box(D.Gold, o + new Vector3(s * .04f, .085f, .113f), new Vector3(.012f, .085f, .0025f), Quaternion.Euler(0f, 0f, s * -16f));
                mb.Box(D.Gold, o + new Vector3(s * .072f, .16f, .113f), new Vector3(.014f, .085f, .0025f), Quaternion.Euler(0f, 0f, s * -30f));
                mb.Box(D.Gold, o + new Vector3(s * .118f, .222f, .113f), new Vector3(.02f, .075f, .0025f), Quaternion.Euler(0f, 0f, s * -44f));
            }
            Lathe(mb, D.Gold, o + new Vector3(0f, .05f, .108f), AlongZ, new[] { P2(0f, 0f), P2(.034f, 0f), P2(.036f, .003f), P2(.03f, .006f), P2(0f, .008f) }, 14);
            // 面頬（只做前半圈）+ 鼻、金牙、垂；后面塞一团黑
            mb.Sphere(D.Black, o + new Vector3(0f, -.03f, -.005f), new Vector3(.088f, .1f, .085f), 10);
            LatheArc(mb, D.Lacquer, o + Vector3.down * .135f, id, new[] { P2(.06f, 0f), P2(.082f, .035f), P2(.094f, .085f), P2(.096f, .11f), P2(.09f, .112f) }, 12, -8f, 188f);
            mb.Sphere(D.Lacquer, o + new Vector3(0f, -.045f, .094f), new Vector3(.016f, .026f, .024f), 7);
            mb.Box(D.Gold, o + new Vector3(0f, -.09f, .089f), new Vector3(.046f, .009f, .004f));
            LatheArc(mb, D.Lacquer, o + Vector3.down * .2f, id, new[] { P2(.125f, 0f), P2(.088f, .065f) }, 10, 5f, 175f);
            // 忍緒：红绳从两侧下来在下巴底下打结
            Tube(mb, D.ClothRed, new[]
            {
                o + new Vector3(-.105f, 0f, .04f), o + new Vector3(-.085f, -.1f, .07f), o + new Vector3(0f, -.15f, .1f),
                o + new Vector3(.085f, -.1f, .07f), o + new Vector3(.105f, 0f, .04f),
            }, .005f, 5);
            Tube(mb, D.ClothRed, new[] { o + new Vector3(0f, -.15f, .1f), o + new Vector3(-.02f, -.24f, .13f) }, .005f, 5);
            Tube(mb, D.ClothRed, new[] { o + new Vector3(0f, -.15f, .1f), o + new Vector3(.025f, -.23f, .13f) }, .005f, 5);
        }

        /// <summary>
        /// 当世具足，摆在矮台和木架上：胴（一道道蓝威）、六片草摺、两片大袖、垂着的籠手、立在台前的脛当，头上是兜。
        /// 占地 0.85 × 0.75，高 1.75。没有鎧櫃（不放看起来能打开的箱子）。
        /// </summary>
        static void DrawArmor(Kit k)
        {
            var mb = k.mb;
            const float z = .36f;
            var id = Quaternion.identity;
            BevelBox(mb, D.Lacquer, new Vector3(0f, .03f, z), new Vector3(.72f, .06f, .6f), .008f);
            BevelBox(mb, D.Post, new Vector3(0f, .68f, z - .1f), new Vector3(.05f, 1.24f, .05f), .006f);
            BevelBox(mb, D.Post, new Vector3(0f, 1.13f, z - .1f), new Vector3(.62f, .04f, .04f), .006f);
            // 胴：压扁的桶，五道威
            var dou = new[] { P2(.19f, 0f), P2(.2f, .05f), P2(.226f, .3f), P2(.236f, .4f), P2(.218f, .46f), P2(.2f, .47f), P2(.19f, .46f), P2(.2f, .4f) };
            k.PushS(new Vector3(0f, .62f, z), Vector3.zero, new Vector3(1f, 1f, .8f));
            Lathe(mb, D.Lacquer, Vector3.zero, id, dou, 20);
            for (int i = 0; i < 5; i++)
            {
                float y = .06f + i * .075f, r = Mathf.Lerp(.2f, .236f, Mathf.Clamp01((y - .05f) / .35f)) + .003f;
                Hoop(mb, D.ClothIndigo, new Vector3(0f, y, 0f), Vector3.up, r, .0055f, 24, 5);
            }
            // 胸口的金具
            Lathe(mb, D.Gold, new Vector3(0f, .36f, .232f), AlongZ, new[] { P2(0f, 0f), P2(.022f, 0f), P2(.024f, .004f), P2(0f, .01f) }, 10);
            k.Pop();
            // 草摺：前面和两侧六片，每片四段小札，越往下越往外张
            for (int i = 0; i < 6; i++)
            {
                float a = -75f + i * 30f;
                var q = Quaternion.Euler(0f, a, 0f);
                for (int j = 0; j < 4; j++)
                {
                    float rad = .195f + j * .02f, y = .575f - j * .082f;
                    var p = q * new Vector3(0f, 0f, rad);
                    var c = new Vector3(p.x, y, z + p.z * .8f);
                    BevelBox(mb, D.Lacquer, c, new Vector3(.155f + j * .012f, .074f, .012f), .004f, q * Quaternion.Euler(-13f, 0f, 0f));
                    mb.Box(D.ClothIndigo, c + Vector3.up * .042f + q * new Vector3(0f, 0f, .003f), new Vector3(.15f + j * .012f, .012f, .012f), q);
                }
            }
            foreach (float s in new[] { -1f, 1f })
            {
                // 大袖：挂在架子两头的六段小札
                for (int j = 0; j < 6; j++)
                {
                    var c = new Vector3(s * (.305f + j * .007f), 1.1f - j * .061f, z);
                    BevelBox(mb, D.Lacquer, c, new Vector3(.014f, .058f, .25f), .004f, Quaternion.Euler(0f, 0f, s * 9f));
                    if (j > 0) mb.Box(D.ClothIndigo, c + new Vector3(s * .004f, .032f, 0f), new Vector3(.014f, .01f, .24f), Quaternion.Euler(0f, 0f, s * 9f));
                }
                mb.Box(D.Gold, new Vector3(s * .3f, 1.136f, z), new Vector3(.016f, .012f, .25f));
                // 籠手：蓝布袖管上三块铁板，底下一只黑手甲
                var sh = new Vector3(s * .25f, 1.08f, z + .04f);
                var el = new Vector3(s * .275f, .86f, z + .07f);
                var wr = new Vector3(s * .27f, .64f, z + .11f);
                Limb(mb, D.ClothIndigo, new[] { sh, el, wr }, new[] { .05f, .045f, .036f }, 8);
                mb.Box(D.Iron, Vector3.Lerp(sh, el, .5f) + new Vector3(s * .04f, 0f, .02f), new Vector3(.012f, .15f, .06f), Quaternion.Euler(-8f, 0f, s * 6f));
                mb.Box(D.Iron, Vector3.Lerp(el, wr, .5f) + new Vector3(s * .032f, 0f, .022f), new Vector3(.012f, .16f, .05f), Quaternion.Euler(-10f, 0f, s * -2f));
                mb.Sphere(D.Black, wr + new Vector3(0f, -.05f, .01f), new Vector3(.036f, .055f, .03f), 8);
                // 脛当：立在台子前面
                var sn = new Vector3(s * .15f, .06f, z + .2f);
                Lathe(mb, D.Lacquer, sn, id, new[] { P2(.045f, 0f), P2(.05f, .1f), P2(.058f, .28f), P2(.07f, .33f), P2(.062f, .335f), P2(.05f, .28f) }, 12);
                Hoop(mb, D.ClothIndigo, sn + Vector3.up * .08f, Vector3.up, .051f, .005f, 12, 4);
                Hoop(mb, D.ClothIndigo, sn + Vector3.up * .24f, Vector3.up, .057f, .005f, 12, 4);
            }
            Kabuto(mb, new Vector3(0f, 1.34f, z));
            KitShot(k, "r5i_armor", new Vector3(0f, 1.0f, z), 2.1f, .2f);
        }

        // =====================================================================
        // 人形
        // =====================================================================
        /// <summary>
        /// 一只坐着的雏人形（s = 1 时高约 0.19）：摊开的衣摆、两只大袖、几层衣领、胡粉的白脸、黑发。
        /// kind：0 男雛（冠、笏）1 女雛（垂发、金冠、桧扇）2 官女（垂发）3 囃子 / 随身（乌帽）。roll：倒了的那只侧躺。
        /// </summary>
        static void HinaDoll(Kit k, Vector3 at, Material robe, Material lining, float s, int kind, float yaw = 0f, float roll = 0f)
        {
            var mb = k.mb;
            var id = Quaternion.identity;
            k.PushS(at, new Vector3(0f, yaw, roll), Vector3.one * s);
            Lathe(mb, robe, Vector3.zero, id, new[] { P2(.078f, 0f), P2(.08f, .012f), P2(.062f, .05f), P2(.042f, .09f), P2(.03f, .115f), P2(.022f, .124f), P2(0f, .126f) }, 14);
            Hoop(mb, lining, new Vector3(0f, .006f, 0f), Vector3.up, .08f, .004f, 16, 4);
            Lathe(mb, lining, new Vector3(0f, .11f, 0f), id, new[] { P2(.031f, 0f), P2(.034f, .006f), P2(.025f, .02f), P2(0f, .022f) }, 12);
            Lathe(mb, D.ClothWhite, new Vector3(0f, .118f, 0f), id, new[] { P2(.024f, 0f), P2(.026f, .005f), P2(.018f, .017f), P2(0f, .018f) }, 12);
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sphere(robe, new Vector3(sx * .062f, .055f, .014f), new Vector3(.04f, .036f, .046f), 9);
                mb.Sphere(lining, new Vector3(sx * .078f, .042f, .046f), new Vector3(.014f, .022f, .018f), 6);
            }
            mb.Sphere(D.CeramicWhite, new Vector3(0f, .152f, 0f), new Vector3(.021f, .025f, .021f), 10);
            if (kind == 1 || kind == 2)
            {
                mb.Sphere(D.Black, new Vector3(0f, .16f, -.006f), new Vector3(.0235f, .022f, .023f), 9);
                Limb(mb, D.Black, new[] { new Vector3(0f, .155f, -.02f), new Vector3(0f, .1f, -.045f), new Vector3(0f, .03f, -.072f) }, new[] { .015f, .02f, .01f }, 6);
                if (kind == 1)
                {
                    mb.Cyl(D.Gold, new Vector3(0f, .18f, .004f), .012f, .017f, .012f, 8);
                    mb.Box(D.Gold, new Vector3(0f, .06f, .072f), new Vector3(.075f, .036f, .003f), Quaternion.Euler(-28f, 0f, 0f));
                }
            }
            else
            {
                mb.Sphere(D.Black, new Vector3(0f, .163f, -.004f), new Vector3(.0225f, .017f, .0225f), 9);
                mb.Box(D.Black, new Vector3(0f, .181f, 0f), new Vector3(.02f, .013f, .024f));
                if (kind == 0)
                {
                    mb.Box(D.Black, new Vector3(0f, .215f, -.012f), new Vector3(.011f, .062f, .002f));
                    mb.Box(D.TrimLight, new Vector3(.018f, .075f, .07f), new Vector3(.012f, .062f, .003f), Quaternion.Euler(-20f, 0f, 8f));
                }
                else mb.Cyl(D.Black, new Vector3(0f, .187f, -.002f), .008f, .004f, .022f, 6);
            }
            k.Pop();
        }

        /// <summary>
        /// 一只站着的市松人形（高 h）：直筒的和服、垂下来的袖子、腰带、白脸上两点黑眼一点红唇、齐刘海的黑发。
        /// 立在一块漆板上。at = 漆板底面中心。
        /// </summary>
        static void IchimatsuDoll(Kit k, Vector3 at, float h, Material kimono, Material obi)
        {
            var mb = k.mb;
            var id = Quaternion.identity;
            float s = h / .3f;
            k.PushS(at, Vector3.zero, Vector3.one * s);
            BevelBox(mb, D.Lacquer, new Vector3(0f, .008f, 0f), new Vector3(.15f, .016f, .12f), .004f);
            Lathe(mb, kimono, new Vector3(0f, .016f, 0f), id, new[] { P2(.05f, 0f), P2(.052f, .01f), P2(.04f, .1f), P2(.036f, .16f), P2(.042f, .2f), P2(.03f, .215f), P2(.016f, .22f) }, 14);
            Lathe(mb, obi, new Vector3(0f, .13f, 0f), id, new[] { P2(.04f, 0f), P2(.043f, .004f), P2(.043f, .04f), P2(.039f, .044f) }, 14);
            foreach (float sx in new[] { -1f, 1f })
            {
                Limb(mb, kimono, new[] { new Vector3(sx * .036f, .215f, 0f), new Vector3(sx * .06f, .17f, .008f), new Vector3(sx * .066f, .1f, .012f) }, new[] { .016f, .022f, .026f }, 7);
                mb.Sphere(D.CeramicWhite, new Vector3(sx * .066f, .085f, .014f), new Vector3(.008f, .012f, .008f), 5);
            }
            mb.Sphere(D.CeramicWhite, new Vector3(0f, .262f, 0f), new Vector3(.03f, .034f, .03f), 12);
            // 黑发：后脑一个罩子，前面一道齐刘海，两边垂到下巴
            LatheArc(mb, D.Black, new Vector3(0f, .23f, -.002f), id, new[] { P2(.034f, 0f), P2(.035f, .035f), P2(.03f, .055f), P2(.016f, .067f), P2(0f, .07f) }, 14, 150f, 390f);
            LatheArc(mb, D.Black, new Vector3(0f, .272f, -.002f), id, new[] { P2(.0335f, 0f), P2(.03f, .014f), P2(.016f, .026f), P2(0f, .028f) }, 10, 20f, 160f);
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Sphere(D.Black, new Vector3(sx * .011f, .266f, .0285f), new Vector3(.0042f, .0022f, .002f), 5);
            }
            mb.Sphere(D.ClothRed, new Vector3(0f, .249f, .029f), new Vector3(.004f, .002f, .002f), 5);
            k.Pop();
        }

        /// <summary>
        /// 雛人形：五段红毯台，顶上金屏风前的内裏雛和两盏雪洞，往下三人官女、五人囃子、随身和橘、樱，最下面两只高坏。
        /// 放了很多年没人收：一只囃子倒在台上，一只官女滚到了下一层。占地 1.3 × 1.0，高 1.45。
        /// </summary>
        static void DrawHina(Kit k)
        {
            var mb = k.mb;
            var id = Quaternion.identity;
            const int n = 5;
            const float W = 1.3f, step = .2f, rise = .22f;
            for (int i = 0; i < n; i++)
            {
                float dz = 1.0f - i * step;
                k.Bb(D.ClothRed, 0, 0, dz / 2, W, rise * (i + 1), dz);
                k.B(D.Black, 0, rise * (i + 1), dz - .006f, W + .002f, .012f, .012f);
            }
            float Ty(int i) => rise * (i + 1);
            float Tz(int i) => 1.0f - i * step - step / 2;
            const int t = n - 1;
            // 六曲金屏风
            for (int i = 0; i < 6; i++)
                mb.Box(D.GoldLeaf, new Vector3(-.3f + i * .12f, Ty(t) + .16f, .035f), new Vector3(.124f, .32f, .006f), Quaternion.Euler(0f, i % 2 == 0 ? 15f : -15f, 0f));
            HinaDoll(k, new Vector3(-.14f, Ty(t), Tz(t)), D.Black, D.ClothRed, .92f, 0);
            HinaDoll(k, new Vector3(.14f, Ty(t), Tz(t)), D.ClothRed, D.ClothPurple, .92f, 1);
            foreach (float sx in new[] { -.46f, .46f })
            {
                var b = new Vector3(sx, Ty(t), Tz(t));
                Lathe(mb, D.Lacquer, b, id, new[] { P2(.032f, 0f), P2(.014f, .012f), P2(.006f, .03f), P2(.006f, .13f), P2(.016f, .14f) }, 10);
                Lathe(mb, D.PaperLamp, b, id, new[] { P2(.016f, .14f), P2(.04f, .165f), P2(.046f, .2f), P2(.034f, .24f), P2(.014f, .252f), P2(0f, .252f) }, 12);
                Hoop(mb, D.Black, b + Vector3.up * .252f, Vector3.up, .014f, .003f, 8, 4);
            }
            // 三人官女：中间那只滚到了下一层
            HinaDoll(k, new Vector3(-.3f, Ty(3), Tz(3)), D.ClothWhite, D.ClothRed, .8f, 2, 8f);
            HinaDoll(k, new Vector3(.3f, Ty(3), Tz(3)), D.ClothWhite, D.ClothRed, .8f, 2, -5f);
            HinaDoll(k, new Vector3(.05f, Ty(2) + .05f, Tz(2) + .03f), D.ClothWhite, D.ClothRed, .8f, 2, 40f, 82f);
            // 五人囃子：一只倒了
            for (int i = 0; i < 5; i++)
            {
                if (i == 2) continue;
                if (i == 3) HinaDoll(k, new Vector3(-.48f + i * .24f, Ty(2) + .045f, Tz(2)), D.ClothIndigo, D.ClothWhite, .7f, 3, -30f, -84f);
                else HinaDoll(k, new Vector3(-.48f + i * .24f, Ty(2), Tz(2)), D.ClothIndigo, D.ClothWhite, .7f, 3, (i - 2) * 4f);
            }
            HinaDoll(k, new Vector3(-.35f, Ty(1), Tz(1)), D.ClothPurple, D.ClothWhite, .76f, 3, 6f);
            HinaDoll(k, new Vector3(.35f, Ty(1), Tz(1)), D.Lacquer, D.ClothRed, .76f, 3, -6f);
            // 橘、樱：漆台上的小盆栽
            foreach (float sx in new[] { -.4f, .4f })
            {
                var b = new Vector3(sx, Ty(0), Tz(0));
                BevelBox(mb, D.Lacquer, b + Vector3.up * .02f, new Vector3(.1f, .04f, .1f), .005f);
                Limb(mb, D.Bark, new[] { b + Vector3.up * .04f, b + new Vector3(.008f, .09f, 0f), b + new Vector3(-.004f, .15f, .004f) }, new[] { .007f, .006f, .004f }, 5);
                for (int i = 0; i < 5; i++)
                {
                    var d = Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(.03f, 0f, 0f);
                    var tip = b + new Vector3(0f, .1f + i % 2 * .03f, 0f) + d + Vector3.up * .03f;
                    Limb(mb, D.Bark, new[] { b + new Vector3(0f, .09f + i % 2 * .03f, 0f), tip }, new[] { .0035f, .002f }, 4);
                    if (sx < 0) FloraCross(mb, floraShrub, tip - Vector3.up * .03f, (Vector3.up + d * 6f).normalized, .1f, i % 3, (Vector3.up + d * 8f).normalized, i * 40f);
                    else
                        for (int j = 0; j < 3; j++)
                            mb.Sphere(D.ClothWhite, tip + Quaternion.Euler(j * 100f, j * 70f, 0f) * new Vector3(.012f, 0f, 0f), new Vector3(.009f, .008f, .009f), 5);
                }
            }
            // 高坏上的菱饼
            foreach (float sx in new[] { -.1f, .1f })
            {
                var b = new Vector3(sx, Ty(0), Tz(0));
                Lathe(mb, D.Lacquer, b, id, new[] { P2(.03f, 0f), P2(.012f, .01f), P2(.008f, .04f), P2(.04f, .055f), P2(.042f, .062f), P2(0f, .062f) }, 12);
                Material[] mochi = { D.Leaf, D.ClothWhite, D.ClothRed };
                for (int j = 0; j < 3; j++)
                    mb.Box(mochi[j], b + Vector3.up * (.07f + j * .014f), new Vector3(.045f, .013f, .03f), Quaternion.Euler(0f, 45f, 0f));
            }
            KitShot(k, "r5i_hina", new Vector3(0f, .8f, .5f), 2.2f, .25f);
        }

        // =====================================================================
        // 人台
        // =====================================================================
        /// <summary>
        /// 人台：包着麻布的躯干（腰收、胸鼓、肩平）、木领帽、车削的木柱、三条弯腿。没盖布的那只肩上搭一块红布、挂一条皮尺、胸口别着珠针。
        /// 占地 0.5 × 0.5，木帽顶 1.55（和原来一样，防尘布按这个高度搭）。
        /// </summary>
        static void DrawDressForm(Kit k)
        {
            var mb = k.mb;
            const float z = .25f;
            var id = Quaternion.identity;
            for (int i = 0; i < 3; i++)
            {
                var d = Quaternion.Euler(0f, i * 120f + 30f, 0f) * Vector3.right;
                Limb(mb, D.Post, new[] { new Vector3(0f, .22f, z), new Vector3(0f, .17f, z) + d * .08f, new Vector3(0f, .05f, z) + d * .18f, new Vector3(0f, .012f, z) + d * .22f },
                    new[] { .016f, .014f, .013f, .016f }, 6);
            }
            Lathe(mb, D.Post, new Vector3(0f, 0f, z), id, new[]
            {
                P2(.024f, .17f), P2(.03f, .21f), P2(.016f, .25f), P2(.017f, .5f), P2(.024f, .52f), P2(.016f, .56f), P2(.017f, .86f), P2(.035f, .9f),
            }, 10);
            var torso = new[]
            {
                P2(0f, 0f), P2(.15f, 0f), P2(.166f, .035f), P2(.168f, .1f), P2(.142f, .2f), P2(.128f, .26f), P2(.15f, .36f), P2(.18f, .43f),
                P2(.178f, .48f), P2(.13f, .52f), P2(.062f, .54f), P2(.05f, .55f),
            };
            k.PushS(new Vector3(0f, .9f, z), Vector3.zero, new Vector3(1f, 1f, .7f));
            Lathe(mb, D.ClothWhite, Vector3.zero, id, torso, 20);
            Hoop(mb, D.TrimMid, new Vector3(0f, .003f, 0f), Vector3.up, .152f, .006f, 20, 5);
            Hoop(mb, D.Black, new Vector3(0f, .26f, 0f), Vector3.up, .1295f, .003f, 20, 4);
            // 前后中缝
            foreach (float ang in new[] { 90f, 270f })
            {
                var d = Quaternion.Euler(0f, -ang, 0f) * Vector3.right;
                var seam = new List<Vector3>();
                for (int i = 1; i < 10; i++) seam.Add(d * (torso[i].x + .0015f) + Vector3.up * torso[i].y);
                Tube(mb, D.TrimMid, seam, .002f, 4);
            }
            foreach (float sx in new[] { -1f, 1f })
                Lathe(mb, D.TrimMid, new Vector3(sx * .172f, .45f, 0f), Quaternion.Euler(0f, 0f, -sx * 80f), new[] { P2(0f, 0f), P2(.045f, 0f), P2(.045f, .008f), P2(0f, .01f) }, 10);
            Lathe(mb, D.TrimMid, new Vector3(0f, .548f, 0f), id, new[] { P2(.051f, 0f), P2(.054f, .05f), P2(.046f, .058f), P2(.014f, .06f), P2(.026f, .078f), P2(.014f, .098f), P2(0f, .102f) }, 12);
            k.Pop();
            if (CoverNext) return; // 盖着防尘布：布、皮尺、珠针都在布底下
            TowelOnBar(mb, D.ClothRed, D.ClothRed, new Vector3(-.1f, 1.265f, z), Quaternion.identity, .15f, .42f, .26f, .128f, 2.3f);
            Tube(mb, D.Straw, new[]
            {
                new Vector3(.07f, 1.02f, z + .118f), new Vector3(.075f, 1.26f, z + .112f), new Vector3(.06f, 1.42f, z + .03f), new Vector3(0f, 1.455f, z - .04f),
                new Vector3(-.03f, 1.43f, z - .05f),
            }, .004f, 4);
            for (int i = 0; i < 5; i++) mb.Sphere(D.Brass, new Vector3(.03f + i * .022f, 1.22f - i * .05f, z + .112f - i * .004f), new Vector3(.005f, .005f, .005f), 5);
            KitShot(k, "r5i_dressform", new Vector3(0f, 1.1f, z), 1.8f, .2f);
        }

        // =====================================================================
        // 脚踏缝纫机
        // =====================================================================
        /// <summary>
        /// 脚踏缝纫机：两片铸铁侧架（外撇的腿、中间交叉的花撑和一个圆环）、后面的斜拉撑、栅格踏板、右边的大飞轮和连杆、皮带，
        /// 木台面上一台黑色的机头（弯臂、手轮、针杆）。占地 0.95 × 0.5；台面 0.72–0.755，机头顶约 1.03（防尘布按这个高度搭）。
        /// </summary>
        static void DrawSewing(Kit k)
        {
            var mb = k.mb;
            const float z = .25f;
            foreach (float sx in new[] { -.4f, .4f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Limb(mb, D.Iron, new[]
                    {
                        new Vector3(sx, .715f, z + sz * .17f), new Vector3(sx, .5f, z + sz * .14f), new Vector3(sx, .25f, z + sz * .17f), new Vector3(sx, .05f, z + sz * .215f),
                        new Vector3(sx, .012f, z + sz * .225f),
                    }, new[] { .013f, .012f, .012f, .013f, .018f }, 6);
                    Limb(mb, D.Iron, new[] { new Vector3(sx, .62f, z + sz * .15f), new Vector3(sx, .4f, z), new Vector3(sx, .13f, z - sz * .19f) }, new[] { .009f, .01f, .009f }, 5);
                }
                Hoop(mb, D.Iron, new Vector3(sx, .4f, z), Vector3.right, .07f, .008f, 16, 5);
                mb.Box(D.Iron, new Vector3(sx, .705f, z), new Vector3(.022f, .022f, .4f));
            }
            // 后面的斜拉撑、踏板轴
            Tube(mb, D.Iron, new[] { new Vector3(-.4f, .58f, z - .18f), new Vector3(.4f, .16f, z - .18f) }, .007f, 5);
            Tube(mb, D.Iron, new[] { new Vector3(.4f, .58f, z - .18f), new Vector3(-.4f, .16f, z - .18f) }, .007f, 5);
            Tube(mb, D.Iron, new[] { new Vector3(-.4f, .1f, z + .01f), new Vector3(.4f, .1f, z + .01f) }, .008f, 5);
            // 踏板：斜着的铁栅格
            k.Push(new Vector3(-.03f, .105f, z + .01f), 0f, 13f);
            Tube(mb, D.Iron, new[] { new Vector3(-.17f, 0f, -.12f), new Vector3(.17f, 0f, -.12f), new Vector3(.17f, 0f, .12f), new Vector3(-.17f, 0f, .12f) }, .008f, 5, true);
            for (int i = 0; i < 6; i++) Tube(mb, D.Iron, new[] { new Vector3(-.125f + i * .05f, 0f, -.12f), new Vector3(-.125f + i * .05f, 0f, .12f) }, .005f, 4);
            k.Pop();
            // 飞轮、连杆、皮带
            var wc = new Vector3(.33f, .4f, z - .03f);
            Hoop(mb, D.Iron, wc, Vector3.right, .155f, .012f, 26, 6);
            for (int i = 0; i < 5; i++)
            {
                var d = Quaternion.Euler(i * 72f + 10f, 0f, 0f) * Vector3.up;
                var side = Vector3.Cross(Vector3.right, d);
                Limb(mb, D.Iron, new[] { wc + d * .02f, wc + d * .08f + side * .018f, wc + d * .148f }, new[] { .007f, .006f, .007f }, 5);
            }
            mb.Cyl(D.Iron, wc - Vector3.right * .02f, .022f, .022f, .09f, 8, true, AlongX);
            Tube(mb, D.Iron, new[] { new Vector3(.15f, .13f, z + .09f), wc + new Vector3(-.03f, -.05f, 0f) }, .005f, 4);
            Tube(mb, D.Straw, new[] { wc + new Vector3(0f, .15f, .03f), new Vector3(.19f, .93f, z + .04f) }, .003f, 4);
            Tube(mb, D.Straw, new[] { wc + new Vector3(0f, .15f, -.03f), new Vector3(.19f, .93f, z - .04f) }, .003f, 4);
            // 台面
            BevelBox(mb, D.TrimMid, new Vector3(0f, .7375f, z), new Vector3(.95f, .035f, .46f), .006f);
            // 机头
            const float y = .755f;
            BevelBox(mb, D.Black, new Vector3(-.02f, y + .02f, z), new Vector3(.4f, .04f, .17f), .01f);
            Limb(mb, D.Black, new[]
            {
                new Vector3(.13f, y + .03f, z), new Vector3(.13f, y + .15f, z), new Vector3(.115f, y + .215f, z), new Vector3(.05f, y + .24f, z),
                new Vector3(-.1f, y + .235f, z), new Vector3(-.17f, y + .22f, z),
            }, new[] { .052f, .046f, .044f, .04f, .038f, .037f }, 10);
            BevelBox(mb, D.Black, new Vector3(-.19f, y + .165f, z), new Vector3(.062f, .17f, .078f), .014f);
            mb.Cyl(D.Iron, new Vector3(-.19f, y + .04f, z + .01f), .0035f, .0035f, .05f, 5);
            mb.Box(D.Iron, new Vector3(-.19f, y + .044f, z + .012f), new Vector3(.03f, .005f, .022f));
            mb.Box(D.Iron, new Vector3(-.19f, y + .041f, z), new Vector3(.07f, .002f, .11f));
            Lathe(mb, D.Iron, new Vector3(.175f, y + .2f, z), AlongX, new[] { P2(0f, 0f), P2(.066f, 0f), P2(.072f, .008f), P2(.066f, .02f), P2(.045f, .02f), P2(.04f, .012f), P2(0f, .012f) }, 16);
            Lathe(mb, D.Gold, new Vector3(.13f, y + .11f, z + .047f), AlongZ, new[] { P2(0f, 0f), P2(.018f, 0f), P2(.018f, .003f), P2(0f, .004f) }, 10);
            mb.Box(D.Gold, new Vector3(-.02f, y + .24f, z + .039f), new Vector3(.16f, .016f, .002f));
            if (!CoverNext)
            {
                // 线轴、压脚底下一块没缝完的布（盖着布时不画，会戳穿布）
                mb.Cyl(D.Iron, new Vector3(.02f, y + .27f, z), .002f, .002f, .045f, 4);
                Lathe(mb, D.TrimLight, new Vector3(.02f, y + .272f, z), Quaternion.identity, new[] { P2(.014f, 0f), P2(.014f, .003f), P2(.006f, .004f), P2(.006f, .03f), P2(.014f, .031f), P2(.014f, .034f) }, 8);
                mb.Cyl(D.ClothRed, new Vector3(.02f, y + .277f, z), .011f, .011f, .024f, 8);
                FoldedCloth(mb, D.ClothIndigo, new Vector3(-.25f, y, z + .06f), 12f, .3f, .014f, .22f, 6.1f);
                KitShot(k, "r5i_sewing", new Vector3(0f, .7f, z), 1.9f, .35f);
            }
        }
    }
}
