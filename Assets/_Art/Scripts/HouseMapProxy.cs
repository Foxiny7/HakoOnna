using UnityEngine;

namespace HakoOnna.Art
{
    /// <summary>
    /// 3D 地图用的简化房子（箱女 / 箱人的俯瞰地图）：每个房间一块地板、半人高的矮墙（门洞留空）、每个容器一个小方块、
    /// 楼梯和后院的连接线。由 HouseBlockoutGenerator 生成，挂在 House_Blockout/Gameplay/HouseMapProxy 上。
    /// <list type="bullet">
    /// <item>它放在真房子正下方很远的地方（见 <see cref="Depth"/>），平时整个物体是关着的，只有打开地图的那台客户端把它打开、用一台单独的相机看它。
    ///   所以不需要专门的层，也不会被探索者看到。</item>
    /// <item>材质是纯色的（HakoOnna/MapUnlit）：不受光、不受雾。看它的相机要关掉后期（renderPostProcessing = false），不然会被压得很暗。</item>
    /// <item>一楼、二楼、土蔵各是一个子节点，可以错开摆放（<see cref="Explode"/>）或单独隐藏（<see cref="ShowGroup"/>）。</item>
    /// </list>
    /// </summary>
    public sealed class HouseMapProxy : MonoBehaviour
    {
        /// <summary>地图模型比真房子低多少米。</summary>
        public const float Depth = 300f;

        public HouseGraph graph;
        /// <summary>0 = 一楼，1 = 二楼，2 = 土蔵。</summary>
        public Transform[] groups = new Transform[3];
        /// <summary>和 graph.containers 一一对应的小方块。改颜色用 <see cref="Tint"/>。</summary>
        public Renderer[] containerMarkers = System.Array.Empty<Renderer>();
        /// <summary>和 graph.rooms 一一对应：房间地板中心（放房间名用）。</summary>
        public Transform[] roomAnchors = System.Array.Empty<Transform>();
        /// <summary>和 graph.links 一一对应：楼梯、后院的连接线；普通相邻为 null。</summary>
        public LineRenderer[] linkLines = System.Array.Empty<LineRenderer>();

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;

        /// <summary>地图里某个容器对应的小方块的位置（世界坐标，已含错开的偏移）。</summary>
        public Vector3 MarkerPosition(int container) => containerMarkers[container].bounds.center;

        /// <summary>开关整张地图。</summary>
        public void Show(bool on)
        {
            gameObject.SetActive(on);
            if (on) Refresh();
        }

        /// <summary>单独显示 / 隐藏一楼（0）、二楼（1）、土蔵（2）。</summary>
        public void ShowGroup(int group, bool on)
        {
            if (group < 0 || group >= groups.Length || !groups[group]) return;
            groups[group].gameObject.SetActive(on);
            Refresh();
        }

        /// <summary>把二楼抬高 floorGap 米、把土蔵往外推 kuraGap 米（朝远离主屋的方向），让三部分互不遮挡。传 0 复原。</summary>
        public void Explode(float floorGap, float kuraGap)
        {
            if (groups[1]) groups[1].localPosition = Vector3.up * floorGap;
            if (groups[2]) groups[2].localPosition = kuraDirection * kuraGap;
            Refresh();
        }

        /// <summary>土蔵相对主屋的水平方向（生成时写好）。</summary>
        public Vector3 kuraDirection = Vector3.forward;

        /// <summary>给一个容器的小方块上色（用 MaterialPropertyBlock，不产生新材质）。</summary>
        public void Tint(int container, Color color)
        {
            if (container < 0 || container >= containerMarkers.Length || !containerMarkers[container]) return;
            block ??= new MaterialPropertyBlock();
            containerMarkers[container].GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            containerMarkers[container].SetPropertyBlock(block);
        }

        /// <summary>按房间锚点现在的位置重画楼梯 / 后院的连接线（错开或隐藏某一组以后调用）。</summary>
        public void Refresh()
        {
            if (!graph) return;
            for (int i = 0; i < linkLines.Length && i < graph.links.Length; i++)
            {
                var line = linkLines[i];
                if (!line) continue;
                var link = graph.links[i];
                var a = roomAnchors[link.a];
                var b = roomAnchors[link.b];
                bool visible = a && b && a.gameObject.activeInHierarchy && b.gameObject.activeInHierarchy;
                line.enabled = visible;
                if (!visible) continue;
                Vector3 p0 = a.position + Vector3.up * .5f, p1 = b.position + Vector3.up * .5f;
                // 一条往上鼓的弧线，看得出是“连过去”的
                float lift = link.kind == HouseGraph.LinkKind.Yard ? Vector3.Distance(p0, p1) * .25f : 1.2f;
                const int n = 12;
                line.positionCount = n + 1;
                for (int k = 0; k <= n; k++)
                {
                    float t = k / (float)n;
                    line.SetPosition(k, Vector3.Lerp(p0, p1, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * lift));
                }
            }
        }
    }
}
