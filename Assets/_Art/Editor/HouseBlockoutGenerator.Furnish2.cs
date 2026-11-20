using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>家具与陈设（縁側、走廊、水回り、土蔵、二楼）。</summary>
    public static partial class HouseBlockoutGenerator
    {
        // ---------------------------------------------------------------- 縁側
        static void FurnEngawa(Kit k)
        {
            // 卷起的竹帘：挂在门楣外侧（门洞加宽了，帘子跟着加长；平面按比例外移，高度按新门楣直接写）
            float head = .55f + DoorTop, by = head - .11f;
            Kit.Pinned = true;
            foreach (float c in new[] { 12.73f, 21.21f })
            {
                float half = 2.7f * OpeningWidthScale / 2 + .05f;
                k.Cr(D.Straw, new Vector3(c - half, by, -.07f), .045f, .045f, 2 * half, AlongX, 10);
                foreach (float x in new[] { c - half + .4f, c + half - .4f }) k.B(D.Straw, x, by + .06f, -.07f, .006f, .12f, .1f);
            }
            k.Cr(D.Straw, new Vector3(26.452f, by, 6.36f - 2.7f * OpeningWidthScale / 2 - .05f), .045f, .045f, 2.7f * OpeningWidthScale + .1f, AlongZ, 10);
            Kit.Pinned = false;
            // 风铃
            k.B(D.Black, 16.9f, 2.82f, -.5f, .003f, .74f, .003f);
            k.S(D.CeramicBlue, 16.9f, 2.43f, -.5f, .045f, .04f, .045f, 10);
            k.B(D.Paper, 16.9f, 2.29f, -.5f, .05f, .12f, .002f);
            // 踏石上的下駄
            Geta(k, 13.0f, .275f, -.45f, 0, D.TrimLight);
            Geta(k, 13.35f, .275f, -.4f, 10f, D.TrimMid);
            // 蚊遣り豚
            k.Push(new Vector3(23.6f, .55f, .35f), 30f);
            k.S(D.CeramicWhite, 0, .1f, 0, .1f, .085f, .12f, 12);
            k.Cr(D.CeramicWhite, new Vector3(0, .1f, .11f), .05f, .045f, .04f, AlongZ, 10);
            k.Cr(D.Black, new Vector3(0, .1f, .15f), .04f, .04f, .002f, AlongZ, 10);
            foreach (float sx in new[] { -.05f, .05f }) k.Bb(D.CeramicWhite, sx, 0, 0, .03f, .03f, .03f);
            k.Cr(D.Ash, new Vector3(0, .1f, -.13f), .002f, .002f, .1f, Quaternion.Euler(-60, 0, 0), 4);
            k.Pop();
        }

        // ---------------------------------------------------------------- 走廊、楼梯间
        static void FurnCorridors(Kit k)
        {
            foreach (float x in new[] { 10.3f, 14.2f, 18.3f, 22.4f }) PendantLamp(k, x, 13.191f, 2.85f, .45f, 2);
            PendantLamp(k, 19.304f, 19.6f, 2.85f, .4f, 2);
            PendantLamp(k, 6.435f, 19.6f, 2.85f, .4f, 2);
            // 黑色转盘电话
            k.Push(new Vector3(16.5f, 1.35f, 14.076f), 180f);
            k.Bb(D.TrimMid, 0, 0, .14f, .48f, .03f, .28f);
            foreach (float sx in new[] { -.2f, .2f }) k.B(D.TrimMid, sx, -.07f, .05f, .025f, .12f, .1f);
            k.Bb(D.ClothWhite, 0, .03f, .14f, .3f, .002f, .22f);
            k.Bb(D.Black, 0, .032f, .13f, .2f, .09f, .2f);
            k.Bb(D.Black, 0, .122f, .13f, .22f, .045f, .06f);
            foreach (float sx in new[] { -.1f, .1f }) k.S(D.Black, sx, .15f, .13f, .035f, .03f, .045f, 8);
            k.Cr(D.CeramicWhite, new Vector3(0, .085f, .225f), .055f, .055f, .004f, Quaternion.Euler(70, 0, 0), 16);
            k.Pop();
            k.Push(new Vector3(17.15f, 1.65f, 14.076f), 180f);
            Calendar(k);
            k.Pop();
            // 裏階段：水桶、扫帚
            Oke(k, 5.43f, 0f, 20.1f, .15f, .26f);
            k.Cr(D.TrimLight, new Vector3(7.5f, .25f, 20.3f), .012f, .012f, 1.0f, Quaternion.Euler(12, 0, 0), 6);
            k.Cr(D.Straw, new Vector3(7.5f, 0f, 20.25f), .09f, .03f, .3f, Quaternion.Euler(12, 0, 0), 8);
        }

        // ---------------------------------------------------------------- 便所、風呂
        static void FurnWetRooms(Kit k)
        {
            PendantLamp(k, 9.009f, 17.4f, 2.85f, .45f, 2);
            // 第三轮：拖鞋、洗手钵、厕纸架、小便器、洗面台的镜子和台面小物都在 WetRooms 里按新隔间重做。
            // 这里只量尺寸（搬家求解的输入不变，容器不会动），不画、不放挡块。
            Kit.Ghost = true;
            Slippers(k, 9.0f, .55f, 14.62f, 0, D.PlasticRed);
            // 洗手钵
            k.Push(new Vector3(7.802f, 1.35f, 16.4f), 90f);
            k.Bb(D.TrimMid, 0, -.025f, .14f, .4f, .025f, .28f);
            foreach (float sx in new[] { -.16f, .16f }) k.B(D.TrimMid, sx, -.09f, .05f, .02f, .12f, .1f);
            k.C(D.CeramicWhite, 0, 0, .14f, .1f, .13f, .1f, 16);
            k.C(D.Water, 0, .1f, .14f, .115f, .115f, .002f, 16);
            k.Cr(D.TrimLight, new Vector3(-.2f, .38f, .06f), .01f, .01f, .4f, AlongX, 6);
            k.B(D.ClothWhite, 0, .24f, .065f, .3f, .28f, .006f);
            k.Pop();
            // 厕纸架
            k.Push(new Vector3(10.216f, .9f, 19.6f), -90f);
            k.B(D.TrimMid, 0, 0, .05f, .18f, .12f, .1f);
            k.Cr(D.Paper, new Vector3(-.065f, -.07f, .07f), .05f, .05f, .13f, AlongX, 12);
            k.Pop();
            Kit.Ghost = false;

            PendantLamp(k, 14.15f, 17.4f, 2.85f, .45f, 2);
            // すのこ
            foreach (float z in new[] { 19.1f, 20.3f }) k.Bb(D.TrimMid, 15.05f, .552f, z, 1.2f, .02f, .06f);
            for (float x = 14.5f; x < 15.62f; x += .11f) k.Bb(D.TrimLight, x, .572f, 19.7f, .08f, .022f, 1.4f);
            Oke(k, 14.7f, .594f, 19.95f);
            Oke(k, 14.72f, .594f + .16f, 19.94f, .13f, .14f);
            // 北墙：镜子 + 两个铜水龙头
            k.Push(new Vector3(15.0f, .55f, 20.511f), 180f);
            k.B(D.TrimLight, 0, .9f, .004f, .46f, .56f, .008f);
            k.B(D.Mirror, 0, .9f, .009f, .4f, .5f, .002f);
            foreach (float sx in new[] { -.12f, .12f })
            {
                k.Cr(D.Brass, new Vector3(sx, .4f, 0), .012f, .012f, .1f, AlongZ, 8);
                k.Cr(D.Brass, new Vector3(sx, .4f, .09f), .01f, .008f, .06f, Quaternion.Euler(180, 0, 0), 8);
                k.C(D.Brass, sx, .41f, .05f, .025f, .025f, .02f, 8);
            }
            k.Pop();
            // 洗面台上方的镜子 + 台面小物（第三轮换成 WetRooms 的昭和洗脸台，这里只量尺寸）
            Kit.Ghost = true;
            k.Push(new Vector3(11.2f, .55f, 14.236f));
            k.B(D.TrimLight, 0, 1.2f, .006f, .5f, .6f, .012f);
            k.B(D.Mirror, 0, 1.2f, .013f, .44f, .54f, .002f);
            k.Pop();
            k.C(D.CeramicWhite, 10.85f, 1.31f, 15.05f, .05f, .05f, .02f, 12);
            k.C(D.Glass, 11.6f, 1.31f, 15.2f, .035f, .04f, .1f, 10);
            Kit.Ghost = false;
            // 浴桶：半边盖着木板，另一半是黑水
            for (int i = 0; i < 8; i++) k.Bb(D.TrimLight, 15.763f + i * .105f, 1.22f, 19.99f, .1f, .02f, .98f);
            k.B(D.Water, 16.95f, 1.19f, 19.99f, .7f, .004f, .75f);

            // 浴桶东侧的燃气烧水釜 + 烟道
            k.Push(new Vector3(17.70f, .55f, 20.02f), 270f);
            k.Bb(D.CeramicWhite, 0f, 0f, 0f, .58f, .88f, .44f);
            k.Bb(D.Iron, 0f, 0f, .222f, .5f, .08f, .004f);
            k.B(D.Iron, 0f, .62f, .223f, .3f, .2f, .004f);
            k.Cr(D.Black, new Vector3(-.07f, .62f, .225f), .045f, .045f, .03f, AlongZ, 14);
            k.Cr(D.PlasticRed, new Vector3(.08f, .62f, .225f), .025f, .025f, .03f, AlongZ, 10);
            k.Pop();
            k.C(D.Iron, 17.70f, 1.43f, 20.02f, .06f, .06f, 1.0f, 12);
            k.Cr(D.Iron, new Vector3(17.70f, 2.43f, 20.02f), .06f, .06f, .51f, AlongZ, 12);
            foreach (float y in new[] { .78f, 1.02f }) k.Cr(D.Brass, new Vector3(17.36f, y, 19.87f), .03f, .03f, .12f, AlongX, 10);
            Blocker("BathHeater", new Vector3(17.70f, .55f, 20.02f), 0f, new Vector3(.46f, .9f, .6f));

            // 排水口（在浴室隔墙北边）
            k.Bb(D.Black, 14.1f, .55f, 19.0f, .22f, .004f, .22f);
            for (int i = 0; i < 5; i++) k.Bb(D.Iron, 14.02f + i * .04f, .552f, 19.0f, .012f, .004f, .2f);

            // 第二个洗身位：镜子 + 水龙头 + 小搁板 + 矮凳 + 桶
            k.Push(new Vector3(11.9f, .55f, 20.511f), 180f);
            k.B(D.TrimLight, 0, .9f, .004f, .46f, .56f, .008f);
            k.B(D.Mirror, 0, .9f, .009f, .4f, .5f, .002f);
            foreach (float sx in new[] { -.12f, .12f })
            {
                k.Cr(D.Brass, new Vector3(sx, .4f, 0), .012f, .012f, .1f, AlongZ, 8);
                k.Cr(D.Brass, new Vector3(sx, .4f, .09f), .01f, .008f, .06f, Quaternion.Euler(180, 0, 0), 8);
            }
            k.B(D.TrimLight, 0, 1.24f, .07f, .5f, .02f, .14f);
            k.C(D.CeramicWhite, -.15f, 1.25f, .07f, .03f, .03f, .14f, 8);
            k.C(D.ClothRed, -.06f, 1.25f, .07f, .028f, .024f, .12f, 8);
            k.Bb(D.CeramicWhite, .1f, 1.25f, .07f, .09f, .03f, .06f);
            k.Pop();
            k.Push(new Vector3(11.9f, .55f, 20.05f));
            k.Bb(D.TrimLight, 0, .2f, 0, .3f, .03f, .26f);
            foreach (float sx in new[] { -.12f, .12f }) k.Bb(D.TrimMid, sx, 0, 0, .04f, .2f, .24f);
            k.Pop();
            Blocker("WashStool2", new Vector3(11.9f, .55f, 20.05f), 0f, new Vector3(.32f, .24f, .3f));
            Oke(k, 12.35f, .55f, 19.85f, .13f, .14f);

            // 西墙毛巾杆
            k.Cr(D.Brass, new Vector3(10.42f, 1.95f, 18.05f), .01f, .01f, .7f, AlongZ, 8);
            foreach (float z in new[] { 18.07f, 18.73f }) k.B(D.Brass, 10.4f, 1.95f, z, .05f, .015f, .015f);
            k.B(D.ClothWhite, 10.43f, 1.72f, 18.25f, .012f, .46f, .26f);
            k.B(D.ClothIndigo, 10.432f, 1.76f, 18.55f, .012f, .38f, .24f);

            // 更衣处（东墙）：长凳、衣钩、浴衣、毛巾、体重秤、草席
            k.Push(new Vector3(17.76f, .55f, 15.6f), 270f);
            k.Bb(D.TrimLight, 0, .38f, 0, 1.3f, .04f, .34f);
            foreach (float sx in new[] { -.58f, .58f })
            {
                foreach (float sz in new[] { -.12f, .12f }) k.Bb(D.TrimMid, sx, 0, sz, .05f, .38f, .05f);
                k.Bb(D.TrimMid, sx, .1f, 0, .05f, .04f, .24f);
            }
            k.Bb(D.TrimMid, 0, .1f, 0, 1.16f, .04f, .04f);
            k.Bb(D.ClothIndigo, -.3f, .42f, .02f, .34f, .06f, .26f);
            k.Bb(D.ClothWhite, -.3f, .48f, .02f, .32f, .014f, .06f);
            k.Bb(D.ClothWhite, .28f, .42f, 0f, .3f, .05f, .22f);
            k.Bb(D.ClothWhite, .29f, .47f, 0f, .28f, .045f, .2f);
            k.Pop();
            Blocker("DatsuijoBench", new Vector3(17.76f, .55f, 15.6f), 0f, new Vector3(.34f, .42f, 1.3f));
            k.B(D.TrimMid, 17.915f, 2.05f, 15.6f, .05f, .08f, 1.3f);
            foreach (float z in new[] { 15.1f, 15.6f, 16.1f }) k.Cr(D.Brass, new Vector3(17.89f, 2.0f, z), .01f, .008f, .07f, Quaternion.Euler(0, 0, 70), 6);
            k.B(D.ClothIndigo, 17.905f, 1.62f, 15.1f, .02f, .76f, .42f);
            k.B(D.ClothWhite, 17.893f, 1.93f, 15.1f, .006f, .1f, .44f);
            k.B(D.ClothWhite, 17.905f, 1.78f, 16.1f, .012f, .42f, .3f);
            k.Bb(D.Straw, 17.15f, .552f, 15.6f, .6f, .006f, .9f);
            k.Bb(D.CeramicWhite, 17.15f, .558f, 16.75f, .28f, .055f, .34f);
            k.B(D.Glass, 17.15f, .6135f, 16.84f, .1f, .001f, .06f);
            k.B(D.Black, 17.15f, .614f, 16.84f, .005f, .0015f, .05f);

            // 便所：西墙小便器（朝颜）——第三轮在 WetRooms 里重做，这里只量尺寸
            Kit.Ghost = true;
            k.C(D.CeramicWhite, 7.97f, 1.0f, 18.5f, .07f, .17f, .32f, 16);
            k.C(D.Black, 7.97f, 1.3f, 18.5f, .15f, .15f, .002f, 16);
            k.C(D.CeramicWhite, 7.95f, .55f, 18.5f, .028f, .028f, .45f, 8);
            k.C(D.Brass, 7.86f, 1.32f, 18.5f, .012f, .012f, .7f, 6);
            Blocker("Urinal", new Vector3(7.98f, .55f, 18.5f), 0f, new Vector3(.34f, .85f, .36f));
            Kit.Ghost = false;
        }

        // ---------------------------------------------------------------- 土蔵
        static void FurnKura(Kit k)
        {
            // 梁下吊着的提灯
            k.B(D.Black, 10.323f, 2.65f, 27.2f, .005f, .1f, .005f);
            k.C(D.Lacquer, 10.323f, 2.57f, 27.2f, .085f, .085f, .03f, 14);
            k.S(D.PaperLamp, 10.323f, 2.42f, 27.2f, .15f, .14f, .15f, 14);
            k.C(D.Lacquer, 10.323f, 2.26f, 27.2f, .085f, .085f, .03f, 14);
            k.S(D.ClothRed, 10.323f, 2.42f, 27.2f - .147f, .055f, .055f, .003f, 12);
            // 东墙木架
            k.Push(new Vector3(15.18f, .3f, 29.45f), 270f);
            var lv = ShelfUnit(k, 1.8f, 1.9f, .36f, 4, D.BeamDark);
            for (int i = 0; i < 3; i++)
            {
                k.C(D.CeramicBrown, -.72f + i * .11f, lv[0], 0, .05f, .045f, .14f, 10);
                k.C(D.CeramicBrown, -.72f + i * .11f, lv[0] + .14f, 0, .045f, .012f, .07f, 10);
            }
            k.C(D.CeramicBrown, .55f, lv[0], 0, .1f, .14f, .32f, 14);
            for (int i = 0; i < 4; i++)
                k.Cr(D.Paper, new Vector3(-.75f, lv[1] + .03f + (i / 2) * .055f, -.06f + (i % 2) * .065f), .03f, .03f, .42f, AlongX, 8);
            k.Bb(D.Paper, .4f, lv[1], 0, .22f, .12f, .28f);
            k.Bb(D.CeramicBrown, .4f, lv[1] + .12f, 0, .2f, .01f, .26f);
            // 兜
            k.C(D.Iron, 0, lv[2], 0, .2f, .14f, .1f, 14);
            k.S(D.Iron, 0, lv[2] + .14f, 0, .13f, .12f, .14f, 12);
            foreach (float s in new[] { -1f, 1f }) k.Br(D.Gold, s * .06f, lv[2] + .3f, .1f, .015f, .22f, .01f, 0, 0, s * -20f);
            k.Bb(D.Lacquer, -.5f, lv[2], 0, .3f, .04f, .25f);
            k.Cr(D.Straw, new Vector3(-.75f, lv[3] + .06f, 0), .06f, .02f, .8f, AlongX, 8);
            k.S(D.ClothRed, .5f, lv[3] + .12f, 0, .11f, .12f, .1f, 10);
            k.S(D.ClothWhite, .5f, lv[3] + .14f, .07f, .06f, .05f, .03f, 8);
            k.Pop();
            Blocker("KuraShelf", new Vector3(15.18f, .3f, 29.45f), 270f, new Vector3(1.8f, 1.9f, .36f));
            // 靠在西墙上的梯子
            k.Push(new Vector3(8.5f, .3f, 27.2f), 0, 0, 16f);
            foreach (float sz in new[] { -.2f, .2f }) k.Bb(D.TrimLight, 0, 0, sz, .05f, 2.4f, .04f);
            for (float y = .3f; y < 2.3f; y += .28f) k.B(D.TrimLight, 0, y, 0, .035f, .035f, .4f);
            k.Pop();
        }

        // ---------------------------------------------------------------- 二楼
        static void Fan(Kit k, float x, float y, float z, float yaw)
        {
            k.Push(new Vector3(x, y, z), yaw);
            k.C(D.Black, 0, 0, 0, .15f, .13f, .04f, 16);
            k.C(D.Iron, 0, .04f, 0, .02f, .02f, .55f, 8);
            k.S(D.CeramicWhite, 0, .65f, -.05f, .08f, .08f, .11f, 10);
            for (int i = 0; i < 3; i++)
            {
                var rot = Quaternion.Euler(0, 0, i * 120f);
                k.mb.Sphere(D.CeramicBlue, new Vector3(0, .65f, .07f) + rot * new Vector3(0, .085f, 0), new Vector3(.06f, .09f, .008f), 8, rot);
            }
            for (int i = 0; i < 20; i++)
            {
                var rot = Quaternion.Euler(0, 0, i * 18f);
                k.mb.Box(D.Iron, new Vector3(0, .65f, .08f) + rot * new Vector3(0, .18f, 0), new Vector3(.058f, .006f, .006f), rot);
            }
            for (int i = 0; i < 8; i++)
            {
                var rot = Quaternion.Euler(0, 0, i * 45f);
                k.mb.Box(D.Iron, new Vector3(0, .65f, .095f) + rot * new Vector3(0, .09f, 0), new Vector3(.004f, .18f, .004f), rot);
            }
            k.Pop();
        }

        static void Books(Kit k, float x0, float x1, float y, System.Random rng)
        {
            Material[] mats = { D.ClothRed, D.ClothIndigo, D.ClothPurple, D.Paper, D.TrimMid, D.Black, D.ClothWhite };
            float x = x0;
            while (x < x1 - .03f)
            {
                float w = .025f + (float)rng.NextDouble() * .03f, h = .18f + (float)rng.NextDouble() * .12f;
                if (rng.NextDouble() < .08) { x += .05f; continue; }
                k.Bb(mats[rng.Next(mats.Length)], x + w / 2, y, .01f, w - .003f, h, .2f);
                x += w;
            }
        }

        static void FurnUpstairs(Kit k)
        {
            const float F = 3.053f, C2 = 5.35f;
            var rng = new System.Random(77);

            // 寝室
            PendantLamp(k, 9.0085f, 9.652f, C2, .5f, 0);
            Fan(k, 10.95f, F, 7.62f, 160f);
            k.C(D.Brass, 7.55f, 3.5f, 11.15f, .06f, .05f, .03f, 12);
            k.C(D.Brass, 7.55f, 3.53f, 11.15f, .008f, .008f, .2f, 6);
            k.C(D.PaperLamp, 7.55f, 3.66f, 11.15f, .11f, .07f, .14f, 14);
            k.Push(new Vector3(7.72f, 3.58f, 10.95f), 250f, -8f);
            PortraitFrame(k, .12f, .16f);
            k.Pop();
            Slippers(k, 7.4f, F, 9.2f, 90f, D.ClothIndigo);

            // 二楼和室：床の間 + 书架
            PendantLamp(k, 16.73f, 9.652f, C2, .35f, 1);
            k.Bb(D.TrimMid, 20.30f, F, 9.9f, .41f, .17f, 1.9f);
            k.Bb(D.Lacquer, 20.115f, F, 9.9f, .04f, .175f, 1.92f);
            // 床柱、落とし掛け、下がり壁：整组落地（搬家时 +1.1 m），高度按新层高写
            float clear = Levels[G1].wallTop - Levels[G1].floorTop, otoshi = F + DoorTop - .15f;
            Kit.Pinned = true;
            k.Bb(D.TrimLight, 20.4555f, F, 8.895f, .11f, clear, .11f);
            k.Bb(D.Post, 20.12f, otoshi, 9.9f, .06f, .07f, 1.9f);
            k.Bb(P.Plaster, 20.12f, otoshi + .07f, 9.9f, .03f, F + clear - otoshi - .07f, 1.9f);
            k.Push(new Vector3(20.511f, otoshi + .03f, 9.9f), -90f);
            k.Bb(D.ClothIndigo, 0, -1.1f, .004f, .5f, 1.1f, .006f);
            k.Bb(D.Paper, 0, -.92f, .008f, .38f, .72f, .002f);
            for (int i = 0; i < 4; i++) k.B(D.Ink, .05f - i * .03f, -.45f - i * .09f, .0095f, .018f, .14f + i * .03f, .001f);
            k.Cr(D.Lacquer, new Vector3(-.28f, -1.11f, .015f), .013f, .013f, .56f, AlongX, 8);
            k.Pop();
            Kit.Pinned = false;
            // 第三轮：床の間的花瓶换成古董花瓶（CC0 模型）+ 枯枝；Swap 保持搬家求解的输入不变
            k.Swap(() => k.C(D.CeramicBlue, 20.3f, F + .17f, 10.4f, .06f, .04f, .25f, 12),
                () => k.Model("antique_ceramic_vase_01", new Vector3(20.3f, F + .17f, 10.4f), 120f, .7f));
            k.Swap(() => k.S(D.Leaf, 20.3f, F + .5f, 10.4f, .1f, .12f, .08f, 7),
                () => k.Model("dead_quiver_branch_01", new Vector3(20.3f, F + .17f + .26f, 10.4f), 80f, 1.05f));
            Blocker("Toko2F", new Vector3(20.30f, F, 9.9f), 0, new Vector3(.41f, .17f, 1.9f));
            k.Push(new Vector3(13.8f, F, 11.99f), 180f);
            var lv = ShelfUnit(k, 1.2f, 1.8f, .3f, 4, D.TrimMid);
            foreach (float y in lv) Books(k, -.56f, .56f, y, rng);
            k.Pop();
            Blocker("Bookshelf", new Vector3(13.8f, F, 11.99f), 180f, new Vector3(1.2f, 1.8f, .3f));

            // 物置
            PendantLamp(k, 9.652f, 16.4f, C2, .5f, 2);
            k.Push(new Vector3(11.315f, F, 15.05f), 270f);
            lv = ShelfUnit(k, 1.3f, 1.7f, .37f, 3, D.TrimMid);
            for (int i = 0; i < 4; i++) k.Bb(i % 2 == 0 ? D.ClothPurple : D.ClothRed, -.32f, lv[0] + i * .06f, 0, .5f, .06f, .3f);
            k.C(D.CeramicBrown, .35f, lv[0], 0, .1f, .08f, .22f, 12);
            for (int i = 0; i < 3; i++) k.Cr(D.Paper, new Vector3(-.6f, lv[1] + .03f, -.08f + i * .07f), .03f, .03f, .55f, AlongX, 8);
            k.Bb(D.TrimMid, .3f, lv[1], 0, .32f, .2f, .18f);
            k.Bb(D.Black, .22f, lv[1] + .04f, .091f, .12f, .12f, .002f);
            k.Cr(D.Brass, new Vector3(.4f, lv[1] + .1f, .09f), .025f, .025f, .01f, AlongZ, 10);
            k.S(D.ClothRed, -.3f, lv[2] + .11f, 0, .11f, .12f, .1f, 10);
            k.S(D.ClothWhite, -.3f, lv[2] + .13f, .07f, .06f, .05f, .03f, 8);
            Books(k, .0f, .55f, lv[2], rng);
            k.Pop();
            Blocker("MonookiShelf", new Vector3(11.315f, F, 15.05f), 270f, new Vector3(1.3f, 1.7f, .37f));
            k.Push(new Vector3(7.99f, F, 18.2f), 0, 0, 6f);
            k.Bb(D.Lacquer, 0, 0, 0, .07f, 1.4f, .5f);
            k.Bb(D.GoldLeaf, .036f, .05f, 0, .002f, 1.3f, .44f);
            k.Pop();

            // 子供部屋
            PendantLamp(k, 14.8f, 16.4f, C2, .5f, 0);
            // 第三轮：红书包改成倒在地上、盖子翻开、里面空了、东西撒了一地（一看就是已经被翻过的，不像能搜的盒子）
            k.Swap(() =>
            {
                k.Push(new Vector3(15.72f, F, 14.5f), 10f);
                k.Bb(D.PlasticRed, 0, 0, 0, .25f, .3f, .17f);
                k.Br(D.PlasticRed, 0, .24f, .095f, .25f, .14f, .02f, -15f, 0, 0);
                k.B(D.Brass, 0, .16f, .11f, .04f, .03f, .01f);
                k.Pop();
            }, () => EmptiedRandoseru(k, new Vector3(15.72f, F, 14.5f), 10f, new System.Random(1972)));
            k.C(D.Iron, 16.2f, 3.77f, 14.55f, .06f, .06f, .02f, 12);
            k.Cr(D.Iron, new Vector3(16.2f, 3.79f, 14.55f), .008f, .008f, .32f, Quaternion.Euler(20, 0, 0), 6);
            k.Cr(D.Iron, new Vector3(16.2f, 4.04f, 14.66f), .03f, .09f, .1f, Quaternion.Euler(150, 0, 0), 12);
            k.C(D.TrimLight, 16.05f, 3.77f, 14.9f, .028f, .022f, .13f, 10);
            k.S(D.TrimLight, 16.05f, 3.94f, 14.9f, .038f, .042f, .038f, 10);
            k.S(D.Black, 16.05f, 3.955f, 14.892f, .04f, .036f, .037f, 10);
            k.S(D.PlasticRed, 13.2f, F + .1f, 17.0f, .1f, .1f, .1f, 12);
            k.Bb(D.CeramicBlue, 13.55f, F, 16.7f, .06f, .06f, .06f);
            k.Bb(D.ClothRed, 13.64f, F, 16.74f, .06f, .06f, .06f);
            k.Bb(D.TrimLight, 13.6f, F + .06f, 16.71f, .06f, .06f, .06f);
            foreach (float z in new[] { 14.95f, 17.35f })
            {
                k.Push(new Vector3(11.662f, 4.0f, z), 90f);
                k.B(D.Paper, 0, 0, .002f, .36f, .26f, .004f);
                k.S(D.ClothRed, -.11f, .07f, .0045f, .035f, .035f, .001f, 8);
                k.B(D.Leaf, 0, -.1f, .0045f, .32f, .015f, .001f);
                for (int i = 0; i < 3; i++)
                {
                    float sx = -.06f + i * .07f;
                    k.S(D.Ink, sx, .02f, .0045f, .014f, .014f, .001f, 6);
                    k.B(D.Ink, sx, -.03f, .0045f, .004f, .07f, .001f);
                    k.Br(D.Ink, sx, -.01f, .0045f, .05f, .004f, .001f, 0, 0, 0);
                }
                k.Pop();
            }

            // 二楼走廊
            foreach (float x in new[] { 8.0f, 12.9f, 17.8f }) PendantLamp(k, x, 13.191f, C2, .45f, 2);
            foreach (float x in new[] { 9.6f, 14.8f }) PendantLamp(k, x, 19.626f, C2, .45f, 2);
        }
    }
}
