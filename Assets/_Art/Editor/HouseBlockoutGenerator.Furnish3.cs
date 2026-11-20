using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第三遍：按房间补家具。房间放大以后墙面大片空着，这里按每个房间的用途列一张清单，
    /// 沿墙自动找地方摆：避开门口（含收在墙边的门扇）、窗、楼梯扶手墙、已有家具和容器、
    /// 交互站位、出生点、暗门地毯、步行路线；放不下就跳过。
    /// 全部按真实尺寸书写，再统一放大 FurnitureScale；会挡路的放 Blocker_*。
    /// 依然不做箱子、抽屉、带门的柜子、篮子这类看起来能搜的东西（架子一律开放式）。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        sealed class Piece
        {
            public string name;
            public float w, d, h;   // 真实尺寸：沿墙宽、离墙深、高（放大前）
            public float lift;      // 底面离地（世界米数，挂墙件用）
            public float blockH;    // 挡块高度（真实尺寸，0 = 不放）
            public bool wall, corner;
            public System.Action<Kit, System.Random> draw;
        }

        sealed class FillCtx
        {
            public Room room;
            public float fl, ceil, top;
            public Rect inner;
            public readonly List<Bounds> hard = new List<Bounds>();
            /// <summary>hard 里前 hardFixed 个是门、窗、楼梯井、门扇；之后是放大过的暗门地毯 / 交互站位。</summary>
            public int hardFixed;
            /// <summary>房间中部家具组用的禁区：暗门地毯按原样（地毯本身可以走），交互站位四周多留 25 cm。</summary>
            public readonly List<Bounds> keep = new List<Bounds>();
            public readonly List<Bounds> occ = new List<Bounds>();
            /// <summary>和 occ 一一对应：没有碰撞体的矮东西（坐垫、玩具），中部家具组只要不压上去，不用留走道。</summary>
            public readonly List<bool> occSoft = new List<bool>();
            public readonly List<Bounds> placed = new List<Bounds>();
            /// <summary>审图机位的视线段（成对：机位、前方点）。</summary>
            public readonly List<Vector2> views = new List<Vector2>();
        }

        const float FillGap = .04f; // 家具背面离墙面

        static float Rf(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static void FurnishByRoom(Transform parent)
        {
            if (Occupied == null || KeepOut == null) return;
            int placed = 0, extra = 0, centered = 0;
            var missed = new List<string>();
            var perRoom = new List<string>();
            FixtureWarnings.Clear();
            RuinReport.Clear();
            RuinShots.Clear();
            SheetLog.Clear();
            foreach (var room in DRooms)
            {
                if (!room.hasFloor || room.group == GFence || IsStairRoom(room) || room.id == "Kaidan" || room.id == "UraKaidan") continue;
                var program = RoomProgram(room);
                if (program.Count == 0 && !HasFixtures(room)) continue;
                var rng = new System.Random(StableHash(room.id) ^ 0x2F6B1);
                var ctx = MakeCtx(room);
                var k = new Kit();
                RoomFixtures(k, ctx); // 便所隔间、風呂隔墙（先占位，后面的家具避开）
                // 先摆房间中央的家具组（占中间），再沿墙摆清单
                int nc = 0;
                foreach (var set in CenterSets(room))
                {
                    if (TryPlaceCenter(k, ctx, set, rng)) nc++;
                    else missed.Add(room.id + "/中央" + set.name);
                }
                centered += nc;
                int n = 0;
                foreach (var p in program)
                {
                    if (TryPlace(k, ctx, p, rng)) n++;
                    else missed.Add(room.id + "/" + p.name);
                }
                // 清单摆完还空着的墙按长度继续补：大房间多摆，小房间少摆；走廊只稀疏地挂几幅画
                bool corridor = IsCorridor(room);
                int floorMore = FillRemaining(k, ctx, rng, Fillers(room, false), FloorProbe, 3.0f, 8);
                int wallMore = FillRemaining(k, ctx, rng, Fillers(room, true), WallProbe, corridor ? 9f : 3.5f, corridor ? 2 : 6);
                placed += n;
                extra += floorMore + wallMore;
                perRoom.Add($"{room.id} 中{nc}+{n}+{floorMore}+{wallMore}");
                Abandon(k, ctx); // 第三轮：碎纸、墙皮、落叶、蜘蛛网、物置塌顶（最后放，只占剩下的地）
                k.mb.Build(parent, "Furn_Fill_" + room.id);
            }
            Debug.Log($"[Hako Art] 按房间补家具：中央家具组 {centered} 组 + 清单 {placed} 件 + 按空墙补 {extra} 件（{string.Join("，", perRoom)}）" +
                      (missed.Count > 0 ? $"；清单里放不下 {missed.Count} 件：{string.Join(" ", missed)}" : ""));
            Debug.Log($"[Hako Art] 废弃感（碎屑堆数 + 蜘蛛网张数）：{string.Join("，", RuinReport)}");
            Debug.Log($"[Hako Art] 防尘布 {SheetLog.Count} 块：{string.Join("，", SheetLog)}");
            Debug.Log($"[Hako Art] 便所、風呂的固定设备：{(FixtureWarnings.Count == 0 ? "没有和旧陈设、门口、容器正面冲突" : string.Join("；", FixtureWarnings))}");
        }

        static readonly Piece FloorProbe = new Piece { name = "probe", w = .5f, d = .45f, h = 1.0f };
        static readonly Piece WallProbe = new Piece { name = "probe", w = .6f, d = .05f, h = .6f, lift = 1.45f, wall = true };

        /// <summary>沿墙还能放下探针件的总长度（米，按中心点可放的范围算）。</summary>
        static float FreeWallLength(FillCtx c, Piece probe)
        {
            float hw = FurnitureScale * probe.w / 2, len = 0f;
            foreach (var e in EdgesOf(c.room))
            {
                if (!HasWall(c.room, e)) continue;
                InnerSpan(c.room, e, out float lo, out float hi);
                for (float s = lo + hw + .03f; s <= hi - hw - .03f + 1e-4f; s += .1f)
                    if (Fits(c, probe, e, s)) len += .1f;
            }
            return len;
        }

        /// <summary>按剩余空墙长度（每 perPiece 米一件，最多 cap 件）轮流从 fillers 里补。</summary>
        static int FillRemaining(Kit k, FillCtx c, System.Random rng, List<Piece> fillers, Piece probe, float perPiece, int cap)
        {
            if (fillers.Count == 0) return 0;
            int target = Mathf.Min(cap, Mathf.FloorToInt(FreeWallLength(c, probe) / perPiece));
            int added = 0, misses = 0, i = rng.Next(fillers.Count);
            while (added < target && misses < fillers.Count)
            {
                if (TryPlace(k, c, fillers[i++ % fillers.Count], rng)) { added++; misses = 0; }
                else misses++;
            }
            return added;
        }

        /// <summary>按空墙继续补的件：只放可以重复出现的东西（盔甲、雛人形、电视这类孤品不重复）。</summary>
        static List<Piece> Fillers(Room room, bool wall)
        {
            string id = room.id;
            if (wall)
            {
                if (id == "Daidokoro") return Pieces(PUtensils(), PPainting(2));
                if (id == "Kura") return Pieces(PToolRack());
                if (id == "Kodomo") return Pieces(PDrawings(), PPainting(0));
                if (id == "Benjo" || id == "Furo") return Pieces(); // 厕所、浴室不挂画
                if (IsTatami(room)) return Pieces(PScroll(room, 1), PPainting(1), PScroll(room, 0), PPainting(2), PScroll(room, 3));
                if (IsCorridor(room)) return Pieces(PPainting(0), PPainting(1), PClock(), PPainting(3), PPainting(2));
                return Pieces(PPainting(1), PPainting(0), PPainting(2));
            }
            switch (id)
            {
                case "Genkan": return Pieces(PBench(), PPlant(1), PFlowerStand(), PPlant(2), PConsole());
                case "Daidokoro": return Pieces(PShelf(1.2f, 1.6f, 4, 0), PPlant(3), PShelf(1.4f, .9f, 2, 0));
                case "Nando":
                case "Monooki": return Pieces(PShelf(1.2f, 1.8f, 4, 2), PFutonStack(), PStandMirror());
                case "Benjo":
                case "Furo": return Pieces();
                case "Kura": return Pieces(PShelf(1.4f, 2.0f, 5, 4), PRiceBales());
                case "Kodomo": return Pieces(PShelf(1.0f, 1.4f, 3, 3), PPlant(3));
            }
            if (IsCorridor(room)) return Pieces(); // 走廊保持通畅，不再沿墙补家具
            if (IsTatami(room)) return Pieces(PAndon(), PPlant(0), PZabutonStack(), PPlant(2), PHibachi(), PPlant(3));
            return Pieces(PPlant(3));
        }

        static List<Piece> Pieces(params Piece[] ps) => new List<Piece>(ps);

        // =====================================================================
        // 每个房间的清单（按优先级：大件在前）
        // =====================================================================
        static List<Piece> RoomProgram(Room room)
        {
            switch (room.id)
            {
                case "Genkan":
                    return Pieces(PTsuitate(), PBench(), PConsole(), PCoatRack(), PFlowerStand(), PBoots(), PPlant(1), PPlant(2),
                        PPainting(1), PPainting(0), PClock(), PLanternHook());
                case "Daidokoro":
                    return Pieces(PWorkTable(), PShelf(1.2f, 1.6f, 4, 0), PShelf(1.0f, 1.6f, 4, 0), PPump(), PShelf(1.4f, .9f, 2, 0),
                        POilTins(), PStool(true), PMouseTrap(), PMouseTrap(), PUtensils(), PUtensils(), PPlant(3), PPainting(2));
                case "Tsugi":
                    return Pieces(PShelf(1.0f, .9f, 2, 2), PAndon(), PZabutonStack(), PPlant(3), PScroll(room, 1), PPainting(0));
                case "Butsuma":
                    return Pieces(PByobu(room, false), PAndon(), PZabutonStack(), PPlant(2), PScroll(room, 0));
                case "Zashiki":
                    return Pieces(PArmor(), PByobu(room, true), PKoto(), PAndon(), PAndon(), PZabutonStack(), PPlant(1), PHibachi(), PPainting(2));
                case "Ima":
                    return Pieces(PTV(), PShelf(1.0f, .9f, 2, 1), PAndon(), PZabutonStack(), PPlant(0), PHibachi(), PPainting(1), PScroll(room, 2));
                case "Nando":
                    return Pieces(PFutonStack(), PFutonStack(), PShelf(1.2f, 1.8f, 4, 2), PDressForm(), PSewing(), PSpinningWheel(), PStandMirror(),
                        POilTins(), PMouseTrap());
                case "Oku":
                    return Pieces(PHina(), PByobu(room, false), PZabutonStack(), PPlant(2), PAndon(), PScroll(room, 3));
                // 走廊、縁側：日式住宅的走廊基本是空的，只留一两件
                case "Engawa":
                    return Pieces(PVeranda(), PPlant(1), PPlant(3), PWateringCan());
                case "EngawaE":
                    return Pieces(PBench(), PPlant(0), PBoots());
                case "Rouka":
                    return Pieces(PConsole(), PPlant(3), PClock(), PPainting(0));
                // 便所、風呂：隔间、洗手处、洗脸台、洗衣处都在 WetRooms 里按位置直接摆
                case "Benjo":
                case "Furo":
                    return Pieces();
                case "Kura":
                    return Pieces(PShelf(1.4f, 2.0f, 5, 4), PShelf(1.4f, 2.0f, 5, 4), PArmor(), PHina(), PLadder(), PRiceBales(), PRiceBales(),
                        PSpinningWheel(), PDressForm(), PToolRack(), POilTins(), PSpade(), PStool(true), PMouseTrap(), PLanternHook());
                case "Shinshitsu":
                    return Pieces(PByobu(room, false), PFutonStack(), PStandMirror(), PAndon(), PZabutonStack(), PPlant(0), PScroll(room, 3), PPainting(0));
                case "Washitsu2F":
                    return Pieces(PByobu(room, true), PShelf(1.0f, .9f, 2, 1), PAndon(), PZabutonStack(), PPlant(2), PHibachi(), PScroll(room, 2));
                case "Rouka2F":
                case "Rouka2FN":
                    return Pieces(PConsole(), PPlant(1), PPainting(0));
                case "Monooki":
                    return Pieces(PFutonStack(), PShelf(1.2f, 1.8f, 4, 2), PLadder(), PDressForm(), PSewing(), PStandMirror(), PRockingHorse(),
                        POilTins(), PStool(true), PMouseTrap());
                case "Kodomo":
                    return Pieces(PFallenShelf(), PShelf(1.2f, .9f, 2, 3), PShelf(1.0f, 1.4f, 3, 3), PRockingHorse(), PDrawings(), PDrawings(), PPlant(3), PPainting(0));
                default:
                    return new List<Piece>();
            }

        }

        // ---------------------------------------------------------------- 清单里的件
        static Piece PConsole() => new Piece { name = "Console", w = 1.2f, d = .4f, h = 1.7f, blockH = .8f, draw = DrawConsole };
        static Piece PPlant(int kind) => kind switch
        {
            0 => new Piece { name = "RubberPlant", w = .6f, d = .6f, h = 1.6f, blockH = 1.2f, corner = true, draw = (k, r) => DrawPlant(k, r, 0) },
            1 => new Piece { name = "Palm", w = 1.1f, d = .85f, h = 1.25f, blockH = 1.0f, corner = true, draw = (k, r) => DrawPlant(k, r, 1) },
            2 => new Piece { name = "Bamboo", w = .6f, d = .6f, h = 1.95f, blockH = 1.4f, corner = true, draw = (k, r) => DrawPlant(k, r, 2) },
            _ => new Piece { name = "Haran", w = .7f, d = .62f, h = 1.2f, blockH = .9f, corner = true, draw = (k, r) => DrawPlant(k, r, 3) },
        };
        static Piece PFlowerStand() => new Piece { name = "FlowerStand", w = .7f, d = .58f, h = 1.55f, blockH = 1.0f, corner = true, draw = DrawFlowerStand };
        static Piece PAndon() => new Piece { name = "Andon", w = .36f, d = .36f, h = .9f, blockH = .82f, corner = true, draw = (k, r) => DrawAndon(k) };
        static Piece PByobu(Room room, bool gold) => new Piece { name = "Byobu", w = 2.25f, d = .45f, h = 1.6f, blockH = 1.6f, draw = (k, r) => DrawByobu(k, r, gold, room) };
        static Piece PZabutonStack() => new Piece { name = "Zabutons", w = .6f, d = .62f, h = .48f, blockH = .42f, corner = true, draw = DrawZabutonStack };
        static Piece PFutonStack() => new Piece { name = "Futons", w = 1.1f, d = .78f, h = .78f, blockH = .7f, draw = DrawFutonStack };
        static Piece PShelf(float w, float h, int n, int content) =>
            new Piece { name = "Shelf", w = w, d = .38f, h = h + (h < 1.1f ? .45f : 0f), blockH = h, draw = (k, r) => DrawShelf(k, r, w, h, n, content) };
        static Piece PBench() => new Piece { name = "Bench", w = 1.6f, d = .42f, h = .55f, blockH = .47f, draw = DrawBench };
        static Piece PVeranda() => new Piece { name = "VerandaSet", w = 2.0f, d = .9f, h = .95f, blockH = .9f, draw = DrawVeranda };
        static Piece PTV() => new Piece { name = "ShowaTV", w = .8f, d = .58f, h = 1.35f, blockH = .86f, draw = (k, r) => DrawTV(k) };
        static Piece PWorkTable() => new Piece { name = "WorkTable", w = 1.7f, d = .75f, h = 1.08f, blockH = .85f, draw = DrawWorkTable };
        static Piece PPump() => new Piece { name = "HandPump", w = 1.25f, d = .7f, h = 1.2f, blockH = 1.0f, draw = (k, r) => DrawPump(k) };
        static Piece PArmor() => new Piece { name = "Armor", w = .85f, d = .75f, h = 1.75f, blockH = 1.7f, draw = (k, r) => DrawArmor(k) };
        static Piece PDressForm() => new Piece { name = "DressForm", w = .5f, d = .5f, h = 1.62f, blockH = 1.55f, corner = true, draw = (k, r) => DrawDressForm(k) };
        static Piece PSewing() => new Piece { name = "SewingMachine", w = .95f, d = .5f, h = 1.1f, blockH = 1.0f, draw = (k, r) => DrawSewing(k) };
        static Piece PHina() => new Piece { name = "HinaDolls", w = 1.3f, d = 1.0f, h = 1.45f, blockH = 1.1f, draw = (k, r) => DrawHina(k) };
        static Piece PStandMirror() => new Piece { name = "Mirror", w = .6f, d = .5f, h = 1.65f, blockH = 1.6f, corner = true, draw = (k, r) => DrawStandMirror(k) };
        static Piece PCoatRack() => new Piece { name = "CoatRack", w = .6f, d = .6f, h = 1.85f, blockH = 1.8f, corner = true, draw = (k, r) => DrawCoatRack(k) };
        static Piece PTsuitate() => new Piece { name = "Tsuitate", w = 1.3f, d = .42f, h = 1.3f, blockH = 1.3f, draw = DrawTsuitate };
        static Piece PHibachi() => new Piece { name = "Hibachi", w = .55f, d = .55f, h = .55f, blockH = .5f, corner = true, draw = (k, r) => DrawHibachi(k) };
        static Piece PWasher() => new Piece { name = "Washer", w = .8f, d = .62f, h = 1.15f, blockH = 1.1f, corner = true, draw = (k, r) => DrawWasher(k) };
        static Piece PWashTubs() => new Piece { name = "WashTubs", w = .85f, d = .75f, h = .65f, blockH = .5f, corner = true, draw = DrawWashTubs };
        static Piece PRockingHorse() => new Piece { name = "RockingHorse", w = 1.1f, d = .45f, h = .95f, blockH = .8f, draw = (k, r) => DrawRockingHorse(k) };
        static Piece PSpinningWheel() => new Piece { name = "SpinningWheel", w = 1.0f, d = .42f, h = .85f, blockH = .8f, draw = (k, r) => DrawSpinningWheel(k) };
        static Piece PKoto() => new Piece { name = "Koto", w = 1.9f, d = .4f, h = .3f, blockH = .28f, draw = (k, r) => DrawKoto(k) };
        static Piece PRiceBales() => new Piece { name = "RiceBales", w = 1.6f, d = .82f, h = .76f, blockH = .74f, draw = DrawRiceBales };

        // 挂墙件：没有挡块
        static Piece PPainting(int kind) => new Piece
        {
            name = "Painting", w = .9f, d = .05f, h = .65f, lift = 1.5f, wall = true,
            draw = (k, r) => { k.Push(new Vector3(0, .325f, 0)); Painting(k, r, .9f, .65f, kind); k.Pop(); },
        };
        static Piece PClock() => new Piece { name = "Clock", w = .34f, d = .14f, h = .62f, lift = 1.75f, wall = true, draw = (k, r) => DrawClock(k) };
        static Piece PUtensils() => new Piece { name = "UtensilRail", w = 1.2f, d = .14f, h = .6f, lift = 1.15f, wall = true, draw = DrawUtensils };
        static Piece PToolRack() => new Piece { name = "ToolRack", w = 1.4f, d = .16f, h = .9f, lift = .9f, wall = true, draw = DrawToolRack };
        static Piece PDrawings() => new Piece { name = "Drawings", w = .95f, d = .03f, h = .55f, lift = .95f, wall = true, draw = DrawDrawings };

        /// <summary>挂轴：和室挂在长押上（轴顶贴长押下沿），其他房间离地 0.8 m。</summary>
        static Piece PScroll(Room room, int kind)
        {
            const float w = .55f, h = 1.5f;
            float lift = IsTatami(room) ? NageshiY(room) - FloorY(room) - FurnitureScale * h - .03f : .8f;
            return new Piece { name = "Scroll", w = w, d = .03f, h = h, lift = lift, wall = true, draw = (k, r) => HangingScroll(k, r, w, h, kind, room) };
        }

        static float NageshiY(Room room)
        {
            float kt = room.group == G0 ? .55f + DoorTop : Mathf.Min(Levels[G1].wallTop, Levels[G1].floorTop + DoorTop);
            return kt + .075f;
        }

        // =====================================================================
        // 沿墙找位置
        // =====================================================================
        static FillCtx MakeCtx(Room room)
        {
            var c = new FillCtx { room = room, fl = FloorY(room), ceil = CeilY(room) };
            var r = room.r;
            c.inner = Rect.MinMaxRect(r.xMin + T + .01f, r.yMin + T + .01f, r.xMax - T - .01f, r.yMax - T - .01f);
            bool beams = IsDoma(room) || room.group == GKura;
            c.top = IsTatami(room) ? NageshiY(room) - .02f : c.ceil - (beams ? .45f : .12f);
            var area = new Bounds(new Vector3(r.center.x, (c.fl + c.ceil) / 2, r.center.y),
                new Vector3(r.width + .6f, c.ceil - c.fl + .4f, r.height + .6f));
            void Hard(Bounds b) { if (b.Intersects(area)) c.hard.Add(b); }

            foreach (var o in DOps)
            {
                if (o.group != room.group) continue;
                float lo = o.center - o.width / 2, hi = o.center + o.width / 2;
                if (o.window) // 窗：窗台以下可以放矮家具
                    Hard(LineBox(o.alongX, o.line, lo - .12f, hi + .12f, c.fl + o.sill - .05f, c.fl + o.top + .05f, .75f));
                else if (o.sill > 0) // 楼梯井的扶手墙：上面是空的
                    Hard(LineBox(o.alongX, o.line, lo - .12f, hi + .12f, c.fl - .3f, c.ceil + .1f, .8f));
                else // 门：两侧各 1.25 m 留空，门楣上的欄間也不挂东西
                    Hard(LineBox(o.alongX, o.line, lo - .35f, hi + .35f, c.fl - .3f, c.ceil + .1f, 1.25f));
            }
            // 推拉门开着时门扇停靠的墙面（Doors.cs 选好的），前面不能放东西
            foreach (var z in ParkZones)
            {
                var x = z;
                x.Expand(new Vector3(.04f, 0f, .04f));
                Hard(x);
            }
            // 容器正面前 1 m 不放任何陈设（第三轮）
            foreach (var f in ContainerFronts) Hard(f);
            c.hardFixed = c.hard.Count;
            foreach (var b in KeepOut)
            {
                var x = b;
                x.Expand(.5f);
                Hard(x);
                if (x.Intersects(area)) c.keep.Add(b.size.x > 1f ? b : x);
            }
            // 审图机位（ArtSnapshot 的室内机位）：机位到前方 2.8 m 这一段两侧 1 m 内不放落地家具
            foreach (var (p, look) in ArtSnapshot.InteriorCameraSpots())
            {
                if (p.y <= c.fl || p.y >= c.ceil) continue;
                var a = new Vector2(p.x, p.z);
                var dir = new Vector2(look.x, look.z) - a;
                c.views.Add(a);
                c.views.Add(dir.sqrMagnitude > 1e-4f ? a + dir.normalized * 2.8f : a);
            }
            for (int i = 0; i < Occupied.Count; i++)
                if (Occupied[i].Intersects(area))
                {
                    c.occ.Add(Occupied[i]);
                    c.occSoft.Add(OccupiedSoft != null && i < OccupiedSoft.Count && OccupiedSoft[i]);
                }
            return c;
        }

        static Bounds BoxOnWall(Edge e, float s0, float s1, float y0, float y1, float d0, float d1)
        {
            var b = new Bounds(OnWall(e, s0, y0, d0), Vector3.zero);
            b.Encapsulate(OnWall(e, s1, y1, d1));
            return b;
        }

        static float WallYaw(Edge e) => e.side switch { 0 => 0f, 1 => 180f, 2 => 90f, _ => -90f };

        static Bounds PieceBox(FillCtx c, Piece p, Edge e, float s)
        {
            float S = FurnitureScale, hw = S * p.w / 2, d0 = T + FillGap, y0 = c.fl + p.lift;
            return BoxOnWall(e, s - hw, s + hw, y0, y0 + S * p.h, d0, d0 + S * p.d);
        }

        /// <summary>挂墙件往外多算 0.35 m：前面被高家具挡住的墙不挂。</summary>
        static Bounds CheckBox(FillCtx c, Piece p, Edge e, float s)
        {
            if (!p.wall) return PieceBox(c, p, e, s);
            float S = FurnitureScale, hw = S * p.w / 2, d0 = T + FillGap, y0 = c.fl + p.lift;
            return BoxOnWall(e, s - hw, s + hw, y0, y0 + S * p.h, d0, d0 + Mathf.Max(S * p.d, .35f));
        }

        static bool Fits(FillCtx c, Piece p, Edge e, float s)
        {
            var box = PieceBox(c, p, e, s);
            if (box.min.x < c.inner.xMin || box.max.x > c.inner.xMax || box.min.z < c.inner.yMin || box.max.z > c.inner.yMax) return false;
            if (box.max.y > c.top) return false;
            var chk = CheckBox(c, p, e, s);
            foreach (var z in c.hard) if (chk.Intersects(z)) return false;
            var pad = chk;
            pad.Expand(p.wall ? .08f : .24f);
            foreach (var o in c.occ) if (pad.Intersects(o)) return false;
            foreach (var o in c.placed) if (pad.Intersects(o)) return false;
            if (p.wall) return true;
            if (LaneClearance(box) < .5f) return false;
            var rect = Rect.MinMaxRect(box.min.x, box.min.z, box.max.x, box.max.z);
            for (int i = 0; i + 1 < c.views.Count; i += 2)
                if (SegRectDist(c.views[i], c.views[i + 1], rect) < 1f) return false;
            // 前面留出站人的地方
            float S = FurnitureScale, hw = S * p.w / 2, d1 = T + FillGap + S * p.d;
            var front = BoxOnWall(e, s - hw, s + hw, c.fl + .05f, c.fl + 1.5f, d1 + .01f, d1 + (IsCorridor(c.room) ? .75f : .9f));
            foreach (var o in c.occ) if (front.Intersects(o)) return false;
            foreach (var o in c.placed) if (front.Intersects(o)) return false;
            return true;
        }

        /// <summary>
        /// 扫四面墙，每 5 cm 试一次，连成可放的区段；角落件优先贴角，其他的放在最长的空墙段中间（带一点随机偏移）。
        /// </summary>
        static bool TryPlace(Kit k, FillCtx c, Piece p, System.Random rng)
        {
            float hw = FurnitureScale * p.w / 2;
            var room = c.room;
            bool found = false;
            float bestScore = float.MinValue, bestS = 0f;
            Edge bestE = default;
            void Consider(Edge e, float s, float score)
            {
                if (score <= bestScore || !Fits(c, p, e, s)) return;
                bestScore = score; bestE = e; bestS = s; found = true;
            }
            var runs = new List<Vector2>();
            foreach (var e in EdgesOf(room))
            {
                if (!HasWall(room, e)) continue;
                InnerSpan(room, e, out float lo, out float hi);
                float a = lo + hw + .03f, b = hi - hw - .03f;
                if (b < a) continue;
                runs.Clear();
                float start = float.NaN, last = 0f;
                for (float s = a; s <= b + 1e-4f; s += .05f)
                {
                    if (Fits(c, p, e, s)) { if (float.IsNaN(start)) start = s; last = s; }
                    else if (!float.IsNaN(start)) { runs.Add(new Vector2(start, last)); start = float.NaN; }
                }
                if (!float.IsNaN(start)) runs.Add(new Vector2(start, last));
                foreach (var run in runs)
                {
                    float len = run.y - run.x, j = (float)rng.NextDouble();
                    if (p.corner)
                    {
                        if (run.x <= a + .01f) Consider(e, run.x, 20f + j);
                        if (run.y >= b - .01f) Consider(e, run.y, 20f + j);
                    }
                    float score = Mathf.Min(len, 8f) + j;
                    Consider(e, Mathf.Clamp((run.x + run.y) / 2 + (j - .5f) * len * .3f, run.x, run.y), score);
                    Consider(e, (run.x + run.y) / 2, score - .01f);
                }
            }
            if (!found) return false;

            float S = FurnitureScale, yaw = WallYaw(bestE);
            var sheet = p.wall ? null : SheetFor(c, p, bestE, bestS);
            k.PushS(OnWall(bestE, bestS, c.fl + p.lift, T + FillGap), new Vector3(0, yaw, 0), Vector3.one * S);
            CoverNext = sheet != null;
            p.draw(k, rng);
            CoverNext = false;
            if (sheet != null) DustSheet(k, new System.Random(StableHash(room.id + "/" + p.name) ^ 0x5EE7), sheet);
            k.Pop();
            if (sheet != null)
            {
                SheetLog.Add(room.id + "/" + p.name);
                ShotAt($"sheet_{room.id}_{p.name}", c, OnWall(bestE, bestS, c.fl + Mathf.Min(.9f, S * p.h * .5f), T + FillGap + S * p.d / 2), 2.1f, .75f);
            }
            if (!p.wall && p.blockH > 0f)
                Blocker(room.id + "_" + p.name, OnWall(bestE, bestS, c.fl, T + FillGap + S * p.d / 2), yaw,
                    new Vector3(S * p.w, S * p.blockH, S * p.d));
            c.placed.Add(CheckBox(c, p, bestE, bestS));
            return true;
        }

        // =====================================================================
        // 画、挂轴、钟、涂鸦（局部：正面朝 +z，背面贴 z=0）
        // =====================================================================
        /// <summary>画框，原点在框中心。kind 0 遗影 1 水墨山水 2 书法 3 暗色底的白衣女人。</summary>
        static void Painting(Kit k, System.Random rng, float w, float h, int kind)
        {
            const float fd = .035f, bw = .04f;
            if (kind == 0)
            {
                // 遗影：黑框，上面两角斜挂黑缎带
                float pw = w * .55f, ph = h * .95f;
                k.Push(new Vector3(0, 0, .016f));
                PortraitFrame(k, pw, ph);
                k.Br(D.Black, -pw / 2 + .06f, ph / 2 - .06f, .018f, .17f, .025f, .004f, 0, 0, 45f);
                k.Br(D.Black, pw / 2 - .06f, ph / 2 - .06f, .018f, .17f, .025f, .004f, 0, 0, -45f);
                k.Pop();
                return;
            }
            var fm = kind == 3 ? D.Gold : D.Post;
            k.B(fm, 0, h / 2 - bw / 2, fd / 2, w, bw, fd);
            k.B(fm, 0, -h / 2 + bw / 2, fd / 2, w, bw, fd);
            k.B(fm, -w / 2 + bw / 2, 0, fd / 2, bw, h - 2 * bw, fd);
            k.B(fm, w / 2 - bw / 2, 0, fd / 2, bw, h - 2 * bw, fd);
            float iw = w - 2 * bw, ih = h - 2 * bw, z = .02f;
            k.B(kind == 3 ? D.Lacquer : D.Paper, 0, 0, z - .005f, iw, ih, .01f);
            if (kind == 1) InkLandscape(k, rng, iw, ih, z);
            else if (kind == 2) Calligraphy(k, rng, iw, ih, z, 3);
            else LadyInWhite(k, iw, ih, z);
        }

        static void InkLandscape(Kit k, System.Random rng, float iw, float ih, float z)
        {
            for (int i = 0; i < 3; i++)
            {
                float px = (i - 1) * iw * .22f + Rf(rng, -.02f, .02f);
                float py = ih * (i == 1 ? .28f : i == 0 ? .12f : .05f);
                float a = Rf(rng, 28f, 40f) * Mathf.Deg2Rad;
                float len = Mathf.Min(iw * .2f / Mathf.Cos(a), (py + ih * .3f) / Mathf.Sin(a));
                float t = i == 1 ? .01f : .006f;
                k.Br(D.Ink, px - len / 2 * Mathf.Cos(a), py - len / 2 * Mathf.Sin(a), z, len, t, .002f, 0, 0, a * Mathf.Rad2Deg);
                k.Br(D.Ink, px + len / 2 * Mathf.Cos(a), py - len / 2 * Mathf.Sin(a), z, len, t, .002f, 0, 0, -a * Mathf.Rad2Deg);
            }
            float tx = iw * .3f, ty = -ih * .32f;
            k.Br(D.Ink, tx, ty + ih * .12f, z, .007f, ih * .26f, .002f, 0, 0, 8f);
            for (int i = 0; i < 3; i++)
                k.S(D.Ink, tx + Rf(rng, -.04f, .04f), ty + ih * (.16f + .07f * i), z, .045f, .012f, .0015f, 8);
            k.Cr(D.CeramicWhite, new Vector3(-iw * .32f, ih * .3f, z - .001f), .03f, .03f, .002f, AlongZ, 16);
        }

        static void Calligraphy(Kit k, System.Random rng, float iw, float ih, float z, int cols)
        {
            float cw = iw / (cols + 1);
            for (int col = 0; col < cols; col++)
            {
                float cx = iw / 2 - cw * (col + 1);
                int chars = 3 + rng.Next(2);
                float ch = ih * .8f / chars;
                for (int j = 0; j < chars; j++)
                {
                    float cy = ih * .4f - ch * (j + .5f);
                    int strokes = 3 + rng.Next(3);
                    for (int s = 0; s < strokes; s++)
                        k.Br(D.Ink, cx + Rf(rng, -.15f, .15f) * cw, cy + Rf(rng, -.3f, .3f) * ch, z,
                            Rf(rng, .3f, .7f) * cw, Mathf.Max(.005f, cw * .09f), .002f, 0, 0, rng.Next(4) * 45f + Rf(rng, -10f, 10f));
                }
            }
            k.B(D.ClothRed, -iw / 2 + cw * .5f, -ih * .36f, z, cw * .25f, cw * .25f, .002f);
        }

        static void LadyInWhite(Kit k, float iw, float ih, float z)
        {
            k.S(D.ClothWhite, 0, -ih * .14f, z, iw * .17f, ih * .33f, .0015f, 12);
            k.S(D.Black, 0, ih * .2f, z + .0003f, iw * .09f, ih * .2f, .0015f, 10);
            k.S(D.CeramicWhite, 0, ih * .25f, z + .0006f, iw * .05f, ih * .075f, .0015f, 10);
        }

        static void Enso(Kit k, float pw, float ph, float z)
        {
            float r = Mathf.Min(pw, ph) * .32f;
            for (int i = 0; i < 20; i++)
            {
                float a = (i + 1.5f) / 22f * Mathf.PI * 2;
                float t = .005f + .009f * Mathf.Sin(i / 19f * Mathf.PI);
                k.Br(D.Ink, Mathf.Cos(a) * r, Mathf.Sin(a) * r, z, r * .3f, t, .002f, 0, 0, a * Mathf.Rad2Deg + 90f);
            }
        }

        /// <summary>幽霊画：白衣、长发、没有脚，下摆淡出。</summary>
        static void Yurei(Kit k, float pw, float ph, float z)
        {
            k.S(D.ClothWhite, 0, -ph * .06f, z, pw * .2f, ph * .34f, .0015f, 12);
            for (int i = 0; i < 7; i++)
                k.Br(D.Black, pw * (-.09f + i * .03f), ph * .14f, z + .0004f, .004f, ph * .32f, .0015f, 0, 0, (i - 3) * 3f);
            k.S(D.CeramicWhite, pw * .02f, ph * .27f, z + .0008f, pw * .065f, ph * .05f, .0015f, 10);
            k.S(D.Black, pw * .02f, ph * .32f, z + .0006f, pw * .085f, ph * .045f, .0015f, 10);
            k.Br(D.CeramicWhite, pw * .13f, ph * .06f, z + .0008f, .006f, ph * .09f, .0015f, 0, 0, -15f);
            k.Br(D.CeramicWhite, -pw * .1f, ph * .05f, z + .0008f, .006f, ph * .09f, .0015f, 0, 0, 12f);
        }

        /// <summary>
        /// 挂轴：局部底边 y=0，顶边 y=h。第三轮：画心用 Met 的 CC0 古画（按画面比例定宽高，不裁不拉，上下各一条金襕“一文字”）；
        /// kind 3（幽霊画）和缺贴图时用手绘：0 书法 1 山水 2 圆相 3 幽霊画。
        /// </summary>
        static void HangingScroll(Kit k, System.Random rng, float w, float h, int kind, Room room = null)
        {
            k.B(D.Black, 0, h - .015f, .004f, .006f, .03f, .004f);
            k.Cr(D.Lacquer, new Vector3(-w / 2 - .01f, h - .04f, .012f), .009f, .009f, w + .02f, AlongX, 8);
            k.Bb(D.ClothIndigo, 0, .03f, .006f, w, h - .07f, .004f);
            float pw = w * .76f, ph = (h - .07f) * .7f, py = .03f + (h - .07f) * .56f;
            string oid = ScrollPainting(room, kind);
            var art = oid != null ? MetMat(oid) : null;
            if (art != null)
            {
                var uv = MetContent(oid);
                float a = MetAspect(art, uv);
                float qw = Mathf.Min(pw * 1.08f, ph * a), qh = qw / a;
                k.B(D.Gold, 0, py + qh / 2 + .028f, .0088f, qw + .02f, .045f, .001f);
                k.B(D.Gold, 0, py - qh / 2 - .017f, .0088f, qw + .02f, .022f, .001f);
                k.B(D.Paper, 0, py, .0083f, qw + .012f, qh + .012f, .001f);
                PictureQuad(k, art, new Vector3(0, py, .0092f), qw, qh, uv);
            }
            else
            {
                k.B(D.Paper, 0, py, .0085f, pw, ph, .001f);
                k.Push(new Vector3(0, py, 0));
                const float z = .0095f;
                switch (kind)
                {
                    case 0: Calligraphy(k, rng, pw, ph, z, 1); break;
                    case 1: InkLandscape(k, rng, pw, ph, z); break;
                    case 2: Enso(k, pw, ph, z); break;
                    default: Yurei(k, pw, ph, z); break;
                }
                k.Pop();
            }
            k.Cr(D.Lacquer, new Vector3(-w / 2 - .035f, .03f, .014f), .014f, .014f, w + .07f, AlongX, 10);
        }

        static void DrawClock(Kit k)
        {
            // 柱时计：上半钟面，下半玻璃门里能看到钟摆
            k.Push(new Vector3(0, .31f, 0));
            k.B(D.Post, 0, .13f, .06f, .3f, .3f, .12f);
            k.B(D.Post, 0, -.15f, .01f, .3f, .26f, .02f);
            foreach (float sx in new[] { -.14f, .14f }) k.B(D.Post, sx, -.15f, .06f, .02f, .26f, .12f);
            k.B(D.Post, 0, -.27f, .06f, .3f, .02f, .12f);
            k.Cr(D.CeramicWhite, new Vector3(0, .13f, .12f), .11f, .11f, .006f, AlongZ, 20);
            k.B(D.Ink, 0, .16f, .1275f, .008f, .07f, .002f);
            k.B(D.Ink, .03f, .13f, .1275f, .06f, .006f, .002f);
            k.B(D.Brass, 0, -.1f, .06f, .004f, .16f, .004f);
            k.Cr(D.Brass, new Vector3(0, -.19f, .057f), .03f, .03f, .006f, AlongZ, 12);
            k.B(D.Glass, 0, -.145f, .118f, .26f, .23f, .004f);
            k.Pop();
        }

        static void DrawDrawings(Kit k, System.Random rng)
        {
            Material[] crayon = { D.ClothRed, D.ClothIndigo, D.Leaf, D.Gold };
            for (int i = 0; i < 3; i++)
            {
                float x = -.3f + i * .3f, y = .28f + Rf(rng, -.03f, .03f);
                k.Push(new Vector3(x, y, .004f), 0, 0, Rf(rng, -6f, 6f));
                k.B(D.Paper, 0, 0, 0, .26f, .36f, .002f);
                k.S(D.PlasticRed, 0, .165f, .003f, .008f, .008f, .004f, 6);
                if (i == 1)
                {
                    // 一家三口，旁边一个涂黑的高个子
                    for (int j = 0; j < 3; j++) StickFigure(k, -.09f + j * .05f, -.12f, .0015f, .1f + j % 2 * .02f, crayon[j]);
                    StickFigure(k, .08f, -.12f, .0015f, .24f, D.Ink);
                    k.S(D.Ink, .08f, .07f, .0015f, .03f, .035f, .0008f, 8);
                }
                else
                {
                    // 房子 + 太阳 + 一个人
                    k.B(crayon[(i + 1) % 4], -.04f, -.08f, .0015f, .12f, .09f, .001f);
                    k.Br(D.ClothRed, -.075f, -.005f, .0015f, .09f, .006f, .001f, 0, 0, 35f);
                    k.Br(D.ClothRed, -.005f, -.005f, .0015f, .09f, .006f, .001f, 0, 0, -35f);
                    k.Cr(D.Gold, new Vector3(.08f, .11f, .0012f), .03f, .03f, .001f, AlongZ, 12);
                    StickFigure(k, .07f, -.13f, .0015f, .11f, crayon[i % 4]);
                }
                k.Pop();
            }
        }

        static void StickFigure(Kit k, float x, float y, float z, float h, Material m)
        {
            const float t = .004f;
            k.Cr(m, new Vector3(x, y + h * .82f, z - .0005f), h * .1f, h * .1f, .001f, AlongZ, 10);
            k.B(m, x, y + h * .5f, z, t, h * .4f, .001f);
            k.Br(m, x - h * .07f, y + h * .15f, z, t, h * .32f, .001f, 0, 0, -22f);
            k.Br(m, x + h * .07f, y + h * .15f, z, t, h * .32f, .001f, 0, 0, 22f);
            k.B(m, x, y + h * .58f, z, h * .42f, t, .001f);
        }

        static void DrawUtensils(Kit k, System.Random rng)
        {
            const float y = .55f, z = .07f;
            k.Cr(D.Iron, new Vector3(-.58f, y, z), .01f, .01f, 1.16f, AlongX, 8);
            foreach (float sx in new[] { -.55f, .55f }) k.B(D.Iron, sx, y, z / 2, .02f, .02f, z);
            float x = -.44f;
            for (int i = 0; i < 5; i++, x += .22f)
            {
                k.B(D.Iron, x, y - .03f, z, .004f, .05f, .004f);
                switch ((i + rng.Next(2)) % 4)
                {
                    case 0: // 长柄木勺
                        k.B(D.TrimLight, x, y - .22f, z, .016f, .34f, .01f);
                        k.S(D.TrimLight, x, y - .42f, z + .015f, .045f, .03f, .045f, 8);
                        break;
                    case 1: // 铁锅
                        k.B(D.Iron, x, y - .15f, z, .03f, .2f, .015f);
                        k.Cr(D.Iron, new Vector3(x, y - .36f, z - .02f), .12f, .12f, .03f, AlongZ, 16);
                        break;
                    case 2: // 竹笊篱
                        k.B(D.Bamboo, x, y - .08f, z, .015f, .1f, .01f);
                        k.Cr(D.Bamboo, new Vector3(x, y - .22f, z - .01f), .1f, .1f, .03f, AlongZ, 14);
                        break;
                    default: // 抹布
                        k.B(D.ClothWhite, x, y - .17f, z, .14f, .28f, .006f);
                        break;
                }
            }
        }

        static void DrawToolRack(Kit k, System.Random rng)
        {
            k.Bb(D.BeamDark, 0, .74f, .015f, 1.4f, .1f, .03f);
            for (int i = 0; i < 5; i++) k.Cr(D.TrimMid, new Vector3(-.56f + i * .28f, .79f, .03f), .012f, .012f, .08f, AlongZ, 6);
            for (int i = 0; i < 2; i++)
            {
                float x = -.56f + i * .28f;
                k.B(D.TrimLight, x, .62f, .07f, .025f, .3f, .02f);
                k.Br(D.Iron, x + .06f, .47f, .07f, .14f, .025f, .004f, 0, 0, -15f);
            }
            k.B(D.TrimMid, 0, .7f, .07f, .03f, .14f, .025f);
            k.Br(D.Iron, 0, .45f, .07f, .1f, .38f, .003f, 0, 0, Rf(rng, -3f, 3f));
            Ring(k, D.Straw, new Vector3(.25f, .55f, .07f), .15f, .025f);
            k.Cr(D.Straw, new Vector3(.52f, .6f, .03f), .17f, .01f, .1f, AlongZ, 18);
        }

        /// <summary>竖直平面（XY）上的一圈，用短方块拼。</summary>
        static void Ring(Kit k, Material m, Vector3 c, float r, float t)
        {
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float a = (i + .5f) / n * Mathf.PI * 2;
                k.Br(m, c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, c.z, 2 * Mathf.PI * r / n + .005f, t, t, 0, 0, a * Mathf.Rad2Deg + 90f);
            }
        }

        // =====================================================================
        // 落地家具（局部：x 沿墙居中，y 从地面起，z 从墙面起往房间里）
        // =====================================================================
        static void DrawConsole(Kit k, System.Random rng)
        {
            const float W = 1.2f, Dp = .4f, H = .8f;
            k.Bb(D.TrimMid, 0, H - .04f, Dp / 2, W, .04f, Dp);
            k.Bb(D.TrimMid, 0, H - .13f, Dp / 2, W - .08f, .09f, Dp - .05f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { .045f, Dp - .045f })
                    k.Bb(D.Post, sx * (W / 2 - .045f), 0, sz, .045f, H - .04f, .045f);
            k.Bb(D.TrimMid, 0, .12f, Dp / 2, W - .09f, .025f, Dp - .07f);
            k.Bb(D.ClothIndigo, -.32f, .145f, .2f, .26f, .05f, .2f);
            k.Bb(D.ClothRed, -.31f, .195f, .2f, .24f, .04f, .19f);
            k.C(D.CeramicBrown, .3f, .145f, .2f, .09f, .07f, .2f, 12);
            // 盖着防尘布：桌面摆件照样“摆”（随机数照常用掉），只是不写网格
            bool mute = k.mb.Mute;
            if (CoverNext) k.mb.Mute = true;
            TableDeco(k, rng, rng.Next(4), H, Dp / 2);
            k.mb.Mute = mute;
            k.Push(new Vector3(0, 1.38f, 0));
            Painting(k, rng, .82f, .58f, rng.Next(4));
            k.Pop();
        }

        /// <summary>桌面摆件：0 生花 1 台灯 2 盆栽松 3 香炉 + 茶罐。</summary>
        static void TableDeco(Kit k, System.Random rng, int kind, float y, float z)
        {
            switch (kind)
            {
                case 0:
                    k.C(D.CeramicBlue, .25f, y, z, .06f, .03f, .24f, 12);
                    k.C(D.CeramicBlue, .25f, y + .24f, z, .03f, .04f, .04f, 10);
                    for (int i = 0; i < 5; i++)
                    {
                        float len = Rf(rng, .22f, .4f);
                        var dir = Quaternion.Euler(Rf(rng, -12f, 12f), 0, -40f + i * 20f + Rf(rng, -6f, 6f)) * Vector3.up;
                        var b0 = new Vector3(.25f, y + .27f, z);
                        k.Cr(D.Bark, b0, .005f, .003f, len, Quaternion.FromToRotation(Vector3.up, dir), 5);
                        var tip = b0 + dir * len;
                        k.S(i % 2 == 0 ? D.ClothRed : D.ClothWhite, tip.x, tip.y, tip.z, .025f, .022f, .025f, 7);
                        var leaf = b0 + dir * (len * .6f);
                        k.S(D.Leaf, leaf.x, leaf.y, leaf.z, .03f, .012f, .02f, 6);
                    }
                    break;
                case 1:
                    k.C(D.Brass, -.3f, y, z, .07f, .06f, .025f, 12);
                    k.C(D.Brass, -.3f, y + .025f, z, .012f, .012f, .3f, 8);
                    k.C(D.PaperLamp, -.3f, y + .26f, z, .14f, .08f, .16f, 14);
                    break;
                case 2:
                    k.Bb(D.CeramicBrown, 0, y, z, .34f, .06f, .2f);
                    k.Cr(D.Bark, new Vector3(0, y + .06f, z), .02f, .012f, .2f, Quaternion.Euler(0, 0, 25f), 6);
                    k.S(D.Pine, -.09f, y + .25f, z, .12f, .05f, .08f, 8);
                    k.S(D.Pine, .06f, y + .21f, z, .09f, .04f, .07f, 8);
                    k.S(D.Pine, -.02f, y + .31f, z, .07f, .04f, .06f, 8);
                    break;
                default:
                    k.C(D.Brass, 0, y, z, .07f, .08f, .08f, 12);
                    k.C(D.Brass, 0, y + .08f, z, .05f, .03f, .04f, 10);
                    k.C(D.CeramicBrown, -.32f, y, z, .055f, .055f, .14f, 12);
                    k.C(D.CeramicWhite, .32f, y, z, .05f, .05f, .12f, 12);
                    break;
            }
        }

        /// <summary>
        /// 第三轮：全是枯死的盆栽（荒废很久）。0 素烧陶盆（模型）+ 枯树枝；1 蓝釉盆 + 一丛枯荨麻；
        /// 2 竹子枯黄、叶子掉了一地；3 蓝釉盆 + 枯草，有时整盆翻倒、土撒出来。周围落着枯叶。
        /// </summary>
        static void DrawPlant(Kit k, System.Random rng, int kind)
        {
            float cz = kind == 1 ? .32f : .3f;
            bool tipped = kind == 3 && rng.Next(3) == 0;
            if (tipped)
            {
                // 翻倒的盆：侧躺，土从盆口撒出来，枯草压在地上
                k.Push(new Vector3(0, .2f, cz), Rf(rng, -40f, 40f), 0f, 82f);
                k.C(D.CeramicBlue, 0, -.2f, 0, .16f, .21f, .4f, 16);
                k.C(D.CeramicBlue, 0, .2f, 0, .22f, .22f, .03f, 16);
                k.Pop();
                k.S(D.Ground, -.3f, .01f, cz, .2f, .03f, .16f, 10);
                k.S(D.Ground, -.45f, .005f, cz + .05f, .1f, .015f, .08f, 8);
                for (int i = 0; i < 4; i++)
                    k.Model("nettle_plant", new Vector3(-.35f - i * .06f, 0, cz + Rf(rng, -.12f, .12f)), Rf(rng, 0f, 360f), 2.6f,
                        i % 2 == 0 ? "medium_a" : "small_a", 0f, Rf(rng, 60f, 85f));
                DeadLeaves(k, rng, 10, .55f, cz);
                return;
            }
            if (kind == 0)
            {
                // 素烧陶盆（Poly Haven planter_pot_clay）× 1.5 ≈ 40 cm 口径
                k.Model("planter_pot_clay", new Vector3(0, 0, cz), Rf(rng, 0f, 360f), 1.5f);
                k.C(D.Ground, 0, .3f, cz, .17f, .17f, .02f, 14);
                k.Model("dead_quiver_branch_01", new Vector3(0, .26f, cz), Rf(rng, 0f, 360f), 2.6f);
                DeadLeaves(k, rng, 9, .45f, cz);
                return;
            }
            var pot = kind == 1 || kind == 3 ? D.CeramicBlue : D.CeramicBrown;
            k.C(pot, 0, 0, cz, .16f, .21f, .4f, 16);
            k.C(pot, 0, .4f, cz, .22f, .22f, .03f, 16);
            k.C(D.Ground, 0, .405f, cz, .195f, .195f, .03f, 14);
            var c0 = new Vector3(0, .43f, cz);
            switch (kind)
            {
                case 1:
                    // 一丛枯荨麻（Poly Haven nettle_plant 的枯黄贴图），放大到半人高，往外耷拉
                    string[] parts = { "tall_a", "tall_b", "medium_a", "medium_b", "tall_a" };
                    for (int i = 0; i < parts.Length; i++)
                    {
                        float a = i * 72f + Rf(rng, -15f, 15f);
                        var off = Quaternion.Euler(0, a, 0) * new Vector3(.07f, 0, 0);
                        k.Model("nettle_plant", c0 + off - new Vector3(0, .02f, 0), a, Rf(rng, 2.8f, 3.4f), parts[i], Rf(rng, -18f, 18f), Rf(rng, -14f, 14f));
                    }
                    DeadLeaves(k, rng, 8, .5f, cz);
                    break;
                case 2:
                    // 竹子枯黄，叶子掉光了只剩几簇
                    for (int i = 0; i < 5; i++)
                    {
                        var p = c0 + new Vector3(Rf(rng, -.09f, .09f), 0, Rf(rng, -.06f, .09f));
                        float h = Rf(rng, 1.1f, 1.45f);
                        var tilt = Quaternion.Euler(Rf(rng, 0f, 9f), 0, Rf(rng, -9f, 9f));
                        k.Cr(D.Straw, p, .016f, .013f, h, tilt, 7);
                        if (rng.Next(3) == 0)
                        {
                            var q = p + tilt * Vector3.up * (h * .85f);
                            k.S(D.Straw, q.x + Rf(rng, -.05f, .05f), q.y, q.z + Rf(rng, -.02f, .05f), .08f, .03f, .06f, 7);
                        }
                    }
                    DeadLeaves(k, rng, 16, .6f, cz);
                    break;
                default:
                    // 枯草：叶子全耷拉下来
                    for (int i = 0; i < 4; i++)
                        k.Model("nettle_plant", c0 + new Vector3(Rf(rng, -.06f, .06f), -.02f, Rf(rng, -.06f, .06f)), Rf(rng, 0f, 360f),
                            Rf(rng, 2.6f, 3.2f), i % 2 == 0 ? "medium_a" : "medium_b", Rf(rng, -10f, 10f));
                    for (int i = 0; i < 9; i++)
                    {
                        float yaw = i * 39.7f + Rf(rng, -6f, 6f), pitch = Rf(rng, 55f, 80f), len = Rf(rng, .35f, .5f);
                        var rot = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(pitch, 0, 0);
                        k.mb.Box(D.Straw, c0 + rot * new Vector3(0, len / 2, 0), new Vector3(.06f, len, .005f), rot);
                    }
                    DeadLeaves(k, rng, 7, .45f, cz);
                    break;
            }
        }

        /// <summary>盆周围的枯叶（扁的小椭球），半径 r 内随机撒 n 片。</summary>
        static void DeadLeaves(Kit k, System.Random rng, int n, float r, float cz)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Rf(rng, 0f, Mathf.PI * 2), d = Rf(rng, .18f, r);
                var p = new Vector3(Mathf.Cos(a) * d, .004f, cz + Mathf.Sin(a) * d * .7f);
                if (p.z < .03f) p.z = .03f + Rf(rng, 0f, .05f);
                k.mb.Sphere(rng.Next(3) == 0 ? D.Bark : D.Straw, p, new Vector3(Rf(rng, .025f, .045f), .003f, Rf(rng, .012f, .022f)), 6,
                    Quaternion.Euler(0, Rf(rng, 0f, 360f), 0));
            }
        }

        static void DrawFlowerStand(Kit k, System.Random rng)
        {
            const float z = .22f, H = .95f;
            foreach (float sx in new[] { -.15f, .15f })
                foreach (float sz in new[] { -.15f, .15f })
                    k.Bb(D.Lacquer, sx, 0, z + sz, .035f, H, .035f);
            k.Bb(D.Lacquer, 0, H - .03f, z, .4f, .03f, .4f);
            k.Bb(D.Lacquer, 0, H - .1f, z, .34f, .07f, .34f);
            k.Bb(D.Lacquer, 0, .3f, z, .34f, .02f, .34f);
            k.C(D.CeramicWhite, 0, H, z, .1f, .13f, .16f, 14);
            k.C(D.Ground, 0, H + .15f, z, .12f, .12f, .01f, 12);
            var c0 = new Vector3(0, H + .16f, z);
            if (rng.Next(2) == 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    var rot = Quaternion.Euler(0, i * 45f, 0) * Quaternion.Euler(30f, 0, 0);
                    k.mb.Box(D.Leaf, c0 + rot * new Vector3(0, .15f, 0), new Vector3(.03f, .3f, .004f), rot);
                }
                // 兰：一枝花茎，白花沿茎上段排开
                var stem = Quaternion.Euler(10, 0, -10);
                k.Cr(D.Leaf, c0, .004f, .003f, .4f, stem, 5);
                var up = stem * Vector3.up;
                for (int i = 0; i < 5; i++)
                {
                    var p = c0 + up * (.24f + i * .035f) + new Vector3(.03f, 0, .02f);
                    k.S(D.ClothWhite, p.x, p.y, p.z, .03f, .025f, .02f, 7);
                }
            }
            else
            {
                // 蕨：叶从盆口拱出去再垂下来（只往前、两侧长，不碰墙）
                float[] ang = { 45f, 8f, -38f }, len = { .12f, .11f, .12f }, wid = { .075f, .065f, .045f };
                for (int i = 0; i < 11; i++)
                {
                    var spin = Quaternion.Euler(0, 190f + i * 16f + Rf(rng, -5f, 5f), 0);
                    var p = c0 + spin * new Vector3(.05f, 0, 0);
                    float lift = Rf(rng, -8f, 8f);
                    for (int j = 0; j < 3; j++)
                    {
                        var rot = spin * Quaternion.Euler(0, 0, ang[j] + lift);
                        var dir = rot * Vector3.right;
                        k.mb.Box(D.Leaf, p + dir * (len[j] / 2), new Vector3(len[j], .004f, wid[j]), rot);
                        p += dir * len[j];
                    }
                }
                for (int i = 0; i < 5; i++)
                {
                    var rot = Quaternion.Euler(0, i * 72f + Rf(rng, -10f, 10f), 0) * Quaternion.Euler(0, 0, 70f);
                    k.mb.Box(D.Leaf, c0 + rot * new Vector3(.08f, 0, 0), new Vector3(.16f, .004f, .05f), rot);
                }
            }
        }

        static void DrawAndon(Kit k)
        {
            k.Push(new Vector3(0, 0, .18f));
            k.Bb(D.Lacquer, 0, 0, 0, .3f, .03f, .3f);
            foreach (float sx in new[] { -.14f, .14f })
                foreach (float sz in new[] { -.14f, .14f })
                    k.Bb(D.Lacquer, sx, 0, sz, .02f, .78f, .02f);
            k.Bb(D.PaperLamp, 0, .18f, 0, .26f, .5f, .26f);
            k.Bb(D.Lacquer, 0, .68f, 0, .27f, .015f, .27f);
            k.Bb(D.Lacquer, 0, .76f, 0, .3f, .02f, .3f);
            k.Bb(D.Lacquer, 0, .78f, 0, .02f, .12f, .02f);
            k.Pop();
        }

        /// <summary>
        /// 屏风，折成锯齿立着。第三轮：画面用 Met 的 CC0 古画——照片是六曲屏风，取其中连续四扇，一扇对一扇（折缝对齐、不拉伸）；
        /// 缺贴图时退回六扇手绘（金屏画松枝，纸屏画远山和红日）。
        /// </summary>
        static void DrawByobu(Kit k, System.Random rng, bool gold, Room room)
        {
            var (oid, first) = ByobuPainting(room, gold);
            var art = MetMat(oid);
            int n = art != null ? 4 : 6;
            float pw = art != null ? .55f : .38f;
            const float ph = 1.55f, z = .22f, ang = 22f;
            float step = pw * Mathf.Cos(ang * Mathf.Deg2Rad);
            var face = gold ? D.GoldLeaf : D.Paper;
            var content = MetContent(oid);
            for (int i = 0; i < n; i++)
            {
                k.Push(new Vector3((i - (n - 1) / 2f) * step, 0, z), i % 2 == 0 ? ang : -ang);
                k.Bb(face, 0, .035f, 0, pw - .03f, ph - .07f, .012f);
                k.Bb(D.Lacquer, 0, 0, 0, pw, .035f, .024f);
                k.Bb(D.Lacquer, 0, ph - .035f, 0, pw, .035f, .024f);
                k.Bb(D.Lacquer, -pw / 2 + .012f, 0, 0, .024f, ph, .024f);
                k.Bb(D.Lacquer, pw / 2 - .012f, 0, 0, .024f, ph, .024f);
                const float fz = .0075f;
                if (art != null)
                {
                    float sw = content.width / 6f;
                    var uv = new Rect(content.xMin + (first + i) * sw, content.yMin, sw, content.height);
                    PictureQuad(k, art, new Vector3(0, .035f + (ph - .07f) / 2, fz), pw - .03f, ph - .07f, uv);
                }
                else if (gold)
                {
                    float y = ph * (.55f + .05f * Mathf.Sin(i * 1.3f));
                    k.Br(D.Bark, 0, y, fz, pw - .05f, .025f, .002f, 0, 0, i % 2 == 0 ? 6f : -6f);
                    if (i > 0 && i < n - 1)
                    {
                        k.S(D.Pine, Rf(rng, -.07f, .07f), y + .08f, fz, .11f, .045f, .0015f, 8);
                        k.S(D.Pine, Rf(rng, -.07f, .07f), y - .06f, fz, .09f, .04f, .0015f, 8);
                    }
                    k.B(D.Gold, 0, ph * .86f, fz, pw - .05f, .05f, .002f);
                    k.B(D.Gold, 0, ph * .2f, fz, pw - .05f, .07f, .002f);
                }
                else
                {
                    float py = ph * Rf(rng, .48f, .62f), a = Rf(rng, 25f, 35f);
                    float len = pw * .5f;
                    k.Br(D.Ink, -len * .4f, py - .05f, fz, len, .007f, .002f, 0, 0, a);
                    k.Br(D.Ink, len * .4f, py - .05f, fz, len, .007f, .002f, 0, 0, -a);
                    k.Br(D.Ink, 0, ph * .3f, fz, pw - .08f, .004f, .002f, 0, 0, Rf(rng, -4f, 4f));
                    if (i == 4) k.Cr(D.ClothRed, new Vector3(0, ph * .8f, fz - .001f), .07f, .07f, .002f, AlongZ, 16);
                }
                k.Pop();
            }
        }

        static void DrawZabutonStack(Kit k, System.Random rng)
        {
            var m = rng.Next(3) switch { 0 => D.ClothPurple, 1 => D.ClothIndigo, _ => D.ClothRed };
            for (int i = 0; i < 6; i++)
            {
                k.Push(new Vector3(Rf(rng, -.02f, .02f), i * .068f, .31f + Rf(rng, -.02f, .02f)), Rf(rng, -4f, 4f));
                k.Bb(m, 0, 0, 0, .52f, .045f, .55f);
                k.Bb(m, 0, .045f, 0, .47f, .02f, .5f);
                k.B(D.Black, 0, .066f, 0, .02f, .004f, .02f);
                k.Pop();
            }
        }

        static void DrawFutonStack(Kit k, System.Random rng)
        {
            Material[] mats = { D.ClothWhite, D.ClothIndigo, D.ClothRed, D.ClothPurple };
            float y = 0;
            int off = rng.Next(4);
            for (int i = 0; i < 4; i++)
            {
                float t = i == 0 ? .18f : .14f;
                var m = mats[(i + off) % mats.Length];
                k.Push(new Vector3(Rf(rng, -.025f, .025f), y, .38f), Rf(rng, -2f, 2f));
                k.Bb(m, 0, 0, 0, 1.0f, t, .66f);
                k.Cr(m, new Vector3(-.5f, t / 2, .33f), t / 2, t / 2, 1.0f, AlongX, 10);
                k.Pop();
                y += t;
            }
            k.Cr(D.ClothWhite, new Vector3(-.4f, y + .06f, .32f), .06f, .06f, .45f, AlongX, 10);
            k.Cr(D.ClothWhite, new Vector3(.02f, y + .06f, .36f), .06f, .06f, .4f, AlongX, 10);
        }

        /// <summary>开放木架。content：0 厨房 1 书 2 布料杂物 3 玩具 4 土蔵杂物 5 毛巾脸盆。</summary>
        static void DrawShelf(Kit k, System.Random rng, float w, float h, int n, int content)
        {
            const float d = .38f;
            k.Push(new Vector3(0, 0, d / 2));
            var lv = ShelfUnit(k, w, h, d, n, content == 4 ? D.BeamDark : D.TrimMid);
            float clear = (h - .03f) / n - .03f;
            for (int i = 0; i < n; i++) ShelfRow(k, rng, lv[i], w - .1f, clear, content, i);
            if (h < 1.1f) TableDeco(k, rng, rng.Next(4), h, 0f);
            k.Pop();
        }

        static void ShelfRow(Kit k, System.Random rng, float y, float wr, float clear, int content, int row)
        {
            if (content == 1 || (content == 2 && row == 0))
            {
                Books(k, -wr / 2, wr / 2 - (rng.NextDouble() < .5 ? .25f : 0f), y, rng);
                return;
            }
            float x = -wr / 2;
            for (int guard = 0; guard < 20 && x < wr / 2 - .08f; guard++)
            {
                float used = ShelfItem(k, rng, x, y, Mathf.Min(clear - .02f, .4f), wr / 2 - x, content);
                if (used <= 0f) break;
                x += used + Rf(rng, .02f, .07f);
            }
        }

        /// <summary>在 x 处（左缘）摆一件，返回占用宽度；放不下返回 0。</summary>
        static float ShelfItem(Kit k, System.Random rng, float x, float y, float clear, float room, int content)
        {
            int pick = rng.Next(4);
            switch (content)
            {
                case 0: // 厨房
                    if (pick == 0)
                    {
                        float r = Rf(rng, .07f, .1f), h = Mathf.Min(clear, Rf(rng, .16f, .28f));
                        if (2 * r > room) return 0f;
                        var m = rng.Next(2) == 0 ? D.CeramicBrown : D.CeramicBlue;
                        k.C(m, x + r, y, 0, r * .8f, r, h * .7f, 12);
                        k.C(m, x + r, y + h * .7f, 0, r, r * .55f, h * .3f, 12);
                        return 2 * r;
                    }
                    if (pick == 1)
                    {
                        if (room < .16f) return 0f;
                        int cnt = 3 + rng.Next(4);
                        for (int i = 0; i < cnt; i++) k.C(D.CeramicWhite, x + .075f, y + i * .04f, 0, .045f, .075f, .04f, 12);
                        return .15f;
                    }
                    if (pick == 2)
                    {
                        const float r = .1f;
                        if (room < .2f || clear < .22f) return 0f;
                        for (int i = 0; i < 5; i++)
                            k.Cr(i % 2 == 0 ? D.CeramicWhite : D.CeramicBlue, new Vector3(x + r, y + r + .004f, -.11f + i * .028f), r, r, .012f, Quaternion.Euler(78f, 0, 0), 16);
                        return 2 * r;
                    }
                    if (room < .2f) return 0f;
                    k.S(D.CeramicBrown, x + .1f, y + .08f, 0, .09f, .08f, .09f, 12);
                    k.C(D.CeramicBrown, x + .1f, y + .15f, 0, .04f, .045f, .025f, 10);
                    k.Cr(D.CeramicBrown, new Vector3(x + .17f, y + .08f, 0), .015f, .01f, .1f, Quaternion.Euler(0, 0, -50f), 6);
                    return .2f;
                case 2: // 布料、纸捆、卷轴
                    if (pick <= 1)
                    {
                        if (room < .32f) return 0f;
                        var m = rng.Next(3) switch { 0 => D.ClothPurple, 1 => D.ClothRed, _ => D.ClothIndigo };
                        int cnt = 2 + rng.Next(3);
                        for (int i = 0; i < cnt && (i + 1) * .06f < clear; i++)
                            k.Bb(i % 2 == 0 ? m : D.ClothWhite, x + .15f, y + i * .06f, Rf(rng, -.01f, .01f), .3f, .055f, .26f);
                        return .3f;
                    }
                    if (pick == 2)
                    {
                        if (room < .26f) return 0f;
                        k.Bb(D.Paper, x + .13f, y, 0, .26f, .12f, .2f);
                        k.Bb(D.TrimLight, x + .13f, y - .001f, 0, .01f, .122f, .202f);
                        return .26f;
                    }
                    if (room < .5f) return 0f;
                    for (int i = 0; i < 3; i++) k.Cr(D.Paper, new Vector3(x, y + .03f, -.08f + i * .07f), .03f, .03f, .48f, AlongX, 8);
                    return .48f;
                case 3: // 玩具
                    switch (pick)
                    {
                        case 0: // 不倒翁
                            if (room < .16f) return 0f;
                            k.S(D.ClothRed, x + .08f, y + .08f, 0, .08f, .08f, .075f, 12);
                            k.S(D.ClothWhite, x + .08f, y + .1f, .06f, .045f, .04f, .02f, 8);
                            k.S(D.Black, x + .06f, y + .105f, .075f, .008f, .008f, .004f, 5);
                            return .16f;
                        case 1: // 一对小芥子
                            if (room < .14f) return 0f;
                            foreach (float sx in new[] { .03f, .1f })
                            {
                                k.C(sx < .05f ? D.ClothRed : D.TrimLight, x + sx, y, 0, .025f, .025f, .13f, 10);
                                k.S(D.TrimLight, x + sx, y + .16f, 0, .035f, .035f, .035f, 10);
                                k.S(D.Black, x + sx, y + .175f, -.005f, .036f, .025f, .034f, 8);
                            }
                            return .14f;
                        case 2: // 陀螺 + 球
                            if (room < .24f) return 0f;
                            k.C(D.TrimMid, x + .05f, y, 0, .005f, .05f, .05f, 12);
                            k.C(D.ClothRed, x + .05f, y + .05f, 0, .05f, .02f, .03f, 12);
                            k.S(D.PlasticRed, x + .17f, y + .07f, 0, .07f, .07f, .07f, 12);
                            return .24f;
                        default: // 纸鹤
                            if (room < .14f) return 0f;
                            k.Br(D.ClothWhite, x + .07f, y + .03f, 0, .12f, .02f, .05f, 0, 20f, 0);
                            k.Br(D.ClothWhite, x + .07f, y + .045f, 0, .03f, .02f, .12f, 0, 20f, 25f);
                            return .14f;
                    }
                case 4: // 土蔵：坛子、折起的提灯、卷轴、旧人形
                    switch (pick)
                    {
                        case 0:
                        {
                            float r = Rf(rng, .1f, .14f), h = Mathf.Min(clear, Rf(rng, .22f, .32f));
                            if (2 * r > room) return 0f;
                            k.C(D.CeramicBrown, x + r, y, 0, r * .75f, r, h * .65f, 14);
                            k.C(D.CeramicBrown, x + r, y + h * .65f, 0, r, r * .5f, h * .25f, 14);
                            k.C(D.Straw, x + r, y + h * .9f, 0, r * .55f, r * .5f, h * .1f, 12);
                            return 2 * r;
                        }
                        case 1:
                            if (room < .2f) return 0f;
                            k.C(D.PaperLamp, x + .1f, y, 0, .09f, .09f, .1f, 14);
                            for (int i = 0; i < 4; i++) k.C(D.Black, x + .1f, y + .02f + i * .022f, 0, .092f, .092f, .004f, 14);
                            k.C(D.Lacquer, x + .1f, y + .1f, 0, .06f, .06f, .015f, 12);
                            return .2f;
                        case 2:
                            if (room < .45f) return 0f;
                            for (int i = 0; i < 3; i++)
                            {
                                k.Cr(i == 1 ? D.ClothIndigo : D.Paper, new Vector3(x, y + .025f + (i == 2 ? .045f : 0f), -.05f + (i == 2 ? .03f : i * .06f)), .025f, .025f, .42f, AlongX, 8);
                                k.Cr(D.Lacquer, new Vector3(x - .015f, y + .025f + (i == 2 ? .045f : 0f), -.05f + (i == 2 ? .03f : i * .06f)), .012f, .012f, .45f, AlongX, 6);
                            }
                            return .45f;
                        default:
                            if (room < .16f || clear < .3f) return 0f;
                            k.Bb(D.Lacquer, x + .08f, y, 0, .16f, .03f, .14f);
                            k.C(D.ClothRed, x + .08f, y + .03f, 0, .055f, .035f, .17f, 10);
                            k.S(D.CeramicWhite, x + .08f, y + .23f, 0, .035f, .042f, .035f, 10);
                            k.S(D.Black, x + .08f, y + .245f, -.008f, .04f, .038f, .038f, 10);
                            return .16f;
                    }
                case 5: // 毛巾、脸盆、肥皂
                    if (pick <= 1)
                    {
                        if (room < .3f) return 0f;
                        int cnt = 3 + rng.Next(3);
                        for (int i = 0; i < cnt && (i + 1) * .05f < clear; i++)
                            k.Bb(i % 3 == 2 ? D.ClothIndigo : D.ClothWhite, x + .15f, y + i * .05f, Rf(rng, -.01f, .01f), .3f, .048f, .24f);
                        return .3f;
                    }
                    if (pick == 2)
                    {
                        if (room < .3f) return 0f;
                        k.C(D.Brass, x + .15f, y, 0, .12f, .15f, .08f, 16);
                        k.C(D.Black, x + .15f, y + .081f, 0, .13f, .13f, .002f, 16);
                        return .3f;
                    }
                    if (room < .2f) return 0f;
                    k.B(D.CeramicWhite, x + .05f, y + .015f, 0, .08f, .03f, .05f);
                    k.C(D.Glass, x + .15f, y, 0, .03f, .03f, .16f, 8);
                    k.C(D.Glass, x + .15f, y + .16f, 0, .015f, .012f, .04f, 8);
                    return .2f;
            }
            return 0f;
        }

        static void DrawBench(Kit k, System.Random rng)
        {
            k.Bb(D.TrimLight, 0, .42f, .21f, 1.6f, .05f, .42f);
            foreach (float sx in new[] { -.62f, .62f })
            {
                k.Bb(D.TrimMid, sx, 0, .21f, .06f, .42f, .36f);
                k.Bb(D.TrimMid, sx, 0, .21f, .1f, .04f, .4f);
            }
            k.Bb(D.TrimMid, 0, .1f, .21f, 1.24f, .05f, .04f);
            int what = rng.Next(3);
            if (CoverNext) return; // 盖着防尘布：凳面上的东西不画
            switch (what)
            {
                case 0:
                    k.Bb(D.ClothIndigo, -.4f, .47f, .21f, .5f, .05f, .38f);
                    break;
                case 1:
                    k.Bb(D.Paper, .35f, .47f, .22f, .38f, .015f, .28f);
                    k.B(D.Black, .3f, .493f, .2f, .12f, .01f, .04f);
                    break;
                default:
                    k.Cr(D.Black, new Vector3(-.65f, .5f, .3f), .03f, .01f, .8f, AlongX, 8);
                    k.C(D.Straw, .4f, .47f, .2f, .17f, .02f, .07f, 14);
                    break;
            }
        }

        static void DrawVeranda(Kit k, System.Random rng)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                k.Push(new Vector3(sx * .65f, 0, .42f), sx * -35f);
                k.Bb(D.Straw, 0, .36f, 0, .52f, .07f, .5f);
                k.Bb(D.ClothWhite, 0, .43f, .02f, .46f, .05f, .44f);
                k.Br(D.Straw, 0, .72f, -.23f, .52f, .62f, .05f, -10f, 0, 0);
                foreach (float ax in new[] { -.27f, .27f })
                {
                    k.Bb(D.Straw, ax, .36f, 0, .05f, .26f, .5f);
                    k.Cr(D.Bamboo, new Vector3(ax, .62f, -.25f), .02f, .02f, .5f, AlongZ, 6);
                    foreach (float az in new[] { -.22f, .22f }) k.Bb(D.Bamboo, ax, 0, az, .035f, .36f, .035f);
                }
                k.Pop();
            }
            k.C(D.Straw, 0, .5f, .5f, .29f, .29f, .02f, 20);
            k.C(D.Glass, 0, .52f, .5f, .28f, .28f, .015f, 20);
            for (int i = 0; i < 3; i++)
            {
                var p = Quaternion.Euler(0, i * 120f, 0) * new Vector3(.2f, 0, 0);
                k.C(D.Bamboo, p.x, 0, .5f + p.z, .015f, .015f, .5f, 6);
            }
            k.C(D.CeramicWhite, -.08f, .535f, .45f, .035f, .03f, .06f, 10);
            k.C(D.CeramicWhite, .1f, .535f, .55f, .035f, .03f, .06f, 10);
            k.C(D.Glass, Rf(rng, -.05f, .05f), .535f, .62f, .06f, .05f, .02f, 12);
        }

        /// <summary>矮木台上放一台显像管电视（Poly Haven Television_01），顶上蕾丝垫、兔耳天线、两个瓷摆件。</summary>
        static void DrawTV(Kit k)
        {
            const float top = .4f, z = .29f;
            foreach (float sx in new[] { -.34f, .34f })
                foreach (float sz in new[] { .08f, .5f })
                    k.Br(D.Post, sx, (top - .03f) / 2, sz, .035f, top - .03f, .035f, sz < .3f ? -5f : 5f, 0, sx < 0 ? -5f : 5f);
            k.Bb(D.TrimMid, 0, top - .03f, z, .82f, .03f, .56f);
            k.Bb(D.TrimMid, 0, .1f, z, .74f, .02f, .48f);
            k.Bb(D.Paper, -.16f, .12f, z + .02f, .26f, .025f, .2f);  // 下层压着几本旧杂志
            k.Bb(D.ClothRed, -.15f, .145f, z + .03f, .24f, .006f, .19f);
            k.Bb(D.ClothWhite, 0, top, z - .01f, .7f, .003f, .5f);    // 台面上的蕾丝垫
            k.Model("Television_01", new Vector3(0, top + .003f, z + .02f), 0f);
            float tv = top + .003f + Kit.ModelSize("Television_01").y;
            // 盖着防尘布（CoverNext）：顶上的垫子、摆件不画，只剩兔耳天线从布里戳出来
            if (!CoverNext)
            {
                k.Bb(D.ClothWhite, 0, tv, z, .46f, .003f, .3f);
                k.C(D.Black, .15f, tv + .003f, .25f, .05f, .05f, .03f, 10);
            }
            foreach (float s in new[] { -1f, 1f }) k.Cr(D.Iron, new Vector3(.15f, tv + .03f, .25f), .004f, .003f, .5f, Quaternion.Euler(0, 0, s * 28f), 5);
            if (CoverNext) return;
            k.S(D.CeramicWhite, -.18f, tv + .04f, .3f, .04f, .04f, .035f, 8);
            k.S(D.CeramicWhite, -.18f, tv + .1f, .3f, .03f, .03f, .03f, 8);
        }

        static void DrawWorkTable(Kit k, System.Random rng)
        {
            const float W = 1.7f, Dp = .75f, H = .85f;
            k.Bb(D.TrimLight, 0, H - .05f, Dp / 2, W, .05f, Dp);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { .06f, Dp - .06f })
                    k.Bb(D.Post, sx * (W / 2 - .06f), 0, sz, .065f, H - .05f, .065f);
            k.Bb(D.TrimMid, 0, .14f, Dp / 2, W - .14f, .03f, Dp - .1f);
            // 下层：搪瓷锅（盖子掉在地上）、一摞碗、木桶
            k.Model("pot_enamel_01", new Vector3(-.5f, .17f, .38f), Rf(rng, 0f, 360f), 1.1f, "base");
            k.Model("pot_enamel_01", new Vector3(-.62f, 0f, Dp + .12f), Rf(rng, 0f, 360f), 1.1f, "lid", 0f, Rf(rng, 6f, 12f));
            for (int i = 0; i < 4; i++) k.C(D.CeramicWhite, 0, .17f + i * .04f, .36f, .05f, .08f, .04f, 12);
            k.Model("wooden_bucket_02", new Vector3(.5f, .17f, .38f), Rf(rng, 0f, 360f), .55f);
            // 台面：旧砧板（Poly Haven wooden_cutting_board）
            k.Model("wooden_cutting_board", new Vector3(-.35f, H, .4f), Rf(rng, -8f, 8f));
            k.Cr(D.ClothWhite, new Vector3(-.55f, H + .07f, .38f), .04f, .03f, .38f, AlongX, 10);
            k.S(D.Leaf, -.6f, H + .08f, .38f, .07f, .03f, .05f, 7);
            k.B(D.Iron, -.25f, H + .04f, .5f, .2f, .004f, .045f);
            k.B(D.Post, -.1f, H + .045f, .5f, .1f, .018f, .025f);
            k.C(D.CeramicBrown, .35f, H, .35f, .07f, .14f, .1f, 14);
            k.Cr(D.TrimLight, new Vector3(.35f, H + .05f, .35f), .015f, .012f, .26f, Quaternion.Euler(0, 0, -30f), 6);
            k.C(D.CeramicBlue, .65f, H, .45f, .05f, .09f, .06f, 12);
            k.C(D.Iron, .62f, H, .2f, .1f, .09f, .12f, 14);
            k.C(D.Iron, .62f, H + .12f, .2f, .06f, .03f, .03f, 12);
            k.Br(D.Iron, .62f, H + .2f, .2f, .2f, .012f, .012f, 0, 0, Rf(rng, -5f, 5f));
        }

        /// <summary>手压井泵 + 石流し + 木桶。</summary>
        static void DrawPump(Kit k)
        {
            const float sx = -.15f, z = .36f;
            k.Bb(D.Stone, sx, 0, z, .9f, .3f, .66f);
            k.Bb(D.Stone, sx, .3f, z + .29f, .9f, .12f, .08f);
            k.Bb(D.Stone, sx, .3f, z - .29f, .9f, .12f, .08f);
            foreach (float s in new[] { -1f, 1f }) k.Bb(D.Stone, sx + s * .41f, .3f, z, .08f, .12f, .5f);
            k.Bb(D.Water, sx, .3f, z, .74f, .06f, .5f);
            k.Bb(D.Post, sx, 0, .05f, .12f, 1.0f, .1f);
            k.C(D.Iron, sx, .42f, .14f, .06f, .06f, .55f, 12);
            k.C(D.Iron, sx, .97f, .14f, .075f, .075f, .04f, 12);
            k.Cr(D.Iron, new Vector3(sx, .8f, .17f), .025f, .02f, .2f, Quaternion.Euler(110f, 0, 0), 8);
            k.Bb(D.Iron, sx + .05f, .97f, .14f, .04f, .12f, .03f);
            k.Br(D.Iron, sx + .2f, 1.08f, .14f, .38f, .025f, .03f, 0, 0, 20f);
            Oke(k, .45f, 0f, .35f, .16f, .26f);
            k.Cr(D.TrimLight, new Vector3(.45f, .18f, .35f), .01f, .01f, .42f, Quaternion.Euler(0, 0, 18f), 6);
            k.S(D.TrimLight, .32f, .58f, .35f, .04f, .03f, .04f, 8);
        }

        static void DrawArmor(Kit k)
        {
            const float z = .36f;
            k.Bb(D.Lacquer, 0, 0, z, .7f, .06f, .6f);
            k.Bb(D.Post, 0, .06f, z - .12f, .05f, 1.25f, .05f);
            k.Bb(D.Post, 0, 1.12f, z - .12f, .62f, .04f, .04f);
            k.C(D.Lacquer, 0, .66f, z, .2f, .25f, .46f, 10);
            for (int i = 0; i < 6; i++) k.C(D.ClothIndigo, 0, .7f + i * .065f, z, .208f + i * .008f, .208f + i * .008f, .012f, 10);
            for (int i = 0; i < 5; i++)
            {
                float a = -60f + i * 30f;
                var p = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, .2f);
                k.Br(D.Lacquer, p.x, .5f, z + p.z, .16f, .3f, .025f, 12f, a, 0);
                for (int j = 0; j < 3; j++) k.Br(D.ClothIndigo, p.x * 1.06f, .4f + j * .09f, z + p.z * 1.06f, .165f, .012f, .02f, 12f, a, 0);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                k.Br(D.Lacquer, s * .3f, 1.0f, z + .02f, .04f, .32f, .26f, 0, 0, s * 14f);
                k.Cr(D.ClothIndigo, new Vector3(s * .26f, 1.08f, z + .05f), .045f, .035f, .5f, Quaternion.Euler(0, 0, 180f + s * 8f), 8);
                k.S(D.Black, s * .29f, .58f, z + .05f, .045f, .05f, .04f, 7);
            }
            k.S(D.Black, 0, 1.38f, z, .14f, .11f, .14f, 12);
            k.C(D.Lacquer, 0, 1.24f, z - .02f, .22f, .15f, .12f, 12);
            foreach (float s in new[] { -1f, 1f })
            {
                k.Br(D.Gold, s * .07f, 1.56f, z + .1f, .02f, .26f, .008f, 0, 0, -s * 22f);
                k.Br(D.Lacquer, s * .15f, 1.33f, z + .08f, .06f, .08f, .02f, 0, s * 30f, 0);
            }
            k.S(D.ClothRed, 0, 1.29f, z + .1f, .085f, .07f, .045f, 10);
            k.B(D.Black, -.03f, 1.31f, z + .142f, .025f, .012f, .01f);
            k.B(D.Black, .03f, 1.31f, z + .142f, .025f, .012f, .01f);
            k.B(D.CeramicWhite, 0, 1.26f, z + .142f, .06f, .008f, .006f);
        }

        static void DrawDressForm(Kit k)
        {
            const float z = .25f;
            for (int i = 0; i < 3; i++)
            {
                var dir = Quaternion.Euler(0, i * 120f + 30f, 0) * new Vector3(.2f, -.12f, 0);
                k.Cr(D.Post, new Vector3(0, .14f, z), .014f, .012f, dir.magnitude, Quaternion.FromToRotation(Vector3.up, dir.normalized), 6);
            }
            k.Cr(D.Post, new Vector3(0, .1f, z), .016f, .014f, .9f, Quaternion.identity, 6);
            k.S(D.ClothWhite, 0, 1.18f, z, .16f, .26f, .12f, 14);
            k.S(D.ClothWhite, 0, 1.38f, z, .19f, .07f, .11f, 12);
            k.C(D.TrimMid, 0, 1.43f, z, .045f, .035f, .1f, 10);
            k.C(D.Lacquer, 0, 1.53f, z, .05f, .05f, .02f, 10);
            if (CoverNext) return; // 盖着防尘布：布条、珠针都在布底下（侧面那条红布还会戳出布外）
            k.Br(D.ClothRed, -.08f, 1.1f, z + .115f, .2f, .5f, .008f, 0, 0, 5f);
            k.Br(D.ClothRed, -.17f, .85f, z + .06f, .18f, .5f, .008f, 0, 30f, 0);
            for (int i = 0; i < 4; i++) k.S(D.Brass, .02f + i * .03f, 1.2f - i * .07f, z + .125f, .006f, .006f, .006f, 5);
            k.Br(D.Gold, .05f, 1.36f, z + .1f, .015f, .3f, .003f, 0, 0, -10f);
        }

        static void DrawSewing(Kit k)
        {
            const float z = .25f;
            foreach (float sx in new[] { -.4f, .4f })
            {
                k.Bb(D.Iron, sx, 0, z, .05f, .04f, .44f);
                k.Br(D.Iron, sx, .37f, z - .1f, .025f, .72f, .03f, 12f, 0, 0);
                k.Br(D.Iron, sx, .37f, z + .1f, .025f, .72f, .03f, -12f, 0, 0);
            }
            k.Bb(D.Iron, 0, .3f, z - .17f, .8f, .03f, .025f);
            k.Br(D.Iron, 0, .12f, z + .05f, .5f, .02f, .26f, 14f, 0, 0);
            k.Cr(D.Iron, new Vector3(.36f, .42f, z), .2f, .2f, .02f, AlongX, 20);
            for (int i = 0; i < 4; i++) k.Br(D.Iron, .37f, .42f, z, .01f, .38f, .014f, i * 45f, 0, 0);
            k.Br(D.Black, .37f, .6f, z + .05f, .006f, .36f, .006f, -8f, 0, 0);
            k.Bb(D.TrimMid, 0, .72f, z, .95f, .035f, .48f);
            const float y = .755f;
            k.Bb(D.Black, 0, y, z, .36f, .05f, .17f);
            k.Bb(D.Black, .13f, y + .05f, z, .08f, .2f, .12f);
            k.Bb(D.Black, -.02f, y + .2f, z, .38f, .08f, .1f);
            k.Bb(D.Black, -.19f, y + .09f, z, .06f, .15f, .08f);
            k.B(D.Iron, -.19f, y + .06f, z + .02f, .005f, .06f, .005f);
            k.Cr(D.Brass, new Vector3(.17f, y + .2f, z), .07f, .07f, .03f, AlongX, 14);
            k.B(D.Gold, -.02f, y + .24f, z + .051f, .22f, .03f, .002f);
            if (!CoverNext) k.C(D.ClothRed, .02f, y + .28f, z, .015f, .015f, .04f, 8); // 线轴（盖着布时不画，会戳穿布）
            k.Bb(D.ClothIndigo, -.2f, y, z + .14f, .3f, .004f, .2f);
        }

        /// <summary>雛人形：五段红毯台阶，顶上金屏风前的内裏雛，下面三人官女、五人囃子、随身、橘与樱。</summary>
        static void DrawHina(Kit k)
        {
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
            k.Bb(D.GoldLeaf, 0, Ty(t), .03f, .7f, .32f, .02f);
            Doll(k, -.14f, Ty(t), Tz(t), D.Black, .9f);
            Doll(k, .14f, Ty(t), Tz(t), D.ClothRed, .9f);
            foreach (float sx in new[] { -.45f, .45f })
            {
                k.C(D.Lacquer, sx, Ty(t), Tz(t), .03f, .02f, .16f, 8);
                k.S(D.PaperLamp, sx, Ty(t) + .2f, Tz(t), .045f, .06f, .045f, 10);
            }
            for (int i = 0; i < 3; i++) Doll(k, -.3f + i * .3f, Ty(3), Tz(3), D.ClothWhite, .8f);
            for (int i = 0; i < 5; i++) Doll(k, -.48f + i * .24f, Ty(2), Tz(2), D.ClothIndigo, .7f);
            Doll(k, -.35f, Ty(1), Tz(1), D.ClothPurple, .75f);
            Doll(k, .35f, Ty(1), Tz(1), D.Lacquer, .75f);
            foreach (float sx in new[] { -.4f, .4f })
            {
                k.Bb(D.Lacquer, sx, Ty(0), Tz(0), .1f, .04f, .1f);
                k.Cr(D.Bark, new Vector3(sx, Ty(0) + .04f, Tz(0)), .008f, .006f, .1f, Quaternion.identity, 5);
                k.S(sx < 0 ? D.Leaf : D.ClothWhite, sx, Ty(0) + .17f, Tz(0), .07f, .06f, .07f, 8);
            }
        }

        static void Doll(Kit k, float x, float y, float z, Material robe, float s)
        {
            k.C(robe, x, y, z, .06f * s, .025f * s, .13f * s, 10);
            k.S(D.CeramicWhite, x, y + .155f * s, z, .025f * s, .03f * s, .025f * s, 8);
            k.S(D.Black, x, y + .17f * s, z - .006f * s, .027f * s, .025f * s, .026f * s, 8);
            k.B(D.Gold, x, y + .07f * s, z + .045f * s, .03f * s, .05f * s, .004f);
        }

        /// <summary>落地穿衣镜，上半截盖着紫布（鏡掛け）。</summary>
        static void DrawStandMirror(Kit k)
        {
            k.Bb(D.Lacquer, 0, 0, .26f, .56f, .05f, .42f);
            k.Push(new Vector3(0, .05f, .24f), 0f, -7f);
            k.Bb(D.Lacquer, 0, 0, 0, .52f, 1.5f, .045f);
            k.B(D.Mirror, 0, .78f, .024f, .42f, 1.3f, .003f);
            k.Bb(D.ClothPurple, 0, .78f, .03f, .5f, .74f, .008f);
            k.B(D.ClothPurple, 0, 1.52f, 0, .5f, .02f, .08f);
            k.Br(D.ClothPurple, .2f, 1.0f, .036f, .1f, .5f, .006f, 0, 0, -4f);
            k.Pop();
            k.Br(D.Lacquer, 0, .33f, .1f, .04f, .56f, .03f, 8f, 0, 0);
        }

        static void DrawCoatRack(Kit k)
        {
            const float z = .3f;
            foreach (float a in new[] { 45f, -45f }) k.Br(D.Post, 0, .025f, z, .55f, .05f, .05f, 0, a, 0);
            k.Cr(D.Post, new Vector3(0, .05f, z), .025f, .02f, 1.72f, Quaternion.identity, 8);
            k.S(D.Post, 0, 1.79f, z, .04f, .04f, .04f, 8);
            for (int i = 0; i < 4; i++)
            {
                var dir = Quaternion.Euler(0, i * 90f + 45f, 0) * Quaternion.Euler(0, 0, 50f) * Vector3.right;
                k.Cr(D.Brass, new Vector3(0, 1.6f, z), .01f, .008f, .16f, Quaternion.FromToRotation(Vector3.up, dir), 5);
            }
            k.C(D.Black, .1f, 1.68f, z + .08f, .14f, .14f, .012f, 16);
            k.C(D.Black, .1f, 1.69f, z + .08f, .085f, .075f, .1f, 14);
            k.Br(D.ClothIndigo, -.08f, 1.15f, z + .1f, .4f, .9f, .08f, 0, 20f, 0);
            k.Br(D.ClothIndigo, -.08f, 1.55f, z + .08f, .32f, .1f, .12f, 0, 20f, 0);
            k.Cr(D.Black, new Vector3(.12f, .1f, z - .05f), .045f, .012f, .78f, Quaternion.Euler(4f, 0, -6f), 8);
            k.Cr(D.Post, new Vector3(.08f, .88f, z - .05f), .008f, .008f, .15f, Quaternion.Euler(0, 0, -20f), 5);
            k.Br(D.ClothRed, .14f, 1.35f, z + .02f, .08f, .5f, .02f, 0, -30f, 4f);
        }

        /// <summary>衝立：一扇墨竹屏，正面朝房间。</summary>
        static void DrawTsuitate(Kit k, System.Random rng)
        {
            const float z = .21f;
            foreach (float sx in new[] { -.58f, .58f })
            {
                k.Bb(D.Lacquer, sx, 0, z, .07f, .08f, .42f);
                k.Bb(D.Lacquer, sx, .08f, z, .045f, 1.2f, .045f);
            }
            k.Bb(D.Lacquer, 0, .2f, z, 1.21f, .04f, .04f);
            k.Bb(D.Lacquer, 0, 1.24f, z, 1.21f, .05f, .045f);
            k.Bb(D.Paper, 0, .24f, z, 1.12f, 1.0f, .016f);
            const float fz = z + .0085f;
            for (int i = 0; i < 3; i++)
            {
                float x = -.25f + i * .18f + Rf(rng, -.03f, .03f);
                for (int j = 0; j < 5; j++) k.Bb(D.Ink, x, .28f + j * .19f, fz, .018f, .17f, .002f);
                for (int j = 0; j < 4; j++)
                    k.Br(D.Ink, x + .06f, .5f + j * .18f + Rf(rng, 0f, .05f), fz, .12f, .018f, .002f, 0, 0, Rf(rng, -40f, -15f));
            }
            k.B(D.ClothRed, .42f, .4f, fz, .04f, .04f, .002f);
        }

        static void DrawHibachi(Kit k)
        {
            const float z = .27f;
            k.C(D.CeramicBlue, 0, 0, z, .19f, .22f, .29f, 16);
            k.C(D.Ash, 0, .29f, z, .19f, .19f, .004f, 16);
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                var p = Quaternion.Euler(0, a, 0) * new Vector3(.08f, 0, 0);
                k.Bb(D.Iron, p.x, .294f, z + p.z, .015f, .06f, .015f);
            }
            k.S(D.Iron, 0, .41f, z, .1f, .07f, .1f, 12);
            k.C(D.Iron, 0, .47f, z, .04f, .03f, .02f, 10);
            k.Cr(D.Iron, new Vector3(.08f, .4f, z), .012f, .007f, .1f, Quaternion.Euler(0, 0, -55f), 6);
            k.Br(D.Iron, 0, .53f, z, .16f, .01f, .012f, 0, 0, 0);
            k.Cr(D.Brass, new Vector3(-.15f, .295f, z - .08f), .005f, .004f, .3f, Quaternion.Euler(10, 0, -70), 5);
        }

        /// <summary>昭和的绞干式洗衣机：搪瓷桶 + 顶上两根滚筒。</summary>
        static void DrawWasher(Kit k)
        {
            const float z = .31f;
            foreach (float sx in new[] { -.2f, .2f })
                foreach (float sz in new[] { -.2f, .2f })
                    k.Bb(D.Iron, sx, 0, z + sz, .03f, .26f, .03f);
            k.C(D.CeramicWhite, 0, .25f, z, .26f, .27f, .56f, 18);
            k.C(D.Black, 0, .8f, z, .275f, .275f, .015f, 18);
            k.C(D.Water, 0, .815f, z, .24f, .24f, .002f, 18);
            foreach (float sx in new[] { -.21f, .21f }) k.Bb(D.Iron, sx, .8f, z - .17f, .03f, .32f, .05f);
            k.Cr(D.Black, new Vector3(-.19f, .95f, z - .17f), .035f, .035f, .38f, AlongX, 12);
            k.Cr(D.Black, new Vector3(-.19f, 1.025f, z - .17f), .035f, .035f, .38f, AlongX, 12);
            k.Bb(D.Iron, 0, 1.11f, z - .17f, .46f, .03f, .05f);
            k.Cr(D.Iron, new Vector3(.22f, .99f, z - .17f), .01f, .01f, .14f, AlongX, 6);
            k.Cr(D.Iron, new Vector3(.36f, .99f, z - .17f), .008f, .008f, .12f, Quaternion.identity, 6);
            k.C(D.TrimLight, .36f, 1.1f, z - .17f, .014f, .014f, .06f, 6);
            k.Cr(D.Black, new Vector3(.27f, .35f, z + .05f), .012f, .012f, .35f, Quaternion.Euler(0, 0, -150f), 6);
        }

        static void DrawWashTubs(Kit k, System.Random rng)
        {
            k.C(D.Iron, -.08f, 0, .4f, .3f, .34f, .14f, 18);
            k.C(D.Black, -.08f, .141f, .4f, .3f, .3f, .002f, 18);
            k.Push(new Vector3(-.1f, .15f, .38f), Rf(rng, -10f, 10f), 0f, 6f);
            k.C(D.Brass, 0, 0, 0, .2f, .24f, .1f, 16);
            k.C(D.Black, 0, .101f, 0, .2f, .2f, .002f, 16);
            k.Pop();
            k.Push(new Vector3(.32f, 0, .16f), 0f, -14f);
            k.Bb(D.TrimLight, 0, 0, 0, .3f, .62f, .02f);
            for (int i = 0; i < 12; i++) k.Bb(D.TrimMid, 0, .12f + i * .035f, .012f, .26f, .012f, .008f);
            k.Pop();
            k.S(D.CeramicWhite, .3f, .03f, .45f, .05f, .025f, .035f, 8);
        }

        static void DrawRockingHorse(Kit k)
        {
            const float z = .22f, R = .8f;
            foreach (float sz in new[] { -.12f, .12f })
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = -35f + i * 14f, ar = a * Mathf.Deg2Rad;
                    k.Br(D.TrimMid, Mathf.Sin(ar) * R, R - Mathf.Cos(ar) * R + .02f, z + sz, R * .25f, .035f, .03f, 0, 0, a);
                }
                foreach (float sx in new[] { -.22f, .22f }) k.Br(D.TrimLight, sx, .25f, z + sz * .6f, .03f, .38f, .03f, sz > 0 ? -12f : 12f, 0, 0);
            }
            k.Bb(D.TrimLight, 0, .42f, z, .58f, .2f, .17f);
            k.Br(D.TrimLight, .32f, .68f, z, .12f, .34f, .1f, 0, 0, -28f);
            k.Br(D.TrimLight, .42f, .84f, z, .26f, .11f, .09f, 0, 0, -12f);
            foreach (float s in new[] { -.03f, .03f }) k.Br(D.TrimLight, .34f, .93f, z + s, .03f, .07f, .02f, 0, 0, -20f);
            k.Br(D.Black, .27f, .72f, z, .05f, .34f, .02f, 0, 0, -28f);
            foreach (float s in new[] { -.046f, .046f }) k.S(D.Black, .46f, .87f, z + s, .012f, .012f, .006f, 6);
            k.Bb(D.ClothRed, -.02f, .62f, z, .24f, .03f, .2f);
            k.Cr(D.Black, new Vector3(-.29f, .55f, z), .025f, .006f, .28f, Quaternion.Euler(0, 0, 125f), 6);
        }

        static void DrawSpinningWheel(Kit k)
        {
            const float z = .21f;
            k.Bb(D.TrimMid, 0, 0, z, .9f, .05f, .3f);
            foreach (float s in new[] { -.08f, .08f }) k.Bb(D.TrimMid, .22f, .05f, z + s, .04f, .5f, .04f);
            var c = new Vector3(.22f, .55f, z);
            for (int i = 0; i < 8; i++) k.Br(D.TrimLight, c.x, c.y, c.z, .01f, .54f, .012f, 0, 0, i * 22.5f);
            Ring(k, D.Bamboo, c + new Vector3(0, 0, .03f), .27f, .02f);
            Ring(k, D.Bamboo, c - new Vector3(0, 0, .03f), .27f, .02f);
            k.Cr(D.Iron, c - new Vector3(0, 0, .1f), .015f, .015f, .2f, AlongZ, 6);
            k.Br(D.TrimLight, c.x + .1f, c.y - .05f, z + .12f, .14f, .02f, .02f, 0, 0, -70f);
            k.Bb(D.TrimMid, -.33f, .05f, z, .05f, .22f, .05f);
            k.Cr(D.Iron, new Vector3(-.44f, .29f, z), .004f, .004f, .2f, AlongX, 5);
            k.Cr(D.ClothWhite, new Vector3(-.4f, .29f, z), .025f, .025f, .06f, AlongX, 8);
            k.Br(D.ClothWhite, -.06f, .43f, z, .55f, .003f, .003f, 0, 0, 25f);
        }

        static void DrawKoto(Kit k)
        {
            const float z = .2f;
            foreach (float sx in new[] { -.7f, .7f }) k.Bb(D.Lacquer, sx, 0, z, .07f, .1f, .32f);
            k.Bb(D.TrimMid, 0, .1f, z, 1.82f, .07f, .26f);
            k.Bb(D.TrimLight, 0, .17f, z, 1.78f, .012f, .24f);
            foreach (float sx in new[] { -.88f, .88f }) k.Bb(D.Gold, sx, .17f, z, .04f, .02f, .25f);
            for (int i = 0; i < 13; i++)
            {
                float zz = z - .1f + i * (.2f / 12);
                k.B(D.ClothWhite, 0, .195f, zz, 1.72f, .002f, .002f);
                k.Bb(D.CeramicWhite, -.5f + i * .07f + (i % 3) * .05f, .182f, zz, .012f, .03f, .01f);
            }
            k.Bb(D.ClothPurple, .55f, .1f, z, .7f, .11f, .3f);
        }

        /// <summary>米俵（土蔵里堆着）：下面两排四个，上面一个。</summary>
        static void DrawRiceBales(Kit k, System.Random rng)
        {
            void Bale(Vector3 c, float yaw)
            {
                k.Push(c, yaw);
                k.Cr(D.Straw, new Vector3(-.36f, 0, 0), .2f, .2f, .72f, AlongX, 12);
                foreach (float sx in new[] { -.39f, .35f }) k.Cr(D.Straw, new Vector3(sx, 0, 0), .17f, .17f, .04f, AlongX, 12);
                foreach (float sx in new[] { -.22f, 0f, .22f }) k.Cr(D.TrimMid, new Vector3(sx - .012f, 0, 0), .205f, .205f, .024f, AlongX, 12);
                k.Pop();
            }
            Bale(new Vector3(-.39f, .2f, .22f), Rf(rng, -3f, 3f));
            Bale(new Vector3(.39f, .2f, .22f), Rf(rng, -3f, 3f));
            Bale(new Vector3(-.39f, .2f, .6f), Rf(rng, -3f, 3f));
            Bale(new Vector3(.39f, .2f, .6f), Rf(rng, -3f, 3f));
            Bale(new Vector3(Rf(rng, -.05f, .05f), .54f, .41f), Rf(rng, -4f, 4f));
        }
    }
}
