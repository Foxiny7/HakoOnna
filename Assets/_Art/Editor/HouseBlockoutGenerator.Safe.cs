using UnityEditor;
using UnityEngine;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 金庫（保险箱，居間北墙，`Gameplay/Safe_Kinko`）：昭和的绿漆铸铁保险箱。
    /// <list type="bullet">
    /// <item>箱体是空心的（五块壁板 + 前面一圈门框），里面一块搁板，钥匙挂点 `KeyAnchor` 正好在搁板上。</item>
    /// <item>`Door`、`Door/Dial`、`Door/Handle` 是三个单独的物体，轴心分别在合页、转盘中心、把手轴上，程序可以直接转：
    ///   门绕 Y 轴 +105° 是开，转盘和把手绕自己的 Z 轴转。</item>
    /// <item>灰盒的 `Body` 方块还在（碰撞体照旧），只是不显示了。</item>
    /// </list>
    /// 尺寸写的是放大以前的（0.6 × 0.83 × 0.55），场景里跟着 `Safe_Kinko` 一起放大。
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        /// <summary>门开到头的角度（绕 Y 轴）。</summary>
        internal const float SafeDoorOpenAngle = 105f;

        static void SafeDetails(Transform root)
        {
            var safe = root.Find("Gameplay/Safe_Kinko");
            if (safe == null) return;
            var blockBody = safe.Find("Body");
            if (blockBody != null && blockBody.TryGetComponent<MeshRenderer>(out var blockRenderer)) blockRenderer.enabled = false;
            var blockDial = safe.Find("Dial");
            if (blockDial != null) Object.DestroyImmediate(blockDial.gameObject);

            const float W = .6f, Dp = .55f, Base = .06f, Top = .8f, Wall = .04f;
            const float zf = Dp / 2;                 // 箱体正面
            const float ox = .22f, oy0 = .15f, oy1 = .71f; // 门洞：x ±0.22，y 0.15–0.71
            float midY = (Base + Top) / 2, bodyH = Top - Base;

            // ---------- 箱体 ----------
            var mb = new MeshBatch();
            BevelBox(mb, D.Iron, new Vector3(0f, Base / 2, 0f), new Vector3(W + .03f, Base, Dp + .03f), .008f);          // 底座
            BevelBox(mb, D.PaintGreen, new Vector3(0f, Top + .014f, 0f), new Vector3(W + .03f, .028f, Dp + .03f), .008f); // 顶盖
            mb.Box(D.PaintGreen, new Vector3(0f, midY, -zf + Wall / 2), new Vector3(W, bodyH, Wall));                      // 背板
            foreach (float s in new[] { -1f, 1f })
                mb.Box(D.PaintGreen, new Vector3(s * (W - Wall) / 2, midY, 0f), new Vector3(Wall, bodyH, Dp));             // 侧板
            mb.Box(D.PaintGreen, new Vector3(0f, Top - Wall / 2, 0f), new Vector3(W - Wall * 2, Wall, Dp));                // 顶板
            mb.Box(D.PaintGreen, new Vector3(0f, Base + Wall / 2, 0f), new Vector3(W - Wall * 2, Wall, Dp));               // 底板
            // 门框：门洞四周
            float stile = W / 2 - ox;
            foreach (float s in new[] { -1f, 1f })
                mb.Box(D.PaintGreen, new Vector3(s * (ox + stile / 2), midY, zf - Wall / 2), new Vector3(stile, bodyH, Wall));
            mb.Box(D.PaintGreen, new Vector3(0f, (Base + oy0) / 2, zf - Wall / 2), new Vector3(ox * 2, oy0 - Base, Wall));
            mb.Box(D.PaintGreen, new Vector3(0f, (oy1 + Top) / 2, zf - Wall / 2), new Vector3(ox * 2, Top - oy1, Wall));
            // 里面：发黑的内衬、一块铁搁板（钥匙放在上面）
            mb.Box(D.Black, new Vector3(0f, midY, -zf + Wall + .002f), new Vector3(W - Wall * 2, bodyH - Wall * 2, .004f));
            foreach (float s in new[] { -1f, 1f })
                mb.Box(D.Black, new Vector3(s * (W / 2 - Wall - .002f), midY, 0f), new Vector3(.004f, bodyH - Wall * 2, Dp - Wall * 2));
            mb.Box(D.Black, new Vector3(0f, Base + Wall + .002f, 0f), new Vector3(W - Wall * 2, .004f, Dp - Wall * 2));
            mb.Box(D.Iron, new Vector3(0f, .28f, -.01f), new Vector3(W - Wall * 2, .02f, Dp - Wall * 2 - .06f));
            mb.Box(D.Iron, new Vector3(0f, .262f, zf - Wall - .05f), new Vector3(W - Wall * 2, .016f, .012f));             // 搁板前沿的折边
            // 合页：固定在箱体上的两节
            const float hx = .245f, hz = .3f;
            foreach (float y in new[] { .2f, .59f })
            {
                mb.Cyl(D.Iron, new Vector3(hx, y, hz), .014f, .014f, .07f, 10);
                mb.Box(D.Iron, new Vector3(hx + .02f, y + .035f, zf + .012f), new Vector3(.05f, .07f, .024f));
            }
            // 四只铸铁脚轮的护罩（底座四角鼓出来一点）
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    mb.Sphere(D.Iron, new Vector3(sx * (W / 2 - .03f), .03f, sz * (zf - .03f)), new Vector3(.045f, .03f, .045f), 8);
            mb.Build(safe, "Model");

            // ---------- 门 ----------
            var pivot = new Vector3(hx, .43f, hz);
            var dm = new MeshBatch { M = Matrix4x4.Translate(-pivot) };
            const float dz0 = zf, dth = .045f, dzF = dz0 + dth; // 门板：贴着门框正面，厚 4.5 cm
            const float dcy = .43f;
            BevelBox(dm, D.PaintGreen, new Vector3(0f, dcy, dz0 + dth / 2), new Vector3(.46f, .6f, dth), .008f);
            BevelBox(dm, D.PaintGreen, new Vector3(0f, dcy, dzF + .003f), new Vector3(.38f, .52f, .008f), .004f);          // 正面鼓起来的一块
            // 金线：鼓面上描的一圈
            const float gw = .32f, gh = .46f, gz = dzF + .0075f;
            foreach (float s in new[] { -1f, 1f })
            {
                dm.Box(D.Gold, new Vector3(0f, dcy + s * gh / 2, gz), new Vector3(gw + .005f, .005f, .0015f));
                dm.Box(D.Gold, new Vector3(s * gw / 2, dcy, gz), new Vector3(.005f, gh + .005f, .0015f));
            }
            // 铭牌
            dm.Box(D.Brass, new Vector3(0f, .635f, gz + .001f), new Vector3(.12f, .034f, .004f));
            foreach (float s in new[] { -1f, 1f })
                dm.Sphere(D.Iron, new Vector3(s * .05f, .635f, gz + .003f), new Vector3(.004f, .004f, .003f), 6);
            // 转盘外面的刻度圈（固定在门上）和正上方的指针
            var dialAt = new Vector3(0f, .5f, dzF + .007f);
            Lathe(dm, D.Brass, dialAt, AlongZ, new[] { P2(.068f, 0f), P2(.068f, .005f), P2(.052f, .008f), P2(.05f, .002f) }, 28);
            dm.Box(D.Brass, dialAt + new Vector3(0f, .07f, .004f), new Vector3(.008f, .014f, .004f));
            // 钥匙孔的盖片
            var keyAt = new Vector3(.1f, .33f, dzF + .007f);
            dm.Cyl(D.Brass, keyAt, .02f, .018f, .005f, 14, true, AlongZ);
            dm.Box(D.Black, keyAt + new Vector3(0f, -.002f, .0055f), new Vector3(.005f, .016f, .001f));
            dm.Cyl(D.Black, keyAt + new Vector3(0f, .005f, .005f), .005f, .005f, .001f, 8, true, AlongZ);
            // 合页：跟着门走的两节 + 压在门板上的铁片
            foreach (float y in new[] { .27f, .66f })
            {
                dm.Cyl(D.Iron, new Vector3(hx, y, hz), .014f, .014f, .07f, 10);
                dm.Box(D.Iron, new Vector3(hx - .045f, y + .035f, dzF + .002f), new Vector3(.09f, .07f, .005f));
            }
            // 门的里面：塞进门洞的一截、锁机盖板、三根锁栓（在开合那一侧）
            dm.Box(D.PaintGreen, new Vector3(0f, dcy, dz0 - .013f), new Vector3(.43f, .55f, .026f));
            dm.Box(D.Iron, new Vector3(0f, dcy, dz0 - .028f), new Vector3(.36f, .46f, .004f));
            foreach (float y in new[] { .28f, .43f, .58f })
                dm.Cyl(D.Iron, new Vector3(-.212f, y, dz0 - .013f), .011f, .011f, .016f, 8, true, Quaternion.Euler(0f, 0f, 90f));
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sy in new[] { -1f, 1f })
                    dm.Sphere(D.Iron, new Vector3(sx * .16f, dcy + sy * .21f, dz0 - .03f), new Vector3(.007f, .007f, .004f), 6);
            var door = dm.Build(safe, "Door");
            door.transform.localPosition = pivot;

            // ---------- 转盘 ----------
            var tm = new MeshBatch { M = Matrix4x4.Translate(-dialAt) };
            Lathe(tm, D.Black, dialAt, AlongZ, new[]
            {
                P2(.046f, .003f), P2(.048f, .013f), P2(.043f, .021f), P2(.03f, .023f), P2(.025f, .04f), P2(.021f, .044f), P2(0f, .044f),
            }, 28);
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI * 2f / 20f;
                var dir = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
                bool major = i % 5 == 0;
                tm.Box(D.Brass, dialAt + dir * .037f + Vector3.forward * .0225f, new Vector3(major ? .003f : .0018f, major ? .011f : .007f, .001f),
                    Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg));
            }
            for (int i = 0; i < 16; i++) // 捏手上的防滑棱
            {
                float a = i * Mathf.PI * 2f / 16f;
                tm.Box(D.Black, dialAt + new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * .0245f + Vector3.forward * .032f, new Vector3(.003f, .003f, .016f),
                    Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg));
            }
            var dial = tm.Build(safe, "Dial");
            dial.transform.localPosition = dialAt;
            dial.transform.SetParent(door.transform, true);

            // ---------- 把手 ----------
            var handleAt = new Vector3(-.09f, .33f, dzF + .007f);
            var hm = new MeshBatch { M = Matrix4x4.Translate(-handleAt) };
            Lathe(hm, D.Brass, handleAt, AlongZ, new[] { P2(.03f, 0f), P2(.03f, .007f), P2(.019f, .013f), P2(.016f, .04f), P2(0f, .04f) }, 18);
            BevelBox(hm, D.Brass, handleAt + new Vector3(0f, -.05f, .031f), new Vector3(.02f, .12f, .013f), .004f);
            hm.Sphere(D.Brass, handleAt + new Vector3(0f, -.112f, .031f), new Vector3(.015f, .015f, .012f), 10);
            var handle = hm.Build(safe, "Handle");
            handle.transform.localPosition = handleAt;
            handle.transform.SetParent(door.transform, true);

            // 会动的部件不能参加静态合批
            foreach (var go in new[] { door, dial, handle })
                GameObjectUtility.SetStaticEditorFlags(go, 0);

            var look = safe.TransformPoint(new Vector3(0f, .45f, 0f));
            ShotDir("r5k_safe", look, Quaternion.Euler(0f, -20f, 0f) * safe.forward, 1.5f, .35f);
            ShotDir("r5k_safe_open", look, Quaternion.Euler(0f, -20f, 0f) * safe.forward, 1.5f, .35f);
        }
    }
}
