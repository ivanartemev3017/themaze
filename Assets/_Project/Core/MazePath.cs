using System.Collections.Generic;
using UnityEngine;

namespace MazeRunner
{
    /// <summary>Breadth-first search helpers over <see cref="MazeData"/>.</summary>
    public static class MazePath
    {
        /// <summary>Distance in cells from <paramref name="from"/> to every cell; -1 when unreachable.</summary>
        public static int[,] Distances(MazeData m, Vector2Int from)
        {
            var dist = new int[m.W, m.H];
            for (int x = 0; x < m.W; x++) for (int y = 0; y < m.H; y++) dist[x, y] = -1;
            var q = new Queue<Vector2Int>();
            dist[from.x, from.y] = 0;
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in DirExt.All)
                {
                    if (!m.CanMove(c, d)) continue;
                    var n = c + d.Delta();
                    if (dist[n.x, n.y] >= 0) continue;
                    dist[n.x, n.y] = dist[c.x, c.y] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>Shortest path including both ends, or null when unreachable.</summary>
        public static List<Vector2Int> Find(MazeData m, Vector2Int from, Vector2Int to)
        {
            if (!m.InBounds(from) || !m.InBounds(to)) return null;
            var dist = Distances(m, to);
            if (dist[from.x, from.y] < 0) return null;
            var path = new List<Vector2Int> { from };
            var c = from;
            while (c != to)
            {
                foreach (var d in DirExt.All)
                {
                    if (!m.CanMove(c, d)) continue;
                    var n = c + d.Delta();
                    if (dist[n.x, n.y] == dist[c.x, c.y] - 1) { c = n; break; }
                }
                path.Add(c);
            }
            return path;
        }

        public static bool AllConnected(MazeData m)
        {
            var dist = Distances(m, Vector2Int.zero);
            for (int x = 0; x < m.W; x++) for (int y = 0; y < m.H; y++) if (dist[x, y] < 0) return false;
            return true;
        }

        public static Vector2Int Farthest(int[,] dist, System.Func<Vector2Int, bool> filter = null)
        {
            var best = Vector2Int.zero; int bestD = -1;
            for (int x = 0; x < dist.GetLength(0); x++)
            for (int y = 0; y < dist.GetLength(1); y++)
            {
                var c = new Vector2Int(x, y);
                if (dist[x, y] > bestD && (filter == null || filter(c))) { bestD = dist[x, y]; best = c; }
            }
            return best;
        }
    }
}
