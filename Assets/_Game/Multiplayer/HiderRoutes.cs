using System.Collections.Generic;
using HakoOnna.Art;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    // Within a room every box is reachable. Between rooms use the closest pair of boxes.
    internal sealed class HiderRoutes
    {
        private readonly bool[,] links;
        internal bool Allows(int from, int to) => from >= 0 && to >= 0 &&
            from < links.GetLength(0) && to < links.GetLength(1) && links[from, to];

        internal HiderRoutes(ContainerVisual[] boxes)
        {
            int count = boxes.Length;
            links = new bool[count, count];
            var rooms = new string[count];
            var component = new int[count];
            var bridges = new Dictionary<(string, string), (int a, int b, float cost)>();
            for (int i = 0; i < count; i++)
            {
                rooms[i] = boxes[i].transform.parent.name.Split('_')[0];
                component[i] = i;
                for (int j = 0; j < i; j++)
                {
                    if (rooms[i] == rooms[j]) { Link(i, j); continue; }
                    var key = string.CompareOrdinal(rooms[i], rooms[j]) < 0 ? (rooms[i], rooms[j]) : (rooms[j], rooms[i]);
                    var delta = boxes[i].transform.position - boxes[j].transform.position;
                    delta.y *= 2f; // Prefer a short horizontal crossing over moving through a floor.
                    float cost = delta.sqrMagnitude;
                    if (!bridges.TryGetValue(key, out var old) || cost < old.cost) bridges[key] = (i, j, cost);
                }
            }
            var edges = new List<(int a, int b, float cost)>(bridges.Values);
            edges.Sort((a,b) => a.cost.CompareTo(b.cost));
            foreach (var edge in edges)
                if (edge.cost <= 36f || component[edge.a] != component[edge.b]) Link(edge.a, edge.b);

            void Link(int a, int b)
            {
                links[a,b] = links[b,a] = true;
                int before = component[b], after = component[a];
                if (before != after)
                    for (int k = 0; k < count; k++) if (component[k] == before) component[k] = after;
            }
        }
    }
}
