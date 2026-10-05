using System.Collections.Generic;
using UnityEngine;

namespace MazeRunner
{
    /// <summary>
    /// Plans wall shifts that never break the maze: each swap opens a closed wall (creating a loop)
    /// and closes one passage on that loop. Removing an edge from a cycle cannot disconnect the graph,
    /// so every cell — and the exit — stays reachable after any number of shifts.
    /// </summary>
    public static class ShiftPlanner
    {
        public readonly struct Swap
        {
            public readonly Edge Open, Close;
            public Swap(Edge open, Edge close) { Open = open; Close = close; }
        }

        public static List<Swap> Plan(MazeData m, System.Random rng, int swaps, IReadOnlyList<Vector2Int> protectedCells)
        {
            var result = new List<Swap>(swaps);
            var used = new HashSet<Edge>();
            var closed = new List<Edge>();
            m.ForEachEdge(e =>
            {
                if (m.IsShiftable(e) && m.HasWall(e) && !Near(e, protectedCells)) closed.Add(e);
            });

            for (int s = 0; s < swaps && closed.Count > 0; s++)
            {
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    var open = closed[rng.Next(closed.Count)];
                    if (used.Contains(open)) continue;

                    var path = MazePath.Find(m, open.CellA, open.CellB);
                    if (path == null || path.Count < 4) continue; // tiny loops are invisible changes

                    var candidates = new List<Edge>();
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        var e = EdgeBetween(path[i], path[i + 1]);
                        if (m.IsShiftable(e) && !used.Contains(e) && !Near(e, protectedCells)) candidates.Add(e);
                    }
                    if (candidates.Count == 0) continue;

                    var close = candidates[rng.Next(candidates.Count)];
                    // Apply tentatively so the next swap plans against the updated graph.
                    m.SetWall(open, false, notify: false);
                    m.SetWall(close, true, notify: false);
                    used.Add(open); used.Add(close);
                    closed.Remove(open);
                    result.Add(new Swap(open, close));
                    break;
                }
            }

            // Revert — the caller applies the plan later (after the on-screen warning).
            for (int i = result.Count - 1; i >= 0; i--)
            {
                m.SetWall(result[i].Close, false, notify: false);
                m.SetWall(result[i].Open, true, notify: false);
            }
            return result;
        }

        public static Edge EdgeBetween(Vector2Int a, Vector2Int b)
        {
            var d = b - a;
            if (d.x == 1) return Edge.Between(a, Dir.E);
            if (d.x == -1) return Edge.Between(a, Dir.W);
            if (d.y == 1) return Edge.Between(a, Dir.N);
            return Edge.Between(a, Dir.S);
        }

        static bool Near(Edge e, IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null) return false;
            foreach (var c in cells)
            {
                var a = e.CellA; var b = e.CellB;
                if (Cheb(a, c) <= 1 || Cheb(b, c) <= 1) return true;
            }
            return false;
        }

        static int Cheb(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
