using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 两段楼梯的外观（2026-11-22，用户：楼梯的纹理要改，旁边不要有缝）。
    /// <para>原来：每一级是一个从地面顶到踏面的实心方块，2.4 m 宽；楼梯间净宽 3.48 m，所以两边各空着 0.4 m 的深缝，
    /// 侧板和踏步之间还有 4 cm 的缝；每一级贴的是同一段木纹，一级级竖着对齐。</para>
    /// <para>现在：踏板、踢板从一面墙铺到另一面墙（各伸进墙里 5 mm），两面墙上各一条顺着坡度的侧板把踏步的端头收住，
    /// 所以没有任何缝。踏板是一整块带圆角、前沿出挑的木板，木纹顺着板长，每一级取木纹的不同位置；踢板是深色的竖板。
    /// 东墙上一根木扶手。</para>
    /// Structure/Stairs 里的 Step_NN 方块还在（程序按它们的范围判断脚下是木头），缩进 1.5 cm 藏在踏板和踢板后面。
    /// 行走用的斜面碰撞体同样加宽到整个净宽。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>(楼梯间, 中线 x（平面单位）, 起步地面高度, 级数)。和 BuildStairs 里的两段一致。</summary>
        static readonly (string room, float x, float floor, int count)[] Flights =
        {
            ("Kaidan", 13.65f, .55f, 18),
            ("UraKaidan", 4.54f, 0f, 21),
        };

        const float StairPlanZ0 = 10.01f, StairPlanLength = 3.185f;

        /// <summary>楼梯间两面墙之间的净宽。</summary>
        static float StairClearWidth => 1.82f * PlanScale - 2f * T;

        static void StairDetails(Transform parent)
        {
            var mb = new MeshBatch();
            const float board = .04f, nosing = .03f, riserT = .022f;
            float top = Levels[G1].floorTop;
            foreach (var (roomId, xp, floor, count) in Flights)
            {
                float x = xp * PlanScale, w = StairClearWidth + .01f;
                float z0 = StairPlanZ0 * PlanScale, len = StairPlanLength * PlanScale;
                float tread = len / count, rise = (top - floor) / count;
                for (int i = 0; i < count; i++)
                {
                    float y = floor + rise * (i + 1), zf = z0 + i * tread;
                    if (i == count - 1) y = FloorY(RoomById("Rouka2FN")); // 最上面一级和二楼地板齐平
                    // 踢板
                    mb.Box(D.BeamDark, new Vector3(x, y - board - (rise - board) / 2f, zf + riserT / 2f), new Vector3(w, rise - board, riserT),
                        null, 0, new Vector2(i * .37f, i * .23f));
                    // 踏板：前沿出挑，后沿压在上一级踢板底下
                    float depth = tread + nosing + .006f;
                    BevelBox(mb, D.TrimMid, new Vector3(x, y - board / 2f, zf - nosing + depth / 2f), new Vector3(w, board, depth), .007f);
                }

                // 两面墙上的侧板：顺着坡度的一条板，下沿在踏步的阴角线以下，上沿高过踏板前沿
                var a = new Vector3(x, floor, z0);
                var b = new Vector3(x, top, z0 + len);
                var along = (b - a).normalized;
                var rot = Quaternion.LookRotation(along, Vector3.up);
                float pitchCos = Vector3.Dot(along, Vector3.forward);
                float lower = -.03f, upper = rise + .13f; // 竖直方向：相对阴角线
                float thick = (upper - lower) * pitchCos, centre = (upper + lower) / 2f;
                foreach (float s in new[] { -1f, 1f })
                {
                    var c = (a + b) / 2f + Vector3.up * centre + Vector3.right * (s * (w / 2f - .014f));
                    mb.Box(D.BeamDark, c, new Vector3(.028f, thick, (b - a).magnitude + .12f), rot, 2);
                    // 侧板上沿的一道压条
                    mb.Box(D.Post, c + rot * Vector3.up * (thick / 2f + .012f) + Vector3.right * (-s * .006f), new Vector3(.04f, .024f, (b - a).magnitude + .12f), rot, 2);
                }

                // 东墙上的木扶手 + 四个铁托架
                float hx = x + w / 2f - .085f;
                var h0 = new Vector3(hx, floor + rise + .88f, z0 + .1f);
                var h1 = new Vector3(hx, top + .88f, z0 + len - tread + .1f);
                Tube(mb, D.Post, new List<Vector3> { h0 - along * .12f, h0, h1, h1 + along * .12f }, .022f, 8);
                mb.Sphere(D.Post, h0 - along * .12f, Vector3.one * .023f, 8);
                mb.Sphere(D.Post, h1 + along * .12f, Vector3.one * .023f, 8);
                for (int k = 0; k < 4; k++)
                {
                    var p = Vector3.Lerp(h0, h1, (k + .5f) / 4f);
                    mb.Box(D.Iron, p + new Vector3(.04f, -.03f, 0f), new Vector3(.08f, .012f, .02f));
                    mb.Box(D.Iron, p + new Vector3(.076f, -.06f, 0f), new Vector3(.012f, .07f, .03f));
                }

                // 审图：从楼梯脚往上看；贴着西墙看踏步和墙的交接
                ShotDir("r5s_" + roomId, new Vector3(x, floor + 1.3f, z0 + 2.2f), Vector3.back, 2.6f, .4f);
                ShotDir("r5s_" + roomId + "_wall", new Vector3(x - w / 2f + .25f, floor + .8f, z0 + 1.4f), new Vector3(.9f, 0f, -1f), 1.5f, .5f);
            }
            mb.Build(parent, "StairFlights");
        }
    }
}
