using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 3D 地图（HouseMapProxy）的审图截图：箱女视图一张、探索者视图五张，存到 Docs/screenshots/detail/map_*.png。
    /// 相机照 NetworkHider.Views.cs 的地图相机（纯色底、关后处理、视角 60°）。
    /// 游戏里的房间名、地点名是玩法用 OnGUI 写的，相机拍不到：每张图旁边存一个同名 .json，记下字在图上的位置
    /// （照 NetworkHider.Views.cs 的写法：锚点往上 1 m，Rect(p.x - 25, 高 - p.y)），再用 Tools~/map_labels.py 叠上字。
    /// 拍完把地图恢复成生成时的样子（关着、不错开、箱女视图、没有高亮、没有颜色），不存场景。
    /// 命令行：Unity -batchmode -executeMethod HakoOnna.Art.EditorTools.MapSnapshot.GenerateAndShoot -quit
    /// </summary>
    public static class MapSnapshot
    {
        const string OutDir = "Docs/screenshots/detail";
        const int W = 1600, H = 900;
        static readonly Color Background = new Color(.025f, .032f, .04f);

        public static void GenerateAndShoot()
        {
            HouseBlockoutGenerator.Generate();
            Shoot();
        }

        [MenuItem("Hako/Art/9c 3D 地图截图检查（箱女 / 探索者视图）", priority = 92)]
        public static void Shoot()
        {
            var map = Object.FindFirstObjectByType<HouseMapProxy>(FindObjectsInactive.Include);
            if (map == null || map.graph == null)
            {
                Debug.LogWarning("[Hako Art] 地图截图：场景里没有 HouseMapProxy / HouseGraph，先生成场景");
                return;
            }
            Directory.CreateDirectory(OutDir);
            var cam = new GameObject("MapReviewCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 200f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var graph = map.graph;
            int shots = 0;
            try
            {
                map.Show(true);

                // ---- 箱女视图：和 NetworkHider 打开地图时一样（错开 5 m、全部显示、每帧按能不能转移上色）----
                map.SetExplorerView(false);
                map.Explode(5f, 5f);
                Filter(map, 2);
                int ima = RoomIndex(graph, "Ima");
                int current = FirstContainerIn(graph, ima);
                TintLikeHider(map, current);
                Orbit(cam, RendererCenter(map), 35f, 50f, 56f);
                Render(cam, map, "map_ghost_all", $"箱女视图（不变）· 全部 · 箱女在 {Label(graph, ima)} 的容器里", -1);
                shots++;

                // ---- 探索者视图 ----
                map.SetExplorerView(true);
                map.Explode(5f, 0f);
                // 开过的由玩法按每个人自己的记录刷绿；审图随便挑几个：玄関、台所的全部 + 居間的第一个
                int green = 0;
                for (int i = 0; i < graph.containers.Length; i++)
                {
                    int ri = graph.containerRoom[i];
                    bool opened = ri >= 0 && (graph.rooms[ri].id == "Genkan" || graph.rooms[ri].id == "Daidokoro" || i == current);
                    if (!opened) continue;
                    map.Tint(i, HouseMapProxy.ExplorerOpened);
                    green++;
                }

                var views = new (string file, int floor, string here, float yaw, float pitch, float dist, bool onRoom, string title)[]
                {
                    ("map_explorer_1F_ima", 0, "Ima", 0f, 55f, 28f, true, "探索者视图 · 一楼（Z）· 你在 居間 · 朝北"),
                    ("map_explorer_1F_yard", 0, "Daidokoro", 0f, 60f, 62f, false, "探索者视图 · 一楼 + 院子 · 你在 台所"),
                    ("map_explorer_2F", 1, "Kodomo", 180f, 55f, 30f, false, "探索者视图 · 二楼（X）· 你在 子供部屋 · 朝南"),
                    ("map_explorer_all", 2, "Ima", 35f, 45f, 44f, false, "探索者视图 · 全部（C）· 二楼抬高 5 m、土蔵不动 · 你在 居間"),
                    ("map_explorer_top", 0, "Genkan", 0f, 88f, 60f, false, "探索者视图 · 一楼正上方 · 你在 玄関"),
                };
                foreach (var v in views)
                {
                    Filter(map, v.floor);
                    int here = RoomIndex(graph, v.here);
                    map.Highlight(here);
                    var center = v.onRoom && here >= 0 ? map.roomAnchors[here].position : RendererCenter(map);
                    Orbit(cam, center, v.yaw, v.pitch, v.dist);
                    Render(cam, map, v.file, v.title, here);
                    shots++;
                }
                Debug.Log($"[Hako Art] 地图截图：{shots} 张（{OutDir}/map_*.png + .json），探索者视图里刷绿的容器 {green} 个");
            }
            finally
            {
                // 恢复成生成时的样子：先在开着的时候复原（Refresh 要看锚点开没开），最后整个关掉
                for (int g = 0; g < map.groups.Length; g++) map.ShowGroup(g, true);
                map.SetExplorerView(false);
                map.Explode(0f, 0f);
                map.Show(false);
                Object.DestroyImmediate(cam.gameObject);
            }
        }

        static void Filter(HouseMapProxy map, int floor)
        {
            map.ShowGroup(0, floor != 1);
            map.ShowGroup(1, floor != 0);
            map.ShowGroup(2, floor != 1);
        }

        static int RoomIndex(HouseGraph graph, string id)
        {
            for (int i = 0; i < graph.rooms.Length; i++)
                if (graph.rooms[i].id == id) return i;
            return -1;
        }

        static string Label(HouseGraph graph, int room) => room >= 0 ? graph.rooms[room].label : "?";

        static int FirstContainerIn(HouseGraph graph, int room)
        {
            for (int i = 0; i < graph.containerRoom.Length; i++)
                if (graph.containerRoom[i] == room) return i;
            return 0;
        }

        /// <summary>照 NetworkHider.Views.cs 上色：自己红、同房间黄、隔墙绿、楼梯 / 后院青、够不着灰（审图里不算能量和占用）。</summary>
        static void TintLikeHider(HouseMapProxy map, int current)
        {
            var graph = map.graph;
            int from = graph.containerRoom[current];
            for (int i = 0; i < map.containerMarkers.Length; i++)
            {
                int to = graph.containerRoom[i];
                Color c;
                if (i == current) c = Color.red;
                else if (to == from) c = Color.yellow;
                else if (graph.TryGetLink(from, to, out var kind)) c = kind == HouseGraph.LinkKind.Wall ? Color.green : Color.cyan;
                else c = Color.gray;
                map.Tint(i, c);
            }
        }

        static Vector3 RendererCenter(HouseMapProxy map)
        {
            var b = new Bounds();
            bool any = false;
            foreach (var r in map.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b.center;
        }

        /// <summary>和 NetworkHider 的地图相机一样绕着中心转：Euler(俯角, 朝向, 0)，往后退 dist。</summary>
        static void Orbit(Camera cam, Vector3 center, float yaw, float pitch, float dist)
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.SetPositionAndRotation(center - rot * Vector3.forward * dist, rot);
        }

        static void Render(Camera cam, HouseMapProxy map, string file, string title, int here)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            string labels = LabelsJson(cam, map, here);
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            try
            {
                File.WriteAllBytes(Path.Combine(OutDir, file + ".png"), tex.EncodeToPNG());
                File.WriteAllText(Path.Combine(OutDir, file + ".json"),
                    $"{{\"file\":\"{file}\",\"title\":\"{title}\",\"width\":{W},\"height\":{H},\"labels\":[{labels}]}}", new UTF8Encoding(false));
            }
            catch (IOException e) { Debug.LogWarning($"[Hako Art] 地图截图 {file} 写不进去（文件被占用？），跳过：{e.Message}"); }
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        /// <summary>房间名（探索者视图跳过没有地板的楼梯井）和地点名（只有探索者视图有）在图上的位置。</summary>
        static string LabelsJson(Camera cam, HouseMapProxy map, int here)
        {
            var sb = new StringBuilder();
            void Add(Vector3 anchor, string text, string kind)
            {
                var p = cam.WorldToScreenPoint(anchor + Vector3.up);
                if (p.z <= 0f) return;
                if (sb.Length > 0) sb.Append(',');
                // 不用 string.Format：格式说明里的 "}}" 会被当成转义，数字写不出来
                sb.Append("{\"text\":\"").Append(text).Append("\",\"kind\":\"").Append(kind)
                  .Append("\",\"x\":").Append((p.x - 25f).ToString("F1", CultureInfo.InvariantCulture))
                  .Append(",\"y\":").Append((H - p.y).ToString("F1", CultureInfo.InvariantCulture)).Append('}');
            }
            var graph = map.graph;
            for (int i = 0; i < map.roomAnchors.Length; i++)
            {
                var a = map.roomAnchors[i];
                if (!a || !a.gameObject.activeInHierarchy) continue;
                if (map.ExplorerView && (i >= map.roomHighlights.Length || !map.roomHighlights[i])) continue;
                Add(a.position, graph.rooms[i].label, i == here ? "here" : "room");
            }
            if (map.ExplorerView)
                for (int i = 0; i < map.landmarks.Length; i++)
                {
                    var l = map.landmarks[i];
                    if (!l || !l.gameObject.activeInHierarchy) continue;
                    Add(l.position, map.landmarkLabels[i], "landmark");
                }
            return sb.ToString();
        }
    }
}
