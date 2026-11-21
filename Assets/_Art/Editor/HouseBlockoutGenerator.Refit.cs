using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第四轮（2026-11-20）：BuildProps 里的场景家具（床、桌椅、灶台、浴桶、坐垫……）原来是几块方块拼的、没有纹理
    /// （用户截图：“这个床根本不像现实的床，就是几块几何体拼接在一起”）。
    /// 搬家求解算完、污渍也找好地方以后，把它们的子物体换成：CC0 模型（铁床架、餐桌、椅子、学生桌、床头小桌）
    /// + 算出来的软东西（床垫、被子、枕头、坐垫，见 Soft.cs）+ 倒角木件和车削出来的器物。
    /// 只换样子：根节点的位置、朝向、放大倍数、BoxCollider 都不动；Firebox（台所烟熏要找它）留一个空节点；
    /// 桌面高度和原来一样（搬家时跟着桌子走的摆件不会浮空、不会陷进去）。座敷床の間归 ZashikiArt（运行时换模型），不碰。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>榻榻米面比地面高 6.5 mm（软东西放在榻榻米上要抬这么多，不然陷进去）。</summary>
        const float SoftTatamiLift = .0075f;

        /// <summary>old_bed_frame 的弹簧床面：离地高度、宽、长（FBX 里量的，模型自身尺寸，床长沿 z）。</summary>
        const float BedDeckY = .496f, BedDeckW = .865f, BedDeckL = 1.839f;

        static Vector2 Pr(float r, float y) => new Vector2(r, y);

        /// <summary>按最小、最大坐标给的倒角方块。</summary>
        static void BevelMM(MeshBatch mb, Material m, float x0, float x1, float y0, float y1, float z0, float z1, float bev = .006f) =>
            BevelBox(mb, m, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, y1 - y0, z1 - z0), bev);

        // =====================================================================
        // 外部模型：量朝向、非等比摆放
        // =====================================================================
        static readonly Dictionary<string, float> HighSideCache = new Dictionary<string, float>();

        /// <summary>
        /// 模型“高的那部分”（高度 above 以上的顶点，按总高的比例）偏向自身 +z 还是 -z：床头、椅背在哪一头。
        /// 读不到顶点（或前后差不多）时用 fallback。
        /// </summary>
        static float HighSideZ(string id, float above, float fallback)
        {
            string key = id + "|" + above;
            if (HighSideCache.TryGetValue(key, out float v)) return v;
            var b = ExternalModels.LocalBounds(id);
            float sum = 0f;
            int n = 0;
            try
            {
                foreach (var p in ExternalModels.LocalVerts(id))
                    if (p.y > b.min.y + above * b.size.y) { sum += p.z - b.center.z; n++; }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Hako Art] 读不到 {id} 的顶点（{e.Message}），朝向按默认");
            }
            v = n > 0 && Mathf.Abs(sum / n) > .02f * b.size.z ? Mathf.Sign(sum / n) : fallback;
            HighSideCache[key] = v;
            Debug.Log($"[Hako Art] {id}：高处的 {n} 个顶点偏向 {(v > 0 ? "+z" : "-z")}");
            return v;
        }

        /// <summary>非等比缩放的模型：转好、缩放好以后最低点落在 basePos.y，水平居中在 basePos。</summary>
        static Matrix4x4 PlaceNU(string id, Vector3 basePos, Quaternion rot, Vector3 scale)
        {
            var b = ExternalModels.LocalBounds(id);
            Vector3 mn = Vector3.one * float.MaxValue, mx = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var p = rot * Vector3.Scale(b.center + Vector3.Scale(b.extents, ExternalModels.Corner(i)), scale);
                mn = Vector3.Min(mn, p);
                mx = Vector3.Max(mx, p);
            }
            return Matrix4x4.TRS(basePos + new Vector3(-(mn.x + mx.x) / 2f, -mn.y, -(mn.z + mx.z) / 2f), rot, scale);
        }

        // =====================================================================
        // 入口
        // =====================================================================
        static void RefitFixtures(Transform root)
        {
            var props = root.Find("Props");
            if (props == null) return;
            var done = new List<string>();
            foreach (var fx in props.Cast<Transform>().ToList())
            {
                int seed = StableHash(fx.name);
                System.Action<MeshBatch> draw = null;
                string need = null; // 要用的外部模型；缺了就不换（保持原来的方块，不让家具消失）
                switch (fx.name)
                {
                    case "Shinshitsu_MainBed": need = "old_bed_frame"; draw = mb => WesternBed(mb, 1.5f, true, seed); break;
                    case "Kodomo_SingleBed": need = "old_bed_frame"; draw = mb => WesternBed(mb, .92f, false, seed); break;
                    case "Oku_GuestFuton":
                        draw = mb => LaidFutonSoft(mb, new Vector3(0f, SoftTatamiLift, 0f), 0f, D.GinghamGreen, D.ClothWhite, D.ClothIndigo, seed, false);
                        break;
                    case "Daidokoro_DiningTable": need = "painted_wooden_table"; draw = DiningTable; break;
                    case "DiningChair_S":
                    case "DiningChair_N": need = "painted_wooden_chair_01"; draw = mb => ModelChair(mb, "painted_wooden_chair_01", .9f); break;
                    case "Kodomo_DeskChair": need = "painted_wooden_chair_02"; draw = mb => ModelChair(mb, "painted_wooden_chair_02", .72f); break;
                    case "Shinshitsu_Bedside": need = "side_table_01"; draw = Bedside; break;
                    case "Kodomo_StudyDesk": need = "SchoolDesk_01"; draw = StudyDesk; break;
                    case "Tsugi_TeaTable": draw = mb => TeaTable(mb, seed); break;
                    case "Ima_IroriTable": draw = mb => IroriTable(mb, seed); break;
                    case "Washitsu2F_WritingTable": draw = WritingTable; break;
                    case "Genkan_ShoeBench": draw = ShoeBench; break;
                    case "Daidokoro_KamadoAndSink": draw = KamadoAndSink; break;
                    case "Furo_HinokiTub": draw = mb => HinokiTub(mb, seed); break;
                    case "Furo_WashingStool": draw = WashingStool; break;
                    case "Ima_Zabuton":
                    case "Butsuma_PrayerCushion":
                    case "Washitsu2F_Zabuton":
                        if (RefitCushion(fx, seed))
                        {
                            done.Add(fx.name);
                            ShotOfTransform(fx.name, fx);
                        }
                        continue;
                }
                if (draw == null) continue;
                if (need != null && !ExternalModels.Has(need))
                {
                    Debug.LogWarning($"[Hako Art] 缺模型 {need}，{fx.name} 保持原来的方块");
                    continue;
                }
                var fire = fx.Find("Firebox");
                Vector3? firePos = fire != null ? fire.localPosition : (Vector3?)null;
                for (int i = fx.childCount - 1; i >= 0; i--) Object.DestroyImmediate(fx.GetChild(i).gameObject);
                var batch = new MeshBatch();
                draw(batch);
                batch.Build(fx, "Visual");
                if (firePos.HasValue) ArtKit.Empty(fx, "Firebox", firePos.Value);
                done.Add(fx.name);
                if (!fx.name.Contains("Chair")) ShotOfTransform(fx.name, fx); // 审图（椅子在餐桌、学生桌那张里）
            }
            Debug.Log($"[Hako Art] 场景家具换样子 {done.Count} 件：{string.Join(" ", done)}");
        }

        // =====================================================================
        // 床
        // =====================================================================
        /// <summary>
        /// 洋室的旧铁床：old_bed_frame（床头朝 +z，和原来的 Headboard 一样）+ 条纹床垫（中间塌）+ 枕头 + 被子。
        /// twin：主卧两张单人床并排（一张铺着、被子掀开一角；一张光床垫，枕头歪着，脚那头一条叠起来的毛毯）。
        /// </summary>
        static void WesternBed(MeshBatch mb, float width, bool twin, int seed)
        {
            const string id = "old_bed_frame";
            var b = ExternalModels.LocalBounds(id);
            float tall = HighSideZ(id, .8f, -1f);
            var rot = Quaternion.Euler(0f, tall > 0f ? 0f : 180f, 0f);
            int n = twin ? 2 : 1;
            float each = width / n, sx = each / b.size.x, sz = 2.04f / b.size.z;
            for (int i = 0; i < n; i++)
            {
                float cx = twin ? (i - .5f) * each : 0f;
                mb.Model(id, PlaceNU(id, new Vector3(cx, 0f, 0f), rot, new Vector3(sx, 1f, sz)));
                bool bare = twin && i == 1;
                var ms = new PuffSpec
                {
                    w = BedDeckW * sx - .012f, d = BedDeckL * sz - .015f, hTop = .1f, hBot = .05f, corner = .05f, rampTop = .06f, rampBot = .04f,
                    dome = .06f, sag = bare ? .045f : .02f, wrinkle = .003f, pucker = .0015f, pipe = .007f, pipeMat = D.Ticking,
                    seed = Mathf.Abs(seed % 50) * .3f + i * 7f, nx = 18, nz = 34,
                };
                var at = new Vector3(cx, BedDeckY + .003f, 0f);
                Puff(mb, D.Ticking, D.Ticking, at, Quaternion.identity, ms);
                float pz = ms.d / 2f - .22f;
                var ps = new PuffSpec
                {
                    w = Mathf.Min(.6f, ms.w - .12f), d = .36f, hTop = .055f, hBot = .045f, corner = .08f, rampTop = .13f, rampBot = .1f,
                    dome = .4f, dent = bare ? 0f : .03f, dentAt = new Vector2(.03f, -.02f), dentR = .1f, wrinkle = .006f, pucker = .004f,
                    seed = i * 3.7f + 1f, nx = 16, nz = 12,
                };
                float py = at.y + PuffTop(ms, 0f, pz) - .012f;
                if (bare)
                {
                    Puff(mb, D.ClothWhite, D.ClothWhite, new Vector3(cx + .07f, py, pz - .06f), Quaternion.Euler(0f, 17f, 0f), ps);
                    float fz = -ms.d / 2f + .3f;
                    FoldedCloth(mb, D.ClothPurple, new Vector3(cx - .04f, at.y + PuffTop(ms, 0f, fz) - .008f, fz), -8f, .5f, .06f, .36f, 5.3f);
                }
                else
                {
                    Puff(mb, D.ClothWhite, D.ClothWhite, new Vector3(cx, py, pz), Quaternion.identity, ps);
                    const float thick = .05f;
                    float mTop = ms.hBot + ms.hTop * (1f + ms.dome);
                    var qs = new SheetSpec
                    {
                        x0 = -ms.w / 2f, x1 = ms.w / 2f, z0 = -ms.d / 2f, z1 = ms.d / 2f - .5f, top = mTop + thick * .5f + .004f,
                        round = .04f, cone = .06f, floorY = -at.y,
                    };
                    var qm = twin ? D.Check : D.GinghamRed;
                    Quilt(mb, qm, D.ClothWhite, at, Quaternion.identity, qs, .2f, .2f, .05f, 0f, thick, seed + i);
                    FoldRoll(mb, qm, at + new Vector3(-ms.w / 2f - .02f, qs.top, qs.z1), Quaternion.identity, ms.w + .04f, .03f, i * 1.3f);
                }
            }
        }

        // =====================================================================
        // 桌椅（CC0 模型 + 桌上的碗、书）
        // =====================================================================
        static readonly Vector2[] YunomiProfile =
            { Pr(0, 0), Pr(.028f, 0), Pr(.031f, .004f), Pr(.034f, .07f), Pr(.035f, .08f), Pr(.032f, .081f), Pr(.031f, .075f), Pr(.029f, .012f), Pr(0, .01f) };
        static readonly Vector2[] GlassProfile =
            { Pr(0, 0), Pr(.028f, 0), Pr(.032f, .098f), Pr(.033f, .1f), Pr(.03f, .1f), Pr(.027f, .004f), Pr(0, .004f) };
        static readonly Vector2[] KyusuProfile =
        {
            Pr(0, 0), Pr(.042f, 0), Pr(.048f, .004f), Pr(.066f, .025f), Pr(.07f, .045f), Pr(.064f, .068f), Pr(.05f, .082f), Pr(.046f, .084f),
            Pr(.044f, .088f), Pr(.02f, .093f), Pr(.014f, .096f), Pr(.012f, .101f), Pr(.012f, .108f), Pr(.008f, .112f), Pr(0, .113f),
        };
        static readonly Vector2[] TetsubinProfile =
        {
            Pr(0, 0), Pr(.07f, 0), Pr(.09f, .01f), Pr(.115f, .04f), Pr(.12f, .07f), Pr(.113f, .11f), Pr(.09f, .135f), Pr(.06f, .145f),
            Pr(.056f, .147f), Pr(.04f, .152f), Pr(.014f, .155f), Pr(.012f, .158f), Pr(.012f, .166f), Pr(.006f, .17f), Pr(0, .171f),
        };
        static readonly Vector2[] HagamaProfile =
        {
            Pr(0, 0), Pr(.1f, 0), Pr(.15f, .03f), Pr(.17f, .07f), Pr(.175f, .085f), Pr(.25f, .09f), Pr(.252f, .1f), Pr(.176f, .105f),
            Pr(.168f, .17f), Pr(.162f, .18f), Pr(.152f, .18f), Pr(.15f, .16f), Pr(0, .16f),
        };

        /// <summary>
        /// 台所的餐桌：painted_wooden_table 缩到原来的 1.45 × 0.72 × 0.8。桌上原来就有一碗插着筷子的饭、蓝盘子、倒下的茶杯
        /// （Furnish.cs 台所，跟着桌子搬家），这里不再加东西，免得冲淡那碗饭。
        /// </summary>
        static void DiningTable(MeshBatch mb)
        {
            const string id = "painted_wooden_table";
            var b = ExternalModels.LocalBounds(id);
            mb.Model(id, PlaceNU(id, Vector3.zero, Quaternion.identity, new Vector3(1.45f / b.size.x, .72f / b.size.y, .8f / b.size.z)));
        }

        /// <summary>椅子模型：椅背朝 +z（原来的 Back 在 +z，人朝 -z 坐着对着桌子）。</summary>
        static void ModelChair(MeshBatch mb, string id, float scale)
        {
            float back = HighSideZ(id, .7f, -1f);
            mb.Model(id, ExternalModels.Place(id, null, Vector3.zero, Quaternion.Euler(0f, back > 0f ? 0f : 180f, 0f), scale));
        }

        /// <summary>主卧的床头小桌（side_table_01，两层开放的隔板）。台灯、相框本来就在上面（Furnish2 寝室），这里只加一只玻璃杯。</summary>
        static void Bedside(MeshBatch mb)
        {
            const string id = "side_table_01";
            var b = ExternalModels.LocalBounds(id);
            mb.Model(id, PlaceNU(id, Vector3.zero, Quaternion.identity, new Vector3(.46f / b.size.x, .45f / b.size.y, .46f / b.size.z)));
            Lathe(mb, D.Glass, new Vector3(.12f, .45f, .1f), Quaternion.identity, GlassProfile, 16);
        }

        /// <summary>儿童房：两张学生桌并排（兄弟俩各一张）；左边摊开的练习本和铅笔，右边一本合着的书。</summary>
        static void StudyDesk(MeshBatch mb)
        {
            const string id = "SchoolDesk_01";
            var b = ExternalModels.LocalBounds(id);
            var sc = new Vector3(.62f / b.size.x, .72f / b.size.y, Mathf.Min(.59f, b.size.z) / b.size.z);
            foreach (float cx in new[] { -.32f, .32f })
                mb.Model(id, PlaceNU(id, new Vector3(cx, 0f, 0f), Quaternion.identity, sc));
            const float top = .72f;
            OpenNotebook(mb, new Vector3(-.3f, top, .02f), -6f);
            Tube(mb, D.PlasticRed, new[] { new Vector3(-.12f, top + .004f, -.02f), new Vector3(-.05f, top + .004f, .13f) }, .0038f, 6);
            OldBook(mb, new Vector3(.36f, top, -.06f), 21f, P.Carpet, .16f, .028f, .22f);
        }

        /// <summary>合着的书：封面（上、下、书脊）+ 发黄的书页。at = 底面中心。</summary>
        static void OldBook(MeshBatch mb, Vector3 at, float yaw, Material cover, float w, float h, float d)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            const float t = .0025f;
            mb.Box(cover, at + q * new Vector3(0f, t / 2f, 0f), new Vector3(w, t, d), q);
            mb.Box(cover, at + q * new Vector3(0f, h - t / 2f, 0f), new Vector3(w, t, d), q);
            mb.Box(cover, at + q * new Vector3(-w / 2f + t / 2f, h / 2f, 0f), new Vector3(t, h, d), q);
            mb.Box(D.PaperOld, at + q * new Vector3(.002f, h / 2f, 0f), new Vector3(w - .008f, h - 2f * t, d - .006f), q);
        }

        /// <summary>摊开的练习本：两页往书脊那边微微拱起。at = 书脊下面的中点。</summary>
        static void OpenNotebook(MeshBatch mb, Vector3 at, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            foreach (float s in new[] { -1f, 1f })
                mb.Box(D.Paper, at + q * new Vector3(s * .076f, .0045f, 0f), new Vector3(.15f, .002f, .21f), q * Quaternion.Euler(0f, 0f, s * -3f));
            mb.Box(D.ClothIndigo, at + q * new Vector3(0f, .001f, 0f), new Vector3(.31f, .002f, .215f), q);
        }

        // =====================================================================
        // 和室的矮桌
        // =====================================================================
        /// <summary>次の間的茶桌：倒角桌面、一圈幕板、四条腿；桌上一把急須、两只茶杯。</summary>
        static void TeaTable(MeshBatch mb, int seed)
        {
            const float W = .86f, H = .31f, Dp = .56f, t = .03f, ins = .04f, ah = .05f, at = .018f;
            var wood = P.WoodMidA;
            BevelMM(mb, wood, -W / 2f, W / 2f, H - t, H, -Dp / 2f, Dp / 2f, .006f);
            float y0 = H - t - ah, y1 = H - t;
            BevelMM(mb, wood, -W / 2f + ins, W / 2f - ins, y0, y1, -Dp / 2f + ins, -Dp / 2f + ins + at, .003f);
            BevelMM(mb, wood, -W / 2f + ins, W / 2f - ins, y0, y1, Dp / 2f - ins - at, Dp / 2f - ins, .003f);
            BevelMM(mb, wood, -W / 2f + ins, -W / 2f + ins + at, y0, y1, -Dp / 2f + ins, Dp / 2f - ins, .003f);
            BevelMM(mb, wood, W / 2f - ins - at, W / 2f - ins, y0, y1, -Dp / 2f + ins, Dp / 2f - ins, .003f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    BevelBox(mb, P.WoodDarkA, new Vector3(sx * (W / 2f - ins - .0225f), (H - t) / 2f, sz * (Dp / 2f - ins - .0225f)), new Vector3(.045f, H - t, .045f), .004f);
            Kyusu(mb, new Vector3(0f, H, 0f), 30f + Mathf.Abs(seed % 40));
            Lathe(mb, D.CeramicWhite, new Vector3(.28f, H, .06f), Quaternion.identity, YunomiProfile, 18);
            Lathe(mb, D.CeramicWhite, new Vector3(-.26f, H, -.1f), Quaternion.identity, YunomiProfile, 18);
        }

        /// <summary>急須：圆肚茶壶、带盖钮，壶嘴朝 +x，侧把朝 -z（横手）。at = 底面中心。</summary>
        static void Kyusu(MeshBatch mb, Vector3 at, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Lathe(mb, D.CeramicBrown, at, q, KyusuProfile, 22);
            Tube(mb, D.CeramicBrown, new[] { at + q * new Vector3(.062f, .035f, 0f), at + q * new Vector3(.085f, .05f, 0f), at + q * new Vector3(.1f, .068f, 0f), at + q * new Vector3(.108f, .08f, 0f) }, .009f, 8);
            Tube(mb, D.CeramicBrown, new[] { at + q * new Vector3(0f, .05f, -.06f), at + q * new Vector3(0f, .056f, -.125f) }, .011f, 8);
        }

        /// <summary>
        /// 居間的囲炉裏卓：厚木框（炉縁）、四面围板，中间铜皮内衬的炉膛，灰堆成一个小丘、几块炭，
        /// 五徳（铁圈三脚）上坐一把铁壶，火箸斜插在灰里。顶面高度同原来（0.34）。
        /// </summary>
        static void IroriTable(MeshBatch mb, int seed)
        {
            const float W = 1.4f, H = .34f, Dp = .83f, ox = .395f, oz = .26f, fh = .06f, pit = .2f;
            var frame = P.WoodDarkA;
            var side = P.WoodDarkB;
            BevelMM(mb, frame, -W / 2f, W / 2f, H - fh, H, oz, Dp / 2f, .008f);
            BevelMM(mb, frame, -W / 2f, W / 2f, H - fh, H, -Dp / 2f, -oz, .008f);
            BevelMM(mb, frame, -W / 2f, -ox, H - fh, H, -oz, oz, .008f);
            BevelMM(mb, frame, ox, W / 2f, H - fh, H, -oz, oz, .008f);
            float sy = H - fh;
            BevelMM(mb, side, -W / 2f + .01f, W / 2f - .01f, 0f, sy, Dp / 2f - .035f, Dp / 2f - .01f, .004f);
            BevelMM(mb, side, -W / 2f + .01f, W / 2f - .01f, 0f, sy, -Dp / 2f + .01f, -Dp / 2f + .035f, .004f);
            BevelMM(mb, side, -W / 2f + .01f, -W / 2f + .035f, 0f, sy, -Dp / 2f + .035f, Dp / 2f - .035f, .004f);
            BevelMM(mb, side, W / 2f - .035f, W / 2f - .01f, 0f, sy, -Dp / 2f + .035f, Dp / 2f - .035f, .004f);
            BevelMM(mb, D.RustSheet, -ox, ox, pit, sy, oz - .004f, oz, .001f);
            BevelMM(mb, D.RustSheet, -ox, ox, pit, sy, -oz, -oz + .004f, .001f);
            BevelMM(mb, D.RustSheet, -ox, -ox + .004f, pit, sy, -oz, oz, .001f);
            BevelMM(mb, D.RustSheet, ox - .004f, ox, pit, sy, -oz, oz, .001f);
            var ash = new PuffSpec
            {
                w = 2f * ox - .01f, d = 2f * oz - .01f, hTop = .035f, hBot = .001f, corner = .02f, rampTop = .16f, rampBot = .01f,
                dome = .9f, wrinkle = .006f, pucker = 0f, seed = 2.2f, nx = 16, nz = 12,
            };
            Puff(mb, D.Ash, D.Ash, new Vector3(0f, pit, 0f), Quaternion.identity, ash);
            var rng = new System.Random(seed);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.3f + (float)rng.NextDouble(), r = .03f + .05f * (float)rng.NextDouble();
                var c = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                c.y = pit + PuffTop(ash, c.x, c.z) + .006f;
                mb.Sphere(D.Black, c, new Vector3(.024f, .016f, .019f), 8, Quaternion.Euler(0f, a * 57f, 0f));
            }
            const float ringY = .372f, ringR = .1f;
            var ring = new List<Vector3>();
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2f / 18f;
                ring.Add(new Vector3(Mathf.Cos(a) * ringR, ringY, Mathf.Sin(a) * ringR));
            }
            Tube(mb, D.Iron, ring, .006f, 6, true);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + .4f;
                var top = new Vector3(Mathf.Cos(a) * ringR, ringY, Mathf.Sin(a) * ringR);
                float bx = Mathf.Cos(a) * .13f, bz = Mathf.Sin(a) * .13f;
                Tube(mb, D.Iron, new[] { top, new Vector3(bx, pit + PuffTop(ash, bx, bz) - .01f, bz) }, .006f, 6);
            }
            Tetsubin(mb, new Vector3(0f, ringY - .014f, 0f), 20f);
            Tube(mb, D.Iron, new[] { new Vector3(.26f, pit + .03f, .13f), new Vector3(.33f, .46f, .19f) }, .004f, 5);
            Tube(mb, D.Iron, new[] { new Vector3(.28f, pit + .03f, .12f), new Vector3(.35f, .46f, .175f) }, .004f, 5);
        }

        /// <summary>铁壶（鉄瓶）：扁圆的壶身、小盖钮，壶嘴朝 +x，提梁沿 z 拱过头顶。at = 底面中心。</summary>
        static void Tetsubin(MeshBatch mb, Vector3 at, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Lathe(mb, D.Iron, at, q, TetsubinProfile, 22);
            Tube(mb, D.Iron, new[] { at + q * new Vector3(.1f, .05f, 0f), at + q * new Vector3(.14f, .08f, 0f), at + q * new Vector3(.162f, .112f, 0f), at + q * new Vector3(.17f, .128f, 0f) }, .013f, 8);
            var arc = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
            {
                float t = Mathf.PI * i / 12f;
                arc.Add(at + q * new Vector3(0f, .14f + .11f * Mathf.Sin(t), .085f * Mathf.Cos(t)));
            }
            Tube(mb, D.Iron, arc, .006f, 6);
            mb.Sphere(D.Iron, arc[0], Vector3.one * .012f, 8);
            mb.Sphere(D.Iron, arc[arc.Count - 1], Vector3.one * .012f, 8);
        }

        /// <summary>二楼和室的文机：倒角桌面、两头翘起的筆返し、板脚；桌上写了一半的信、信封、砚台和一支笔。</summary>
        static void WritingTable(MeshBatch mb)
        {
            const float W = 1.12f, H = .35f, Dp = .58f;
            var wood = P.WoodDarkB;
            BevelMM(mb, wood, -W / 2f, W / 2f, H - .025f, H, -Dp / 2f, Dp / 2f, .005f);
            foreach (float sx in new[] { -1f, 1f })
            {
                float a = sx * (W / 2f - .04f), e = sx * W / 2f;
                BevelMM(mb, wood, Mathf.Min(a, e), Mathf.Max(a, e), H, H + .02f, -Dp / 2f, Dp / 2f, .006f);
                float l0 = sx * (W / 2f - .09f), l1 = sx * (W / 2f - .06f);
                BevelMM(mb, wood, Mathf.Min(l0, l1), Mathf.Max(l0, l1), .03f, H - .025f, -Dp / 2f + .03f, Dp / 2f - .03f, .004f);
                float f0 = sx * (W / 2f - .11f), f1 = sx * (W / 2f - .04f);
                BevelMM(mb, wood, Mathf.Min(f0, f1), Mathf.Max(f0, f1), 0f, .03f, -Dp / 2f - .01f, Dp / 2f + .01f, .005f);
            }
            mb.Box(D.PaperOld, new Vector3(-.02f, H + .001f, .02f), new Vector3(.21f, .002f, .3f), Quaternion.Euler(0f, 7f, 0f));
            mb.Box(D.Paper, new Vector3(.26f, H + .0015f, -.08f), new Vector3(.11f, .003f, .22f), Quaternion.Euler(0f, -12f, 0f));
            BevelMM(mb, D.Black, -.42f, -.32f, H, H + .02f, -.1f, .07f, .004f);
            mb.Box(D.Water, new Vector3(-.37f, H + .0205f, .045f), new Vector3(.07f, .001f, .03f));
            Tube(mb, D.Bamboo, new[] { new Vector3(-.28f, H + .006f, .12f), new Vector3(-.1f, H + .006f, .17f) }, .005f, 6);
            Tube(mb, D.Black, new[] { new Vector3(-.1f, H + .006f, .17f), new Vector3(-.075f, H + .005f, .177f) }, .0045f, 6);
        }

        /// <summary>玄関的长凳：倒角凳面、四条腿、下层搁板，凳面上一块蓝布垫子。</summary>
        static void ShoeBench(MeshBatch mb)
        {
            const float W = 1.25f, H = .43f, Dp = .42f;
            var wood = P.WoodMidA;
            BevelMM(mb, wood, -W / 2f, W / 2f, H - .035f, H, -Dp / 2f, Dp / 2f, .006f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    BevelBox(mb, P.WoodDarkA, new Vector3(sx * (W / 2f - .06f), (H - .035f) / 2f, sz * (Dp / 2f - .05f)), new Vector3(.05f, H - .035f, .05f), .005f);
            BevelMM(mb, wood, -W / 2f + .085f, W / 2f - .085f, .09f, .108f, -Dp / 2f + .05f, Dp / 2f - .05f, .003f);
            var pad = new PuffSpec
            {
                w = .82f, d = .35f, hTop = .022f, hBot = .012f, corner = .04f, rampTop = .05f, rampBot = .02f, dome = .25f,
                wrinkle = .0025f, pucker = .0015f, seed = 3.3f, nx = 20, nz = 10,
            };
            Puff(mb, D.ClothIndigo, D.ClothIndigo, new Vector3(0f, H, 0f), Quaternion.Euler(0f, 3f, 0f), pad);
        }

        // =====================================================================
        // 台所、浴室
        // =====================================================================
        /// <summary>
        /// 台所的灶台 + 水槽台（3.2 m 长，正面 +z）：左边土かまど——正面一个铁框火口（火膛熏黑、一层灰、几块炭），
        /// 台面方洞里坐着一只铁羽釜、盖着木盖；右边人研ぎ（水磨石）的水槽台，水槽口一圈凸边、铁漏水口、一汪脏水，
        /// 后面一根铜水龙头弯过来；中间台面上一块砧板和一把菜刀。台面顶 0.86 同原来。
        /// </summary>
        static void KamadoAndSink(MeshBatch mb)
        {
            const float Top = .86f, Base = .78f, Dz = .35f, Tz = .37f;
            var clay = D.Dirt;
            var stone = D.Stone;
            const float k0 = -1.6f, k1 = -.4f, fx0 = -1.22f, fx1 = -.82f, fy0 = .2f, fy1 = .5f, back = .08f;
            BevelMM(mb, clay, k0, fx0, 0f, Base, -Dz, Dz, .015f);
            BevelMM(mb, clay, fx1, k1, 0f, Base, -Dz, Dz, .015f);
            BevelMM(mb, clay, fx0, fx1, 0f, fy0, -Dz, Dz, .008f);
            BevelMM(mb, clay, fx0, fx1, fy1, Base, -Dz, Dz, .008f);
            BevelMM(mb, clay, fx0, fx1, fy0, fy1, -Dz, back, .004f);
            mb.Box(D.Black, new Vector3((fx0 + fx1) / 2f, (fy0 + fy1) / 2f, back + .001f), new Vector3(fx1 - fx0, fy1 - fy0, .002f));
            foreach (float x in new[] { fx0 + .001f, fx1 - .001f })
                mb.Box(D.Black, new Vector3(x, (fy0 + fy1) / 2f, (back + Dz) / 2f), new Vector3(.002f, fy1 - fy0, Dz - back));
            mb.Box(D.Black, new Vector3((fx0 + fx1) / 2f, fy1 - .001f, (back + Dz) / 2f), new Vector3(fx1 - fx0, .002f, Dz - back));
            var ash = new PuffSpec
            {
                w = fx1 - fx0 - .01f, d = Dz - back - .01f, hTop = .02f, hBot = .001f, corner = .02f, rampTop = .08f, rampBot = .01f,
                dome = .6f, wrinkle = .004f, pucker = 0f, seed = 1.1f, nx = 10, nz = 8,
            };
            Puff(mb, D.Ash, D.Ash, new Vector3((fx0 + fx1) / 2f, fy0, (back + Dz) / 2f), Quaternion.identity, ash);
            for (int i = 0; i < 3; i++)
                mb.Sphere(D.Black, new Vector3(-1.08f + i * .07f, fy0 + .025f, .17f + (i % 2) * .05f), new Vector3(.03f, .018f, .022f), 8, Quaternion.Euler(0f, i * 50f, 0f));
            const float fw = .03f, ft = .012f;
            BevelMM(mb, D.Iron, fx0 - fw, fx1 + fw, fy1, fy1 + fw, Dz, Dz + ft, .003f);
            BevelMM(mb, D.Iron, fx0 - fw, fx1 + fw, fy0 - fw, fy0, Dz, Dz + ft, .003f);
            BevelMM(mb, D.Iron, fx0 - fw, fx0, fy0, fy1, Dz, Dz + ft, .003f);
            BevelMM(mb, D.Iron, fx1, fx1 + fw, fy0, fy1, Dz, Dz + ft, .003f);
            const float hx0 = -1.19f, hx1 = -.85f, hz = .17f;
            BevelMM(mb, clay, k0, hx0, Base, Top, -Tz, Tz, .01f);
            BevelMM(mb, clay, hx1, k1, Base, Top, -Tz, Tz, .01f);
            BevelMM(mb, clay, hx0, hx1, Base, Top, -Tz, -hz, .006f);
            BevelMM(mb, clay, hx0, hx1, Base, Top, hz, Tz, .006f);
            Lathe(mb, D.Iron, new Vector3(-1.02f, Top - .09f, 0f), Quaternion.identity, HagamaProfile, 26);
            mb.Cyl(P.WoodDarkA, new Vector3(-1.02f, Top + .09f, 0f), .172f, .172f, .025f, 24);
            BevelMM(mb, P.WoodDarkA, -1.12f, -.92f, Top + .115f, Top + .135f, -.018f, .018f, .004f);

            const float s0 = .63f, s1 = 1.33f, sz = .2f, sb = .70f;
            BevelMM(mb, stone, k1, s0, 0f, Base, -Dz, Dz, .01f);
            BevelMM(mb, stone, s1, 1.6f, 0f, Base, -Dz, Dz, .01f);
            BevelMM(mb, stone, s0, s1, 0f, Base, -Dz, -sz, .006f);
            BevelMM(mb, stone, s0, s1, 0f, Base, sz, Dz, .006f);
            BevelMM(mb, stone, s0, s1, 0f, sb, -sz, sz, .004f);
            BevelMM(mb, stone, k1, s0, Base, Top, -Tz, Tz, .008f);
            BevelMM(mb, stone, s1, 1.6f, Base, Top, -Tz, Tz, .008f);
            BevelMM(mb, stone, s0, s1, Base, Top, -Tz, -sz, .004f);
            BevelMM(mb, stone, s0, s1, Base, Top, sz, Tz, .004f);
            const float lip = .025f, lh = .015f;
            BevelMM(mb, stone, s0 - lip, s1 + lip, Top, Top + lh, sz, sz + lip, .004f);
            BevelMM(mb, stone, s0 - lip, s1 + lip, Top, Top + lh, -sz - lip, -sz, .004f);
            BevelMM(mb, stone, s0 - lip, s0, Top, Top + lh, -sz, sz, .004f);
            BevelMM(mb, stone, s1, s1 + lip, Top, Top + lh, -sz, sz, .004f);
            mb.Cyl(D.Iron, new Vector3(.98f, sb + .001f, 0f), .032f, .032f, .003f, 14);
            mb.Cyl(D.Water, new Vector3(.93f, sb + .002f, .04f), .1f, .1f, .001f, 18);
            const float tx = .98f, tz = -.32f;
            Tube(mb, D.Brass, new[]
            {
                new Vector3(tx, Top, tz), new Vector3(tx, 1.14f, tz), new Vector3(tx, 1.19f, tz + .025f), new Vector3(tx, 1.2f, tz + .09f),
                new Vector3(tx, 1.17f, tz + .15f), new Vector3(tx, 1.12f, tz + .16f),
            }, .012f, 8);
            mb.Cyl(D.Brass, new Vector3(tx, .98f, tz), .022f, .022f, .05f, 12);
            mb.Cyl(D.Brass, new Vector3(tx + .02f, 1.005f, tz), .005f, .005f, .03f, 6, true, AlongX);
            mb.Box(D.Brass, new Vector3(tx + .052f, 1.005f, tz), new Vector3(.008f, .07f, .012f));
            BevelMM(mb, P.WoodLightA, -.25f, .21f, Top, Top + .03f, -.14f, .14f, .004f);
            BevelBox(mb, D.Iron, new Vector3(0f, Top + .032f, .06f), new Vector3(.17f, .003f, .035f), .0008f, Quaternion.Euler(0f, 15f, 0f));
            BevelBox(mb, P.WoodMidB, new Vector3(-.13f, Top + .038f, .095f), new Vector3(.1f, .016f, .02f), .003f, Quaternion.Euler(0f, 15f, 0f));
        }

        /// <summary>
        /// 浴室的桧木浴桶：四面厚板 + 桶底 + 垫木、口沿一圈、外面两道铁箍；里面一汪黑水，
        /// 左边一半盖着三块风呂蓋（露出来的那一半看得见水面：不是能藏人的空桶）。
        /// </summary>
        static void HinokiTub(MeshBatch mb, int seed)
        {
            const float W = 1.68f, Dp = .98f, t = .045f, y0 = .02f, y1 = .64f;
            var wood = P.WoodLightA;
            foreach (float s in new[] { -1f, 1f })
            {
                float zIn = s * (Dp / 2f - t), zOut = s * Dp / 2f;
                BevelMM(mb, wood, -W / 2f, W / 2f, y0, y1, Mathf.Min(zIn, zOut), Mathf.Max(zIn, zOut), .004f);
                float xIn = s * (W / 2f - t), xOut = s * W / 2f;
                BevelMM(mb, wood, Mathf.Min(xIn, xOut), Mathf.Max(xIn, xOut), y0, y1, -Dp / 2f + t, Dp / 2f - t, .004f);
            }
            BevelMM(mb, wood, -W / 2f + t, W / 2f - t, y0, .07f, -Dp / 2f + t, Dp / 2f - t, .002f);
            foreach (float sx in new[] { -.6f, .6f })
                BevelMM(mb, P.WoodMidA, sx - .04f, sx + .04f, 0f, y0, -Dp / 2f + .02f, Dp / 2f - .02f, .003f);
            foreach (float s in new[] { -1f, 1f })
            {
                BevelMM(mb, wood, -W / 2f, W / 2f, y1, y1 + .03f, s * .455f - .035f, s * .455f + .035f, .006f);
                BevelMM(mb, wood, s * .805f - .035f, s * .805f + .035f, y1, y1 + .03f, -.42f, .42f, .006f);
            }
            foreach (float by in new[] { .13f, .5f })
                foreach (float s in new[] { -1f, 1f })
                {
                    float za = s * Dp / 2f, zb = s * (Dp / 2f + .004f);
                    BevelMM(mb, D.Iron, -W / 2f - .004f, W / 2f + .004f, by, by + .035f, Mathf.Min(za, zb), Mathf.Max(za, zb), .001f);
                    float xa = s * W / 2f, xb = s * (W / 2f + .004f);
                    BevelMM(mb, D.Iron, Mathf.Min(xa, xb), Mathf.Max(xa, xb), by, by + .035f, -Dp / 2f, Dp / 2f, .001f);
                }
            mb.Box(D.Water, new Vector3(0f, .52f, 0f), new Vector3(W - 2f * t, .004f, Dp - 2f * t));
            var rng = new System.Random(seed);
            for (int i = 0; i < 3; i++)
            {
                float yaw = (float)rng.NextDouble() * 4f - 2f;
                BevelBox(mb, P.WoodLightB, new Vector3(-.66f + i * .31f, y1 + .03f + .0125f, 0f), new Vector3(.3f, .025f, 1.0f), .005f, Quaternion.Euler(0f, yaw, 0f));
            }
        }

        /// <summary>浴室的木凳：倒角凳面、两块板脚。</summary>
        static void WashingStool(MeshBatch mb)
        {
            var wood = P.WoodLightA;
            BevelMM(mb, wood, -.195f, .195f, .23f, .26f, -.15f, .15f, .006f);
            foreach (float sx in new[] { -1f, 1f })
                BevelMM(mb, wood, sx * .165f - .0125f, sx * .165f + .0125f, 0f, .23f, -.14f, .14f, .004f);
        }

        // =====================================================================
        // 坐垫（本身就是一个方块，没有子物体、没有碰撞体）
        // =====================================================================
        /// <summary>
        /// 坐垫原来是 0.50 × 0.09 × 0.48 的方块本身（缩放就是尺寸）：去掉方块的网格，缩放改回等比（搬家放大的倍数），
        /// 下面挂一个软的座布団。居間蓝布、仏間紫色天鹅绒的拝み座布団、二楼和室格子布。
        /// </summary>
        static bool RefitCushion(Transform t, int seed)
        {
            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) return false;
            float s = t.localScale.y / .09f;
            Object.DestroyImmediate(mr);
            Object.DestroyImmediate(mf);
            t.localScale = Vector3.one * s;
            var m = t.name switch
            {
                "Butsuma_PrayerCushion" => D.ClothPurple,
                "Washitsu2F_Zabuton" => D.Check,
                _ => D.ClothIndigo,
            };
            var mb = new MeshBatch();
            SoftZabuton(mb, new Vector3(0f, -.045f + SoftTatamiLift, 0f), 0f, m, Mathf.Abs(seed % 97) * .1f, .5f, .48f);
            mb.Build(t, "Visual");
            return true;
        }
    }
}
