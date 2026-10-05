using System.Collections.Generic;
using UnityEngine;

namespace MazeRunner
{
    /// <summary>Seeded maze generation: depth-first backtracker, optional braiding (loops), start/exit, shiftable edges.</summary>
    public static class MazeGen
    {
        public static MazeData Generate(LevelConfig cfg)
        {
            var rng = new System.Random(cfg.Seed);
            var m = new MazeData(cfg.W, cfg.H);

            Carve(m, rng);
            Braid(m, rng, cfg.Braid);

            // Start in a random corner, exit on the border cell farthest from it.
            var corners = new[] { new Vector2Int(0, 0), new Vector2Int(m.W - 1, 0), new Vector2Int(0, m.H - 1), new Vector2Int(m.W - 1, m.H - 1) };
            m.Start = corners[rng.Next(4)];
            var dist = MazePath.Distances(m, m.Start);
            m.Exit = MazePath.Farthest(dist, c => c.x == 0 || c.y == 0 || c.x == m.W - 1 || c.y == m.H - 1);
            m.ExitSide = PickExitSide(m, m.Exit);
            m.SetWall(Edge.Between(m.Exit, m.ExitSide), false, notify: false);

            MarkShiftable(m, rng, cfg.ShiftableFraction);
            return m;
        }

        static void Carve(MazeData m, System.Random rng)
        {
            var visited = new bool[m.W, m.H];
            var stack = new Stack<Vector2Int>();
            var start = new Vector2Int(rng.Next(m.W), rng.Next(m.H));
            visited[start.x, start.y] = true;
            stack.Push(start);
            var options = new List<Dir>(4);

            while (stack.Count > 0)
            {
                var c = stack.Peek();
                options.Clear();
                foreach (var d in DirExt.All)
                {
                    var n = c + d.Delta();
                    if (m.InBounds(n) && !visited[n.x, n.y]) options.Add(d);
                }
                if (options.Count == 0) { stack.Pop(); continue; }

                var dir = options[rng.Next(options.Count)];
                var next = c + dir.Delta();
                m.SetWall(Edge.Between(c, dir), false, notify: false);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }
        }

        /// <summary>Removes a wall from a fraction of dead ends so the maze has loops and real choices.</summary>
        static void Braid(MazeData m, System.Random rng, float fraction)
        {
            if (fraction <= 0f) return;
            var options = new List<Dir>(4);
            for (int x = 0; x < m.W; x++)
            for (int y = 0; y < m.H; y++)
            {
                var c = new Vector2Int(x, y);
                if (m.OpenNeighbours(c) != 1 || rng.NextDouble() > fraction) continue;

                options.Clear();
                foreach (var d in DirExt.All)
                    if (m.InBounds(c + d.Delta()) && m.HasWall(c, d)) options.Add(d);
                if (options.Count == 0) continue;

                // Prefer joining two dead ends — removes both at once.
                Dir pick = options[rng.Next(options.Count)];
                foreach (var d in options)
                    if (m.OpenNeighbours(c + d.Delta()) == 1) { pick = d; break; }
                m.SetWall(Edge.Between(c, pick), false, notify: false);
            }
        }

        static Dir PickExitSide(MazeData m, Vector2Int c)
        {
            // On a corner choose the side facing away from the start.
            var sides = new List<Dir>(2);
            if (c.y == m.H - 1) sides.Add(Dir.N);
            if (c.x == m.W - 1) sides.Add(Dir.E);
            if (c.y == 0) sides.Add(Dir.S);
            if (c.x == 0) sides.Add(Dir.W);
            Dir best = sides[0]; float bestScore = float.MinValue;
            foreach (var s in sides)
            {
                Vector2 away = c - m.Start;
                float score = Vector2.Dot(away, s.Delta());
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        static void MarkShiftable(MazeData m, System.Random rng, float fraction)
        {
            if (fraction <= 0f) return;
            m.ForEachEdge(e =>
            {
                if (m.IsBorder(e)) return;
                if (e.Touches(m.Start) || e.Touches(m.Exit)) return;
                if (rng.NextDouble() < fraction) m.SetShiftable(e, true);
            });
        }
    }
}
