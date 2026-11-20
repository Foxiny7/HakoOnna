using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 防尘布（第三轮，2026-11-19）：有的家具上盖着一块旧布，布从顶面边上包过去往下垂，四角比四边垂得低，碰到地面就堆在地上。
    /// 用户确认可以盖（“不用管，玩家发现没有 E 就不会搜了”）；容器一律不盖（容器要能开，也不能挡）。
    /// 布是算出来的网格（两面都画，掀起的下摆看得到里面），没有碰撞，家具自己的挡块不变。
    /// 盖哪几件：SheetFor（第三遍沿墙补的件）、CZataku（座卓，奥の間和二楼和室）。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>布搭着的形状，全是家具的局部坐标（Kit 当前的变换，+z 朝外）。</summary>
        sealed class SheetSpec
        {
            public float x0, x1, z0, z1, top;          // 布搭着的顶面
            public float drop = .45f, backDrop = .12f;  // 前面、左右垂下的布长；背面（靠墙）垂下的布长
            public float round = .03f;                  // 包过边的圆角（人台的肩膀大）
            public float flare;                         // 往下张开（人台像个钟罩）
            public float cone = .14f;                   // 四角多出来的布斜着往外撑开（每往下垂 1 m 往外多少）
            public bool dustTop = true;                 // 顶面落灰（颜色暗一些）；人台不要，肩膀上一块暗的平顶像个箱子头
            public Rect hump;                           // 顶上凸起的一块（电视机身、缝纫机机头）
            public float humpH, humpFall = .1f;         // 凸起高出顶面多少；离开凸起多远降回顶面
            public float floorY;                        // 地面（榻榻米面比地板高 6.5 mm）
            public float wallZ = float.NegativeInfinity, wallX0 = float.NegativeInfinity, wallX1 = float.PositiveInfinity;
        }

        /// <summary>正在画的这件要盖布：画家具的函数看到它就不画顶上的摆件（会戳穿布），随机数照常消耗，别的东西的位置不变。</summary>
        static bool CoverNext;
        static readonly List<string> SheetLog = new List<string>();

        /// <summary>第三遍沿墙补的件里哪些盖布、盖成什么样（按房间 + 件名 + 位置定，重新生成结果不变）。</summary>
        static SheetSpec SheetFor(FillCtx c, Piece p, Edge e, float s)
        {
            float chance = p.name switch
            {
                "ShowaTV" => .8f,
                "SewingMachine" => .75f,
                "DressForm" => .7f,
                "Console" => .5f,
                "Bench" => .45f,
                _ => 0f,
            };
            if (chance <= 0f) return null;
            var r = new System.Random(StableHash(c.room.id + "/" + p.name) ^ Mathf.RoundToInt(s * 100f));
            if (r.NextDouble() >= chance) return null;

            float S = FurnitureScale;
            // 两边的墙在局部 x 上的位置（角落里的件，布不能穿墙）
            InnerSpan(c.room, e, out float lo, out float hi);
            var localX = Quaternion.Euler(0f, WallYaw(e), 0f) * Vector3.right;
            float sign = Vector3.Dot(localX, e.alongX ? Vector3.right : Vector3.forward) >= 0f ? 1f : -1f;
            float xa = (lo - s) / S * sign, xb = (hi - s) / S * sign;
            var spec = new SheetSpec
            {
                floorY = (IsTatami(c.room) ? .0075f : .003f) / S,
                wallZ = -FillGap / S,
                wallX0 = Mathf.Min(xa, xb),
                wallX1 = Mathf.Max(xa, xb),
            };
            switch (p.name)
            {
                case "ShowaTV":
                {
                    // 矮木台（顶 0.4、0.82 × 0.56）上的显像管电视：布盖住电视，从机身斜着搭到台面边上再垂下来，兔耳天线戳在外面
                    var ms = Kit.ModelSize("Television_01");
                    const float top = .4f, z = .29f, tz = z + .02f;
                    spec.x0 = -.415f; spec.x1 = .415f; spec.z0 = z - .285f; spec.z1 = z + .285f; spec.top = top + .006f;
                    spec.hump = new Rect(-ms.x / 2 - .012f, tz - ms.z / 2 - .012f, ms.x + .024f, ms.z + .024f);
                    spec.humpH = ms.y + .014f; spec.humpFall = .09f;
                    spec.drop = .36f; spec.backDrop = .1f; spec.round = .025f;
                    break;
                }
                case "SewingMachine":
                    // 脚踏缝纫机：布盖着台面和机头，下面露出铁脚架和踏板
                    spec.x0 = -.48f; spec.x1 = .48f; spec.z0 = .005f; spec.z1 = .495f; spec.top = .785f;
                    spec.hump = new Rect(-.24f, .15f, .48f, .2f); spec.humpH = .27f; spec.humpFall = .12f;
                    spec.drop = .44f; spec.backDrop = .14f; spec.round = .03f;
                    break;
                case "DressForm":
                    // 人台：脖子顶上的木帽把布撑起一个尖，布搭过两个肩膀（肩宽 ±0.19）往下垂，像个站着的人；下面露出三脚架。
                    // 第 9 次生成顶面太窄、圆角太大，看着是一颗胶囊；现在顶面就是肩膀那一块，圆角小，脖子单独顶起来
                    spec.x0 = -.14f; spec.x1 = .14f; spec.z0 = .17f; spec.z1 = .33f; spec.top = 1.46f;
                    spec.hump = new Rect(-.05f, .2f, .1f, .1f); spec.humpH = .1f; spec.humpFall = .1f;
                    spec.round = .06f; spec.flare = .03f; spec.cone = .05f; spec.drop = 1.3f; spec.backDrop = 1.3f;
                    spec.dustTop = false;
                    break;
                case "Console":
                    // 玄関、走廊的长桌：布只盖桌面（墙上的画不盖），下层隔板露在下摆下面
                    spec.x0 = -.605f; spec.x1 = .605f; spec.z0 = -.005f; spec.z1 = .405f; spec.top = .806f;
                    spec.drop = .5f; spec.backDrop = .1f; spec.round = .02f;
                    break;
                case "Bench":
                    spec.x0 = -.805f; spec.x1 = .805f; spec.z0 = -.005f; spec.z1 = .425f; spec.top = .476f;
                    spec.drop = .34f; spec.backDrop = .1f; spec.round = .02f;
                    break;
                default:
                    return null;
            }
            // 布不出这件家具自己的宽度（两边可能就是容器，布不能搭到容器上）
            spec.wallX0 = Mathf.Max(spec.wallX0, -p.w / 2 - .02f);
            spec.wallX1 = Mathf.Min(spec.wallX1, p.w / 2 + .02f);
            return spec;
        }

        /// <summary>褶子、下摆的起伏（绕顶面一圈首尾相接）。</summary>
        sealed class SheetFolds
        {
            const float R = .12f; // 算周长时四角当成这个半径的圆角
            readonly SheetSpec s;
            readonly float total, k1, k2, k3, p1, p2, p3, amp, noise;

            public SheetFolds(System.Random rng, SheetSpec spec)
            {
                s = spec;
                total = 2f * (s.x1 - s.x0) + 2f * (s.z1 - s.z0) + 2f * Mathf.PI * R;
                k1 = Mathf.Max(1f, Mathf.Round(total / Rf(rng, .16f, .22f)));
                k2 = Mathf.Max(1f, Mathf.Round(total / Rf(rng, .3f, .42f)));
                k3 = Mathf.Max(1f, Mathf.Round(total / Rf(rng, .5f, .7f)));
                p1 = Rf(rng, 0f, 6.283f); p2 = Rf(rng, 0f, 6.283f); p3 = Rf(rng, 0f, 6.283f);
                amp = Rf(rng, .018f, .028f);
                noise = Rf(rng, 0f, 100f);
            }

            float Wave(float k, float sp, float p) => Mathf.Sin(2f * Mathf.PI * k * sp / total + p);

            /// <summary>竖褶往外鼓多少（不会往里收，免得穿进家具）：褶脊窄、褶沟宽；垂得越长褶子越深。</summary>
            public float Fold(float sp, float fall)
            {
                float w = .5f + .5f * (.6f * Wave(k1, sp, p1) + .4f * Wave(k2, sp, p2));
                return amp * w * w * (.7f + 1.8f * Mathf.Min(fall, 1.1f));
            }

            /// <summary>下摆高低起伏。</summary>
            public float Hem(float sp) => .028f * Wave(k3, sp, p3);

            /// <summary>顶面上的小皱。</summary>
            public float Wrinkle(float x, float z) => .009f * (Mathf.PerlinNoise(x * 4.5f + noise, z * 4.5f - noise) - .5f);

            /// <summary>
            /// 顶面边上最近点 (qx, qz) 沿顶面一圈的弧长（前边从左往右起，顺时针），四角按圆角算，连续、首尾相接。
            /// cornerW：在角上的程度（角的正中 1，两条边上 0），角上布多，褶子大。
            /// </summary>
            public float Perimeter(float qx, float qz, float dx, float dz, out float cornerW)
            {
                float sx = s.x1 - s.x0, sz = s.z1 - s.z0, q = R * Mathf.PI / 2f;
                cornerW = 0f;
                bool l = dx < 0f, r = dx > 0f, b = dz < 0f, f = dz > 0f;
                float a = Mathf.Atan2(dz, dx);
                if (f && !l && !r) return qx - s.x0;                                                       // 前边
                if (f && r) { cornerW = Mathf.Sin(2f * a); return sx + (Mathf.PI / 2f - a) * R; }          // 右前角
                if (r && !b) return sx + q + (s.z1 - qz);                                                  // 右边
                if (r) { cornerW = Mathf.Sin(-2f * a); return sx + q + sz - a * R; }                        // 右后角
                if (b && !l) return sx + sz + 2f * q + (s.x1 - qx);                                        // 后边
                if (b) { cornerW = Mathf.Sin(2f * (a + Mathf.PI)); return 2f * sx + sz + 2f * q + (-Mathf.PI / 2f - a) * R; } // 左后角
                if (!f) return 2f * sx + sz + 3f * q + (qz - s.z0);                                       // 左边
                cornerW = Mathf.Sin(2f * (a - Mathf.PI / 2f));                                              // 左前角
                return 2f * sx + 2f * sz + 3f * q + (Mathf.PI - a) * R;
            }
        }

        static float SheetTop(SheetSpec s, float x, float z)
        {
            float h = s.top;
            if (s.humpH > 0f)
            {
                float ox = Mathf.Max(0f, Mathf.Max(s.hump.xMin - x, x - s.hump.xMax));
                float oz = Mathf.Max(0f, Mathf.Max(s.hump.yMin - z, z - s.hump.yMax));
                float t = 1f - Mathf.Clamp01(Mathf.Sqrt(ox * ox + oz * oz) / s.humpFall);
                h += s.humpH * t * t * (3f - 2f * t);
            }
            return h;
        }

        /// <summary>软的上限：离上限 k 以内开始慢慢压扁，永远不超过上限（布贴着墙、贴着旁边的家具压平，不会切出一道直边）。</summary>
        static float SoftUpper(float x, float lim, float k)
        {
            float a = lim - k;
            return x <= a ? x : lim - k * Mathf.Exp((a - x) / k);
        }

        /// <summary>
        /// 摊平的布上一点 (u, v) 盖上去以后的位置：顶面以内贴着顶面；以外先绕过圆角，再往下垂（带竖褶、下摆起伏），
        /// 垂到地面以下的部分堆在地上往外摊。四角的布比四边长一点（最多长 12%、最多 7 cm，角上拖一个尖），
        /// 而且从角上斜着往外撑开成一个锥。
        /// </summary>
        static Vector3 SheetPoint(SheetSpec s, SheetFolds f, float u, float v)
        {
            float qx = Mathf.Clamp(u, s.x0, s.x1), qz = Mathf.Clamp(v, s.z0, s.z1);
            float dx = u - qx, dz = v - qz;
            float e = Mathf.Sqrt(dx * dx + dz * dz);
            float top = SheetTop(s, qx, qz) + f.Wrinkle(qx, qz);
            if (e < 1e-6f) return new Vector3(u, top, v);
            var n = new Vector2(dx, dz) / e;
            float md = Mathf.Max(s.drop, s.backDrop);
            if (dx != 0f && dz != 0f) e = Mathf.Min(e, md + Mathf.Min(.12f * md, .07f));
            float rr = Mathf.Max(.005f, s.round), arc = rr * Mathf.PI / 2f;
            float off, fall;
            if (e <= arc)
            {
                float ang = e / rr;
                off = rr * Mathf.Sin(ang);
                fall = rr * (1f - Mathf.Cos(ang));
            }
            else
            {
                float t = e - arc;
                off = rr + t * s.flare;
                fall = rr + t * Mathf.Sqrt(1f - s.flare * s.flare);
            }
            float sp = f.Perimeter(qx, qz, dx, dz, out float cornerW);
            // 角上多出来的布从桌角斜着往外撑开成一个锥（桌布挂在桌角上的样子），不是贴着桌角直上直下
            // （第 9 次生成角上贴得太紧，褶子又大，看着是一道深色的裂缝）
            off += s.cone * cornerW * Mathf.Max(0f, fall - rr);
            float hang = Mathf.Clamp01((fall - rr * .5f) / .25f);
            hang = hang * hang * (3f - 2f * hang);
            off += hang * f.Fold(sp, fall) * (1f + .7f * cornerW);
            fall += hang * f.Hem(sp) * Mathf.Clamp01((fall - .1f) / .4f);
            float y = top - fall, floor = s.floorY + .003f;
            if (y < floor)
            {
                float extra = floor - y;
                y = floor + .0015f * (1f + Mathf.Sin(sp * 23f));
                off += Mathf.Min(extra, .22f) * .8f;
            }
            var p = new Vector3(qx + n.x * off, y, qz + n.y * off);
            const float knee = .015f;
            p.x = SoftUpper(p.x, s.wallX1 - .006f, knee);
            p.x = -SoftUpper(-p.x, -(s.wallX0 + .006f), knee);
            p.z = -SoftUpper(-p.z, -(s.wallZ + .006f), knee);
            return p;
        }

        /// <summary>
        /// 摊平的布在一个方向上的网格线：顶面的两条边、包边圆角的终点、凸起的边和凸起降回顶面的地方都正好落在线上，
        /// 圆角上分得细（≤ 1.6 cm），别处 ≤ 4.5 cm。原来是均匀的格子，跨在顶面边上的那一格是一块平的斜面，
        /// 会切进家具的边角（第 9 次生成：长桌的桌角、桌边从布里戳出来一道）。
        /// </summary>
        static List<float> SheetAxis(float lo, float a, float b, float hi, float arc, float h0, float h1, float hf)
        {
            const float step = .045f, fine = .016f;
            var marks = new List<float> { lo, a, b, hi };
            if (a - arc > lo) marks.Add(a - arc);
            if (b + arc < hi) marks.Add(b + arc);
            if (hf > 0f)
                foreach (float x in new[] { h0 - hf, h0, h1, h1 + hf })
                    if (x > a && x < b) marks.Add(x);
            marks.Sort();
            var xs = new List<float>();
            for (int i = 0; i + 1 < marks.Count; i++)
            {
                float x0 = marks[i], x1 = marks[i + 1], len = x1 - x0;
                if (len < 1e-4f) continue;
                bool wrap = (x1 <= a + 1e-5f && x0 >= a - arc - 1e-5f) || (x0 >= b - 1e-5f && x1 <= b + arc + 1e-5f);
                int n = Mathf.Max(1, Mathf.CeilToInt(len / (wrap ? fine : step) - 1e-3f));
                for (int j = 0; j < n; j++) xs.Add(x0 + len * j / n);
            }
            xs.Add(marks[marks.Count - 1]);
            return xs;
        }

        /// <summary>按 SheetSpec 盖一块布（网格线对齐顶面的边，约 4.5 cm 一格、圆角上更细，逐顶点法线，两面都画）。</summary>
        static void DustSheet(Kit k, System.Random rng, SheetSpec s)
        {
            var m = rng.NextDouble() < .5 ? D.DustSheet : D.DustSheetOld;
            var folds = new SheetFolds(rng, s);
            float arc = Mathf.Max(.005f, s.round) * Mathf.PI / 2f, hf = s.humpH > 0f ? s.humpFall : 0f;
            var us = SheetAxis(s.x0 - s.drop, s.x0, s.x1, s.x1 + s.drop, arc, s.hump.xMin, s.hump.xMax, hf);
            var vs = SheetAxis(s.z0 - s.backDrop, s.z0, s.z1, s.z1 + s.drop, arc, s.hump.yMin, s.hump.yMax, hf);
            int nu = us.Count - 1, nv = vs.Count - 1;
            var P = new Vector3[nu + 1, nv + 1];
            var U = new Vector2[nu + 1, nv + 1];
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    float u = us[i], v = vs[j];
                    P[i, j] = SheetPoint(s, folds, u, v);
                    U[i, j] = new Vector2(u, v);
                }
            var N = new Vector3[nu + 1, nv + 1];
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    var du = P[Mathf.Min(i + 1, nu), j] - P[Mathf.Max(i - 1, 0), j];
                    var dv = P[i, Mathf.Min(j + 1, nv)] - P[i, Mathf.Max(j - 1, 0)];
                    var nn = Vector3.Cross(dv, du);
                    N[i, j] = nn.sqrMagnitude > 1e-14f ? nn.normalized : Vector3.up;
                }
            bool OnTop(int i, int j) => U[i, j].x >= s.x0 - 1e-4f && U[i, j].x <= s.x1 + 1e-4f && U[i, j].y >= s.z0 - 1e-4f && U[i, j].y <= s.z1 + 1e-4f;
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    Vector3 a = P[i, j], b = P[i + 1, j], c = P[i + 1, j + 1], d = P[i, j + 1];
                    if ((c - a).sqrMagnitude < 1e-10f && (d - b).sqrMagnitude < 1e-10f) continue; // 角上收成一点的格子
                    Vector3 na = N[i, j], nb = N[i + 1, j], nc = N[i + 1, j + 1], nd = N[i, j + 1];
                    Vector2 ua = U[i, j], ub = U[i + 1, j], uc = U[i + 1, j + 1], ud = U[i, j + 1];
                    // 顶面（四个角都还在顶面上的格子）落了一层灰，颜色暗一些；里面一律用布本来的颜色
                    var outer = s.dustTop && OnTop(i, j) && OnTop(i + 1, j) && OnTop(i + 1, j + 1) && OnTop(i, j + 1) ? D.DustSheetTop : m;
                    k.mb.TriS(outer, a, b, c, na, nb, nc, ua, ub, uc);
                    k.mb.TriS(outer, a, c, d, na, nc, nd, ua, uc, ud);
                    k.mb.TriS(m, a, c, b, -na, -nc, -nb, ua, uc, ub);
                    k.mb.TriS(m, a, d, c, -na, -nd, -nc, ua, ud, uc);
                }
        }
    }
}
