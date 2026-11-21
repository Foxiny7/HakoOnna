using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 家具与陈设（一楼主要房间）。全部是视觉网格；会挡路的大件另放 Blocker_*。
    /// 刻意不加箱子、抽屉柜、带盖的盒子、篮子这类看起来能搜的东西，避免和可搜索容器混淆。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>
        /// 带局部坐标系栈的网格构建器。坐标按 V3 旧尺寸书写；顶层的每一次图元调用、每一个 Push…Pop 块
        /// 算一“件”。第一遍（Probing）只量每件的包围盒，第二遍按 Rescale 算出的搬家 + 放大矩阵真正写网格。
        /// 第三遍（按房间补家具，见 Furnish3）不再变换，直接按新尺寸书写。
        /// </summary>
        sealed class Kit
        {
            public static bool Probing;
            public static List<Bounds> ItemBounds = new List<Bounds>();
            public static List<bool> ItemPinned = new List<bool>();
            /// <summary>这件是 Blocker（带碰撞体），步行路线检查只管它们。</summary>
            public static List<bool> ItemSolid = new List<bool>();
            /// <summary>每件的搬家 + 放大矩阵（第二遍用；null = 原样）。</summary>
            public static List<Matrix4x4> ItemXform;
            /// <summary>
            /// 为 true 时登记的件“高度已按新层高书写”：所在组垂直方向只跟地板走，
            /// 不按天花 / 门楣规则抬高（暖帘、床の間这类）。
            /// </summary>
            public static bool Pinned;
            /// <summary>
            /// 为 true 时登记的件只在第一遍量尺寸（搬家求解的输入不变），第二遍不画、不放挡块：
            /// 第三轮换掉的旧陈设（便所的旧洗手钵、小便器，旧洗脸台上的镜子和小物）。
            /// </summary>
            public static bool Ghost;
            static int next;

            public static void BeginPass(bool probing)
            {
                Probing = probing;
                Pinned = false;
                Ghost = false;
                next = 0;
                if (probing) { ItemBounds = new List<Bounds>(); ItemPinned = new List<bool>(); ItemSolid = new List<bool>(); }
            }

            /// <summary>登记一个挡块，返回它的搬家 + 放大矩阵（第一遍为单位阵）。</summary>
            public static Matrix4x4 Claim(Bounds probeBounds)
            {
                int i = next++;
                if (Probing) { ItemBounds.Add(probeBounds); ItemPinned.Add(Pinned); ItemSolid.Add(true); return Matrix4x4.identity; }
                return ItemXform != null && i < ItemXform.Count ? ItemXform[i] : Matrix4x4.identity;
            }

            readonly MeshBatch main = new MeshBatch();
            readonly MeshBatch probe = new MeshBatch();
            readonly Stack<Matrix4x4> st = new Stack<Matrix4x4>();
            int current = -1;

            public MeshBatch mb => Probing ? probe : main;

            void Begin()
            {
                current = next++;
                probe.Track = true;
                probe.HasBounds = false;
                mb.M = Probing || ItemXform == null || current >= ItemXform.Count ? Matrix4x4.identity : ItemXform[current];
                main.Mute = !Probing && (IsDropped(mb.M) || Ghost);
            }

            /// <summary>搬家后哪里都放不下、被撤掉的件：矩阵全零（m33 = 0）。</summary>
            public static bool IsDropped(Matrix4x4 m) => m.m33 == 0f;

            void End()
            {
                if (Probing)
                {
                    ItemBounds.Add(probe.HasBounds ? new Bounds((probe.Min + probe.Max) / 2, probe.Max - probe.Min) : new Bounds());
                    ItemPinned.Add(Pinned);
                    ItemSolid.Add(false);
                }
                mb.M = Matrix4x4.identity;
                main.Mute = false;
            }

            /// <summary>顶层的单个图元自成一件。</summary>
            void Prim(System.Action emit)
            {
                if (st.Count > 0) { emit(); return; }
                Begin();
                emit();
                End();
            }

            public void Push(Vector3 p, float yaw = 0f, float pitch = 0f, float roll = 0f)
            {
                if (st.Count == 0) Begin();
                st.Push(mb.M);
                mb.M = mb.M * Matrix4x4.TRS(p, Quaternion.Euler(pitch, yaw, roll), Vector3.one);
            }

            /// <summary>带缩放的局部坐标系（只用于蓑衣这类压扁的形状，法线会有轻微偏差）。</summary>
            public void PushS(Vector3 p, Vector3 euler, Vector3 scale)
            {
                if (st.Count == 0) Begin();
                st.Push(mb.M);
                mb.M = mb.M * Matrix4x4.TRS(p, Quaternion.Euler(euler), scale);
            }

            public void Pop()
            {
                mb.M = st.Pop();
                if (st.Count == 0) End();
            }

            public void B(Material m, float x, float y, float z, float w, float h, float d) => Prim(() => mb.Box(m, new Vector3(x, y, z), new Vector3(w, h, d)));
            public void Bb(Material m, float x, float y0, float z, float w, float h, float d) => Prim(() => mb.Box(m, new Vector3(x, y0 + h / 2, z), new Vector3(w, h, d)));

            public void Br(Material m, float x, float y, float z, float w, float h, float d, float pitch, float yaw, float roll) =>
                Prim(() => mb.Box(m, new Vector3(x, y, z), new Vector3(w, h, d), Quaternion.Euler(pitch, yaw, roll)));

            public void C(Material m, float x, float y0, float z, float r0, float r1, float h, int seg = 12) => Prim(() => mb.Cyl(m, new Vector3(x, y0, z), r0, r1, h, seg));
            public void Cr(Material m, Vector3 b, float r0, float r1, float h, Quaternion rot, int seg = 10) => Prim(() => mb.Cyl(m, b, r0, r1, h, seg, true, rot));
            public void S(Material m, float x, float y, float z, float rx, float ry, float rz, int seg = 10) => Prim(() => mb.Sphere(m, new Vector3(x, y, z), new Vector3(rx, ry, rz), seg));

            /// <summary>
            /// 外部 CC0 模型（ExternalModels）：转好、缩放好以后最低点落在 basePos，水平居中。
            /// 和图元一样算一件（或属于外层 Push…Pop 那一件），跟着家具一起搬家、放大。
            /// </summary>
            public void Model(string id, Vector3 basePos, float yaw = 0f, float scale = 1f, string only = null, float pitch = 0f, float roll = 0f) =>
                Prim(() => mb.Model(id, ExternalModels.Place(id, only, basePos, Quaternion.Euler(pitch, yaw, roll), scale), only));

            /// <summary>模型在不转动时的尺寸（米，乘过 scale）；缺模型时为 0。</summary>
            public static Vector3 ModelSize(string id, float scale = 1f, string only = null) => ExternalModels.LocalBounds(id, only).size * scale;

            /// <summary>
            /// 第一遍按 probe 量尺寸、第二遍画 real，两遍都算一件：旧陈设换成模型时用，
            /// 搬家求解看到的还是原来的包围盒（容器位置不会因此变动）。probe 必须是原来那一件。
            /// </summary>
            public void Swap(System.Action probe, System.Action real)
            {
                Push(Vector3.zero);
                if (Probing) probe(); else real();
                Pop();
            }
        }

        static readonly Quaternion AlongX = Quaternion.Euler(0, 0, -90); // 圆柱 +Y → +X
        static readonly Quaternion AlongZ = Quaternion.Euler(90, 0, 0);  // 圆柱 +Y → +Z

        static bool FurnishPass;

        static void FurnishRooms(Transform parent, Transform root)
        {
            var fills = new (string name, System.Action<Kit> fill)[]
            {
                ("Genkan", FurnGenkan), ("Daidokoro", FurnDaidokoro), ("Tsugi", FurnTsugi), ("Butsuma", FurnButsuma),
                ("Zashiki", FurnZashiki), ("Ima", FurnIma), ("Nando", FurnNando), ("Oku", FurnOku),
                ("Engawa", FurnEngawa), ("Corridors", FurnCorridors), ("WetRooms", FurnWetRooms),
                ("Kura", FurnKura), ("Upstairs", FurnUpstairs),
            };
            FurnishPass = true;
            try
            {
                // 第一遍：按旧坐标量出每件陈设的包围盒
                Kit.BeginPass(true);
                foreach (var f in fills) f.fill(new Kit());
                // 和代码生成的家具、容器、出生点一起分组，算出搬到新尺寸 + 放大的变换
                Kit.ItemXform = SolveRelocation(Kit.ItemBounds, Kit.ItemPinned, Kit.ItemSolid, root);
                // 第三轮：换掉的场景物体删掉；容器正面 1 m 内的旧陈设撤掉
                RemoveReplacedProps(root);
                ContainerFronts = ComputeContainerFronts(root);
                ClearContainerFronts();
                // 第二遍：带变换真正生成
                Kit.BeginPass(false);
                foreach (var f in fills)
                {
                    var k = new Kit();
                    f.fill(k);
                    k.mb.Build(parent, "Furn_" + f.name);
                }
                var xform2 = Kit.ItemXform;
                // 第三遍：按房间大小补家具（直接按新尺寸摆，避开上面已经占用的地方）
                Kit.ItemXform = null;
                Kit.BeginPass(false);
                FurnishByRoom(parent);
                ReportOverlaps(root, Kit.ItemBounds, xform2, Kit.ItemSolid);
            }
            finally
            {
                FurnishPass = false;
                Kit.Probing = false;
                Kit.ItemXform = null;
                Occupied = null;
                KeepOut = null;
            }
        }

        // ---------------------------------------------------------------- 小件
        static void PendantLamp(Kit k, float x, float z, float ceil, float drop, int style)
        {
            float y = ceil - drop;
            k.B(D.Black, x, (ceil + y) / 2, z, .008f, drop, .008f);
            k.C(D.Lacquer, x, ceil - .02f, z, .045f, .045f, .02f, 10);
            switch (style)
            {
                case 0: // 昭和乳白玻璃灯罩
                    k.C(D.Brass, x, y - .04f, z, .025f, .025f, .04f, 8);
                    k.C(D.LampGlass, x, y - .16f, z, .22f, .05f, .12f, 16);
                    k.S(D.LampGlass, x, y - .15f, z, .045f, .055f, .045f, 8);
                    break;
                case 1: // 和纸灯笼
                    k.C(D.Lacquer, x, y - .03f, z, .09f, .09f, .03f, 14);
                    k.S(D.PaperLamp, x, y - .25f, z, .2f, .22f, .2f, 14);
                    k.C(D.Lacquer, x, y - .48f, z, .1f, .1f, .03f, 14);
                    break;
                default: // 搪瓷灯罩 + 裸灯泡
                    k.C(D.Iron, x, y - .1f, z, .17f, .03f, .1f, 14);
                    k.S(D.LampGlass, x, y - .1f, z, .04f, .05f, .04f, 8);
                    break;
            }
        }

        static void Zabuton(Kit k, float x, float y, float z, float yaw, Material m)
        {
            k.Push(new Vector3(x, y, z), yaw);
            // 第四轮：两块方块叠的坐垫换成四边圆鼓、中间缝住的软座布団（Soft.cs）；Swap：搬家求解看到的还是原来的方块
            k.Swap(() =>
            {
                k.Bb(m, 0, 0, 0, .52f, .05f, .55f);
                k.Bb(m, 0, .05f, 0, .46f, .025f, .49f);
                k.B(D.Black, 0, .076f, 0, .02f, .004f, .02f);
            }, () => SoftZabuton(k.mb, new Vector3(0f, SoftTatamiLift, 0f), 0f, m, Mathf.Repeat(x * 7.3f + z * 3.1f, 50f)));
            k.Pop();
        }

        static void Geta(Kit k, float x, float y, float z, float yaw, Material m)
        {
            k.Push(new Vector3(x, y, z), yaw);
            foreach (float sx in new[] { -.065f, .065f })
            {
                k.Bb(m, sx, .03f, 0, .1f, .025f, .22f);
                k.Bb(D.Post, sx, 0, .06f, .09f, .03f, .02f);
                k.Bb(D.Post, sx, 0, -.06f, .09f, .03f, .02f);
                k.B(D.ClothRed, sx, .058f, .04f, .012f, .006f, .1f);
            }
            k.Pop();
        }

        static void Slippers(Kit k, float x, float y, float z, float yaw, Material m)
        {
            k.Push(new Vector3(x, y, z), yaw);
            foreach (float sx in new[] { -.065f, .065f })
            {
                k.Bb(m, sx, 0, 0, .1f, .02f, .25f);
                k.Br(m, sx, .035f, .06f, .1f, .03f, .1f, -12f, 0, 0);
            }
            k.Pop();
        }

        static void Oke(Kit k, float x, float y, float z, float r = .14f, float h = .16f)
        {
            k.C(D.TrimLight, x, y, z, r, r * 1.12f, h, 14);
            k.C(D.Black, x, y + h, z, r * 1.08f, r * 1.08f, .002f, 14);
            k.C(D.Brass, x, y + h * .2f, z, r * 1.03f + .004f, r * 1.03f + .004f, .014f, 14);
            k.C(D.Brass, x, y + h * .75f, z, r * 1.09f + .004f, r * 1.09f + .004f, .014f, 14);
        }

        static void Calendar(Kit k)
        {
            k.B(D.Paper, 0, 0, .005f, .3f, .44f, .01f);
            k.B(D.PlasticRed, 0, .16f, .011f, .26f, .07f, .002f);
            for (int r = 0; r < 5; r++) k.B(D.Ink, 0, .07f - r * .055f, .0105f, .25f, .004f, .001f);
            for (int c = 0; c < 6; c++) k.B(D.Ink, -.125f + c * .05f, -.04f, .0105f, .003f, .22f, .001f);
        }

        /// <summary>相框，正面朝局部 +z，原点在框中心。</summary>
        static void PortraitFrame(Kit k, float w, float h)
        {
            k.B(D.Black, 0, 0, 0, w, h, .03f);
            float iw = w * .78f / 2, ih = h * .82f / 2;
            k.mb.Poly(D.Portrait,
                new[] { new Vector3(-iw, -ih, .0155f), new Vector3(iw, -ih, .0155f), new Vector3(iw, ih, .0155f), new Vector3(-iw, ih, .0155f) },
                new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) }, Vector3.forward);
        }

        /// <summary>开放式木架，正面朝局部 +z，底面在 y=0。返回各层板面高度。</summary>
        static float[] ShelfUnit(Kit k, float w, float h, float d, int n, Material m)
        {
            foreach (float sx in new[] { -1f, 1f }) k.Bb(m, sx * (w / 2 - .015f), 0, 0, .03f, h, d);
            k.Bb(m, 0, 0, -d / 2 + .01f, w - .06f, h, .02f);
            var levels = new float[n];
            for (int i = 0; i <= n; i++)
            {
                float y = i * (h - .03f) / n;
                k.Bb(m, 0, y, .01f, w - .06f, .03f, d - .02f);
                if (i < n) levels[i] = y + .03f;
            }
            return levels;
        }

        // ---------------------------------------------------------------- 玄関
        static void FurnGenkan(Kit k)
        {
            PendantLamp(k, 3.86f, 3.54f, 2.85f, .5f, 0);
            Geta(k, 6.55f, 0f, 4.15f, 80f, D.TrimLight);
            Geta(k, 6.6f, 0f, 4.8f, 100f, D.TrimMid);
            // 伞立
            k.C(D.CeramicBlue, .33f, 0f, 3.0f, .12f, .13f, .48f, 14);
            k.C(D.Black, .33f, .48f, 3.0f, .118f, .118f, .002f, 14);
            k.Cr(D.Black, new Vector3(.3f, .05f, 2.98f), .012f, .012f, .95f, Quaternion.Euler(6, 0, -8), 6);
            k.Cr(D.Straw, new Vector3(.36f, .42f, 3.02f), .05f, .015f, .62f, Quaternion.Euler(-5, 0, 7), 8);
            // 门上的扁额
            k.Push(new Vector3(7.642f, 2.6f, 4.504f), -90f);
            k.B(D.Post, 0, 0, .02f, .92f, .3f, .04f);
            k.B(D.TrimLight, 0, 0, .042f, .82f, .22f, .006f);
            for (int i = 0; i < 3; i++) k.B(D.Ink, -.22f + i * .22f, 0, .0455f, .1f, .14f, .002f);
            k.Pop();

            // 入口内侧的蔺草脚垫
            k.Bb(D.Straw, 3.86f, 0f, .78f, 1.1f, .012f, .55f);
            k.Bb(D.ClothIndigo, 3.86f, 0f, .78f, 1.14f, .008f, .59f);

            // 石臼 + 靠墙的杵
            k.C(D.Stone, 1.1f, 0f, 2.6f, .29f, .25f, .2f, 16);
            k.C(D.Stone, 1.1f, .2f, 2.6f, .25f, .3f, .22f, 16);
            k.C(D.Black, 1.1f, .421f, 2.6f, .2f, .2f, .002f, 16);
            k.Cr(D.TrimMid, new Vector3(1.1f, .5f, 2.44f), .085f, .085f, .32f, AlongZ, 12);
            var kine = new Vector3(-.93f, .76f, 0f);
            k.Cr(D.TrimLight, new Vector3(1.08f, .5f, 2.6f), .022f, .022f, kine.magnitude, Quaternion.FromToRotation(Vector3.up, kine.normalized), 6);
            Blocker("Usu", new Vector3(1.1f, 0f, 2.6f), 0f, new Vector3(.62f, .5f, .62f));

            // 西墙挂钉：斗笠 + 蓑衣
            k.Cr(D.TrimMid, new Vector3(.08f, 2.0f, 3.72f), .015f, .012f, .13f, AlongX, 6);
            k.Cr(D.Straw, new Vector3(.1f, 2.06f, 3.72f), .23f, .01f, .1f, AlongX, 18);
            k.Cr(D.TrimMid, new Vector3(.2f, 2.06f, 3.72f), .012f, .012f, .002f, AlongX, 6);
            k.PushS(new Vector3(.1f, 1.88f, 3.72f), Vector3.zero, new Vector3(.42f, 1f, 1f));
            k.C(D.Straw, 0f, -.86f, 0f, .34f, .12f, .86f, 14);
            k.C(D.Straw, 0f, -.3f, 0f, .27f, .2f, .14f, 14);
            k.C(D.TrimMid, 0f, -.2f, 0f, .215f, .215f, .02f, 14);
            k.C(D.Bark, 0f, -.9f, 0f, .35f, .34f, .05f, 14);
            k.Pop();

            // 南墙边捆好的旧报纸
            for (int i = 0; i < 3; i++)
            {
                k.Push(new Vector3(2.45f, i * .1f, .36f), i * 7f - 6f);
                k.Bb(D.Paper, 0f, 0f, 0f, .4f, .095f, .29f);
                k.Bb(D.TrimLight, 0f, -.001f, 0f, .008f, .099f, .296f);
                k.Bb(D.TrimLight, 0f, -.001f, 0f, .406f, .099f, .008f);
                k.Pop();
            }
            Blocker("Newspapers", new Vector3(2.45f, 0f, .36f), 0f, new Vector3(.46f, .3f, .34f));

            // 东北角：蜘蛛抱蛋盆栽
            k.C(D.CeramicBlue, 7.2f, 0f, 6.6f, .15f, .19f, .3f, 14);
            k.C(D.CeramicBlue, 7.2f, .3f, 6.6f, .2f, .2f, .03f, 14);
            k.C(D.Ground, 7.2f, .3f, 6.6f, .175f, .175f, .02f, 14);
            for (int i = 0; i < 9; i++)
            {
                float yaw = i * 40f + (i % 3) * 13f, pitch = 14f + (i % 3) * 11f, len = .5f + (i % 4) * .07f;
                var dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.up;
                var c = new Vector3(7.2f, .32f, 6.6f) + dir * (len / 2);
                k.Br(D.Leaf, c.x, c.y, c.z, .085f, len, .006f, pitch, yaw, 0f);
            }
            Blocker("PottedPlant", new Vector3(7.2f, 0f, 6.6f), 0f, new Vector3(.45f, .8f, .45f));

            // 北墙：昭和广告镜
            k.Push(new Vector3(6.55f, 1.5f, 7.0f), 180f);
            k.B(D.TrimMid, 0f, 0f, .012f, .44f, .96f, .024f);
            k.B(D.Mirror, 0f, -.06f, .025f, .36f, .72f, .002f);
            k.B(D.Black, 0f, .37f, .025f, .36f, .12f, .002f);
            for (int i = 0; i < 4; i++) k.B(D.Gold, -.12f + i * .08f, .37f, .0265f, .05f, .07f, .001f);
            k.Pop();
        }

        // ---------------------------------------------------------------- 台所
        static void FurnDaidokoro(Kit k)
        {
            PendantLamp(k, 3.86f, 10.6f, 2.85f, .5f, 2);
            // 柴火堆
            for (int row = 0; row < 5; row++)
                for (int i = 0; i < 4 - row % 2; i++)
                {
                    float z = 13.62f + i * .12f + row % 2 * .06f, y = .055f + row * .1f;
                    k.Cr(D.Bark, new Vector3(1.73f, y, z), .052f, .05f, .72f + i % 2 * .03f, Quaternion.Euler(0, 0, 90), 7);
                }
            Blocker("Firewood", new Vector3(1.35f, 0, 13.78f), 0, new Vector3(.8f, .62f, .45f));
            // 水缸
            k.C(D.CeramicBrown, .42f, 0, 13.6f, .17f, .24f, .32f, 16);
            k.C(D.CeramicBrown, .42f, .32f, 13.6f, .24f, .19f, .3f, 16);
            k.C(D.TrimMid, .42f, .62f, 13.6f, .2f, .2f, .03f, 16);
            k.Cr(D.TrimLight, new Vector3(.42f, .665f, 13.6f), .008f, .008f, .32f, Quaternion.Euler(0, 0, -80), 6);
            k.C(D.TrimLight, .74f, .65f, 13.6f, .04f, .045f, .05f, 10);
            Blocker("WaterJar", new Vector3(.42f, 0, 13.6f), 0, new Vector3(.5f, .7f, .5f));
            // 水缸上方的搁板
            k.Push(new Vector3(.08f, 1.3f, 13.5f), 90f);
            foreach (float y in new[] { 0f, .42f })
            {
                k.B(D.TrimMid, 0, y, .14f, .9f, .025f, .28f);
                foreach (float sx in new[] { -.38f, .38f }) k.B(D.TrimMid, sx, y - .07f, .05f, .025f, .12f, .1f);
            }
            k.C(D.CeramicWhite, -.3f, .0125f, .14f, .06f, .08f, .04f, 12);
            k.C(D.CeramicWhite, -.3f, .0525f, .14f, .06f, .08f, .04f, 12);
            k.C(D.CeramicBrown, -.08f, .0125f, .12f, .07f, .06f, .16f, 12);
            k.C(D.CeramicBlue, .12f, .0125f, .14f, .045f, .045f, .07f, 12);
            k.C(D.Glass, .3f, .0125f, .12f, .03f, .03f, .18f, 8);
            k.C(D.Glass, .3f, .19f, .12f, .03f, .012f, .06f, 8);
            k.C(D.Iron, -.25f, .4325f, .14f, .07f, .07f, .14f, 12);
            k.C(D.CeramicWhite, .05f, .4325f, .14f, .1f, .1f, .03f, 14);
            k.Bb(D.Paper, .28f, .4325f, .14f, .14f, .12f, .1f);
            k.Pop();
            // 灶上方的荒神棚
            k.Push(new Vector3(.08f, 2.2f, 12.6f), 90f);
            k.B(D.TrimMid, 0, 0, .09f, .5f, .02f, .18f);
            k.Bb(D.Paper, -.1f, .01f, .06f, .06f, .16f, .01f);
            k.Bb(D.Ink, -.1f, .05f, .066f, .012f, .08f, .002f);
            k.C(D.CeramicWhite, .12f, .01f, .1f, .025f, .03f, .07f, 10);
            k.S(D.Leaf, .12f, .12f, .1f, .05f, .07f, .04f, 7);
            k.Pop();
            // 东墙日历
            k.Push(new Vector3(7.642f, 1.5f, 11.6f), -90f);
            Calendar(k);
            k.Pop();
            // 玄関与台所之间的暖簾（门洞居中，沿墙方向跟门走；高度按新门顶直接写）
            const float nz = 7.078f, nx = 3.86f;
            float top = 2.31f * OpeningTopScale - .04f, nw = 3.6f * OpeningWidthScale, pw = nw / 3;
            Kit.Pinned = true;
            k.Cr(D.TrimMid, new Vector3(nx - nw / 2 - .03f, top + .02f, nz), .012f, .012f, nw + .06f, AlongX, 8);
            // 第四轮：三片平板换成有竖褶、下摆不齐的布（Soft.cs HangCloth）；白色家纹圆片贴不住起褶的布，不画了（Swap 里第一遍照旧量尺寸）
            for (int i = 0; i < 3; i++)
            {
                float cx = nx - pw + i * pw, sd = i * 1.7f + .3f;
                bool shot = i == 1;
                k.Swap(() => k.B(D.ClothIndigo, cx, top - .32f, nz, pw - .03f, .64f, .006f), () =>
                {
                    HangCloth(k.mb, D.ClothIndigo, D.ClothIndigo, new Vector3(cx - (pw - .03f) / 2f, top, nz), Quaternion.identity,
                        new HangSpec { width = pw - .03f, length = .64f, thick = .004f, foldAmp = .008f, foldLen = .14f, hem = .006f, seed = sd });
                    if (shot) KitShot(k, "noren", new Vector3(cx, top - .35f, nz), 2.0f, -.25f);
                });
            }
            foreach (float sz in new[] { -1f, 1f })
                k.Swap(() => k.S(D.ClothWhite, nx, top - .3f, nz + sz * .0035f, .12f, .12f, .001f, 14), () => { });
            Kit.Pinned = false;
            // 灶台上的瓶子、铁壶
            k.C(D.CeramicBrown, .3f, .86f, 11.1f, .045f, .045f, .2f, 10);
            k.C(D.CeramicBrown, .3f, 1.06f, 11.1f, .045f, .015f, .06f, 10);
            k.C(D.Glass, .32f, .86f, 11.24f, .035f, .035f, .17f, 8);
            k.C(D.Iron, .55f, .86f, 11.95f, .1f, .09f, .12f, 14);
            k.C(D.Iron, .55f, .98f, 11.95f, .06f, .03f, .03f, 12);
            k.Cr(D.Iron, new Vector3(.55f, .92f, 12.04f), .015f, .01f, .12f, Quaternion.Euler(50, 0, 0), 6);
            k.Br(D.Iron, .55f, 1.06f, 11.95f, .012f, .01f, .2f, 0, 0, 0);
            // 饭桌上：立着筷子的一碗饭
            k.C(D.CeramicWhite, 4.55f, .72f, 11.2f, .045f, .06f, .06f, 12);
            k.S(D.ClothWhite, 4.55f, .785f, 11.2f, .05f, .02f, .05f, 8);
            k.B(D.TrimLight, 4.558f, .88f, 11.2f, .006f, .2f, .006f);
            k.B(D.TrimLight, 4.542f, .88f, 11.205f, .006f, .2f, .006f);
            k.C(D.CeramicBlue, 4.15f, .72f, 11.15f, .1f, .1f, .015f, 14);
            k.Cr(D.CeramicWhite, new Vector3(4.66f, .755f, 11.58f), .035f, .03f, .07f, AlongZ, 10);

            // 灶台上方西墙：挂杆 + 铁锅、汤勺、擦菜板、抹布
            k.Cr(D.TrimMid, new Vector3(.13f, 1.6f, 12.05f), .012f, .012f, .9f, AlongZ, 6);
            foreach (float z in new[] { 12.08f, 12.9f }) k.B(D.TrimMid, .1f, 1.6f, z, .05f, .03f, .025f);
            k.Cr(D.Iron, new Vector3(.09f, 1.3f, 12.25f), .13f, .13f, .025f, AlongX, 16);
            k.B(D.Iron, .1f, 1.5f, 12.25f, .02f, .18f, .02f);
            k.S(D.Iron, .13f, 1.2f, 12.52f, .045f, .03f, .045f, 8);
            k.B(D.TrimLight, .12f, 1.39f, 12.52f, .012f, .36f, .012f);
            k.B(D.Brass, .095f, 1.42f, 12.7f, .01f, .24f, .11f);
            // 第四轮：抹布搭在挂杆上（Soft.cs TowelOnBar），原来是一块竖着的薄板
            k.Swap(() => k.B(D.ClothWhite, .1f, 1.36f, 12.86f, .008f, .44f, .14f),
                () =>
                {
                    TowelOnBar(k.mb, D.ClothWhite, D.ClothWhite, new Vector3(.13f, 1.6f, 12.93f), Quaternion.Euler(0f, 90f, 0f), .14f, .4f, .12f, .012f, 2.3f);
                    KitShot(k, "dishcloth", new Vector3(.13f, 1.4f, 12.6f), 1.5f, .1f);
                });

            // 北墙高处晾着的萝卜干
            k.Cr(D.Bamboo, new Vector3(3.3f, 2.4f, 13.97f), .018f, .018f, 2.1f, AlongX, 8);
            foreach (float x in new[] { 3.32f, 5.38f }) k.B(D.TrimMid, x, 2.43f, 14.02f, .03f, .08f, .12f);
            for (int i = 0; i < 10; i++)
            {
                float x = 3.42f + i * .2f, len = .38f + (i * 7 % 5) * .03f;
                k.B(D.Black, x, 2.36f, 13.97f, .004f, .08f, .004f);
                k.C(D.Paper, x, 2.32f - len, 13.97f + (i % 2) * .015f, .018f, .036f, len, 8);
                k.S(D.Bark, x, 2.33f, 13.97f, .03f, .05f, .03f, 7);
            }
        }

        // ---------------------------------------------------------------- 次の間
        /// <summary>
        /// 次の間金屏风的一扇（局部 +x 是正面）。art = null 时画第二轮的样子（只在第一遍量尺寸时用）；
        /// 否则正面贴古画的一条：四扇从左到右连续取画，每条按扇面比例取宽度、画面高度取满（不拉伸）。
        /// </summary>
        static void TsugiByobuPanel(Kit k, float x, float z, float yaw, int i, Material art)
        {
            k.Push(new Vector3(x, .55f, z), yaw);
            k.Bb(D.GoldLeaf, 0, .02f, 0, .018f, 1.41f, .35f);
            k.Bb(D.Lacquer, 0, 0, 0, .026f, .03f, .37f);
            k.Bb(D.Lacquer, 0, 1.42f, 0, .026f, .03f, .37f);
            k.Bb(D.Lacquer, 0, 0, .175f, .026f, 1.45f, .02f);
            k.Bb(D.Lacquer, 0, 0, -.175f, .026f, 1.45f, .02f);
            if (art == null)
            {
                k.Br(D.Ink, .011f, .55f + i * .12f, 0, .002f, .45f, .025f, 35f - i * 20f, 0, 0);
                k.S(D.Pine, .011f, .95f + (i % 2) * .15f, (i - 1.5f) * .04f, .002f, .07f, .12f, 8);
            }
            else
            {
                const float qw = .34f, qh = 1.40f, u0 = .39f;
                var cont = MetContent("65670");
                var tex = art.GetTexture("_BaseMap");
                float du = tex == null ? .0965f : qw / qh * cont.height * tex.height / tex.width;
                // 画面朝局部 +x（转 90° 后画的 +x 落在扇面的 -z，也就是看画人的左边），所以 U 反着取
                var uv = Rect.MinMaxRect(u0 + (i + 1) * du, cont.yMin, u0 + i * du, cont.yMax);
                k.Push(new Vector3(.0095f, 0, 0), 90f);
                PictureQuad(k, art, new Vector3(0, .725f, 0), qw, qh, uv);
                k.Pop();
            }
            k.Pop();
        }

        static void FurnTsugi(Kit k)
        {
            PendantLamp(k, 10.296f, 4.504f, 2.85f, .35f, 1);
            // 金屏风（四扇，折成锯齿）：东北角贴北墙，正面朝南，给茶桌当背景（西北角让给长持）。
            // 第三轮：画面换成 Met 65670（桜・梅・柳図屏風）中间偏右那一段（柳树干 + 两棵大樱树），四扇连成一幅；
            // 原来手画的墨枝和松叶（远看是黑色八角形）不要了。Swap：第一遍仍按原样量尺寸，搬家结果不变。
            var art = MetMat("65670");
            for (int i = 0; i < 4; i++)
            {
                float x = 11.326f + .18f + i * .355f, z = 6.8375f + (i % 2 == 0 ? .045f : -.045f);
                float yaw = (i % 2 == 0 ? 20f : -20f) + 90f;
                int pi = i;
                k.Swap(() => TsugiByobuPanel(k, x, z, yaw, pi, null), () => TsugiByobuPanel(k, x, z, yaw, pi, art));
            }
            Blocker("Byobu", new Vector3(12.0385f, .55f, 6.8375f), 0, new Vector3(1.42f, 1.5f, .24f));
            // 茶桌一组（茶桌在 BuildProps）：屏风前面、离两面墙各 1 m 左右自由摆放，坐垫在南边面朝屏风；
            // 离墙超过 SnapReach 不会被推到墙边，也躲开北门、东门的留空区
            // 火钵 + 铁壶：茶桌西边，转 180° 让火箸朝着茶桌
            k.Push(new Vector3(10.529f, .55f, 5.738f), 180f);
            // 第四轮审图：原来是蓝圆柱 + 方块五徳 + 球形铁壶，换成车削的褐釉陶火钵（DrawHibachi 的中心在局部 z=.27，往回挪）。
            // Swap：第一遍仍按原来的圆柱量尺寸，搬家结果不变；挡块不变
            k.Swap(() =>
            {
                k.C(D.CeramicBlue, 0, 0, 0, .19f, .22f, .29f, 16);
                k.C(D.Ash, 0, .29f, 0, .19f, .19f, .004f, 16);
                foreach (float a in new[] { 0f, 120f, 240f })
                {
                    var p = Quaternion.Euler(0, a, 0) * new Vector3(.08f, 0, 0);
                    k.Bb(D.Iron, p.x, .294f, p.z, .015f, .06f, .015f);
                }
                k.S(D.Iron, 0, .41f, 0, .1f, .07f, .1f, 12);
                k.C(D.Iron, 0, .47f, 0, .04f, .03f, .02f, 10);
                k.Cr(D.Iron, new Vector3(.08f, .40f, 0), .012f, .007f, .1f, Quaternion.Euler(0, 0, -55), 6);
                k.Br(D.Iron, 0, .53f, 0, .16f, .01f, .012f, 0, 0, 0);
                k.Cr(D.Brass, new Vector3(-.15f, .295f, -.08f), .005f, .004f, .3f, Quaternion.Euler(10, 0, -70), 5);
                k.Cr(D.Brass, new Vector3(-.15f, .295f, -.05f), .005f, .004f, .3f, Quaternion.Euler(14, 0, -70), 5);
            }, () =>
            {
                k.Push(new Vector3(0f, 0f, -.27f));
                DrawHibachi(k);
                k.Pop();
            });
            k.Pop();
            Blocker("Hibachi", new Vector3(10.529f, .55f, 5.738f), 0, new Vector3(.46f, .32f, .46f));
            Zabuton(k, 11.339f, .55f, 5.038f, 0, D.ClothPurple);
        }

        // ---------------------------------------------------------------- 仏間
        static void FurnButsuma(Kit k)
        {
            PendantLamp(k, 15.443f, 4.504f, 2.85f, .35f, 1);
            // 仏坛：局部 z=0 是柜子正面，+z 朝房间；経机在供台下面
            k.Push(new Vector3(16.829f, .55f, 6.398f), 180f);
            k.B(D.Black, 0, 1.0f, .004f, 1.1f, 1.05f, .008f);
            k.B(D.Gold, 0, 1.05f, .009f, .96f, .9f, .004f);
            k.B(D.GoldLeaf, 0, .62f, .07f, 1.0f, .05f, .14f);
            k.B(D.Lacquer, 0, .66f, .045f, .9f, .04f, .09f);
            k.B(D.GoldLeaf, 0, .78f, .05f, .8f, .04f, .1f);
            k.B(D.GoldLeaf, 0, .92f, .04f, .5f, .04f, .08f);
            k.C(D.Gold, 0, .94f, .04f, .06f, .05f, .03f, 12);
            k.S(D.Gold, 0, 1.04f, .04f, .045f, .08f, .04f, 10);
            k.S(D.Gold, 0, 1.14f, .04f, .025f, .03f, .025f, 8);
            k.S(D.Gold, 0, 1.08f, .02f, .09f, .14f, .01f, 12);
            foreach (float sx in new[] { -.36f, .36f })
            {
                k.B(D.Gold, sx, 1.42f, .06f, .004f, .1f, .004f);
                k.C(D.Gold, sx, 1.24f, .06f, .045f, .03f, .12f, 6);
            }
            foreach (float sx in new[] { -.18f, .18f })
            {
                k.Bb(D.Lacquer, sx, .80f, .05f, .07f, .21f, .025f);
                k.Bb(D.Gold, sx, .83f, .064f, .03f, .14f, .002f);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                k.Push(new Vector3(sx * .6f, 0, 0), sx * 15f);
                k.Bb(D.Lacquer, sx * .015f, .45f, .29f, .03f, 1.1f, .58f);
                k.Bb(D.Gold, -sx * .002f, .5f, .29f, .004f, 1.0f, .5f);
                k.Pop();
            }
            const float ay = .475f; // 供台台面
            k.C(D.Brass, 0, ay, .15f, .07f, .06f, .06f, 14);
            k.C(D.Ash, 0, ay + .06f, .15f, .055f, .055f, .003f, 12);
            for (int i = 0; i < 3; i++)
                k.Cr(D.CeramicBrown, new Vector3(-.02f + i * .02f, ay + .06f, .15f), .002f, .002f, .14f, Quaternion.Euler(i * 4 - 4, 0, i * 5 - 5), 4);
            foreach (float sx in new[] { -1f, 1f })
            {
                k.C(D.Brass, sx * .27f, ay, .12f, .035f, .02f, .12f, 10);
                k.C(D.Candle, sx * .27f, ay + .12f, .12f, .012f, .012f, .1f, 8);
                k.S(D.Flame, sx * .27f, ay + .245f, .12f, .009f, .022f, .009f, 6);
                k.C(D.Brass, sx * .42f, ay, .1f, .035f, .05f, .16f, 10);
                k.S(D.Leaf, sx * .42f, ay + .24f, .1f, .07f, .1f, .05f, 7);
            }
            k.B(D.ClothRed, .14f, ay + .005f, .24f, .1f, .01f, .1f);
            k.S(D.Brass, .14f, ay + .045f, .24f, .05f, .035f, .05f, 10);
            k.Bb(D.Lacquer, 0, .26f, .16f, .7f, .04f, .26f);
            foreach (float sx in new[] { -.3f, .3f }) k.Bb(D.Lacquer, sx, 0, .16f, .05f, .26f, .22f);
            k.S(D.ClothRed, -.15f, .35f, .16f, .07f, .05f, .07f, 8);
            k.B(D.Paper, .15f, .305f, .16f, .2f, .01f, .14f);
            k.Pop();
            // 盆提灯
            foreach (float x in new[] { 15.95f, 17.7f })
            {
                k.Push(new Vector3(x, .55f, 6.55f));
                for (int i = 0; i < 3; i++)
                {
                    k.Push(Vector3.zero, i * 120f);
                    k.Br(D.Lacquer, 0, .5f, .06f, .015f, 1.0f, .015f, -7f, 0, 0);
                    k.Pop();
                }
                k.C(D.Lacquer, 0, .98f, 0, .1f, .1f, .02f, 12);
                k.C(D.Lacquer, 0, .93f, 0, .05f, .05f, .03f, 10);
                k.S(D.PaperLamp, 0, .76f, 0, .12f, .16f, .12f, 12);
                k.C(D.Lacquer, 0, .58f, 0, .05f, .05f, .03f, 10);
                k.C(D.Gold, 0, .5f, 0, .008f, .008f, .08f, 6);
                k.Pop();
            }
            // 北墙长押上的遗影
            foreach (float x in new[] { 14.95f, 15.45f, 15.95f })
            {
                k.Push(new Vector3(x, 2.64f, 6.92f), 180f, 10f);
                PortraitFrame(k, .26f, .32f);
                k.B(D.Black, 0, .17f, .02f, .1f, .03f, .02f);
                k.Pop();
            }
        }

        // ---------------------------------------------------------------- 座敷
        static void FurnZashiki(Kit k)
        {
            PendantLamp(k, 21.2345f, 4.504f, 2.85f, .35f, 1);
            k.Bb(D.Lacquer, 20.0f, .84f, 5.675f, 1.5f, .04f, .75f);
            foreach (float sx in new[] { -.68f, .68f })
                foreach (float sz in new[] { -.31f, .31f })
                    k.Bb(D.Lacquer, 20f + sx, .55f, 5.675f + sz, .06f, .29f, .06f);
            k.C(D.CeramicWhite, 19.7f, .88f, 5.6f, .035f, .03f, .06f, 10);
            k.C(D.CeramicWhite, 19.85f, .88f, 5.8f, .035f, .03f, .06f, 10);
            k.C(D.CeramicBrown, 20.1f, .88f, 5.7f, .06f, .05f, .1f, 12);
            k.C(D.Brass, 20.45f, .88f, 5.8f, .06f, .06f, .02f, 12);
            Blocker("Zataku", new Vector3(20.0f, .55f, 5.675f), 0, new Vector3(1.5f, .33f, .75f));
            Zabuton(k, 19.55f, .55f, 4.95f, 0, D.ClothPurple);
            Zabuton(k, 20.45f, .55f, 4.95f, 0, D.ClothPurple);
            Zabuton(k, 18.95f, .55f, 5.675f, 90, D.ClothPurple);
            // 床の間：床柱、落とし掛け、下がり壁、香炉（落地，按新层高直接写）
            float ceil = Levels[G0].wallTop, otoshi = .55f + DoorTop - .15f;
            Kit.Pinned = true;
            k.Bb(D.TrimLight, 20.335f, .55f, 6.585f, .11f, ceil - .55f, .11f);
            k.Bb(D.Post, 19.45f, otoshi, 6.56f, 1.88f, .07f, .06f);
            k.Bb(P.Plaster, 19.45f, otoshi + .07f, 6.55f, 1.88f, ceil - otoshi - .07f, .03f);
            Kit.Pinned = false;
            k.C(D.Lacquer, 19.9f, .72f, 6.72f, .08f, .08f, .02f, 12);
            // 第三轮：香炉换成古董花瓶（CC0 模型）插一枝枯枝。Swap 让搬家求解看到的还是原来的香炉，容器不动
            k.Swap(() => k.C(D.Brass, 19.9f, .74f, 6.72f, .06f, .07f, .07f, 12),
                () => k.Model("antique_ceramic_vase_01", new Vector3(19.9f, .74f, 6.72f), 35f, .8f));
            k.Swap(() => k.C(D.Brass, 19.9f, .81f, 6.72f, .045f, .02f, .04f, 10),
                () => k.Model("dead_quiver_branch_01", new Vector3(19.9f, .74f + .29f, 6.72f), 205f, 1.15f));
        }

        // ---------------------------------------------------------------- 居間
        static void FurnIma(Kit k)
        {
            PendantLamp(k, 10.296f, 9.652f, 2.85f, .5f, 0);
            Zabuton(k, 10.55f, .55f, 8.12f, 90, D.ClothRed);
            // 神棚（长押上方）
            k.Push(new Vector3(8.75f, 2.48f, 12.146f), 180f);
            k.Bb(D.TrimLight, 0, 0, .15f, .9f, .025f, .3f);
            foreach (float sx in new[] { -.38f, .38f }) k.B(D.TrimLight, sx, -.06f, .06f, .03f, .12f, .12f);
            k.Bb(D.TrimLight, 0, .025f, .13f, .44f, .04f, .2f);
            k.Bb(D.TrimLight, 0, .065f, .12f, .3f, .17f, .14f);
            foreach (float sx in new[] { -.04f, .04f }) k.Bb(D.Gold, sx, .1f, .191f, .05f, .1f, .002f);
            foreach (float s in new[] { -1f, 1f }) k.Br(D.TrimMid, 0, .27f, .12f + s * .06f, .42f, .015f, .15f, s * 25f, 0, 0);
            k.Bb(D.TrimMid, 0, .285f, .12f, .44f, .02f, .02f);
            foreach (float sx in new[] { -.32f, .32f })
            {
                k.C(D.CeramicWhite, sx, .025f, .16f, .025f, .03f, .07f, 10);
                k.S(D.Leaf, sx, .14f, .16f, .05f, .07f, .04f, 7);
            }
            k.Cr(D.Straw, new Vector3(-.45f, .33f, .3f), .018f, .018f, .9f, AlongX, 8);
            foreach (float sx in new[] { -.25f, 0f, .25f })
            {
                k.B(D.Paper, sx, .26f, .3f, .03f, .1f, .002f);
                k.B(D.Paper, sx + .012f, .19f, .3f, .03f, .06f, .002f);
            }
            k.Pop();
            // 挂钟
            k.Push(new Vector3(11.0f, 1.95f, 12.146f), 180f);
            k.B(D.Post, 0, 0, .06f, .3f, .56f, .12f);
            k.Cr(D.CeramicWhite, new Vector3(0, .13f, .12f), .11f, .11f, .006f, AlongZ, 20);
            k.B(D.Ink, 0, .16f, .1275f, .008f, .07f, .002f);
            k.B(D.Ink, .03f, .13f, .1275f, .06f, .006f, .002f);
            k.B(D.Glass, 0, -.13f, .121f, .2f, .22f, .004f);
            k.B(D.Brass, 0, -.12f, .11f, .004f, .16f, .004f);
            k.Cr(D.Brass, new Vector3(0, -.2f, .106f), .03f, .03f, .006f, AlongZ, 12);
            k.Pop();
        }

        // ---------------------------------------------------------------- 納戸
        static void FurnNando(Kit k)
        {
            PendantLamp(k, 15.443f, 9.652f, 2.85f, .55f, 2);
            k.Push(new Vector3(16.85f, .55f, 11.935f), 180f);
            var lv = ShelfUnit(k, 1.5f, 1.8f, .37f, 4, D.TrimMid);
            k.C(D.CeramicBrown, -.55f, lv[0], 0, .1f, .08f, .25f, 12);
            k.C(D.CeramicBrown, -.3f, lv[0], 0, .08f, .06f, .2f, 12);
            k.C(D.CeramicBlue, .45f, lv[0], 0, .11f, .07f, .3f, 12);
            // 第四轮：圆柱的布卷、方块的叠布换成软网格（布卷看得到最外一层的布边，叠布四角圆、一边塌）；Swap：搬家照旧
            k.Swap(() =>
            {
                k.Cr(D.ClothWhite, new Vector3(-.68f, lv[1] + .11f, .02f), .11f, .11f, .62f, AlongX, 12);
                k.Cr(D.ClothIndigo, new Vector3(.02f, lv[1] + .11f, .02f), .11f, .11f, .62f, AlongX, 12);
                k.Bb(D.ClothPurple, -.45f, lv[2], 0, .38f, .08f, .28f);
                k.Bb(D.ClothRed, -.45f, lv[2] + .08f, 0, .36f, .07f, .27f);
                k.Bb(D.ClothWhite, .2f, lv[2], 0, .42f, .12f, .28f);
            }, () =>
            {
                SoftRoll(k.mb, D.ClothWhite, null, new Vector3(-.37f, lv[1], .02f), Quaternion.identity, .62f, .11f, seed: 1.3f);
                SoftRoll(k.mb, D.ClothIndigo, null, new Vector3(.33f, lv[1], .02f), Quaternion.identity, .62f, .11f, seed: 4.1f);
                FoldedCloth(k.mb, D.ClothPurple, new Vector3(-.45f, lv[2], 0f), 2f, .38f, .08f, .28f, 2.2f);
                FoldedCloth(k.mb, D.ClothRed, new Vector3(-.45f, lv[2] + .08f, 0f), -4f, .36f, .07f, .27f, 3.7f);
                FoldedCloth(k.mb, D.ClothWhite, new Vector3(.2f, lv[2], 0f), 3f, .42f, .12f, .28f, 5.9f);
                KitShot(k, "nando_shelf", new Vector3(0f, lv[1] + .15f, 0f), 1.9f, .15f);
            });
            k.Bb(D.Paper, -.45f, lv[3], 0, .3f, .1f, .22f);
            // 玻璃柜里的日本人形
            k.Bb(D.Lacquer, .35f, lv[3], 0, .3f, .03f, .24f);
            k.C(D.ClothRed, .35f, lv[3] + .03f, 0, .07f, .045f, .2f, 10);
            k.S(D.CeramicWhite, .35f, lv[3] + .27f, 0, .042f, .05f, .042f, 10);
            k.S(D.Black, .35f, lv[3] + .29f, -.01f, .048f, .045f, .046f, 10);
            foreach (float sx in new[] { .21f, .49f })
                foreach (float sz in new[] { -.11f, .11f })
                    k.Bb(D.Lacquer, sx, lv[3] + .03f, sz, .01f, .34f, .01f);
            k.Bb(D.Lacquer, .35f, lv[3] + .37f, 0, .3f, .01f, .24f);
            k.Pop();
            Blocker("NandoShelf", new Vector3(16.85f, .55f, 11.935f), 180f, new Vector3(1.5f, 1.8f, .37f));
        }

        // ---------------------------------------------------------------- 奥の間
        static void FurnOku(Kit k)
        {
            PendantLamp(k, 21.2345f, 9.652f, 2.85f, .35f, 1);
            // 镜台：镜子被紫布盖住大半
            k.Push(new Vector3(24.1f, .55f, 7.7f), 270f);
            k.Bb(D.Post, 0, 0, 0, .5f, .34f, .34f);
            k.Bb(D.Lacquer, 0, .34f, -.06f, .36f, .66f, .04f);
            k.B(D.Mirror, 0, .72f, -.038f, .3f, .54f, .004f);
            // 第四轮：盖镜子的紫布从一块方块换成搭在镜框顶上、前面垂下来起褶的布（Soft.cs TowelOnBar）；Swap：搬家照旧
            k.Swap(() =>
            {
                k.Bb(D.ClothPurple, 0, .58f, -.06f, .4f, .5f, .09f);
                k.Br(D.ClothPurple, .12f, .62f, -.01f, .08f, .2f, .01f, 0, 0, 8f);
            }, () =>
            {
                TowelOnBar(k.mb, D.ClothPurple, D.ClothPurple, new Vector3(-.21f, .976f, -.06f), Quaternion.identity, .42f, .43f, .1f, .024f, 3.1f,
                    foldAmp: .012f, thick: .004f, skew: .06f);
                KitShot(k, "kyodai", new Vector3(0f, .75f, 0f), 1.4f, .3f);
            });
            k.Bb(D.Lacquer, .12f, .34f, .1f, .12f, .01f, .04f);
            k.C(D.CeramicWhite, -.12f, .34f, .08f, .03f, .025f, .06f, 10);
            k.Pop();
            Blocker("Kyodai", new Vector3(24.1f, .55f, 7.7f), 270f, new Vector3(.5f, 1.25f, .34f));
            // 衣桁上挂着红色振袖
            k.Push(new Vector3(21.6f, .55f, 8.6f));
            foreach (float sx in new[] { -.72f, .72f })
            {
                k.Bb(D.Lacquer, sx, 0, 0, .05f, .04f, .45f);
                k.Bb(D.Lacquer, sx, 0, 0, .04f, 1.6f, .04f);
            }
            k.Bb(D.Lacquer, 0, 1.58f, 0, 1.75f, .04f, .045f);
            k.Bb(D.Lacquer, 0, 1.15f, 0, 1.44f, .03f, .035f);
            // 第四轮：振袖从几块薄板换成挂在衣桁上、有竖褶、下摆微张的布（身、两袖、白衿、金线一道；Soft.cs HangCloth）；Swap：搬家照旧
            k.Swap(() =>
            {
                k.Bb(D.ClothRed, 0, .22f, .03f, .62f, 1.36f, .03f);
                foreach (float sx in new[] { -.55f, .55f }) k.Bb(D.ClothRed, sx, 1.08f, .03f, .5f, .5f, .025f);
                k.Bb(D.Gold, 0, .5f, .047f, .62f, .05f, .004f);
                k.Bb(D.ClothWhite, -.05f, 1.3f, .047f, .04f, .28f, .004f);
                k.Bb(D.ClothWhite, .05f, 1.3f, .047f, .04f, .28f, .004f);
            }, () =>
            {
                HangCloth(k.mb, D.ClothRed, D.ClothRed, new Vector3(-.31f, 1.58f, .044f), Quaternion.identity,
                    new HangSpec { width = .62f, length = 1.36f, thick = .006f, foldAmp = .007f, foldLen = .16f, flare = .04f, hem = .008f, seed = 1.9f });
                foreach (float sx in new[] { -.55f, .55f })
                    HangCloth(k.mb, D.ClothRed, D.ClothRed, new Vector3(sx - .25f, 1.58f, .03f), Quaternion.identity,
                        new HangSpec { width = .5f, length = .5f, thick = .005f, foldAmp = .006f, foldLen = .12f, hem = .01f, seed = 2.7f + sx });
                foreach (float sx in new[] { -.05f, .05f })
                    HangCloth(k.mb, D.ClothWhite, D.ClothWhite, new Vector3(sx - .02f, 1.44f, .052f), Quaternion.identity,
                        new HangSpec { width = .04f, length = .28f, thick = .002f, foldAmp = 0f, hem = 0f });
                HangCloth(k.mb, D.Gold, D.Gold, new Vector3(-.315f, .55f, .053f), Quaternion.identity,
                    new HangSpec { width = .63f, length = .05f, thick = .002f, foldAmp = 0f, hem = 0f });
                KitShot(k, "kimono", new Vector3(0f, .95f, 0f), 2.4f, .15f);
            });
            k.Pop();
            Blocker("Iko", new Vector3(21.6f, .55f, 8.6f), 0, new Vector3(1.5f, 1.65f, .45f));
            // 行灯
            k.Push(new Vector3(24.0f, .55f, 11.2f));
            k.Bb(D.Lacquer, 0, 0, 0, .3f, .03f, .3f);
            foreach (float sx in new[] { -.14f, .14f })
                foreach (float sz in new[] { -.14f, .14f })
                    k.Bb(D.Lacquer, sx, 0, sz, .02f, .78f, .02f);
            k.Bb(D.PaperLamp, 0, .18f, 0, .26f, .5f, .26f);
            k.Bb(D.Lacquer, 0, .76f, 0, .3f, .02f, .3f);
            k.Bb(D.Lacquer, 0, .78f, 0, .02f, .12f, .02f);
            k.Pop();
        }
    }
}
