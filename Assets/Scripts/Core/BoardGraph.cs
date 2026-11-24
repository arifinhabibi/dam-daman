using System.Collections.Generic;

namespace Pamor.Core
{
    /// <summary>
    /// The dam-daman line board: 37 points. A 5x5 alquerque square (x 2..6, y 0..4) with a 6-point
    /// triangle on each side: x 1 (y 1..3) and x 0 (y 0, 2, 4) on Hitam's side, x 7 and x 8 on Merah's.
    /// Points are joined by lines; pieces move between neighbouring points on a line and jump in a
    /// straight line along one. See docs/DECISIONS.md, "Board rules".
    /// </summary>
    public static class BoardGraph
    {
        public const int Width = 9, Height = 5;
        public static readonly int PointCount;
        public static readonly int[] Xs, Ys;
        /// <summary>Every line as an ordered list of point ids.</summary>
        public static readonly int[][] Lines;
        /// <summary>Rays[p] = every direction leaving p along a line, as the points in order away from p.</summary>
        public static readonly int[][][] Rays;
        /// <summary>RayDirs[p][i] = (dx, dy) sign of the first step of Rays[p][i].</summary>
        public static readonly (int dx, int dy)[][] RayDirs;

        static readonly int[,] idAt = new int[Width, Height];

        static BoardGraph()
        {
            var coords = new List<(int x, int y)>();
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                idAt[x, y] = -1;
                bool square = x >= 2 && x <= 6;
                bool innerTri = (x == 1 || x == 7) && y >= 1 && y <= 3;
                bool outerTri = (x == 0 || x == 8) && y % 2 == 0;
                if (square || innerTri || outerTri)
                {
                    idAt[x, y] = coords.Count;
                    coords.Add((x, y));
                }
            }
            PointCount = coords.Count;
            Xs = new int[PointCount]; Ys = new int[PointCount];
            for (int i = 0; i < PointCount; i++) { Xs[i] = coords[i].x; Ys[i] = coords[i].y; }

            var lines = new List<int[]>();
            void Line(params (int x, int y)[] pts)
            {
                var ids = new int[pts.Length];
                for (int i = 0; i < pts.Length; i++) ids[i] = idAt[pts[i].x, pts[i].y];
                lines.Add(ids);
            }
            // Rows of the square; the middle row runs through both triangles.
            foreach (var y in new[] { 0, 1, 3, 4 }) Line((2, y), (3, y), (4, y), (5, y), (6, y));
            Line((0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (6, 2), (7, 2), (8, 2));
            // Columns of the square.
            for (int x = 2; x <= 6; x++) Line((x, 0), (x, 1), (x, 2), (x, 3), (x, 4));
            // The two long diagonals.
            Line((2, 0), (3, 1), (4, 2), (5, 3), (6, 4));
            Line((2, 4), (3, 3), (4, 2), (5, 1), (6, 0));
            // The diamond's edges, each continuing out as a triangle ray.
            Line((0, 0), (1, 1), (2, 2), (3, 3), (4, 4));
            Line((0, 4), (1, 3), (2, 2), (3, 1), (4, 0));
            Line((4, 0), (5, 1), (6, 2), (7, 3), (8, 4));
            Line((4, 4), (5, 3), (6, 2), (7, 1), (8, 0));
            // Cross lines of each triangle row.
            Line((1, 1), (1, 2), (1, 3));
            Line((0, 0), (0, 2), (0, 4));
            Line((7, 1), (7, 2), (7, 3));
            Line((8, 0), (8, 2), (8, 4));
            Lines = lines.ToArray();

            var rays = new List<int[]>[PointCount];
            var dirs = new List<(int, int)>[PointCount];
            for (int i = 0; i < PointCount; i++) { rays[i] = new List<int[]>(); dirs[i] = new List<(int, int)>(); }
            foreach (var line in Lines)
            {
                for (int i = 0; i < line.Length; i++)
                {
                    int p = line[i];
                    if (i + 1 < line.Length)
                    {
                        var fwd = new int[line.Length - i - 1];
                        for (int k = 0; k < fwd.Length; k++) fwd[k] = line[i + 1 + k];
                        rays[p].Add(fwd); dirs[p].Add(Dir(p, fwd[0]));
                    }
                    if (i > 0)
                    {
                        var back = new int[i];
                        for (int k = 0; k < i; k++) back[k] = line[i - 1 - k];
                        rays[p].Add(back); dirs[p].Add(Dir(p, back[0]));
                    }
                }
            }
            Rays = new int[PointCount][][];
            RayDirs = new (int, int)[PointCount][];
            for (int i = 0; i < PointCount; i++) { Rays[i] = rays[i].ToArray(); RayDirs[i] = dirs[i].ToArray(); }
        }

        static (int, int) Dir(int from, int to) => (System.Math.Sign(Xs[to] - Xs[from]), System.Math.Sign(Ys[to] - Ys[from]));

        public static int At(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height ? idAt[x, y] : -1;

        /// <summary>Point name: column letter a..i (x 0..8) and row 1..5 (y 0..4), e.g. "c3" is the left apex.</summary>
        public static string Name(int p) => $"{(char)('a' + Xs[p])}{Ys[p] + 1}";

        public static int Parse(string name)
        {
            int p = At(name[0] - 'a', name[1] - '1');
            if (p < 0) throw new System.ArgumentException("No point named " + name);
            return p;
        }
    }
}
