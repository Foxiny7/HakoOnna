using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第四轮（2026-11-20）：没人打理的院子——房子（含土蔵）墙根、院墙内侧、竹篱笆脚下一丛丛枯掉的荨麻、贴地的杂草，
    /// 院墙下偶尔一堆枯枝（CC0：Poly Haven nettle_plant 干枯版贴图、weed_plant_02、dry_branches_medium_01）。
    /// 只是视觉、没有碰撞体；门、玻璃拉门前后各留 0.7 m 不长（不挡路、不挡沓脱石），也躲开树、石灯笼、水井这些挡块。
    /// 随机数单独一条（不影响院子其余东西的位置）。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        static readonly string[] NettleParts = { "nettle_plant_tall_a", "nettle_plant_tall_b", "nettle_plant_medium_a", "nettle_plant_medium_b" };
        static readonly string[] WeedParts = { "weed_plant_02_a", "weed_plant_02_b", "weed_plant_02_c", "weed_plant_02_d", "weed_plant_02_e" };
        static readonly string[] BranchParts = { "dry_branches_medium_01_a", "dry_branches_medium_01_b", "dry_branches_medium_01_c" };

        static void GardenWeeds(Transform parent, (Vector3 a, Vector3 b)[] bambooFences)
        {
            if (!ExternalModels.Has("nettle_plant") || !ExternalModels.Has("weed_plant_02"))
            {
                Debug.LogWarning("[Hako Art] 缺 nettle_plant / weed_plant_02，院子不长杂草");
                return;
            }
            var rng = new System.Random(4207);
            float Rr(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var tall = new MeshBatch();   // 荨麻、枯枝：投影
            var low = new MeshBatch();    // 贴地的杂草：不投影（太小，省一点）
            var fence = RoomById("Fence");
            bool hasBranches = ExternalModels.Has("dry_branches_medium_01");

            // 挡块（树、石灯笼、水井、石头……）的底面矩形，杂草离它们 0.25 m 以外
            var blocks = new List<(Vector3 p, Quaternion q, Vector3 c, Vector3 s)>();
            if (BlockerRoot != null)
                foreach (Transform t in BlockerRoot)
                    if (t.TryGetComponent<BoxCollider>(out var bc)) blocks.Add((t.position, t.rotation, bc.center, bc.size));

            bool Free(Vector3 p)
            {
                var v = new Vector2(p.x, p.z);
                foreach (var r in DRooms)
                    if (r.group != GFence && r.r.Contains(v)) return false;
                if (fence != null)
                {
                    var inner = fence.r;
                    float m = T + .1f;
                    if (v.x < inner.xMin + m || v.x > inner.xMax - m || v.y < inner.yMin + m || v.y > inner.yMax - m) return false;
                }
                foreach (var b in blocks)
                {
                    var l = Quaternion.Inverse(b.q) * (p - b.p) - b.c;
                    if (Mathf.Abs(l.x) < b.s.x / 2f + .25f && Mathf.Abs(l.z) < b.s.z / 2f + .25f) return false;
                }
                return true;
            }

            int clumps = 0, branches = 0;
            void Clump(Vector3 p, Vector3 along, Vector3 outward, bool big)
            {
                // 第四轮审图后：原来 2.4–3.3 倍、一丛一两棵，白天看也只是墙根零星几棵小苗；放大、加密成“没人管”的样子
                var n = NettleParts[rng.Next(NettleParts.Length)];
                float s = big ? Rr(3.1f, 4.3f) : Rr(2.4f, 3.4f);
                tall.Model("nettle_plant", ExternalModels.Place("nettle_plant", n, p, Quaternion.Euler(0f, Rr(0f, 360f), Rr(-6f, 6f)), s), n);
                int k = 2 + rng.Next(2);
                for (int i = 0; i < k; i++)
                {
                    var w = WeedParts[rng.Next(WeedParts.Length)];
                    var q = p + along * Rr(-.5f, .5f) + outward * Rr(.02f, .3f);
                    low.Model("weed_plant_02", ExternalModels.Place("weed_plant_02", w, q, Quaternion.Euler(0f, Rr(0f, 360f), 0f), Rr(2.3f, 3.4f)), w);
                }
                clumps++;
            }
            void Branch(Vector3 p, Vector3 along)
            {
                if (!hasBranches) return;
                var n = BranchParts[rng.Next(BranchParts.Length)];
                float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg + Rr(-12f, 12f);
                tall.Model("dry_branches_medium_01", ExternalModels.Place("dry_branches_medium_01", n, p, Quaternion.Euler(0f, yaw, 0f), Rr(.8f, 1.05f)), n);
                branches++;
            }

            // ---- 房子、土蔵的墙根（和 ExteriorWalls 一样找外墙段）----
            foreach (var room in DRooms)
            {
                if (room.group != G0 && room.group != GKura) continue;
                foreach (var e in EdgesOf(room))
                {
                    if (!HasWall(room, e)) continue;
                    float a = e.alongX ? room.r.xMin : room.r.yMin, b = e.alongX ? room.r.xMax : room.r.yMax;
                    var cuts = new List<float> { a, b };
                    foreach (var o in DRooms)
                    {
                        if (o.group != room.group || o == room) continue;
                        foreach (float v in e.alongX ? new[] { o.r.xMin, o.r.xMax } : new[] { o.r.yMin, o.r.yMax })
                            if (v > a + .01f && v < b - .01f) cuts.Add(v);
                    }
                    cuts.Sort();
                    var doors = new List<Vector2>();
                    foreach (var g in GapsOn(room, e))
                        if (g.door || g.glass || g.bottom < FloorHeight(room) + .3f) doors.Add(new Vector2(g.a - .7f, g.b + .7f));
                    var outward = -Inward(e);
                    var along = e.alongX ? Vector3.right : Vector3.forward;
                    for (int i = 0; i < cuts.Count - 1; i++)
                    {
                        if (cuts[i + 1] - cuts[i] < .6f) continue;
                        float mid = (cuts[i] + cuts[i + 1]) / 2f;
                        if (RoomAt(OnWall(e, mid, 1f, -.05f), room.group) != null) continue; // 里墙
                        foreach (var run in Minus(cuts[i] + .25f, cuts[i + 1] - .25f, doors))
                            for (float s = run.x + Rr(.1f, 1.0f); s < run.y; s += Rr(1.1f, 2.3f))
                            {
                                var p = OnWall(e, s, 0f, -Rr(.1f, .28f));
                                if (!Free(p) || !Free(p + outward * .35f)) continue;
                                if (clumps == 6) ShotDir("weeds_house", p + Vector3.up * .35f, Quaternion.Euler(0f, 25f, 0f) * outward, 2.8f, 1.1f); // 第四轮审图
                                Clump(p, along, outward, true);
                            }
                    }
                }
            }

            // ---- 院墙内侧 ----
            if (fence != null)
                foreach (var e in EdgesOf(fence))
                {
                    var cuts = new List<Vector2>();
                    foreach (var g in GapsOn(fence, e)) cuts.Add(new Vector2(g.a - .8f, g.b + .8f));
                    var outward = Inward(e); // 离开墙面朝院子里
                    var along = e.alongX ? Vector3.right : Vector3.forward;
                    foreach (var run in Minus(e.from + .4f, e.to - .4f, cuts))
                        for (float s = run.x + Rr(.2f, 1.6f); s < run.y; s += Rr(2.4f, 4.6f))
                        {
                            var p = OnWall(e, s, 0f, T + Rr(.12f, .3f));
                            if (!Free(p)) continue;
                            ShotDir("weeds_fence", p + Vector3.up * .35f, Quaternion.Euler(0f, 25f, 0f) * outward, 3.0f, 1.2f); // 第四轮审图（只记第一处）
                            if (rng.Next(5) == 0) Branch(OnWall(e, s + 1.1f, 0f, T + .3f), along);
                            Clump(p, along, outward, rng.Next(3) > 0);
                        }
                }

            // ---- 竹篱笆脚下（两面轮流）----
            foreach (var (a, b) in bambooFences)
            {
                var dir = b - a;
                dir.y = 0f;
                float len = dir.magnitude;
                if (len < .5f) continue;
                dir /= len;
                var side = new Vector3(dir.z, 0f, -dir.x);
                int flip = 0;
                for (float s = Rr(.3f, 1.0f); s < len - .3f; s += Rr(1.6f, 2.8f), flip++)
                {
                    var o = side * ((flip % 2 == 0 ? 1f : -1f) * Rr(.14f, .3f));
                    var p = a + dir * s + o;
                    p.y = 0f;
                    if (!Free(p)) continue;
                    ShotDir("weeds_bamboo", p + Vector3.up * .3f, Quaternion.Euler(0f, 25f, 0f) * o.normalized, 2.6f, 1.0f); // 第四轮审图（只记第一处）
                    Clump(p, dir, o.normalized, false);
                }
            }

            tall.Build(parent, "GardenWeeds");
            low.Build(parent, "GardenWeedsLow", false);
            Debug.Log($"[Hako Art] 院子杂草：{clumps} 丛，枯枝 {branches} 堆");
        }
    }
}
