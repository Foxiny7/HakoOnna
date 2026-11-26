using UnityEngine;

namespace HakoOnna.Art
{
    /// <summary>
    /// 3D 地图用的简化房子：每个房间一块地板、半人高的矮墙（门洞留空）、每个容器一个小方块、楼梯和后院的连接线。
    /// 由 HouseBlockoutGenerator 生成，挂在 House_Blockout/Gameplay/HouseMapProxy 上。箱女 / 箱人和探索者共用这一个。
    /// <list type="bullet">
    /// <item>它放在真房子正下方很远的地方（见 <see cref="Depth"/>），平时整个物体是关着的，只有打开地图的那台客户端把它打开、用一台单独的相机看它。
    ///   所以不需要专门的层，也不会被别人看到。</item>
    /// <item>材质是纯色的（HakoOnna/MapUnlit）：不受光、不受雾。看它的相机要关掉后期（renderPostProcessing = false），不然会被压得很暗。</item>
    /// <item>一楼、二楼、土蔵各是一个子节点，可以错开摆放（<see cref="Explode"/>）或单独隐藏（<see cref="ShowGroup"/>）。</item>
    /// <item>探索者视图（2026-11-26 起，<see cref="SetExplorerView"/>）：多显示院子地面、围墙、竹篱笆、三个出口、金庫、仏壇；
    ///   后院的两条连接线不画（那是“箱女一次转移够得着”，不是能走的路）；容器先全部刷成黄色（<see cref="ExplorerUnopened"/>），
    ///   开过的由玩法刷成绿色（<see cref="ExplorerOpened"/>，2026-11-26 用户定）。“你在这里”用 <see cref="Highlight"/> 高亮一个房间。</item>
    /// </list>
    /// </summary>
    public sealed class HouseMapProxy : MonoBehaviour
    {
        /// <summary>地图模型比真房子低多少米。</summary>
        public const float Depth = 300f;

        /// <summary>探索者视图里容器的默认颜色：没开过。</summary>
        public static readonly Color ExplorerUnopened = new Color(.95f, .8f, .25f);
        /// <summary>探索者视图里开过的容器的颜色。什么算“开过”由玩法决定（见玩法文档），这里只给颜色。
        /// 比黄色暗一截：分不清红绿的玩家靠亮度也能分开，开过的也就不那么抢眼。</summary>
        public static readonly Color ExplorerOpened = new Color(.22f, .62f, .3f);

        public HouseGraph graph;
        /// <summary>0 = 一楼，1 = 二楼，2 = 土蔵。</summary>
        public Transform[] groups = new Transform[3];
        /// <summary>和 graph.containers 一一对应的小方块。改颜色用 <see cref="Tint"/>。</summary>
        public Renderer[] containerMarkers = System.Array.Empty<Renderer>();
        /// <summary>和 graph.rooms 一一对应：房间地板中心（放房间名用）。</summary>
        public Transform[] roomAnchors = System.Array.Empty<Transform>();
        /// <summary>和 graph.links 一一对应：楼梯、后院的连接线；普通相邻为 null。</summary>
        public LineRenderer[] linkLines = System.Array.Empty<LineRenderer>();

        /// <summary>和 graph.rooms 一一对应：“你在这里”的高亮地板，平时关着。没有地板的房间（二楼的两个楼梯井）为 null。</summary>
        public Renderer[] roomHighlights = System.Array.Empty<Renderer>();
        /// <summary>只在探索者视图里显示的节点（院子地面、围墙、竹篱笆、出口、金庫、仏壇），平时关着。挂在一楼那一组下面，跟着一楼一起显示 / 隐藏。</summary>
        public GameObject[] explorerOnly = System.Array.Empty<GameObject>();
        /// <summary>探索者视图里要写名字的地方（出口、金庫、仏壇），名字在 <see cref="landmarkLabels"/> 里，一一对应。
        /// 和 <see cref="roomAnchors"/> 一样在地面高度，名字照房间名的写法往上抬 1 m 写。</summary>
        public Transform[] landmarks = System.Array.Empty<Transform>();
        public string[] landmarkLabels = System.Array.Empty<string>();

        /// <summary>现在是不是探索者视图。不存进场景，场景加载后总是箱女视图。</summary>
        public bool ExplorerView { get; private set; }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        /// <summary>上一次高亮的房间；-2 = 还没调过（第一次调用时把每块都设一遍）。</summary>
        int highlighted = -2;

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

        /// <summary>把二楼抬高 floorGap 米、把土蔵往外推 kuraGap 米（朝远离主屋的方向），让三部分互不遮挡。传 0 复原。
        /// 探索者视图里土蔵要留在真实位置（kuraGap = 0），院子里的路才对得上。</summary>
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

        /// <summary>“你在这里”：只亮这一个房间的地板；传 -1 全部关掉。可以每帧调，房间没变时什么都不做。</summary>
        public void Highlight(int room)
        {
            if (room == highlighted) return;
            highlighted = room;
            for (int i = 0; i < roomHighlights.Length; i++)
                if (roomHighlights[i]) roomHighlights[i].gameObject.SetActive(i == room);
        }

        /// <summary>
        /// 切换探索者视图 / 箱女视图。每次打开地图时调一次。
        /// 打开（true）：显示探索者专用的东西，隐藏后院连接线，所有容器刷成 <see cref="ExplorerUnopened"/>；之后由玩法把开过的刷成 <see cref="ExplorerOpened"/>。
        /// 关闭（false）：和以前的箱女地图完全一样，容器去掉颜色（箱女那边每帧自己上色）。两种都会关掉高亮。
        /// </summary>
        public void SetExplorerView(bool on)
        {
            ExplorerView = on;
            foreach (var node in explorerOnly)
                if (node) node.SetActive(on);
            for (int i = 0; i < containerMarkers.Length; i++)
            {
                if (!containerMarkers[i]) continue;
                if (on) Tint(i, ExplorerUnopened);
                else containerMarkers[i].SetPropertyBlock(null);
            }
            Highlight(-1);
            Refresh();
        }

        /// <summary>按房间锚点现在的位置重画楼梯 / 后院的连接线（错开、隐藏某一组、切换视图以后调用）。</summary>
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
                // 探索者视图不画后院的线：它表示箱女一次转移够得着，探索者看了会以为風呂能直通土蔵
                bool visible = a && b && a.gameObject.activeInHierarchy && b.gameObject.activeInHierarchy
                               && !(ExplorerView && link.kind == HouseGraph.LinkKind.Yard);
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
