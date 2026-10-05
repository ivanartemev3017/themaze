using System;
using UnityEngine;

namespace MazeRunner
{
    public enum Dir { N, E, S, W }

    public static class DirExt
    {
        public static readonly Dir[] All = { Dir.N, Dir.E, Dir.S, Dir.W };

        public static Vector2Int Delta(this Dir d) => d switch
        {
            Dir.N => new Vector2Int(0, 1),
            Dir.E => new Vector2Int(1, 0),
            Dir.S => new Vector2Int(0, -1),
            _     => new Vector2Int(-1, 0),
        };
    }

    /// <summary>
    /// Wall segment on the cell grid. Vertical edges sit on x-boundary I between cells (I-1,J) and (I,J);
    /// horizontal edges sit on y-boundary J between cells (I,J-1) and (I,J).
    /// </summary>
    public readonly struct Edge : IEquatable<Edge>
    {
        public readonly bool Vertical;
        public readonly int I, J;

        public Edge(bool vertical, int i, int j) { Vertical = vertical; I = i; J = j; }

        public static Edge Between(Vector2Int cell, Dir d) => d switch
        {
            Dir.E => new Edge(true,  cell.x + 1, cell.y),
            Dir.W => new Edge(true,  cell.x,     cell.y),
            Dir.N => new Edge(false, cell.x,     cell.y + 1),
            _     => new Edge(false, cell.x,     cell.y),
        };

        public Vector2Int CellA => Vertical ? new Vector2Int(I - 1, J) : new Vector2Int(I, J - 1);
        public Vector2Int CellB => new Vector2Int(I, J);

        public bool Touches(Vector2Int c) => CellA == c || CellB == c;

        public bool Equals(Edge o) => Vertical == o.Vertical && I == o.I && J == o.J;
        public override bool Equals(object obj) => obj is Edge e && Equals(e);
        public override int GetHashCode() => (Vertical ? 1 : 0) | (I << 1) | (J << 16);
        public override string ToString() => $"{(Vertical ? "V" : "H")}({I},{J})";
    }

    /// <summary>Pure maze model: grid of cells with walls on edges. Knows nothing about GameObjects.</summary>
    public sealed class MazeData
    {
        public readonly int W, H;
        readonly bool[,] _v; // (W+1) x H
        readonly bool[,] _h; // W x (H+1)
        readonly bool[,] _vShift;
        readonly bool[,] _hShift;

        public Vector2Int Start;
        public Vector2Int Exit;
        public Dir ExitSide;

        /// <summary>Raised when a wall appears (true) or disappears (false).</summary>
        public event Action<Edge, bool> WallChanged;

        public MazeData(int w, int h)
        {
            W = w; H = h;
            _v = new bool[w + 1, h];
            _h = new bool[w, h + 1];
            _vShift = new bool[w + 1, h];
            _hShift = new bool[w, h + 1];
            for (int i = 0; i <= w; i++) for (int j = 0; j < h; j++) _v[i, j] = true;
            for (int i = 0; i < w; i++) for (int j = 0; j <= h; j++) _h[i, j] = true;
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < W && c.y < H;

        public bool IsBorder(Edge e) => e.Vertical ? (e.I == 0 || e.I == W) : (e.J == 0 || e.J == H);

        public bool HasWall(Edge e) => e.Vertical ? _v[e.I, e.J] : _h[e.I, e.J];

        public bool HasWall(Vector2Int c, Dir d) => HasWall(Edge.Between(c, d));

        public bool CanMove(Vector2Int c, Dir d)
        {
            var n = c + d.Delta();
            return InBounds(n) && !HasWall(Edge.Between(c, d));
        }

        public void SetWall(Edge e, bool on, bool notify = true)
        {
            if (HasWall(e) == on) return;
            if (e.Vertical) _v[e.I, e.J] = on; else _h[e.I, e.J] = on;
            if (notify) WallChanged?.Invoke(e, on);
        }

        public bool IsShiftable(Edge e) => e.Vertical ? _vShift[e.I, e.J] : _hShift[e.I, e.J];

        public void SetShiftable(Edge e, bool on)
        {
            if (e.Vertical) _vShift[e.I, e.J] = on; else _hShift[e.I, e.J] = on;
        }

        /// <summary>Visits every edge of the grid, border included.</summary>
        public void ForEachEdge(Action<Edge> visit)
        {
            for (int i = 0; i <= W; i++) for (int j = 0; j < H; j++) visit(new Edge(true, i, j));
            for (int i = 0; i < W; i++) for (int j = 0; j <= H; j++) visit(new Edge(false, i, j));
        }

        public int OpenNeighbours(Vector2Int c)
        {
            int n = 0;
            foreach (var d in DirExt.All) if (CanMove(c, d)) n++;
            return n;
        }
    }
}
