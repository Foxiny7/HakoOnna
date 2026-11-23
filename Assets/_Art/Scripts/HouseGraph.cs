using System;
using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art
{
    /// <summary>
    /// 房间图：这栋房子有哪些房间、哪些房间相邻、每个容器在哪个房间。
    /// 由 HouseBlockoutGenerator 生成场景时写好，挂在 House_Blockout/Gameplay/HouseGraph 上。
    /// 玩法代码只读它，不要再按物体名字推断房间。
    /// <para>相邻（Link）的含义是“藏匿者一次普通转移够得着”：共用一面墙，或者中间只隔一段没有容器的走廊，
    /// 或者由楼梯 / 后院连着。和有没有门无关。</para>
    /// </summary>
    public sealed class HouseGraph : MonoBehaviour
    {
        public enum LinkKind
        {
            /// <summary>同一层楼、同一栋建筑里的相邻房间。</summary>
            Wall,
            /// <summary>楼梯连着的上下两层。</summary>
            Stairs,
            /// <summary>隔着后院的主屋和土蔵。</summary>
            Yard,
        }

        [Serializable]
        public struct RoomInfo
        {
            public string id;        // "Zashiki"
            public string label;     // "座敷"
            public int floor;        // 0 = 一楼，1 = 二楼
            public int building;     // 0 = 主屋，1 = 土蔵
            public Vector3 center;   // 地板中心（世界坐标）
            public Vector2 size;     // x 方向、z 方向的尺寸
            public float ceilingY;   // 天花板高度（世界坐标）
            public bool hasContainers;
            public Transform vantage; // 俯视机位（位置 + 朝向）；没有容器的房间为空
        }

        [Serializable]
        public struct Link
        {
            public int a, b;
            public LinkKind kind;
        }

        public RoomInfo[] rooms = Array.Empty<RoomInfo>();
        public Link[] links = Array.Empty<Link>();
        /// <summary>全部容器，顺序和 House_Blockout/Containers 下 GetComponentsInChildren 的顺序一致。</summary>
        public ContainerVisual[] containers = Array.Empty<ContainerVisual>();
        /// <summary>containers[i] 所在的房间下标。</summary>
        public int[] containerRoom = Array.Empty<int>();

        int[,] hops;

        public int RoomIndex(string id)
        {
            for (int i = 0; i < rooms.Length; i++)
                if (rooms[i].id == id) return i;
            return -1;
        }

        public int RoomOf(int container) => container >= 0 && container < containerRoom.Length ? containerRoom[container] : -1;

        public int RoomOf(ContainerVisual container) => RoomOf(Array.IndexOf(containers, container));

        /// <summary>这一点在哪个房间里（按平面范围和高度判断）；不在任何房间里（院子、屋外）返回 -1。</summary>
        public int RoomAt(Vector3 p)
        {
            int best = -1;
            float bestDrop = float.MaxValue;
            for (int i = 0; i < rooms.Length; i++)
            {
                var r = rooms[i];
                if (Mathf.Abs(p.x - r.center.x) > r.size.x * .5f || Mathf.Abs(p.z - r.center.z) > r.size.y * .5f) continue;
                float drop = p.y - r.center.y;
                if (drop < -.6f || p.y > r.ceilingY + .3f || drop >= bestDrop) continue;
                bestDrop = drop;
                best = i;
            }
            return best;
        }

        /// <summary>两个房间之间的连接；不相邻返回 false。</summary>
        public bool TryGetLink(int roomA, int roomB, out LinkKind kind)
        {
            foreach (var l in links)
                if ((l.a == roomA && l.b == roomB) || (l.a == roomB && l.b == roomA))
                {
                    kind = l.kind;
                    return true;
                }
            kind = LinkKind.Wall;
            return false;
        }

        public bool Adjacent(int roomA, int roomB) => roomA != roomB && TryGetLink(roomA, roomB, out _);

        /// <summary>和这个房间相邻的房间（下标）。</summary>
        public void Neighbours(int room, List<int> result)
        {
            result.Clear();
            foreach (var l in links)
            {
                if (l.a == room) result.Add(l.b);
                else if (l.b == room) result.Add(l.a);
            }
        }

        /// <summary>一次普通转移能不能从容器 from 到容器 to：同房间，或两个房间相邻。</summary>
        public bool CanTransfer(int from, int to)
        {
            int a = RoomOf(from), b = RoomOf(to);
            return from != to && a >= 0 && b >= 0 && (a == b || Adjacent(a, b));
        }

        /// <summary>从 roomA 到 roomB 最少要换几次房间；同一个房间为 0，走不到为 -1。</summary>
        public int Hops(int roomA, int roomB)
        {
            if (roomA < 0 || roomB < 0 || roomA >= rooms.Length || roomB >= rooms.Length) return -1;
            if (hops == null || hops.GetLength(0) != rooms.Length) BuildHops();
            return hops[roomA, roomB];
        }

        /// <summary>从 room 出发到最近的、满足 goal 的房间要换几次房间（自己满足为 0，走不到为 -1）。</summary>
        public int HopsTo(int room, Predicate<int> goal)
        {
            int best = -1;
            for (int i = 0; i < rooms.Length; i++)
            {
                if (!goal(i)) continue;
                int h = Hops(room, i);
                if (h >= 0 && (best < 0 || h < best)) best = h;
            }
            return best;
        }

        /// <summary>
        /// 方向徽标用：从 room 走到相邻的 neighbour，离“满足 goal 的房间”是不是近了一步。
        /// 例：goal = 二楼的房间，返回 true 的那个相邻房间上画一个 ↑。
        /// </summary>
        public bool LeadsToward(int room, int neighbour, Predicate<int> goal)
        {
            int here = HopsTo(room, goal), there = HopsTo(neighbour, goal);
            return here > 0 && there >= 0 && there < here;
        }

        void BuildHops()
        {
            int n = rooms.Length;
            hops = new int[n, n];
            var queue = new Queue<int>();
            var near = new List<int>();
            for (int s = 0; s < n; s++)
            {
                for (int i = 0; i < n; i++) hops[s, i] = -1;
                hops[s, s] = 0;
                queue.Clear();
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int u = queue.Dequeue();
                    Neighbours(u, near);
                    foreach (int v in near)
                    {
                        if (hops[s, v] >= 0) continue;
                        hops[s, v] = hops[s, u] + 1;
                        queue.Enqueue(v);
                    }
                }
            }
        }

        void OnValidate() => hops = null;
    }
}
