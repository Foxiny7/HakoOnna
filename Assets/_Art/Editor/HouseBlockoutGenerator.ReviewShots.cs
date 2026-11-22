using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第四轮（2026-11-21）审图机位：换成模型、软网格的东西（床、桌、灶台、浴桶、坐垫、毛巾、和服、被褥、洗衣机、院子杂草……）
    /// 生成时记下位置，ArtSnapshot 生成完接着拍（文件名 rev_r4_*，室内补光）。只用来看图，不影响场景。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>这一点脚下的房间（不算院墙；同一平面上取地板在它下面、离得最近的那一层）。</summary>
        static Room RoomUnder(Vector3 p)
        {
            Room best = null;
            float bd = float.MaxValue;
            foreach (var r in DRooms)
            {
                if (r.group == GFence || !r.r.Contains(new Vector2(p.x, p.z))) continue;
                float d = p.y - FloorY(r);
                if (d < -.3f || d >= bd) continue;
                bd = d;
                best = r;
            }
            return best;
        }

        /// <summary>从 dir 那一侧看 look：机位夹在屋子里（离墙 0.2 m、天花下面）；同名只记一次。</summary>
        static void ShotDir(string name, Vector3 look, Vector3 dir, float dist, float up)
        {
            name = name.StartsWith("r5") ? name : "r4_" + name; // 第五轮的机位自带前缀
            foreach (var s in RuinShots)
                if (s.name == name) return;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            var cam = look + dir * dist + Vector3.up * up;
            var room = RoomUnder(look);
            if (room != null)
            {
                cam.x = Mathf.Clamp(cam.x, room.r.xMin + .2f, room.r.xMax - .2f);
                cam.z = Mathf.Clamp(cam.z, room.r.yMin + .2f, room.r.yMax - .2f);
                cam.y = Mathf.Clamp(cam.y, FloorY(room) + .3f, CeilY(room) - .12f);
            }
            else
            {
                // 室外（院子）：机位不能出院墙、视线不能穿过房子；从原方向起左右转着找，再不行就拉近
                bool found = false;
                foreach (float dd in new[] { dist, dist * .75f, dist * .5f })
                {
                    foreach (float turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f })
                    {
                        var c = look + Quaternion.Euler(0f, turn, 0f) * dir * dd + Vector3.up * up;
                        if (!OutdoorClear(c, look)) continue;
                        cam = c;
                        found = true;
                        break;
                    }
                    if (found) break;
                }
            }
            RuinShots.Add((name, cam, look));
        }

        /// <summary>院子里 cam 到 look 这条视线：每一点都在院墙里面（机位离墙 0.3 m 以上），也不进任何房间（房子、土蔵）的范围。</summary>
        static bool OutdoorClear(Vector3 cam, Vector3 look)
        {
            var fence = RoomById("Fence");
            for (int i = 0; i <= 12; i++)
            {
                var p = Vector3.Lerp(cam, look, i / 12f);
                var v = new Vector2(p.x, p.z);
                if (fence != null)
                {
                    var r = fence.r;
                    float m = T + (i == 0 ? .3f : .05f);
                    if (v.x < r.xMin + m || v.x > r.xMax - m || v.y < r.yMin + m || v.y > r.yMax - m) return false;
                }
                foreach (var o in DRooms)
                    if (o.group != GFence && o.r.Contains(v)) return false;
            }
            return true;
        }

        /// <summary>从屋子中间那一侧、斜着 turn 度看 look。</summary>
        static void ShotToward(string name, Vector3 look, float dist, float up, float turn = 20f)
        {
            var dir = Vector3.forward;
            var room = RoomUnder(look);
            if (room != null)
            {
                var d = new Vector3(room.r.center.x - look.x, 0f, room.r.center.y - look.z);
                if (d.sqrMagnitude > .01f) dir = d.normalized;
            }
            ShotDir(name, look, Quaternion.Euler(0f, turn, 0f) * dir, dist, up);
        }

        /// <summary>陈设代码里（第二遍、Swap 的 real 里）记机位：localLook 是当前局部坐标，跟着搬家矩阵走。</summary>
        static void KitShot(Kit k, string name, Vector3 localLook, float dist = 1.6f, float up = .45f)
        {
            if (Kit.Probing || Kit.Ghost || Kit.IsDropped(k.mb.M)) return;
            ShotToward(name, k.mb.M.MultiplyPoint3x4(localLook), dist, up);
        }

        /// <summary>按渲染包围盒拍一件东西（大件退远一点、抬高一点）。</summary>
        static void ShotOfTransform(string name, Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float size = Mathf.Max(b.size.x, b.size.z);
            ShotToward(name, b.center, Mathf.Clamp(size * 1.1f + .6f, 1.0f, 3.0f), Mathf.Clamp(size * .45f + .3f, .45f, 1.2f));
        }
    }
}
